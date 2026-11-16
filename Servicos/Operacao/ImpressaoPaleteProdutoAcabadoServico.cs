using System.Globalization;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Etiqueta do PALETE de Produto Acabado. Reaproveita o mesmo pipeline Zebra da etiqueta da caixa
/// (resolução da impressora do terminal, permissão, gate anti-concorrência e diagnóstico).
/// <para>
/// Efeito puramente local: não chama SAP, não altera o estado do palete nem das caixas. A etiqueta
/// sai quando o palete é gravado, antes e independentemente do envio INT012.
/// </para>
/// </summary>
public sealed class ImpressaoPaleteProdutoAcabadoServico
{
    /// <summary>
    /// Prefixo do código de barras do palete. O número do palete é o código do banco; o código de
    /// barras é este prefixo mais o código com zeros à esquerda, totalizando dez dígitos — o mesmo
    /// formato das etiquetas já em circulação (palete 17747 ⇒ 2000017747).
    /// </summary>
    public const string PrefixoCodigoBarras = "20";

    /// <summary>Dígitos do sequencial dentro do código de barras.</summary>
    public const int DigitosSequencial = 8;

    private readonly ImpressoraEtiquetaServico _impressoraEtiquetaServico;

    public ImpressaoPaleteProdutoAcabadoServico()
        : this(new ImpressoraEtiquetaServico())
    {
    }

    public ImpressaoPaleteProdutoAcabadoServico(ImpressoraEtiquetaServico impressoraEtiquetaServico)
    {
        _impressoraEtiquetaServico = impressoraEtiquetaServico
            ?? throw new ArgumentNullException(nameof(impressoraEtiquetaServico));
    }

    public Task ImprimirPaleteAsync(
        ProdutoAcabadoPalete palete,
        long codigoPalete,
        string terminal,
        string? descricaoMaterial = null,
        IReadOnlyDictionary<string, LoteSapDatas>? datasPorLote = null)
    {
        DadosEtiquetaPaleteProdutoAcabado etiqueta =
            CriarEtiqueta(palete, codigoPalete, terminal, descricaoMaterial, datasPorLote);
        return _impressoraEtiquetaServico.ImprimirEtiquetaPaleteProdutoAcabadoAsync(etiqueta);
    }

    /// <summary>
    /// Código de barras a partir do sequencial do palete. Um sequencial que não couber nos dígitos
    /// previstos é rejeitado em vez de truncado: um código truncado colidiria com outro palete.
    /// </summary>
    public static string MontarCodigoBarras(long codigoPalete)
    {
        if (codigoPalete <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codigoPalete),
                "O palete precisa de um código persistido para gerar o código de barras.");
        }

        string sequencial = codigoPalete.ToString(CultureInfo.InvariantCulture);
        if (sequencial.Length > DigitosSequencial)
        {
            throw new ErroOperacionalEsperadoException(
                $"O código do palete ({sequencial}) excede os {DigitosSequencial} dígitos do código de "
                + "barras. O formato precisa ser revisto antes de seguir imprimindo.");
        }

        return PrefixoCodigoBarras + sequencial.PadLeft(DigitosSequencial, '0');
    }

    /// <summary>
    /// Monta a etiqueta a partir do palete persistido. O bloco de lotes agrupa as caixas por lote,
    /// preservando a ordem em que os lotes aparecem no palete.
    /// </summary>
    public static DadosEtiquetaPaleteProdutoAcabado CriarEtiqueta(
        ProdutoAcabadoPalete palete,
        long codigoPalete,
        string terminal,
        string? descricaoMaterial = null,
        IReadOnlyDictionary<string, LoteSapDatas>? datasPorLote = null)
    {
        ArgumentNullException.ThrowIfNull(palete);

        IReadOnlyList<ProdutoAcabadoCaixa> caixas = palete.Caixas;
        var lotes = caixas
            .GroupBy(c => (c.Lote ?? string.Empty).Trim())
            .Select(g => new LinhaLoteEtiquetaPalete(
                Lote: g.Key.Length == 0 ? "SEM LOTE" : g.Key,
                // GATE 126F: as datas vem do cadastro de LOTE do SAP (API_BATCH_SRV). Lote ausente
                // do mapa, ou com a data nula no SAP, continua "NAO INFORMADA" — declarado, nunca
                // deduzido. Data de validade errada numa etiqueta de expedicao e pior que etiqueta
                // sem data.
                DataFabricacao: FormatarDataLote(datasPorLote, g.Key, d => d.DataFabricacao),
                DataVencimento: FormatarDataLote(datasPorLote, g.Key, d => d.DataVencimento),
                QuantidadeCaixas: g.Count()))
            .ToArray();

        // Data de formação: a caixa mais recente do palete. É factual e verificável — não é data de
        // fabricação do produto, que o Produto Acabado não possui.
        DateTimeOffset? referencia = caixas.Count == 0
            ? null
            : caixas.Max(c => c.ConfirmadoSapEm ?? c.AtualizadoEm ?? c.CriadoEm);

        string material = caixas.Select(c => (c.Material ?? string.Empty).Trim())
            .FirstOrDefault(m => m.Length > 0) ?? string.Empty;

        return new DadosEtiquetaPaleteProdutoAcabado
        {
            Numero = codigoPalete.ToString(CultureInfo.InvariantCulture),
            CodigoBarras = MontarCodigoBarras(codigoPalete),
            Data = referencia is { } data
                ? data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : NaoInformada,
            Material = material,
            DescricaoMaterial = (descricaoMaterial ?? string.Empty).Trim(),
            QuantidadeCaixas = caixas.Count.ToString(CultureInfo.InvariantCulture),
            QuantidadeProdutos = caixas.Sum(c => (long)c.QuantidadeProdutos)
                .ToString(CultureInfo.InvariantCulture),
            PesoBruto = FormatarPeso(palete.PesoBrutoKg),
            PesoLiquido = FormatarPeso(palete.PesoLiquidoKg),
            HandlingUnitSap = palete.HandlingUnitPalete ?? string.Empty,
            DataHoraImpressao = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            Terminal = (terminal ?? string.Empty).Trim(),
            Lotes = lotes
        };
    }

    public const string NaoInformada = "NAO INFORMADA";

    /// <summary>
    /// GATE 126F: data de um lote, no formato do modelo impresso (dd/MM/yyyy). Mapa ausente, lote
    /// que nao veio do SAP, ou data nula no cadastro, resultam em "NAO INFORMADA".
    /// <para>
    /// A comparacao do lote e normalizada (sem espacos, sem diferenciar caixa) porque o lote na
    /// caixa e digitado/lido no chao de fabrica e o do SAP vem do cadastro: "61008561f" e
    /// "61008561F " sao o mesmo lote, e tratar como diferentes apagaria a data silenciosamente.
    /// </para>
    /// </summary>
    internal static string FormatarDataLote(
        IReadOnlyDictionary<string, LoteSapDatas>? datasPorLote,
        string? lote,
        Func<LoteSapDatas, DateTime?> escolher)
    {
        if (datasPorLote is null || datasPorLote.Count == 0)
        {
            return NaoInformada;
        }

        string chave = LoteSapDatas.NormalizarLote(lote);
        if (chave.Length == 0 || !datasPorLote.TryGetValue(chave, out LoteSapDatas? datas))
        {
            return NaoInformada;
        }

        DateTime? data = escolher(datas);
        return data is { } valor
            ? valor.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : NaoInformada;
    }

    /// <summary>
    /// Peso como no modelo impresso: vírgula decimal, sem unidade, até três casas, com os zeros à
    /// direita suprimidos mas nunca abaixo de duas casas (527,450 ⇒ 527,45; 427,322 ⇒ 427,322).
    /// </summary>
    internal static string FormatarPeso(decimal valor)
    {
        string texto = valor.ToString("0.000", CultureInfo.InvariantCulture);
        if (texto.EndsWith('0') && !texto.EndsWith(".00", StringComparison.Ordinal))
        {
            texto = texto[..^1];
        }
        return texto.Replace('.', ',');
    }
}
