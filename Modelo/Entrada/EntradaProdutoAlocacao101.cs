using System.Globalization;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// GATE R2 (Recebimento de Materia-Prima): decide, POR POSICAO do documento 101, o que vai no campo
/// PADRAO de quantidade e o que vai nos campos CUSTOMIZADOS de peso.
/// <para>
/// Regra empresarial aprovada: para item ROH (Materia-Prima), QuantityInEntryUnit recebe a quantidade
/// INTEGRAL do item do Pedido de Compra (OrderQuantity) na unidade do pedido, e o peso liquido
/// efetivamente pesado (ja descontada a tara) vai em ZZ1_PESO_ENTRADA_MMI / ZZ1_PESO_ENTRADA_MMIU.
/// O SAP contabiliza a quantidade do pedido, NAO o peso.
/// </para>
/// <para>
/// Demais classificacoes mantem o comportamento LEGADO (quantidade = peso liquido, sem campos ZZ1).
/// Classificacao INDETERMINADA bloqueia: nunca se presume o tipo do material.
/// </para>
/// Funcao PURA: sem I/O, sem SAP, sem banco.
/// </summary>
public static class EntradaProdutoAlocacao101
{
    /// <summary>Unica unidade de quantidade suportada neste incremento. Outras exigem contrato de conversao.</summary>
    public const string UnidadeSuportada = "KG";

    /// <summary>Escala do contrato SAP dos campos customizados: Edm.Decimal(15,3).</summary>
    public const int CasasDecimaisContratoSap = 3;

    /// <summary>Precisao total do contrato Edm.Decimal(15,3) ⇒ no maximo 12 digitos inteiros.</summary>
    public const decimal LimiteInteiroContratoSap = 1_000_000_000_000m;

    /// <summary>Nome exato do campo customizado de peso (R1B/ARES).</summary>
    public const string CampoPeso = "ZZ1_PESO_ENTRADA_MMI";

    /// <summary>Nome exato do campo customizado de unidade do peso (R1B/ARES).</summary>
    public const string CampoPesoUnidade = "ZZ1_PESO_ENTRADA_MMIU";

    /// <summary>
    /// Formatacao dos campos do contrato (15,3): SEMPRE 3 casas decimais, cultura invariante, sem
    /// separador de milhar. 1000m ⇒ "1000.000"; 296.5m ⇒ "296.500". Espelha o exemplo aprovado.
    /// </summary>
    public static string FormatarDecimalContratoSap(decimal valor)
        => valor.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>true quando o valor cabe em Edm.Decimal(15,3) sem perder informacao.</summary>
    public static bool EscalaCompativelContratoSap(decimal valor)
        => Math.Abs(valor) < LimiteInteiroContratoSap
           && decimal.Round(valor, CasasDecimaisContratoSap, MidpointRounding.AwayFromZero) == valor;

    /// <summary>
    /// Decide a alocacao de UMA posicao. <paramref name="quantidadePedidoItem"/> e TRI-STATE: null
    /// significa DESCONHECIDA (bloqueia no caminho ROH), nunca zero por ausencia.
    /// </summary>
    public static ResultadoAlocacao101 Decidir(
        ClassificacaoEntradaMaterial classificacao,
        decimal? quantidadePedidoItem,
        string? unidadeQuantidadePedido,
        decimal pesoLiquidoKg,
        string descricaoItem)
    {
        if (classificacao == ClassificacaoEntradaMaterial.Indefinido)
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: classificacao do material nao determinada (ProductType ausente ou "
                + "nao consultado). Envio bloqueado — o tipo do material nunca e presumido.");
        }

        if (classificacao != ClassificacaoEntradaMaterial.MateriaPrima)
        {
            // Quimicos (HIBE) e demais tipos: comportamento LEGADO preservado, sem campos customizados.
            return ResultadoAlocacao101.Legado(
                EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(pesoLiquidoKg));
        }

        string unidade = (unidadeQuantidadePedido ?? string.Empty).Trim().ToUpperInvariant();
        if (unidade.Length == 0)
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: unidade de medida da quantidade do pedido ausente. Envio bloqueado.");
        }

        if (!string.Equals(unidade, UnidadeSuportada, StringComparison.Ordinal))
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: unidade de quantidade do pedido '{unidade}' nao suportada neste "
                + $"incremento (somente {UnidadeSuportada}). Conversao de unidade exige contrato aprovado. "
                + "Envio bloqueado.");
        }

        if (quantidadePedidoItem is not decimal quantidade)
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: quantidade do item do Pedido de Compra desconhecida. Envio bloqueado — "
                + "a quantidade documental nunca e presumida nem substituida pelo peso.");
        }

        if (quantidade <= 0m)
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: quantidade do item do Pedido de Compra nao positiva. Envio bloqueado.");
        }

        if (!EscalaCompativelContratoSap(quantidade))
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: quantidade do pedido fora do contrato SAP (15,3). Envio bloqueado.");
        }

        if (pesoLiquidoKg <= 0m)
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: peso liquido nao positivo. Envio bloqueado.");
        }

        if (!EscalaCompativelContratoSap(pesoLiquidoKg))
        {
            return ResultadoAlocacao101.Bloqueada(
                $"Item {descricaoItem}: peso liquido fora do contrato SAP (15,3). Envio bloqueado.");
        }

        return ResultadoAlocacao101.MateriaPrima(
            quantidadePadrao: FormatarDecimalContratoSap(quantidade),
            unidadePadrao: UnidadeSuportada,
            pesoLiquido: FormatarDecimalContratoSap(pesoLiquidoKg),
            unidadePeso: UnidadeSuportada);
    }
}

/// <summary>GATE R2: desfechos da decisao de alocacao. Nenhum deles e permissivo.</summary>
public enum CenarioAlocacao101
{
    /// <summary>ROH: quantidade do pedido no campo padrao + peso liquido nos campos customizados.</summary>
    MateriaPrimaComQuantidadeDoPedido = 0,

    /// <summary>Quimico (HIBE) e demais tipos: comportamento legado, sem campos customizados.</summary>
    Legado = 1,

    /// <summary>Pre-condicao ausente/incompativel: ZERO payload, ZERO POST.</summary>
    Bloqueada = 2
}

/// <summary>
/// GATE R2: resultado da decisao. Os campos customizados sao null em todo cenario que nao seja
/// <see cref="CenarioAlocacao101.MateriaPrimaComQuantidadeDoPedido"/> — valor e unidade sao tudo-ou-nada.
/// </summary>
public sealed record ResultadoAlocacao101
{
    private ResultadoAlocacao101(
        CenarioAlocacao101 cenario,
        string quantidadePadrao,
        string unidadePadrao,
        string? pesoLiquido,
        string? unidadePeso,
        string motivo)
    {
        Cenario = cenario;
        QuantidadePadrao = quantidadePadrao;
        UnidadePadrao = unidadePadrao;
        PesoLiquidoCustomizado = pesoLiquido;
        UnidadePesoCustomizado = unidadePeso;
        Motivo = motivo;
    }

    public CenarioAlocacao101 Cenario { get; }

    /// <summary>Valor de QuantityInEntryUnit (string, cultura invariante).</summary>
    public string QuantidadePadrao { get; }

    /// <summary>Valor de EntryUnit.</summary>
    public string UnidadePadrao { get; }

    /// <summary>Valor de ZZ1_PESO_ENTRADA_MMI; null quando nao aplicavel.</summary>
    public string? PesoLiquidoCustomizado { get; }

    /// <summary>Valor de ZZ1_PESO_ENTRADA_MMIU; null quando nao aplicavel.</summary>
    public string? UnidadePesoCustomizado { get; }

    public string Motivo { get; }

    public bool Aplicavel => Cenario != CenarioAlocacao101.Bloqueada;

    /// <summary>true SOMENTE no caminho ROH, com o par customizado materialmente presente.</summary>
    public bool UsaQuantidadeDoPedido =>
        Cenario == CenarioAlocacao101.MateriaPrimaComQuantidadeDoPedido
        && !string.IsNullOrWhiteSpace(PesoLiquidoCustomizado)
        && !string.IsNullOrWhiteSpace(UnidadePesoCustomizado);

    internal static ResultadoAlocacao101 MateriaPrima(
        string quantidadePadrao, string unidadePadrao, string pesoLiquido, string unidadePeso)
        => new(CenarioAlocacao101.MateriaPrimaComQuantidadeDoPedido,
            quantidadePadrao, unidadePadrao, pesoLiquido, unidadePeso, string.Empty);

    internal static ResultadoAlocacao101 Legado(string quantidadePadrao)
        => new(CenarioAlocacao101.Legado,
            quantidadePadrao, EntradaProdutoAlocacao101.UnidadeSuportada, null, null, string.Empty);

    internal static ResultadoAlocacao101 Bloqueada(string motivo)
        => new(CenarioAlocacao101.Bloqueada, string.Empty, string.Empty, null, null, motivo);
}
