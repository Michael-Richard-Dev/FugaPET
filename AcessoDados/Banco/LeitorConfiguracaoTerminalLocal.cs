using System.Text.Json;

namespace FugaPET_Dev.AcessoDados.Banco;

public static class LeitorConfiguracaoTerminalLocal
{
    private const string NomeArquivoConfiguracao = "configuracao.terminal.json";

    public static ConfiguracaoTerminalLocal Carregar()
    {
        string caminhoArquivo = Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao);
        if (!File.Exists(caminhoArquivo))
        {
            return new ConfiguracaoTerminalLocal();
        }

        string json = File.ReadAllText(caminhoArquivo);
        using JsonDocument documento = JsonDocument.Parse(json);

        if (!documento.RootElement.TryGetProperty("terminal_local", out JsonElement raizTerminal))
        {
            return new ConfiguracaoTerminalLocal();
        }

        TerminalLocalItem padrao = raizTerminal.TryGetProperty("padrao", out JsonElement padraoElemento)
            ? ParseTerminal(padraoElemento)
            : new TerminalLocalItem();

        List<TerminalLocalItem> terminais = [];
        if (raizTerminal.TryGetProperty("terminais", out JsonElement terminaisElemento) &&
            terminaisElemento.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement terminal in terminaisElemento.EnumerateArray())
            {
                terminais.Add(ParseTerminal(terminal));
            }
        }

        return new ConfiguracaoTerminalLocal
        {
            Padrao = padrao,
            Terminais = terminais
        };
    }

    private static TerminalLocalItem ParseTerminal(JsonElement elemento)
    {
        return new TerminalLocalItem
        {
            Maquina = LerTexto(elemento, "maquina", string.Empty),
            NomeTerminal = LerTexto(elemento, "nome_terminal", string.Empty),
            BancoLocalNome = LerTexto(elemento, "banco_local_nome", "LOCAL"),
            IdSetorPadrao = LerLongoNulo(elemento, "codigo_setor_padrao"),
            IdBalancaPadrao = LerLongoNulo(elemento, "codigo_balanca_padrao"),
            ImpressoraPadrao = LerTexto(elemento, "impressora_padrao", string.Empty),
            IdEtiquetaPadrao = LerLongoNulo(elemento, "codigo_etiqueta_padrao"),
            IdTaraPadrao = LerLongoNulo(elemento, "codigo_tara_padrao")
        };
    }

    private static string LerTexto(JsonElement elemento, string propriedade, string valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? valorPadrao
            : valorPadrao;

    private static long? LerLongoNulo(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        if (valor.ValueKind == JsonValueKind.Null || valor.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetInt64(out long numero))
        {
            return numero;
        }

        if (valor.ValueKind == JsonValueKind.String)
        {
            string? texto = valor.GetString();
            if (long.TryParse(texto, out long numeroTexto))
            {
                return numeroTexto;
            }
        }

        return null;
    }
}

