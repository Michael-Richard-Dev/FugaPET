using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: entrada única do GET da descrição do material no SAP
/// (API_PRODUCT_SRV / A_ProductDescription). Retorna um <see cref="ProdutoSapMestre"/> com APENAS a
/// descrição preenchida (Product/Language/ProductDescription) — o ProductType/tipo continua vindo de
/// A_Product em outra etapa. A escolha entre real, demonstração e configuração inválida pertence à
/// <see cref="FabricaProductDescriptionSapServico"/>. Nunca lança para o chamador.
/// </summary>
public interface IProductDescriptionSapServico
{
    bool EhSimulado { get; }
    bool Configurado { get; }

    /// <summary>Descrição do material (melhor idioma) ou null quando não configurado/indisponível/sem descrição.</summary>
    Task<ProdutoSapMestre?> ObterDescricaoAsync(string codigoProduto, CancellationToken cancellationToken = default);
}
