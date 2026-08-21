namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Classificação do erro definitivo de envio da HU, alinhada ao contrato de banco
/// (fn_hu_caixa_registrar_erro): <c>ERRO_DEFINITIVO</c> (pode ou não reprocessar) e <c>NAO_AUTORIZADO</c>
/// (HTTP 401/403; nunca reprocessável). Nunca usada para timeout.
/// </summary>
public enum ResultadoErroHu
{
    /// <summary>Erro determinístico do POST (não 401/403). Pode ou não liberar reprocessamento.</summary>
    ErroDefinitivo,

    /// <summary>HTTP 401/403 — não autorizado. NUNCA reprocessável.</summary>
    NaoAutorizado
}

/// <summary>
/// Texto do banco para <see cref="ResultadoErroHu"/> (coluna/argumento <c>resultado</c>).
/// </summary>
public static class ResultadoErroHuTexto
{
    public static string ParaBanco(ResultadoErroHu resultado)
        => resultado == ResultadoErroHu.NaoAutorizado ? "NAO_AUTORIZADO" : "ERRO_DEFINITIVO";
}

/// <summary>
/// Registro de UMA pesagem da caixa para <c>desenvolvimento.hu_caixa_pesagem</c>. Só a superfície de
/// INSERT autorizada. <c>peso_bruto = peso_liquido + peso_tara</c>, unidade KG. Em MANUAL a balança é
/// nula; em BALANCA a balança é obrigatória (validado cedo no C#; o banco também protege).
/// </summary>
public sealed class RegistroPesagemHuCaixa
{
    public long CodigoHuCaixa { get; init; }
    public long? CodigoBalanca { get; init; }
    public string OrigemPesagem { get; init; } = string.Empty;
    public decimal PesoLido { get; init; }
    public decimal PesoBruto { get; init; }
    public decimal PesoLiquido { get; init; }
    public decimal PesoTara { get; init; }
    public string UnidadePeso { get; init; } = "KG";

    /// <summary>Payload sanitizado da balança (JSON) — nunca credenciais. Opcional (MANUAL costuma ser nulo).</summary>
    public string? PayloadBalancaJson { get; init; }

    public long CodigoUsuario { get; init; }
}
