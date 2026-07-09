using System.Text.Json;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal static class SapErroODataParser
{
    public static SapErroDetalhado ExtrairErroSap(string? responseBody)
    {
        string retornoBruto = responseBody ?? string.Empty;
        if (string.IsNullOrWhiteSpace(retornoBruto))
        {
            return new SapErroDetalhado { RetornoBruto = string.Empty };
        }

        SapErroDetalhado? json = TentarExtrairJson(retornoBruto);
        if (json is not null)
        {
            return json;
        }

        SapErroDetalhado? xml = TentarExtrairXml(retornoBruto);
        if (xml is not null)
        {
            return xml;
        }

        return new SapErroDetalhado
        {
            Mensagem = retornoBruto.Trim(),
            Detalhes = retornoBruto.Trim(),
            RetornoBruto = retornoBruto
        };
    }

    private static SapErroDetalhado? TentarExtrairJson(string responseBody)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("error", out JsonElement erro))
            {
                return null;
            }

            string? codigo = LerString(erro, "code");
            string? mensagem = null;
            if (erro.TryGetProperty("message", out JsonElement mensagemElemento))
            {
                mensagem = mensagemElemento.ValueKind == JsonValueKind.Object
                    ? LerString(mensagemElemento, "value")
                    : mensagemElemento.ValueKind == JsonValueKind.String
                        ? mensagemElemento.GetString()
                        : null;
            }

            List<string> detalhes = [];
            if (erro.TryGetProperty("innererror", out JsonElement inner)
                && inner.TryGetProperty("errordetails", out JsonElement errordetails)
                && errordetails.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement detalhe in errordetails.EnumerateArray())
                {
                    string? code = LerString(detalhe, "code");
                    string? message = LerString(detalhe, "message");
                    if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(message))
                    {
                        detalhes.Add(string.IsNullOrWhiteSpace(code)
                            ? message!
                            : string.IsNullOrWhiteSpace(message)
                                ? code!
                                : $"{code}: {message}");
                    }
                }
            }

            return new SapErroDetalhado
            {
                Codigo = codigo,
                Mensagem = mensagem,
                Detalhes = detalhes.Count == 0 ? null : string.Join(" | ", detalhes),
                RetornoBruto = responseBody
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SapErroDetalhado? TentarExtrairXml(string responseBody)
    {
        try
        {
            XDocument doc = XDocument.Parse(responseBody);
            XElement? erro = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "error");
            if (erro is null)
            {
                return null;
            }

            string? codigo = erro.Elements().FirstOrDefault(e => e.Name.LocalName == "code")?.Value;
            string? mensagem = erro.Elements().FirstOrDefault(e => e.Name.LocalName == "message")?.Value;
            List<string> detalhes = erro.Descendants()
                .Where(e => e.Name.LocalName is "errordetail" or "errordetails")
                .Select(e => e.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return new SapErroDetalhado
            {
                Codigo = Limpar(codigo),
                Mensagem = Limpar(mensagem),
                Detalhes = detalhes.Count == 0 ? null : string.Join(" | ", detalhes),
                RetornoBruto = responseBody
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? LerString(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? Limpar(valor.GetString())
            : null;

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
