namespace FugaPET_Dev.Modelo.Processo;

public sealed record ComponenteConsumoDecisaoOperacional
{
    public const string DecisaoZeroIntencional = "ZERO_INTENCIONAL";

    public long CodigoApontamento { get; init; }
    public string NumeroReserva { get; init; } = string.Empty;
    public string ItemReserva { get; init; } = string.Empty;
    public string CodigoMaterial { get; init; } = string.Empty;
    public string DecisaoOperacional { get; init; } = DecisaoZeroIntencional;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
    public string Estacao { get; init; } = string.Empty;
}

public enum CenarioDecisaoOperacionalConsumo
{
    Registrada = 0,
    JaExistente = 1,
    ConflitoConsumoPositivo = 2,
    DadosInvalidos = 3,
    ErroPersistencia = 4
}

public sealed record ResultadoDecisaoOperacionalConsumo(
    CenarioDecisaoOperacionalConsumo Cenario,
    string Mensagem)
{
    public bool Sucesso =>
        Cenario is CenarioDecisaoOperacionalConsumo.Registrada
            or CenarioDecisaoOperacionalConsumo.JaExistente;

    public static ResultadoDecisaoOperacionalConsumo Registrada() =>
        new(CenarioDecisaoOperacionalConsumo.Registrada, "Componente registrado como não consumido para esta ocorrência.");

    public static ResultadoDecisaoOperacionalConsumo JaExistente() =>
        new(CenarioDecisaoOperacionalConsumo.JaExistente, "Componente já estava registrado como não consumido para esta ocorrência.");

    public static ResultadoDecisaoOperacionalConsumo ConflitoConsumoPositivo() =>
        new(CenarioDecisaoOperacionalConsumo.ConflitoConsumoPositivo, "Componente já possui consumo positivo nesta ocorrência.");

    public static ResultadoDecisaoOperacionalConsumo DadosInvalidos(string mensagem) =>
        new(CenarioDecisaoOperacionalConsumo.DadosInvalidos, mensagem);

    public static ResultadoDecisaoOperacionalConsumo ErroPersistencia() =>
        new(CenarioDecisaoOperacionalConsumo.ErroPersistencia, "Não foi possível registrar a decisão operacional do componente.");
}
