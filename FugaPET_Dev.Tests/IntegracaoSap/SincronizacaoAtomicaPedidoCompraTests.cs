namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class SincronizacaoAtomicaPedidoCompraTests
{
    [Fact]
    public void CargaCompleta_DeveUnirUpsertEInativacaoNaMesmaTransacao()
    {
        string caminho = LocalizarArquivo(
            "AcessoDados",
            "Repositorio",
            "SapPedidoCompraRepositorio.cs");
        string conteudo = File.ReadAllText(caminho);

        Assert.Contains("SincronizarInternoAsync(pedidos, inativarAusentes: true", conteudo);
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", conteudo);
        Assert.Contains("InativarPedidosForaDaListaValidaAsync(", conteudo);
        Assert.DoesNotContain(
            "public async Task<int> InativarPedidosForaDaListaValidaAsync",
            conteudo,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_NaoDeveAcionarCargaCompleta()
    {
        string caminho = LocalizarArquivo(
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs");
        string conteudo = File.ReadAllText(caminho);

        Assert.DoesNotContain("SincronizarCargaCompletaAsync", conteudo, StringComparison.Ordinal);
    }

    private static string LocalizarArquivo(params string[] partes)
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            string candidato = Path.Combine([diretorio.FullName, .. partes]);
            if (File.Exists(candidato))
            {
                return candidato;
            }

            diretorio = diretorio.Parent;
        }

        throw new FileNotFoundException($"Arquivo nao encontrado: {Path.Combine(partes)}");
    }
}
