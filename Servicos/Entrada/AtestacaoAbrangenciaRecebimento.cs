using System.Globalization;
using System.Text.Json;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// Atestacao HUMANA de abrangencia para UM pedido+item.
/// <para>
/// O servico de historico declara <c>AbrangenciaComprovada=false</c> por construcao: a consulta
/// prova o que encontrou, nao a cobertura do historico. O proprio codigo registra que essa
/// atestacao e decisao empresarial externa. Esta classe e o canal por onde essa decisao entra —
/// nomeada, com responsavel, motivo e validade.
/// </para>
/// Nao e um contorno da protecao: ela fornece a evidencia que falta, e so isso. Nenhuma outra
/// condicao do motor e afrouxada.
/// </summary>
public sealed record AtestacaoAbrangenciaRecebimento(
    string NumeroPedido,
    string NumeroItem,
    string Operador,
    string Motivo,
    DateTimeOffset ValidoAteUtc)
{
    public const string NomeArquivo = "entrada_mp_p5_atestacao_abrangencia.json";

    /// <summary>Normaliza o item em 5 digitos, como a consulta local e a exclusividade.</summary>
    public static string NormalizarItem(string? item)
    {
        string texto = (item ?? string.Empty).Trim().TrimStart('0');
        return (texto.Length == 0 ? "0" : texto).PadLeft(5, '0');
    }

    public bool Vale(string? pedido, string? item, DateTimeOffset agoraUtc)
        => !string.IsNullOrWhiteSpace(Operador)
        && !string.IsNullOrWhiteSpace(Motivo)
        && agoraUtc <= ValidoAteUtc
        && string.Equals((pedido ?? string.Empty).Trim(), NumeroPedido.Trim(), StringComparison.Ordinal)
        && string.Equals(NormalizarItem(item), NormalizarItem(NumeroItem), StringComparison.Ordinal);

    /// <summary>
    /// Le a atestacao do arquivo ao lado do executavel. Ausencia, formato invalido ou qualquer
    /// erro resultam em <c>null</c> — isto e, nenhuma atestacao, e o bloqueio permanece.
    /// </summary>
    public static AtestacaoAbrangenciaRecebimento? Carregar(string? diretorio = null)
    {
        try
        {
            string caminho = Path.Combine(diretorio ?? AppContext.BaseDirectory, NomeArquivo);
            if (!File.Exists(caminho)) return null;
            return Interpretar(File.ReadAllText(caminho));
        }
        catch
        {
            return null;
        }
    }

    internal static AtestacaoAbrangenciaRecebimento? Interpretar(string json)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;
            string? Texto(string nome) => raiz.TryGetProperty(nome, out JsonElement valor)
                && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

            string? pedido = Texto("numeroPedido");
            string? item = Texto("numeroItem");
            string? operador = Texto("operador");
            string? motivo = Texto("motivo");
            string? validade = Texto("validoAteUtc");

            if (string.IsNullOrWhiteSpace(pedido) || string.IsNullOrWhiteSpace(item)
                || string.IsNullOrWhiteSpace(operador) || string.IsNullOrWhiteSpace(motivo)
                || !DateTimeOffset.TryParse(validade, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset validoAte))
            {
                return null;
            }

            return new AtestacaoAbrangenciaRecebimento(
                pedido.Trim(), item.Trim(), operador.Trim(), motivo.Trim(), validoAte);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Envolve o servico real de historico e, SOMENTE para o pedido+item atestado, marca
/// <c>AbrangenciaComprovada=true</c>.
/// <para>
/// A marcacao exige que a leitura real tenha sido bem-sucedida, completa, sem documento incerto
/// e sem NENHUM movimento 101/102. Se o SAP devolver qualquer movimento, a atestacao e ignorada:
/// ela cobre a duvida sobre a COBERTURA da consulta, nunca a existencia de um recebimento.
/// </para>
/// </summary>
public sealed class HistoricoMaterialDocumentSapAtestadoServico(
    IHistoricoMaterialDocumentSapServico interno,
    AtestacaoAbrangenciaRecebimento? atestacao,
    Action<string>? registrarDiagnostico = null,
    Func<DateTimeOffset>? agoraUtc = null) : IHistoricoMaterialDocumentSapServico
{
    public bool Disponivel => interno.Disponivel;

    public async Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
        string numeroPedido,
        string numeroItemPedido,
        CancellationToken cancellationToken = default)
    {
        ResultadoHistoricoMaterialSap resultado =
            await interno.ConsultarPorPedidoItemAsync(numeroPedido, numeroItemPedido, cancellationToken);

        if (resultado.AbrangenciaComprovada) return resultado;

        DateTimeOffset agora = (agoraUtc ?? (() => DateTimeOffset.UtcNow))();
        if (atestacao is null || !atestacao.Vale(numeroPedido, numeroItemPedido, agora))
        {
            return resultado;
        }

        if (resultado.Cenario != CenarioHistoricoMaterialSap.Lido
            || !resultado.PaginacaoCompleta
            || resultado.TemDocumentoIncerto
            || resultado.Documentos101 != 0
            || resultado.Movimentos102 != 0)
        {
            registrarDiagnostico?.Invoke(
                "P5: atestacao de abrangencia NAO aplicada — a leitura do historico nao foi "
                + "conclusiva ou encontrou movimentos. Bloqueio preservado.");
            return resultado;
        }

        registrarDiagnostico?.Invoke(
            $"P5: abrangencia ATESTADA MANUALMENTE para {numeroPedido}/{numeroItemPedido} por "
            + $"{atestacao.Operador}, valida ate {atestacao.ValidoAteUtc:O}. Motivo: {atestacao.Motivo}. "
            + "A ausencia de movimentos 101/102 passa a ser aceita como cobertura comprovada "
            + "para ESTE item.");

        return resultado with { AbrangenciaComprovada = true };
    }
}
