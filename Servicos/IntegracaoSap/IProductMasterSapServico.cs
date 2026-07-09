using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1 (Ajuste 7): entrada única do GET do tipo mestre do material no SAP
/// (API_PRODUCT_SRV / A_Product): ProductType/ProductGroup/BaseUnit. Serviço COMPARTILHADO (Entrada e,
/// futuramente, Consumo). Nunca lança para o chamador; a escolha real/demo/config-inválida pertence à
/// <see cref="FabricaProductMasterSapServico"/>.
/// </summary>
public interface IProductMasterSapServico
{
    bool EhSimulado { get; }
    bool Configurado { get; }

    /// <summary>Tipo mestre (A_Product) do material ou null quando não configurado/indisponível/não encontrado.</summary>
    Task<ProdutoSapMestre?> ObterProdutoAsync(string codigoProduto, CancellationToken cancellationToken = default);
}
