using System.Text.Json;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Estado do envio do palete via INT012 (CPI). Timeout pós-envio ⇒ IndeterminadoTimeout (zero retry).</summary>
public enum EstadoPaleteInt012
{
    NaoEnviado,     // gate false / PackagingMaterial ausente/PALLET01 / URL inválida ⇒ fail-closed, sem HTTP
    Confirmado,     // Status="S" + UC_gerada não vazia + nenhuma mensagem Tipo="E"
    ErroStatus,     // Status="E" ou mensagem Tipo="E"
    ContratoStatusPendente, // transporte OK porém sem UC/Status confiáveis (contrato inesperado)
    ErroHttp,       // HTTP não-2xx (rejeição de transporte)
    IndeterminadoTimeout
}

/// <summary>Mensagem do INT012 (_Mensagens[]): Id / Id_Msg / Tipo / Texto — sanitizada.</summary>
public sealed record MensagemPaleteInt012(string? Id, string? IdMsg, string? Tipo, string? Texto);

/// <summary>
/// Resultado tipado do INT012 (POST palete no CPI). Preserva o <c>Status</c> BRUTO e a UC gerada; mensagens
/// sanitizadas. A semântica final de quais valores de Status = sucesso é DEPENDENCIA_ARES_STATUS_INT012 —
/// enquanto não fechada, o parser de TRANSPORTE conclui, mas o resultado fica ContratoStatusPendente
/// (fail-closed, sem confirmar). Nunca expõe segredo/credencial.
/// </summary>
public sealed record ResultadoPaleteInt012
{
    public required EstadoPaleteInt012 Estado { get; init; }
    public string? UcGerada { get; init; }
    public string? StatusBruto { get; init; }
    public IReadOnlyList<MensagemPaleteInt012> Mensagens { get; init; } = [];
    public int? HttpStatus { get; init; }
    public string MensagemSanitizada { get; init; } = string.Empty;

    public static ResultadoPaleteInt012 NaoEnviado(string mensagem)
        => new() { Estado = EstadoPaleteInt012.NaoEnviado, MensagemSanitizada = mensagem };

    public static ResultadoPaleteInt012 Indeterminado(string mensagem, int? httpStatus = null)
        => new() { Estado = EstadoPaleteInt012.IndeterminadoTimeout, HttpStatus = httpStatus, MensagemSanitizada = mensagem };

    public static ResultadoPaleteInt012 ErroHttp(string mensagem, int? httpStatus)
        => new() { Estado = EstadoPaleteInt012.ErroHttp, HttpStatus = httpStatus, MensagemSanitizada = mensagem };
}

/// <summary>
/// Parser de TRANSPORTE do JSON de resposta do INT012: extrai <c>UC_gerada</c>, <c>Status</c> (bruto) e
/// <c>_Mensagens</c> (sanitizadas). NÃO decide sucesso/erro pela semântica de Status (DEPENDENCIA_ARES):
/// quando há Status de erro textual explícito, retorna ErroStatus; caso contrário ContratoStatusPendente.
/// </summary>
public static class PaleteInt012ResponseParser
{
    public static ResultadoPaleteInt012 Parsear(string? corpoJson, int httpStatus)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
        {
            return new ResultadoPaleteInt012
            {
                Estado = EstadoPaleteInt012.ContratoStatusPendente,
                HttpStatus = httpStatus,
                MensagemSanitizada = "Resposta INT012 vazia; contrato de Status pendente (DEPENDENCIA_ARES_STATUS_INT012)."
            };
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(corpoJson);
            JsonElement raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("d", out JsonElement d))
            {
                raiz = d;
            }

            string? uc = ExtrairString(raiz, "UC_gerada");
            string? status = ExtrairString(raiz, "Status");
            IReadOnlyList<MensagemPaleteInt012> mensagens = ExtrairMensagens(raiz);

            // §7 (contrato confirmado): Status="E" OU qualquer mensagem Tipo="E" ⇒ Erro. Status="S" + UC não
            // vazia + nenhum Tipo="E" ⇒ Confirmado. Sem heurística Contains("ERRO"/"ERROR"/"FALHA").
            bool temTipoErro = mensagens.Any(m => string.Equals(m.Tipo?.Trim(), "E", StringComparison.OrdinalIgnoreCase));
            bool statusS = string.Equals(status?.Trim(), "S", StringComparison.OrdinalIgnoreCase);
            bool statusE = string.Equals(status?.Trim(), "E", StringComparison.OrdinalIgnoreCase);

            EstadoPaleteInt012 estado;
            string msg;
            if (statusE || temTipoErro)
            {
                estado = EstadoPaleteInt012.ErroStatus;
                msg = "INT012 retornou erro (Status=E ou mensagem Tipo=E).";
            }
            else if (statusS && !string.IsNullOrWhiteSpace(uc))
            {
                estado = EstadoPaleteInt012.Confirmado;
                msg = $"Palete confirmado no SAP (UC {uc}).";
            }
            else
            {
                estado = EstadoPaleteInt012.ContratoStatusPendente;
                msg = "INT012 transportado, porém sem Status=S + UC_gerada confiáveis.";
            }

            return new ResultadoPaleteInt012
            {
                Estado = estado,
                UcGerada = uc,
                StatusBruto = status,
                Mensagens = mensagens,
                HttpStatus = httpStatus,
                MensagemSanitizada = msg
            };
        }
        catch (JsonException)
        {
            return new ResultadoPaleteInt012
            {
                Estado = EstadoPaleteInt012.ContratoStatusPendente,
                HttpStatus = httpStatus,
                MensagemSanitizada = "Resposta INT012 não é JSON válido."
            };
        }
    }

    private static string? ExtrairString(JsonElement raiz, string propriedade)
        => raiz.ValueKind == JsonValueKind.Object
            && raiz.TryGetProperty(propriedade, out JsonElement v)
            && v.ValueKind == JsonValueKind.String
                ? Sanitizar(v.GetString())
                : null;

    private static IReadOnlyList<MensagemPaleteInt012> ExtrairMensagens(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("_Mensagens", out JsonElement m)
            || m.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<MensagemPaleteInt012> lista = [];
        foreach (JsonElement item in m.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            lista.Add(new MensagemPaleteInt012(
                Prop(item, "Id"),
                Prop(item, "Id_Msg"),
                Prop(item, "Tipo"),
                Prop(item, "Texto")));
        }

        return lista;
    }

    private static string? Prop(JsonElement obj, string nome)
        => obj.TryGetProperty(nome, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? Sanitizar(v.GetString())
            : v.ValueKind is JsonValueKind.Number ? Sanitizar(v.ToString()) : null;

    private static string? Sanitizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        string s = texto.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        foreach (string segredo in new[] { "authorization", "password", "senha", "cookie", "token", "csrf", "basic " })
        {
            int idx = s.IndexOf(segredo, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                s = s[..idx] + "***";
                break;
            }
        }

        return s.Length <= 500 ? s : s[..500];
    }
}
