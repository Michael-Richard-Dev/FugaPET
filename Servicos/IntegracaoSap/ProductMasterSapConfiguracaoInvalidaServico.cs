using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1: arquivo de configuração SAP existente porém malformado. NÃO cai em mock nem em fake;
/// o tipo do material fica indisponível (item Indefinido/bloqueado) sem expor caminho/conteúdo do arquivo.
/// </summary>
internal sealed class ProductMasterSapConfiguracaoInvalidaServico : IProductMasterSapServico
{
    public bool EhSimulado => false;
    public bool Configurado => false;

    public Task<ProdutoSapMestre?> ObterProdutoAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoSapMestre?>(null);
}
