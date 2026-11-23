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

    /// <summary>
    /// EAN da unidade CAIXA do material (A_ProductUnitsOfMeasureEAN, unidade <c>CX</c>), para a barra da
    /// etiqueta da caixa de Produto Acabado. String vazia quando não houver — e aí a etiqueta mantém o aviso
    /// "EAN DO PRODUTO NAO CADASTRADO" em vez de imprimir barra de outra unidade.
    /// <para>
    /// Implementação PADRÃO devolve vazio de propósito: mock, configuração inválida e dublês de teste herdam
    /// o comportamento seguro sem precisar saber do EAN, e só o serviço real busca no SAP.
    /// </para>
    /// </summary>
    Task<string> ObterEanCaixaAsync(string codigoProduto, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}
