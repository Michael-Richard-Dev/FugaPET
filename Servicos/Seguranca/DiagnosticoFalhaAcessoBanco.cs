using System.Net.Sockets;
using System.Text.RegularExpressions;
using Npgsql;

namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>O que impediu o acesso ao banco, do ponto de vista de quem está na tela de login.</summary>
public enum CausaFalhaAcessoBanco
{
    /// <summary>Não foi possível classificar: a mensagem genérica continua valendo.</summary>
    Indeterminada,

    /// <summary>O servidor recusou ESTE computador: não há regra no pg_hba.conf para o endereço dele.</summary>
    EstacaoNaoLiberada,

    /// <summary>O servidor não foi alcançado: rede, cabo, VPN, serviço parado.</summary>
    ServidorInacessivel,

    /// <summary>A senha do PAPEL DE BANCO da aplicação foi recusada — configuração, não o login do usuário.</summary>
    CredencialDoBancoInvalida,

    /// <summary>O banco configurado não existe neste servidor.</summary>
    BancoInexistente,
}

/// <summary>O veredito, com a mensagem pronta para a tela.</summary>
public sealed record FalhaAcessoBanco(CausaFalhaAcessoBanco Causa, string Mensagem)
{
    public bool Identificada => Causa != CausaFalhaAcessoBanco.Indeterminada;
}

/// <summary>
/// TRADUZ A FALHA DE ACESSO AO BANCO NUMA FRASE QUE DIZ O QUE FAZER.
/// <para>
/// Pedido do usuário em 24/11/2026: quando o acesso é recusado porque o IP da estação não está
/// liberado, a tela de login precisa DIZER ISSO — e não "Não foi possível concluir a operação.
/// Acione o suporte.", que manda procurar no lugar errado. Ele perdeu tempo com exatamente esse
/// caso nesta mesma sessão.
/// </para>
/// <para>
/// <b>Não é trocar uma mensagem genérica por outra.</b> Só se afirma "estação não liberada" quando
/// o PRÓPRIO SERVIDOR disse isso: SQLSTATE 28000 com a recusa do pg_hba. Servidor fora do ar, senha
/// de papel errada e banco inexistente são causas diferentes, com mensagens diferentes; o que não
/// se reconhece continua caindo na mensagem antiga. Chutar "IP não cadastrado" num servidor
/// desligado faria alguém liberar IP por horas enquanto o serviço está parado.
/// </para>
/// <para>
/// <b>O endereço vem do SERVIDOR, não da máquina.</b> A recusa do PostgreSQL carrega o host que ele
/// realmente enxergou — que é o que precisa entrar no pg_hba. Entre NAT, VPN e duas placas de rede,
/// o IP que a estação acha que tem pode não ser o que chega lá, e mostrar o errado mandaria liberar
/// um endereço que não resolve nada.
/// </para>
/// </summary>
public static class DiagnosticoFalhaAcessoBanco
{
    public const string MensagemGenerica = "Nao foi possivel concluir a operacao. Acione o suporte.";

    /// <summary>SQLSTATE 28000 — invalid_authorization_specification. É o código da recusa por pg_hba.</summary>
    private const string SqlStateAutorizacaoInvalida = "28000";

    /// <summary>SQLSTATE 28P01 — invalid_password, do PAPEL de banco (não do usuário da aplicação).</summary>
    private const string SqlStateSenhaInvalida = "28P01";

    /// <summary>SQLSTATE 3D000 — invalid_catalog_name: o banco configurado não existe.</summary>
    private const string SqlStateBancoInexistente = "3D000";

    /// <summary>
    /// Extrai o host da recusa do pg_hba. O PostgreSQL escreve, em português ou inglês conforme o
    /// locale do servidor, algo como: no pg_hba.conf entry for host "192.168.254.247", user ...
    /// </summary>
    private static readonly Regex HostDaRecusa = new(
        "host\\s+\"([^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Classifica a exceção. Percorre as exceções internas porque a falha de conexão costuma vir
    /// embrulhada — a <see cref="PostgresException"/> real pode estar duas camadas abaixo.
    /// </summary>
    public static FalhaAcessoBanco Classificar(Exception? erro)
    {
        for (Exception? atual = erro; atual is not null; atual = atual.InnerException)
        {
            if (atual is PostgresException pg)
            {
                FalhaAcessoBanco? porCodigo = ClassificarPostgres(pg);
                if (porCodigo is not null)
                {
                    return porCodigo;
                }
            }

            if (atual is SocketException or TimeoutException)
            {
                return new FalhaAcessoBanco(
                    CausaFalhaAcessoBanco.ServidorInacessivel,
                    MensagemServidorInacessivel);
            }
        }

        return new FalhaAcessoBanco(CausaFalhaAcessoBanco.Indeterminada, MensagemGenerica);
    }

    private static FalhaAcessoBanco? ClassificarPostgres(PostgresException pg)
    {
        string texto = pg.MessageText ?? string.Empty;

        if (pg.SqlState == SqlStateAutorizacaoInvalida
            && texto.Contains("pg_hba", StringComparison.OrdinalIgnoreCase))
        {
            // O endereço é o que o SERVIDOR viu. Sem ele a mensagem ainda vale, mas quem for
            // liberar terá de descobrir o IP — então diz-se o que se sabe, e nada além.
            Match host = HostDaRecusa.Match(texto);
            return new FalhaAcessoBanco(
                CausaFalhaAcessoBanco.EstacaoNaoLiberada,
                MensagemEstacaoNaoLiberada(host.Success ? host.Groups[1].Value : null));
        }

        if (pg.SqlState == SqlStateSenhaInvalida)
        {
            // NÃO é o login do operador: é a credencial do PAPEL de banco, que vive na configuração.
            // Dizer "usuário ou senha inválidos" aqui mandaria o operador redigitar para sempre.
            return new FalhaAcessoBanco(
                CausaFalhaAcessoBanco.CredencialDoBancoInvalida,
                MensagemCredencialDoBanco);
        }

        if (pg.SqlState == SqlStateBancoInexistente)
        {
            return new FalhaAcessoBanco(CausaFalhaAcessoBanco.BancoInexistente, MensagemBancoInexistente);
        }

        return null;
    }

    public const string MensagemServidorInacessivel =
        "Servidor de banco de dados não respondeu. Verifique a rede.";

    public const string MensagemCredencialDoBanco =
        "Credencial do banco recusada: é configuração, não a sua senha.";

    public const string MensagemBancoInexistente =
        "O banco de dados configurado não existe neste servidor.";

    /// <summary>
    /// A frase do IP, com o endereço quando o servidor o informou. Começa por "IP não cadastrado"
    /// de propósito: é o que o usuário pediu para ler, e é a primeira coisa que ele precisa saber.
    /// </summary>
    public static string MensagemEstacaoNaoLiberada(string? endereco)
        => string.IsNullOrWhiteSpace(endereco)
            ? "IP não cadastrado: este computador não está liberado no banco."
            : $"IP não cadastrado: {endereco} não está liberado no banco.";

    /// <summary>
    /// TODAS as frases que esta classe pode devolver, para a tela reservar a largura da maior.
    /// <para>
    /// Existe porque o card de login dimensiona a caixa pelo conteúdo: uma frase mais larga que a
    /// reserva aparece CORTADA — defeito que já aconteceu antes aqui e está documentado no próprio
    /// LoginForm. Com a lista, acrescentar uma mensagem nova não volta a cortar em silêncio.
    /// </para>
    /// <para>
    /// O endereço entra no pior caso plausível (IPv6 completo), e não num IPv4 curto: reservar pela
    /// frase mais curta seria reservar de menos justamente para quem tem a rede mais complicada.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> MensagensPossiveis { get; } =
    [
        MensagemGenerica,
        MensagemServidorInacessivel,
        MensagemCredencialDoBanco,
        MensagemBancoInexistente,
        MensagemEstacaoNaoLiberada("2001:0db8:85a3:0000:0000:8a2e:0370:7334"),
    ];
}
