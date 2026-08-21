using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Modo demonstracao: A_ProductPlant nao e consultado (retorna null para qualquer material × centro).
/// Em demonstracao o envio SAP ja e bloqueado antes (SAP nao configurado); manter null aqui NUNCA
/// assume administracao de lote (nem true nem false).
/// </summary>
internal sealed class ProductPlantSapMockServico : IProductPlantSapServico
{
    public bool Configurado => false;

    public Task<ProdutoCentroSapMestre?> ObterProdutoCentroAsync(
        string material,
        string centro,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoCentroSapMestre?>(null);
}
