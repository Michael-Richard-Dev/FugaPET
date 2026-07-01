namespace FugaPET_Dev.Modelo.Diagnostico;

public sealed class DiagnosticoConsumoSap261Resultado
{
    public bool ProntoParaTesteLocal { get; init; }
    public bool ProntoParaPreview { get; init; }
    public bool ProntoParaEnvioSap { get; init; }
    public IReadOnlyList<DiagnosticoConsumoSap261Item> Itens { get; init; } = [];
}
