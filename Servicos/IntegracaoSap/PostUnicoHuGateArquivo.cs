using System.Text;
using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public interface IPostUnicoHuGate
{
    string GateId { get; }
    bool Disponivel { get; }
    bool Consumido { get; }
    ResultadoConsumoPostUnicoHu TentarConsumir();
}

public sealed record ResultadoConsumoPostUnicoHu(bool ConsumidoAgora, string MensagemSanitizada);

/// <summary>
/// Gate local persistente do HML-2. O marcador é criado com CreateNew imediatamente antes do POST,
/// não contém segredo e nunca é removido automaticamente.
/// </summary>
public sealed class PostUnicoHuGateArquivo : IPostUnicoHuGate
{
    private static readonly Regex GateIdValido = new("^[A-Za-z0-9._-]{1,80}$", RegexOptions.CultureInvariant);
    private readonly string _caminhoMarcador;

    public PostUnicoHuGateArquivo(string gateId, string? diretorioBase = null)
    {
        GateId = NormalizarGateId(gateId);
        string raiz = string.IsNullOrWhiteSpace(diretorioBase)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.GetFullPath(diretorioBase);
        _caminhoMarcador = Path.Combine(raiz, "FugaPET", "Gates", $"{GateId}.consumed");
    }

    public string GateId { get; }
    public bool Consumido => File.Exists(_caminhoMarcador);
    public bool Disponivel => !Consumido;

    public ResultadoConsumoPostUnicoHu TentarConsumir()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_caminhoMarcador)!);
            using FileStream marcador = new(
                _caminhoMarcador,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 256,
                FileOptions.WriteThrough);
            byte[] conteudo = Encoding.UTF8.GetBytes(
                $"gate_id={GateId}{Environment.NewLine}consumido_utc={DateTimeOffset.UtcNow:O}{Environment.NewLine}");
            marcador.Write(conteudo);
            marcador.Flush(flushToDisk: true);
            return new ResultadoConsumoPostUnicoHu(true, "Gate HML-2 consumido para uma única tentativa de POST HU.");
        }
        catch (IOException) when (File.Exists(_caminhoMarcador))
        {
            return new ResultadoConsumoPostUnicoHu(false, "Gate HML-2 já consumido. Novo POST bloqueado.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ResultadoConsumoPostUnicoHu(
                false,
                $"Gate HML-2 indisponível: {ex.GetType().Name}. POST bloqueado por segurança.");
        }
    }

    public static bool GateIdEhValido(string? gateId)
        => !string.IsNullOrWhiteSpace(gateId) && GateIdValido.IsMatch(gateId.Trim());

    private static string NormalizarGateId(string gateId)
    {
        string normalizado = gateId?.Trim() ?? string.Empty;
        return GateIdEhValido(normalizado)
            ? normalizado
            : throw new ArgumentException("GateId HML-2 inválido.", nameof(gateId));
    }
}
