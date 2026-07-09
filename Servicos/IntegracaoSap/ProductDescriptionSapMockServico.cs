using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: serviço de demonstração — NÃO retorna descrição simulada. Em ambiente
/// demonstrativo / banco desabilitado, a descrição fica indisponível (sem fake) e a grid usa o
/// fallback controlado ("Descrição não retornada pelo SAP").
/// </summary>
internal sealed class ProductDescriptionSapMockServico : IProductDescriptionSapServico
{
    public bool EhSimulado => true;
    public bool Configurado => false;

    public Task<ProdutoSapMestre?> ObterDescricaoAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoSapMestre?>(null);
}
