using FugaPET_Dev.Modelo.Ambiente;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// GATE 126V: tara FISICA do material de embalagem do palete, declarada na configuracao operacional
/// (configuracao.sap.json, secao "sap" / "taras_fisicas_embalagem_palete"). NAO e peso de caixa, NAO vem de
/// hu_palete e NAO existe como constante no codigo de producao.
/// <para>
/// A chave logica e composta e inclui o MANDANTE: sap_client + centro + material de embalagem. O mandante
/// restringe o mapeamento ao piloto homologado (QAS110) por CONTRATO DE DADOS — copiar o arquivo para outro
/// ambiente nao autoriza a composicao, porque o mandante ativo deixa de casar.
/// </para>
/// </summary>
public sealed record TaraFisicaEmbalagemConfigurada
{
    /// <summary>Mandante SAP ao qual este mapeamento se aplica. Vazio ⇒ mapeamento inutilizavel (fail-closed).</summary>
    public string SapClient { get; init; } = string.Empty;

    /// <summary>Centro (Plant) do palete.</summary>
    public string Centro { get; init; } = string.Empty;

    /// <summary>Material de embalagem do palete (PackagingMaterial).</summary>
    public string MaterialEmbalagem { get; init; } = string.Empty;

    /// <summary>Peso fisico da embalagem. Interpretado SOMENTE quando <see cref="Unidade"/> e KG.</summary>
    public decimal PesoKg { get; init; }

    /// <summary>Unidade declarada. Diferente de KG ⇒ INVALIDA (nunca convertida silenciosamente).</summary>
    public string Unidade { get; init; } = string.Empty;
}

/// <summary>GATE 126V: desfechos possiveis da resolucao. Nenhum deles significa "zero por ausencia".</summary>
public enum CenarioTaraFisicaEmbalagem
{
    /// <summary>Mapeamento unico, valido e aplicavel.</summary>
    Disponivel = 0,

    /// <summary>Nao existe mapeamento para a chave logica. NAO equivale a zero.</summary>
    NaoConfigurada = 1,

    /// <summary>Mapeamento existe porem viola o contrato (unidade, sinal ou precisao).</summary>
    Invalida = 2,

    /// <summary>Mais de um mapeamento casa a mesma chave logica: ambiguidade nunca e resolvida por escolha.</summary>
    Ambigua = 3
}

/// <summary>
/// GATE 126V: resultado TRI-STATE da resolucao da tara fisica. <see cref="PesoKg"/> e
/// <c>null</c> quando DESCONHECIDA — nunca 0m por ausencia, invalidez ou ambiguidade.
/// </summary>
public sealed class ResultadoTaraFisicaEmbalagem
{
    private ResultadoTaraFisicaEmbalagem(CenarioTaraFisicaEmbalagem cenario, decimal? pesoKg, string mensagem)
    {
        Cenario = cenario;
        PesoKg = pesoKg;
        Mensagem = mensagem;
    }

    public CenarioTaraFisicaEmbalagem Cenario { get; }

    /// <summary>Peso resolvido. null = DESCONHECIDO (fail-closed); 0m EXPLICITO e um valor valido e distinto.</summary>
    public decimal? PesoKg { get; }

    public string Mensagem { get; }

    /// <summary>True SOMENTE com cenario Disponivel E peso materialmente presente.</summary>
    public bool Disponivel => Cenario == CenarioTaraFisicaEmbalagem.Disponivel && PesoKg.HasValue;

    /// <summary>
    /// GATE 126X: um resultado POSITIVO so pode ser emitido com a identidade QAS110 COMPROVADA. Sem ela
    /// nao existe caminho — publico ou interno — para produzir uma tara aplicavel, o que impede contornar
    /// o guard construindo o resultado diretamente. Identidade nula ⇒ Invalida (fail-closed).
    /// </summary>
    public static ResultadoTaraFisicaEmbalagem Resolvida(
        IdentidadeAmbienteQas110? identidadeQas110,
        decimal pesoKg)
        => identidadeQas110 is null
            ? Invalida("tara fisica sem identidade QAS110 comprovada: resultado positivo nao e emissivel.")
            : new(CenarioTaraFisicaEmbalagem.Disponivel, pesoKg, string.Empty);

    public static ResultadoTaraFisicaEmbalagem NaoConfigurada(string mensagem)
        => new(CenarioTaraFisicaEmbalagem.NaoConfigurada, null, mensagem);

    public static ResultadoTaraFisicaEmbalagem Invalida(string mensagem)
        => new(CenarioTaraFisicaEmbalagem.Invalida, null, mensagem);

    public static ResultadoTaraFisicaEmbalagem Ambigua(string mensagem)
        => new(CenarioTaraFisicaEmbalagem.Ambigua, null, mensagem);
}
