using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// A SENHA NÃO É GRAVADA EM LUGAR NENHUM — e esta é a decisão mais importante do incremento 070.
/// <para>
/// O pedido original (24/11/2026) dizia registrar "usuário, senha, pedido, material, data e hora".
/// Tudo foi atendido menos a senha, deliberadamente: as senhas do sistema vivem como hash BCrypt
/// (<c>SenhaServico</c>), e uma coluna de senha em texto seria o único lugar do sistema com
/// credencial legível. Para quem lê o log depois ela também não prova nada — quem autorizou é
/// provado pela IDENTIDADE do usuário.
/// </para>
/// <para>
/// Estes testes existem para que a coluna não volte por boa intenção, como "completar o log". Se
/// alguém precisar mesmo acrescentá-la, que seja depois de ler este arquivo e o cabeçalho do 070.
/// </para>
/// </summary>
public sealed class LiberacaoToleranciaSemSenhaTests
{
    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    [Fact]
    public void IncrementoNaoCriaColunaDeSenha()
    {
        string proposta = LerFonte(
            "BancoDados", "001_incrementais", "070_liberacao_tolerancia_recebimento_DEV_GAIA",
            "070_liberacao_tolerancia_recebimento_DEV_PROPOSTA_GAIA.sql");

        int inicio = proposta.IndexOf("CREATE TABLE IF NOT EXISTS", StringComparison.Ordinal);
        int fim = proposta.IndexOf("COMMENT ON TABLE", inicio, StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio, "bloco CREATE TABLE nao encontrado");

        string criacao = proposta[inicio..fim];
        Assert.DoesNotContain("senha", criacao, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", criacao, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", criacao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositorioNaoTemParametroDeSenha()
    {
        string repo = LerFonte("AcessoDados", "Repositorio", "LiberacaoToleranciaRepositorio.cs");

        Assert.DoesNotContain("@senha", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", repo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O registro é montado a partir do <c>LiberacaoToleranciaRecebimento</c>; se o modelo não tem
    /// campo de senha, não há por onde ela escorrer para o banco.
    /// </summary>
    [Fact]
    public void ModeloDoLogNaoTemCampoDeSenha()
    {
        string modelo = LerFonte("Modelo", "Processo", "LiberacaoToleranciaRecebimento.cs");

        foreach (string proibido in new[] { "Senha {", "Senha{", "string Senha", "Password" })
        {
            Assert.DoesNotContain(proibido, modelo, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A senha trafega do diálogo para a autenticação e para em pé. O serviço de autorização recebe
    /// o parâmetro, repassa, e NUNCA o coloca em mensagem, log ou registro.
    /// </summary>
    [Fact]
    public void ServicoNaoColocaASenhaEmLogNemEmMensagem()
    {
        string servico = LerFonte("Servicos", "Seguranca", "AutorizacaoToleranciaServico.cs");

        Assert.DoesNotContain("TraceWarning($\"[Tolerancia] senha", servico, StringComparison.OrdinalIgnoreCase);
        // A senha so pode aparecer como PARAMETRO e no repasse a autenticacao.
        Assert.DoesNotContain("LoginAutorizador = senha", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("MotivoRecusa = senha", servico, StringComparison.Ordinal);
    }

    /// <summary>A validação do incremento vigia a ausência direto no catálogo do banco.</summary>
    [Fact]
    public void ValidacaoDoIncrementoVigiaAAusenciaDaColuna()
    {
        string validacao = LerFonte(
            "BancoDados", "001_incrementais", "070_liberacao_tolerancia_recebimento_DEV_GAIA",
            "070_liberacao_tolerancia_recebimento_DEV_VALIDACAO_GAIA.sql");

        Assert.Contains("column_name ILIKE '%senha%'", validacao, StringComparison.Ordinal);
        Assert.Contains("nenhuma coluna de senha", validacao, StringComparison.Ordinal);
    }

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
