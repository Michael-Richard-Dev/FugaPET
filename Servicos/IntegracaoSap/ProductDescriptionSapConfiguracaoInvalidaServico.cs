using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: arquivo de configuração SAP existente porém malformado. NÃO cai em mock
/// nem em fake; a descrição fica indisponível (grid usa fallback) sem expor caminho/conteúdo do arquivo.
/// </summary>
internal sealed class ProductDescriptionSapConfiguracaoInvalidaServico : IProductDescriptionSapServico
{
    public bool EhSimulado => false;
    public bool Configurado => false;

    public Task<ProdutoSapMestre?> ObterDescricaoAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoSapMestre?>(null);
}
