using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1: modo demonstração — o material demo (DEMO001) é classificado como ROH (matéria-prima)
/// para não travar a Entrada de Matéria-Prima na demo; demais materiais retornam null (item ficará Indefinido).
/// Sem dados simulados de negócio além do necessário para o fluxo demonstrativo.
/// </summary>
internal sealed class ProductMasterSapMockServico : IProductMasterSapServico
{
    public bool EhSimulado => true;
    public bool Configurado => false;

    public Task<ProdutoSapMestre?> ObterProdutoAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoSapMestre?>(
            string.Equals((codigoProduto ?? string.Empty).Trim(), "DEMO001", StringComparison.OrdinalIgnoreCase)
                ? new ProdutoSapMestre { CodigoProduto = "DEMO001", TipoMaterialSap = "ROH", Consultado = true }
                : null);
}
