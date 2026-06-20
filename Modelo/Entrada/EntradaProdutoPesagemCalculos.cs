namespace FugaPET_Dev.Modelo.Entrada;

public static class EntradaProdutoPesagemCalculos
{
    public const string OrigemBalanca = "BALANCA";
    public const string OrigemManual = "MANUAL";
    public const string StatusValida = "VALIDA";
    public const string StatusCancelada = "CANCELADA";

    /// <summary>Peso liquido = bruto - tara.</summary>
    public static decimal CalcularPesoLiquido(decimal pesoBruto, decimal pesoTara)
        => pesoBruto - pesoTara;

    /// <summary>Leitura valida exige bruto positivo e liquido positivo (bruto maior que a tara).</summary>
    public static bool LeituraTemPesoValido(decimal pesoBruto, decimal pesoLiquido)
        => pesoBruto > 0m && pesoLiquido > 0m;

    /// <summary>
    /// Monta uma nova leitura VALIDA com sequencia seguinte. Peso digitado (MANUAL) nao grava
    /// balanca; so a leitura real de balanca (BALANCA) registra o codigo do equipamento.
    /// </summary>
    public static EntradaProdutoPesagem MontarLeitura(
        IReadOnlyList<EntradaProdutoPesagem> leiturasExistentes,
        decimal pesoBruto,
        decimal pesoTara,
        long? codigoTara,
        string origem,
        long? codigoBalanca,
        string leituraOriginal,
        DateTimeOffset pesadoEm)
        => new()
        {
            Sequencia = leiturasExistentes.Count + 1,
            PesoBrutoKg = pesoBruto,
            PesoTaraKg = pesoTara,
            PesoLiquidoKg = CalcularPesoLiquido(pesoBruto, pesoTara),
            CodigoTara = codigoTara,
            CodigoBalanca = string.Equals(origem, OrigemBalanca, StringComparison.OrdinalIgnoreCase)
                ? codigoBalanca
                : null,
            Origem = origem,
            StatusPesagem = StatusValida,
            LeituraOriginal = leituraOriginal,
            PesadoEm = pesadoEm
        };

    /// <summary>Marca todas as leituras como CANCELADA (preserva o historico).</summary>
    public static IReadOnlyList<EntradaProdutoPesagem> Cancelar(
        IEnumerable<EntradaProdutoPesagem> pesagens)
        => pesagens.Select(pesagem => pesagem with { StatusPesagem = StatusCancelada }).ToList();

    /// <summary>Origem consolidada das leituras validas para exibicao: DIGITADO/LIDO/MULTIPLA.</summary>
    public static string DescreverOrigemConsolidada(IEnumerable<EntradaProdutoPesagem> pesagens)
    {
        List<EntradaProdutoPesagem> validas = PesagensValidas(pesagens).ToList();
        return validas.Count switch
        {
            0 => string.Empty,
            1 when string.Equals(validas[0].Origem, OrigemManual, StringComparison.OrdinalIgnoreCase)
                => "DIGITADO",
            1 => "LIDO",
            _ => "MULTIPLA"
        };
    }

    public static IReadOnlyList<EntradaProdutoPesagem> ValidarSequencias(
        IEnumerable<EntradaProdutoPesagem> pesagens)
        => pesagens
            .Select((pesagem, indice) => pesagem with { Sequencia = indice + 1 })
            .ToList();

    public static decimal SomarPesoBrutoValido(IEnumerable<EntradaProdutoPesagem> pesagens)
        => PesagensValidas(pesagens).Sum(pesagem => pesagem.PesoBrutoKg);

    public static decimal SomarPesoTaraValido(IEnumerable<EntradaProdutoPesagem> pesagens)
        => PesagensValidas(pesagens).Sum(pesagem => pesagem.PesoTaraKg);

    public static decimal SomarPesoLiquidoValido(IEnumerable<EntradaProdutoPesagem> pesagens)
        => PesagensValidas(pesagens).Sum(pesagem => pesagem.PesoLiquidoKg);

    public static bool PossuiLeituraValida(IEnumerable<EntradaProdutoPesagem> pesagens)
        => PesagensValidas(pesagens).Any();

    private static IEnumerable<EntradaProdutoPesagem> PesagensValidas(
        IEnumerable<EntradaProdutoPesagem> pesagens)
        => pesagens.Where(pesagem =>
            string.Equals(
                pesagem.StatusPesagem?.Trim(),
                "VALIDA",
                StringComparison.OrdinalIgnoreCase));
}
