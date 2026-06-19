namespace FugaPET_Dev.Servicos.Seguranca;

public sealed class PermissaoSessaoAplicacao
{
    public string Modulo { get; init; } = string.Empty;
    public string Rotina { get; init; } = string.Empty;
    public string Acao { get; init; } = string.Empty;

    public bool Corresponde(string modulo, string rotina, string acao)
    {
        return TextoIgual(Modulo, modulo)
            && TextoIgual(Rotina, rotina)
            && TextoIgual(Acao, acao);
    }

    private static bool TextoIgual(string atual, string esperado)
    {
        return string.Equals(
            Normalizar(atual),
            Normalizar(esperado),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalizar(string valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : valor.Trim().ToUpperInvariant();
    }
}
