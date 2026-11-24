using System.Net.Sockets;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;
using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// MENSAGEM DE IP NAO CADASTRADO NA TELA DE LOGIN (pedido do usuario em 24/11/2026).
/// <para>
/// Nas palavras dele: "sempre que da um erro de acesso ao banco no inicio como esse que deu agora
/// que eu tive que habilitar o IP, eu preciso que na tela de login mostre essa informacao, de IP
/// nao cadastrado, nao a mensagem aleatoria que esta hoje".
/// </para>
/// <para>
/// O valor destes testes esta tanto no que o classificador AFIRMA quanto no que ele se RECUSA a
/// afirmar: dizer "IP nao cadastrado" quando o servidor esta desligado faria alguem liberar
/// endereco por horas enquanto o servico esta parado.
/// </para>
/// </summary>
public sealed class DiagnosticoFalhaAcessoBancoTests
{
    private static PostgresException Pg(string sqlState, string texto)
        => new(texto, "FATAL", "FATAL", sqlState);

    // ------------------------------------------------------- o caso que o usuario viveu

    /// <summary>
    /// A recusa do pg_hba vira "IP nao cadastrado" COM o endereco. Era este o caso que mostrava a
    /// mensagem generica e mandava o usuario procurar no lugar errado.
    /// </summary>
    [Fact]
    public void RecusaDoPgHba_DizIpNaoCadastradoComOEndereco()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("28000", "no pg_hba.conf entry for host \"192.168.254.247\", user \"fugapet_dev_app\", database \"x\", no encryption"));

        Assert.Equal(CausaFalhaAcessoBanco.EstacaoNaoLiberada, falha.Causa);
        Assert.Contains("IP não cadastrado", falha.Mensagem, StringComparison.Ordinal);
        Assert.Contains("192.168.254.247", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// O ENDERECO VEM DO SERVIDOR. Entre NAT, VPN e duas placas de rede, o IP que a estacao acha que
    /// tem pode nao ser o que chega la — e quem liberasse o errado nao resolveria nada. Aqui o
    /// servidor viu um endereco diferente do "obvio", e e ele que a tela mostra.
    /// </summary>
    [Fact]
    public void OEnderecoMostrado_EOQueOServidorViu()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("28000", "no pg_hba.conf entry for host \"10.8.0.31\", user \"app\""));

        Assert.Contains("10.8.0.31", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sem endereco na recusa a mensagem ainda vale, mas NAO INVENTA um numero. Um IP chutado faria
    /// liberar a maquina errada e o problema continuaria de pe.
    /// </summary>
    [Fact]
    public void SemEnderecoNaRecusa_NaoFabricaEndereco()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("28000", "pg_hba.conf rejects connection for user \"app\""));

        Assert.Equal(CausaFalhaAcessoBanco.EstacaoNaoLiberada, falha.Causa);
        Assert.Contains("IP não cadastrado", falha.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("(", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// A falha costuma vir EMBRULHADA: a PostgresException real fica duas camadas abaixo. Sem
    /// percorrer as internas, justamente o caso do usuario cairia na mensagem generica.
    /// </summary>
    [Fact]
    public void ExcecaoEmbrulhada_AindaEReconhecida()
    {
        Exception embrulhada = new InvalidOperationException(
            "falha ao abrir conexao",
            new NpgsqlException(
                "erro de conexao",
                Pg("28000", "no pg_hba.conf entry for host \"192.168.254.247\"")));

        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(embrulhada);

        Assert.Equal(CausaFalhaAcessoBanco.EstacaoNaoLiberada, falha.Causa);
    }

    // ------------------------------------------------------- o que ele se RECUSA a afirmar

    /// <summary>
    /// 28000 SEM pg_hba NAO e estacao bloqueada. O codigo cobre varias recusas de autorizacao; so a
    /// mencao ao pg_hba prova que foi o endereco. Afirmar na duvida seria trocar uma mensagem
    /// generica por outra, agora com cara de diagnostico.
    /// </summary>
    [Fact]
    public void Autorizacao28000SemPgHba_NaoAfirmaIpNaoCadastrado()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("28000", "role \"fugapet_dev_app\" is not permitted to log in"));

        Assert.NotEqual(CausaFalhaAcessoBanco.EstacaoNaoLiberada, falha.Causa);
        Assert.DoesNotContain("IP", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// SERVIDOR FORA DO AR nao e IP bloqueado. Este e o erro que custaria mais caro: mandaria mexer
    /// no pg_hba enquanto o servico esta parado.
    /// </summary>
    [Fact]
    public void ServidorInacessivel_NaoMandaLiberarIp()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            new NpgsqlException("falha ao conectar", new SocketException(10060)));

        Assert.Equal(CausaFalhaAcessoBanco.ServidorInacessivel, falha.Causa);
        Assert.DoesNotContain("IP não cadastrado", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// 28P01 e a senha do PAPEL DE BANCO, que vive na configuracao — nao o login do operador. Dizer
    /// "usuario ou senha invalidos" aqui mandaria ele redigitar para sempre.
    /// </summary>
    [Fact]
    public void SenhaDoPapelDeBanco_NaoCulpaOLoginDoOperador()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("28P01", "password authentication failed for user \"fugapet_dev_app\""));

        Assert.Equal(CausaFalhaAcessoBanco.CredencialDoBancoInvalida, falha.Causa);
        Assert.Contains("configuração", falha.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>Banco inexistente tem causa propria: nao e rede, nem endereco, nem senha.</summary>
    [Fact]
    public void BancoInexistente_TemCausaPropria()
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(
            Pg("3D000", "database \"fuga_jales_errado\" does not exist"));

        Assert.Equal(CausaFalhaAcessoBanco.BancoInexistente, falha.Causa);
    }

    /// <summary>
    /// O QUE NAO SE RECONHECE CONTINUA CAINDO NA MENSAGEM ANTIGA, palavra por palavra. O pedido era
    /// deixar de mostrar a frase generica NO CASO DO IP, nao abandonar o caminho de quem nao tem
    /// diagnostico nenhum.
    /// </summary>
    [Theory]
    [InlineData("42P01")]
    [InlineData("57P03")]
    public void CausaNaoReconhecida_PreservaAMensagemAntiga(string sqlState)
    {
        FalhaAcessoBanco falha = DiagnosticoFalhaAcessoBanco.Classificar(Pg(sqlState, "qualquer coisa"));

        Assert.Equal(CausaFalhaAcessoBanco.Indeterminada, falha.Causa);
        Assert.False(falha.Identificada);
        Assert.Equal("Nao foi possivel concluir a operacao. Acione o suporte.", falha.Mensagem);
    }

    /// <summary>Exceção banal e nulo tambem caem na generica, sem explodir a tela de login.</summary>
    [Fact]
    public void ErroQualquerOuNulo_CaemNaGenerica()
    {
        Assert.Equal(DiagnosticoFalhaAcessoBanco.MensagemGenerica,
            DiagnosticoFalhaAcessoBanco.Classificar(new InvalidOperationException("x")).Mensagem);

        Assert.Equal(DiagnosticoFalhaAcessoBanco.MensagemGenerica,
            DiagnosticoFalhaAcessoBanco.Classificar(null).Mensagem);
    }

    // ------------------------------------------------------- a frase tem de CABER na tela

    /// <summary>
    /// TODA frase que o classificador devolve esta em <c>MensagensPossiveis</c>, que e de onde a tela
    /// reserva a altura do rotulo. Uma frase fora da lista aparece CORTADA — defeito que eu ja
    /// cometi neste mesmo arquivo ao medir a largura por um literal copiado.
    /// </summary>
    [Fact]
    public void TodaMensagemDevolvida_EstaNaListaQueATelaMede()
    {
        Exception[] casos =
        [
            Pg("28000", "no pg_hba.conf entry for host \"2001:0db8:85a3:0000:0000:8a2e:0370:7334\""),
            Pg("28000", "pg_hba.conf rejects connection"),
            Pg("28P01", "password authentication failed"),
            Pg("3D000", "database does not exist"),
            new NpgsqlException("x", new SocketException(10060)),
            new InvalidOperationException("x"),
        ];

        // A frase do IP traz o endereco que o servidor informou, entao ela nao e literal: a lista a
        // representa pelo PIOR CASO (IPv6 completo). Para as demais, a cobranca e literal.
        int maiorDaLista = DiagnosticoFalhaAcessoBanco.MensagensPossiveis.Max(m => m.Length);

        foreach (Exception caso in casos)
        {
            string mensagem = DiagnosticoFalhaAcessoBanco.Classificar(caso).Mensagem;

            if (!mensagem.StartsWith("IP não cadastrado", StringComparison.Ordinal))
            {
                Assert.Contains(mensagem, DiagnosticoFalhaAcessoBanco.MensagensPossiveis);
            }

            Assert.True(
                mensagem.Length <= maiorDaLista,
                $"a lista reserva {maiorDaLista} caracteres e esta frase tem {mensagem.Length}: {mensagem}");
        }
    }

    /// <summary>
    /// A reserva da tela sai da LISTA do classificador, nunca de uma copia da frase. Foi a copia que
    /// fez a mensagem nova nascer cortada; sem esta guarda, acrescentar frase corta em silencio.
    /// </summary>
    [Fact]
    public void ATela_MedeALista_NaoUmaCopiaDaFrase()
    {
        string tela = FonteDaTelaDeLogin();

        Assert.Contains("DiagnosticoFalhaAcessoBanco.MensagensPossiveis", tela, StringComparison.Ordinal);
        Assert.Contains("TextFormatFlags.WordBreak", tela, StringComparison.Ordinal);
        Assert.Contains("DiagnosticoFalhaAcessoBanco.MensagemGenerica, messageLabel.Font", tela, StringComparison.Ordinal);

        // O literal copiado tem de estar FORA do codigo da tela.
        Assert.DoesNotContain(
            "\"Nao foi possivel concluir a operacao. Acione o suporte.\"", tela, StringComparison.Ordinal);
    }

    /// <summary>A tela classifica a excecao; nao escolhe a frase por conta propria.</summary>
    [Fact]
    public void ATela_UsaOClassificador()
        => Assert.Contains(
            "messageLabel.Text = DiagnosticoFalhaAcessoBanco.Classificar(ex).Mensagem;",
            FonteDaTelaDeLogin(),
            StringComparison.Ordinal);

    private static string FonteDaTelaDeLogin()
        => File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "LoginForm.cs"));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
