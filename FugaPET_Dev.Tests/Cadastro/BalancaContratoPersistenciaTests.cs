namespace FugaPET_Dev.Tests.Cadastro;

public sealed class BalancaContratoPersistenciaTests
{
    private static readonly string RaizProjeto = ObterRaizProjeto();

    [Fact]
    public void Repositorio_AtualizarAsync_NaoDeveAtualizarSituacaoBalanca()
    {
        string conteudo = File.ReadAllText(
            Path.Combine(RaizProjeto, "AcessoDados", "Repositorio", "BalancaRepositorio.cs"));

        int inicio = conteudo.IndexOf(
            "public virtual async Task<int> AtualizarAsync",
            StringComparison.Ordinal);
        int fim = conteudo.IndexOf(
            "public virtual async Task<int> ExcluirAsync",
            inicio,
            StringComparison.Ordinal);

        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = conteudo[inicio..fim];
        Assert.DoesNotContain("situacao_balanca =", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("@situacao_balanca", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void PropostaGaia_DeveCobrirRegrasEDependenciasOperacionais()
    {
        string conteudo = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "BancoDados",
            "001_incrementais",
            "028_h28_cadastro_balanca_regras_banco_GAIA",
            "028_h28_cadastro_balanca_regras_banco_PROPOSTA_GAIA.sql"));

        string[] trechosObrigatorios =
        [
            "ck_balanca_nome_tamanho_funcional",
            "ck_balanca_serial_campos_obrigatorios",
            "baud_rate > 0",
            "data_bits > 0",
            "fn_balanca_dependencias_ativas",
            "trg_balanca_bloquear_inativacao_em_uso",
            "entrada_produto_pesagem",
            "hu_caixa",
            "hu_caixa_pesagem",
            "pesagem_entrada_item",
            "pesagem_entrada_item_leitura"
        ];

        foreach (string trecho in trechosObrigatorios)
        {
            Assert.Contains(trecho, conteudo, StringComparison.Ordinal);
        }
    }

    private static string ObterRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
        {
            diretorio = diretorio.Parent;
        }

        return diretorio?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
