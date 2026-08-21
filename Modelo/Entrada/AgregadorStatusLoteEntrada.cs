namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ResultadoAgregacaoStatusEntrada
{
    public bool Sucesso { get; init; }
    public string? StatusAgregado { get; init; }
    public string Mensagem { get; init; } = string.Empty;

    public static ResultadoAgregacaoStatusEntrada Aprovado(string status)
        => new() { Sucesso = true, StatusAgregado = status };

    public static ResultadoAgregacaoStatusEntrada Inconsistente(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}

public static class AgregadorStatusLoteEntrada
{
    public const string StatusAgregadoEnviadoSap = "ENVIADO_SAP";

    public static ResultadoAgregacaoStatusEntrada Agregar(IEnumerable<string?> statusLotes)
    {
        if (statusLotes is null)
        {
            return ResultadoAgregacaoStatusEntrada.Inconsistente("Lista de status de lote não informada para agregação.");
        }

        string?[] status = statusLotes
            .Select(s => s?.Trim().ToUpperInvariant())
            .ToArray();

        if (status.Length == 0)
        {
            return ResultadoAgregacaoStatusEntrada.Inconsistente("Nenhum status de lote informado para agregação.");
        }

        if (status.Any(string.IsNullOrWhiteSpace))
        {
            return ResultadoAgregacaoStatusEntrada.Inconsistente("Status de lote vazio ou nulo informado para agregação.");
        }

        string[] statusNormalizados = status.Select(s => s!).ToArray();
        string[] permitidos =
        [
            StatusLoteEntrada.FinalizadoLocal,
            StatusLoteEntrada.EnviandoSap,
            StatusLoteEntrada.ConfirmadoSap,
            StatusLoteEntrada.ErroSap,
            StatusLoteEntrada.Cancelado
        ];

        string? invalido = statusNormalizados.FirstOrDefault(s => !permitidos.Contains(s, StringComparer.Ordinal));
        if (invalido is not null)
        {
            return ResultadoAgregacaoStatusEntrada.Inconsistente($"Status de lote inválido para agregação: {invalido}.");
        }

        if (statusNormalizados.Contains(StatusLoteEntrada.EnviandoSap, StringComparer.Ordinal))
        {
            return ResultadoAgregacaoStatusEntrada.Aprovado(StatusAgregadoEnviadoSap);
        }

        if (statusNormalizados.Contains(StatusLoteEntrada.ErroSap, StringComparer.Ordinal))
        {
            return ResultadoAgregacaoStatusEntrada.Aprovado(StatusLoteEntrada.ErroSap);
        }

        if (statusNormalizados.Contains(StatusLoteEntrada.FinalizadoLocal, StringComparer.Ordinal))
        {
            return ResultadoAgregacaoStatusEntrada.Aprovado(StatusLoteEntrada.FinalizadoLocal);
        }

        if (statusNormalizados.All(s => s == StatusLoteEntrada.ConfirmadoSap))
        {
            return ResultadoAgregacaoStatusEntrada.Aprovado(StatusLoteEntrada.ConfirmadoSap);
        }

        if (statusNormalizados.All(s => s == StatusLoteEntrada.Cancelado))
        {
            return ResultadoAgregacaoStatusEntrada.Aprovado(StatusLoteEntrada.Cancelado);
        }

        return ResultadoAgregacaoStatusEntrada.Inconsistente(
            "Combinação de status de lote inconsistente para agregação.");
    }
}