using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica do GET de administracao de lote por material × centro no SAP
/// (API_PRODUCT_SRV / A_ProductPlant): <c>IsBatchManagementRequired</c>. Somente leitura. Nunca lanca
/// para o chamador; a escolha real/demo/config-invalida pertence a <see cref="FabricaProductPlantSapServico"/>.
/// Retorna null quando nao configurado/indisponivel/nao encontrado (o chamador BLOQUEIA o envio nesse caso —
/// nunca assume true nem false).
/// </summary>
public interface IProductPlantSapServico
{
    bool Configurado { get; }

    Task<ProdutoCentroSapMestre?> ObterProdutoCentroAsync(
        string material,
        string centro,
        CancellationToken cancellationToken = default);
}
