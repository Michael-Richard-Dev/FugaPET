using System.Text.Json;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed record EntradaProdutoLancamentoPersistidoComLotes
{
    public long CodigoLancamento { get; init; }
    public string NumeroPedido { get; init; } = string.Empty;
    public string? Fornecedor { get; init; }
    public long? CodigoSetor { get; init; }
    public string? Terminal { get; init; }
    public string StatusLancamento { get; init; } = string.Empty;
    public IReadOnlyList<EntradaProdutoItemPersistidoComLotes> Itens { get; init; } = [];
}

public sealed record EntradaProdutoItemPersistidoComLotes
{
    public long CodigoItem { get; init; }
    public long? CodigoSapPedidoCompraItem { get; init; }
    public string NumeroItemSap { get; init; } = string.Empty;
    public string? Material { get; init; }
    public string? Centro { get; init; }
    public string? Deposito { get; init; }
    public string? Unidade { get; init; }
    public decimal? QuantidadePrevista { get; init; }
    public IReadOnlyList<EntradaProdutoLotePersistidoComPesagens> Lotes { get; init; } = [];
}

public sealed record EntradaProdutoLotePersistidoComPesagens
{
    public long CodigoLote { get; init; }
    public Guid CorrelationId { get; init; }
    public string NumeroLote { get; init; } = string.Empty;
    public DateTime DataFabricacao { get; init; }
    public DateTime DataVencimento { get; init; }
    public string StatusLote { get; init; } = string.Empty;
    public decimal PesoLiquidoTotalKg { get; init; }
    public IReadOnlyList<EntradaProdutoPesagemPersistidaLote> Pesagens { get; init; } = [];
}

public sealed record EntradaProdutoPesagemPersistidaLote
{
    public long CodigoPesagem { get; init; }
    public int Sequencia { get; init; }
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoTaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public long? CodigoTara { get; init; }
    public long? CodigoBalanca { get; init; }
    public string Origem { get; init; } = string.Empty;
    public string StatusPesagem { get; init; } = string.Empty;
    public string? LeituraOriginal { get; init; }
    public string? PayloadBalanca { get; init; }
    public DateTimeOffset PesadoEm { get; init; }
    public string? NumeroLoteSnapshot { get; init; }
    public DateTime? DataFabricacaoSnapshot { get; init; }
    public DateTime? DataVencimentoSnapshot { get; init; }
}

public sealed record ResultadoValidacaoRecuperacaoEntradaLotes
{
    public bool Sucesso { get; init; }
    public string? Divergencia { get; init; }
    public ResultadoPersistenciaEntradaComLotes? ResultadoRecuperado { get; init; }

    public static ResultadoValidacaoRecuperacaoEntradaLotes Ok(ResultadoPersistenciaEntradaComLotes resultado)
        => new() { Sucesso = true, ResultadoRecuperado = resultado };

    public static ResultadoValidacaoRecuperacaoEntradaLotes Falha(string divergencia)
        => new() { Sucesso = false, Divergencia = divergencia };
}

public static class ValidadorRecuperacaoPersistenciaEntradaLotes
{
    private const decimal ToleranciaDecimal = 0.0005m;
    private static readonly TimeSpan ToleranciaPostgres = TimeSpan.FromMilliseconds(1);

    public static ResultadoValidacaoRecuperacaoEntradaLotes Validar(
        EntradaProdutoLancamentoComLotesPersistencia esperado,
        EntradaProdutoLancamentoPersistidoComLotes persistido)
    {
        ArgumentNullException.ThrowIfNull(esperado);
        ArgumentNullException.ThrowIfNull(persistido);

        Dictionary<string, long> codigosItens = new(StringComparer.Ordinal);
        Dictionary<Guid, long> codigosLotes = [];
        Dictionary<Guid, long> codigosPesagens = [];
        Dictionary<Guid, decimal> pesosConsolidados = [];

        string? divergencia = ValidarLancamento(esperado.Lancamento, persistido);
        if (divergencia is not null)
        {
            return ResultadoValidacaoRecuperacaoEntradaLotes.Falha(divergencia);
        }

        Dictionary<string, EntradaProdutoItemComLotesPersistencia> itensEsperados = esperado.Itens
            .ToDictionary(item => NormalizarItemSap(item.NumeroItemSap), StringComparer.Ordinal);
        Dictionary<string, EntradaProdutoItemPersistidoComLotes> itensPersistidos = persistido.Itens
            .ToDictionary(item => NormalizarItemSap(item.NumeroItemSap), StringComparer.Ordinal);

        divergencia = ValidarConjunto(itensEsperados.Keys, itensPersistidos.Keys, "item");
        if (divergencia is not null)
        {
            return ResultadoValidacaoRecuperacaoEntradaLotes.Falha(divergencia);
        }

        foreach ((string numeroItem, EntradaProdutoItemComLotesPersistencia itemEsperado) in itensEsperados)
        {
            EntradaProdutoItemPersistidoComLotes itemPersistido = itensPersistidos[numeroItem];
            codigosItens.Add(numeroItem, itemPersistido.CodigoItem);

            divergencia = ValidarItem(itemEsperado, itemPersistido);
            if (divergencia is not null)
            {
                return ResultadoValidacaoRecuperacaoEntradaLotes.Falha(divergencia);
            }

            divergencia = ValidarLotes(
                itemEsperado,
                itemPersistido,
                codigosLotes,
                codigosPesagens,
                pesosConsolidados);
            if (divergencia is not null)
            {
                return ResultadoValidacaoRecuperacaoEntradaLotes.Falha(divergencia);
            }
        }

        return ResultadoValidacaoRecuperacaoEntradaLotes.Ok(
            ResultadoPersistenciaEntradaComLotes.CriarRecuperado(
                persistido.CodigoLancamento,
                codigosItens,
                codigosLotes,
                codigosPesagens,
                pesosConsolidados));
    }

    private static string? ValidarLancamento(
        EntradaProdutoLancamento esperado,
        EntradaProdutoLancamentoPersistidoComLotes persistido)
    {
        if (!TextoIgual(esperado.NumeroPedido, persistido.NumeroPedido))
        {
            return "Lancamento divergente: numero do pedido.";
        }

        if (!TextoIgual(esperado.Fornecedor, persistido.Fornecedor))
        {
            return "Lancamento divergente: fornecedor.";
        }

        if (esperado.CodigoSetor != persistido.CodigoSetor)
        {
            return "Lancamento divergente: setor.";
        }

        if (!TextoIgual(esperado.Terminal, persistido.Terminal))
        {
            return "Lancamento divergente: terminal.";
        }

        if (!TextoIgual(StatusLoteEntrada.FinalizadoLocal, persistido.StatusLancamento))
        {
            return "Lancamento divergente: status local.";
        }

        return null;
    }

    private static string? ValidarItem(
        EntradaProdutoItemComLotesPersistencia esperado,
        EntradaProdutoItemPersistidoComLotes persistido)
    {
        EntradaProdutoItem item = esperado.Item;
        if (item.CodigoSapPedidoCompraItem != persistido.CodigoSapPedidoCompraItem)
        {
            return $"Item {esperado.NumeroItemSap} divergente: codigo SAP.";
        }

        if (!TextoIgual(NormalizarItemSap(esperado.NumeroItemSap), NormalizarItemSap(persistido.NumeroItemSap)))
        {
            return $"Item {esperado.NumeroItemSap} divergente: numero SAP.";
        }

        if (!TextoIgual(item.Material, persistido.Material)
            || !TextoIgual(item.Centro, persistido.Centro)
            || !TextoIgual(item.Deposito, persistido.Deposito)
            || !TextoIgual(item.Unidade, persistido.Unidade))
        {
            return $"Item {esperado.NumeroItemSap} divergente: dados SAP.";
        }

        if (!DecimalIgual(item.QuantidadePrevista, persistido.QuantidadePrevista))
        {
            return $"Item {esperado.NumeroItemSap} divergente: quantidade prevista.";
        }

        return null;
    }

    private static string? ValidarLotes(
        EntradaProdutoItemComLotesPersistencia itemEsperado,
        EntradaProdutoItemPersistidoComLotes itemPersistido,
        Dictionary<Guid, long> codigosLotes,
        Dictionary<Guid, long> codigosPesagens,
        Dictionary<Guid, decimal> pesosConsolidados)
    {
        Dictionary<Guid, EntradaProdutoLoteComPesagensPersistencia> lotesEsperados =
            itemEsperado.Lotes.ToDictionary(lote => lote.CorrelationId);
        Dictionary<Guid, EntradaProdutoLotePersistidoComPesagens> lotesPersistidos =
            itemPersistido.Lotes.ToDictionary(lote => lote.CorrelationId);

        string? divergencia = ValidarConjunto(lotesEsperados.Keys, lotesPersistidos.Keys, "lote/correlation_id");
        if (divergencia is not null)
        {
            return divergencia;
        }

        foreach ((Guid correlationId, EntradaProdutoLoteComPesagensPersistencia loteEsperado) in lotesEsperados)
        {
            EntradaProdutoLotePersistidoComPesagens lotePersistido = lotesPersistidos[correlationId];

            if (!TextoIgual(loteEsperado.Dados.NumeroLote, lotePersistido.NumeroLote)
                || loteEsperado.Dados.DataFabricacao.Date != lotePersistido.DataFabricacao.Date
                || loteEsperado.Dados.DataVencimento.Date != lotePersistido.DataVencimento.Date)
            {
                return $"Lote {correlationId} divergente: dados do lote.";
            }

            if (!TextoIgual(StatusLoteEntrada.FinalizadoLocal, lotePersistido.StatusLote))
            {
                return $"Lote {correlationId} divergente: status local.";
            }

            decimal pesoEsperado = loteEsperado.Pesagens
                .Where(p => TextoIgual(p.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida))
                .Sum(p => NormalizarDecimal(p.Pesagem.PesoLiquidoKg));
            if (!DecimalIgual(pesoEsperado, lotePersistido.PesoLiquidoTotalKg))
            {
                return $"Lote {correlationId} divergente: peso consolidado.";
            }

            codigosLotes.Add(loteEsperado.CodigoLocal, lotePersistido.CodigoLote);
            pesosConsolidados.Add(loteEsperado.CodigoLocal, NormalizarDecimal(lotePersistido.PesoLiquidoTotalKg));

            divergencia = ValidarPesagens(loteEsperado, lotePersistido, codigosPesagens);
            if (divergencia is not null)
            {
                return divergencia;
            }
        }

        return null;
    }

    private static string? ValidarPesagens(
        EntradaProdutoLoteComPesagensPersistencia loteEsperado,
        EntradaProdutoLotePersistidoComPesagens lotePersistido,
        Dictionary<Guid, long> codigosPesagens)
    {
        Dictionary<int, EntradaProdutoPesagemComCodigoLocalPersistencia> pesagensEsperadas =
            loteEsperado.Pesagens.ToDictionary(pesagem => pesagem.Pesagem.Sequencia);
        Dictionary<int, EntradaProdutoPesagemPersistidaLote> pesagensPersistidas =
            lotePersistido.Pesagens.ToDictionary(pesagem => pesagem.Sequencia);

        string? divergencia = ValidarConjunto(pesagensEsperadas.Keys, pesagensPersistidas.Keys, "pesagem/sequencia");
        if (divergencia is not null)
        {
            return divergencia;
        }

        foreach ((int sequencia, EntradaProdutoPesagemComCodigoLocalPersistencia esperadaComCodigo) in pesagensEsperadas)
        {
            EntradaProdutoPesagem esperada = esperadaComCodigo.Pesagem;
            EntradaProdutoPesagemPersistidaLote persistida = pesagensPersistidas[sequencia];

            if (!DecimalIgual(esperada.PesoBrutoKg, persistida.PesoBrutoKg)
                || !DecimalIgual(esperada.PesoTaraKg, persistida.PesoTaraKg)
                || !DecimalIgual(esperada.PesoLiquidoKg, persistida.PesoLiquidoKg))
            {
                return $"Pesagem {sequencia} divergente: pesos.";
            }

            if (esperada.CodigoTara != persistida.CodigoTara
                || esperada.CodigoBalanca != persistida.CodigoBalanca
                || !TextoIgual(esperada.Origem, persistida.Origem)
                || !TextoIgual(esperada.StatusPesagem, persistida.StatusPesagem)
                || !TextoIgual(esperada.LeituraOriginal, persistida.LeituraOriginal)
                || !JsonEquivalente(esperada.PayloadBalanca, persistida.PayloadBalanca))
            {
                return $"Pesagem {sequencia} divergente: dados operacionais.";
            }

            if (!DataHoraEquivalente(esperada.PesadoEm, persistida.PesadoEm))
            {
                return $"Pesagem {sequencia} divergente: pesado_em.";
            }

            if (!TextoIgual(loteEsperado.Dados.NumeroLote, persistida.NumeroLoteSnapshot)
                || loteEsperado.Dados.DataFabricacao.Date != persistida.DataFabricacaoSnapshot?.Date
                || loteEsperado.Dados.DataVencimento.Date != persistida.DataVencimentoSnapshot?.Date)
            {
                return $"Pesagem {sequencia} divergente: snapshots do lote.";
            }

            codigosPesagens.Add(esperadaComCodigo.CodigoLocalPesagem, persistida.CodigoPesagem);
        }

        return null;
    }

    private static string? ValidarConjunto<T>(
        IEnumerable<T> esperado,
        IEnumerable<T> persistido,
        string nome)
        where T : notnull
    {
        HashSet<T> esperados = esperado.ToHashSet();
        HashSet<T> persistidos = persistido.ToHashSet();
        if (esperados.SetEquals(persistidos))
        {
            return null;
        }

        // §8: NÃO usar FirstOrDefault()+"is not null" para tipos-valor (Guid/int) — o default (Guid.Empty/0) é
        // ambíguo e faria um conjunto só com elementos ADICIONAIS ser reportado como "ausente (Guid.Empty)".
        // Materializa as duas diferenças e escolhe explicitamente por contagem.
        List<T> ausentes = esperados.Except(persistidos).ToList();
        if (ausentes.Count > 0)
        {
            return $"Arvore divergente: {nome} ausente ({ausentes[0]}).";
        }

        List<T> adicionais = persistidos.Except(esperados).ToList();
        return $"Arvore divergente: {nome} adicional ({adicionais[0]}).";
    }

    /// <summary>
    /// §6 — Compara dois JSONB (payload_balanca) de forma SEMÂNTICA, não textual: null/vazio/whitespace é
    /// tratado como AUSÊNCIA (mesma normalização da inserção); dois JSONs válidos são comparados por valor
    /// (whitespace irrelevante, ordem de propriedades de objeto irrelevante, ordem de elementos de array
    /// relevante). JSON inválido resulta em divergência controlada (false), nunca em exceção não tratada.
    /// </summary>
    internal static bool JsonEquivalente(string? esperado, string? persistido)
    {
        bool esperadoAusente = string.IsNullOrWhiteSpace(esperado);
        bool persistidoAusente = string.IsNullOrWhiteSpace(persistido);
        if (esperadoAusente || persistidoAusente)
        {
            return esperadoAusente && persistidoAusente;
        }

        try
        {
            using JsonDocument documentoEsperado = JsonDocument.Parse(esperado!);
            using JsonDocument documentoPersistido = JsonDocument.Parse(persistido!);
            return JsonElementoIgual(documentoEsperado.RootElement, documentoPersistido.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool JsonElementoIgual(JsonElement esquerdo, JsonElement direito)
    {
        if (esquerdo.ValueKind != direito.ValueKind)
        {
            return false;
        }

        switch (esquerdo.ValueKind)
        {
            case JsonValueKind.Object:
                // Dicionários dos DOIS lados: propriedade repetida substitui a anterior (a ÚLTIMA prevalece),
                // reproduzindo o comportamento do jsonb do PostgreSQL. Não se conta diretamente o total de
                // propriedades enumeradas — usa-se a contagem já deduplicada por nome.
                Dictionary<string, JsonElement> propriedadesEsquerdo = [];
                foreach (JsonProperty propriedade in esquerdo.EnumerateObject())
                {
                    propriedadesEsquerdo[propriedade.Name] = propriedade.Value;
                }

                Dictionary<string, JsonElement> propriedadesDireito = [];
                foreach (JsonProperty propriedade in direito.EnumerateObject())
                {
                    propriedadesDireito[propriedade.Name] = propriedade.Value;
                }

                if (propriedadesEsquerdo.Count != propriedadesDireito.Count)
                {
                    return false;
                }

                foreach ((string nome, JsonElement valorEsquerdo) in propriedadesEsquerdo)
                {
                    if (!propriedadesDireito.TryGetValue(nome, out JsonElement valorDireito)
                        || !JsonElementoIgual(valorEsquerdo, valorDireito))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValueKind.Array:
                if (esquerdo.GetArrayLength() != direito.GetArrayLength())
                {
                    return false;
                }

                JsonElement.ArrayEnumerator enumeradorEsquerdo = esquerdo.EnumerateArray();
                JsonElement.ArrayEnumerator enumeradorDireito = direito.EnumerateArray();
                while (enumeradorEsquerdo.MoveNext() && enumeradorDireito.MoveNext())
                {
                    if (!JsonElementoIgual(enumeradorEsquerdo.Current, enumeradorDireito.Current))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValueKind.Number:
                return esquerdo.TryGetDecimal(out decimal decimalEsquerdo)
                       && direito.TryGetDecimal(out decimal decimalDireito)
                    ? decimalEsquerdo == decimalDireito
                    : string.Equals(esquerdo.GetRawText(), direito.GetRawText(), StringComparison.Ordinal);

            case JsonValueKind.String:
                return string.Equals(esquerdo.GetString(), direito.GetString(), StringComparison.Ordinal);

            default:
                // True, False, Null: o ValueKind idêntico já garante a igualdade.
                return true;
        }
    }

    private static bool TextoIgual(string? esquerdo, string? direito)
        => string.Equals(esquerdo?.Trim() ?? string.Empty, direito?.Trim() ?? string.Empty, StringComparison.Ordinal);

    private static bool DecimalIgual(decimal? esquerdo, decimal? direito)
        => esquerdo is null && direito is null
           || esquerdo is not null
           && direito is not null
           && Math.Abs(NormalizarDecimal(esquerdo.Value) - NormalizarDecimal(direito.Value)) <= ToleranciaDecimal;

    private static decimal NormalizarDecimal(decimal valor)
        => decimal.Round(valor, 3, MidpointRounding.AwayFromZero);

    private static string NormalizarItemSap(string? numeroItem)
        => EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItem);

    private static bool DataHoraEquivalente(DateTimeOffset esperado, DateTimeOffset persistido)
        => (TruncarParaMilissegundo(esperado.ToUniversalTime()) - TruncarParaMilissegundo(persistido.ToUniversalTime())).Duration()
           <= ToleranciaPostgres;

    private static DateTimeOffset TruncarParaMilissegundo(DateTimeOffset valor)
    {
        long ticks = valor.Ticks - (valor.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
