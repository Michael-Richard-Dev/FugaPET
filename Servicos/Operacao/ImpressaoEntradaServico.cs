using System.Globalization;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Servico de impressao da Entrada de Produto (H9 Etapa 4). Centraliza o disparo de impressao/
/// reimpressao de etiqueta de materia-prima e a montagem da etiqueta a partir do item persistido,
/// para que a tela nao fale direto com o servico de impressora nem monte modelo de etiqueta.
/// </summary>
public sealed class ImpressaoEntradaServico
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ImpressoraEtiquetaServico _impressora;

    public ImpressaoEntradaServico(ImpressoraEtiquetaServico impressora)
    {
        _impressora = impressora ?? throw new ArgumentNullException(nameof(impressora));
    }

    public Task AquecerAsync() => _impressora.AquecerAsync();

    public Task GarantirImpressoraDisponivelAsync() => _impressora.GarantirImpressoraDisponivelAsync();

    public Task ImprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
        => _impressora.ImprimirEtiquetaMateriaPrimaAsync(etiqueta);

    public Task ReimprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
        => _impressora.ReimprimirEtiquetaMateriaPrimaAsync(etiqueta);

    /// <summary>Monta a etiqueta de materia-prima a partir do item ja persistido (reimpressao).</summary>
    public static DadosEtiquetaMateriaPrima MontarEtiqueta(EntradaProdutoItemPersistido item, string dataVencimento)
        => new()
        {
            CodigoProduto = item.Material,
            DescricaoProduto = item.DescricaoMaterial,
            LoteOrigem = item.NumeroPedido,
            LoteInterno = item.NumeroItem,
            DataFabricacao = item.DataPedido?.ToString("dd/MM/yyyy", CulturaPtBr) ?? string.Empty,
            DataVencimento = dataVencimento,
            CertificadoSanitario = string.Empty,
            Sif = string.Empty,
            Fornecedor = item.Fornecedor,
            NumeroNotaFiscal = string.Empty,
            Peso = item.PesoLiquidoTotalKg.ToString("0.###", CulturaPtBr),
            NumeroPedido = item.NumeroPedido,
            NumeroItem = item.NumeroItem
        };
}
