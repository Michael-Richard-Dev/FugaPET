using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Diagnostico;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Servicos.Diagnostico;

public sealed class DiagnosticoConsumoSap261Servico
{
    private static readonly string[] TabelasObrigatorias =
    [
        "consumo_material_lancamento",
        "consumo_material_item",
        "consumo_material_pesagem"
    ];

    private static readonly Dictionary<string, string[]> ColunasObrigatorias = new(StringComparer.Ordinal)
    {
        ["consumo_material_lancamento"] =
        [
            "codigo_consumo_material_lancamento", "numero_ordem", "status_lancamento",
            "documento_material_sap", "exercicio_documento_material_sap", "enviado_sap_em",
            "consumo_material_lancamento_criado_em", "consumo_material_lancamento_atualizado_em"
        ],
        ["consumo_material_item"] =
        [
            "codigo_consumo_material_item", "codigo_consumo_material_lancamento", "codigo_material",
            "numero_reserva", "item_reserva", "quantidade_consumida_local", "tipo_movimento_sap", "status_item"
        ],
        ["consumo_material_pesagem"] =
        [
            "codigo_consumo_material_pesagem", "codigo_consumo_material_item", "peso_bruto_kg",
            "peso_tara_kg", "peso_liquido_kg", "origem", "pesado_em", "status_pesagem"
        ]
    };

    private static readonly string[] StatusLancamentoObrigatorios =
    [
        "PENDENTE_SAP", "ENVIANDO_SAP", "CONFIRMADO_SAP", "FALHA_SAP", "CANCELADO_LOCAL"
    ];

    private readonly IDiagnosticoConsumoSap261BancoProbe _bancoProbe;
    private readonly Func<ConfiguracaoSap> _carregarConfiguracaoSap;

    public DiagnosticoConsumoSap261Servico()
        : this(
            new DiagnosticoConsumoSap261BancoProbe(
                new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())),
            LeitorConfiguracaoSap.Carregar)
    {
    }

    internal DiagnosticoConsumoSap261Servico(
        IDiagnosticoConsumoSap261BancoProbe bancoProbe,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        _bancoProbe = bancoProbe;
        _carregarConfiguracaoSap = carregarConfiguracaoSap;
    }

    public async Task<DiagnosticoConsumoSap261Resultado> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        List<DiagnosticoConsumoSap261Item> itens = [];
        bool bancoEssencialOk = await VerificarBancoAsync(itens, cancellationToken);
        bool sapEnvioOk = VerificarSap(itens);

        bool prontoLocal = bancoEssencialOk;
        bool prontoPreview = prontoLocal;
        bool prontoEnvioSap = prontoPreview && sapEnvioOk;

        itens.Add(Item(
            "RESUMO",
            "Resultado agregado",
            prontoEnvioSap ? "ALERTA" : prontoPreview ? "OK" : "BLOQUEIO",
            prontoEnvioSap
                ? "Envio SAP real depende de massa autorizada em HML e confirmação manual do usuário."
                : prontoPreview
                    ? "Ambiente pronto para teste local e preview 261."
                    : "Há bloqueios antes do teste local/preview."));

        return new DiagnosticoConsumoSap261Resultado
        {
            ProntoParaTesteLocal = prontoLocal,
            ProntoParaPreview = prontoPreview,
            ProntoParaEnvioSap = prontoEnvioSap,
            Itens = itens
        };
    }

    private async Task<bool> VerificarBancoAsync(List<DiagnosticoConsumoSap261Item> itens, CancellationToken cancellationToken)
    {
        DiagnosticoConsumoSap261BancoSnapshot banco;
        try
        {
            banco = await _bancoProbe.ObterSnapshotAsync(cancellationToken);
            itens.Add(Item("BANCO_CONEXAO", "Conexão com banco", "OK", "Conexão aberta para diagnóstico read-only."));
        }
        catch (Exception ex)
        {
            itens.Add(Item("BANCO_CONEXAO", "Conexão com banco", "BLOQUEIO", $"Banco indisponível para diagnóstico ({ex.GetType().Name})."));
            return false;
        }

        bool essencialOk = true;
        foreach (string tabela in TabelasObrigatorias)
        {
            if (!banco.Tabelas.Contains(tabela))
            {
                itens.Add(Item($"TABELA_{tabela}", $"Tabela {tabela}", "BLOQUEIO", "Tabela obrigatória do SQL 030 não encontrada."));
                essencialOk = false;
                continue;
            }

            itens.Add(Item($"TABELA_{tabela}", $"Tabela {tabela}", "OK", "Tabela encontrada."));
            HashSet<string> colunas = banco.ColunasPorTabela.TryGetValue(tabela, out HashSet<string>? existentes) ? existentes : [];
            foreach (string coluna in ColunasObrigatorias[tabela])
            {
                if (!colunas.Contains(coluna))
                {
                    itens.Add(Item($"COLUNA_{tabela}_{coluna}", $"Coluna {tabela}.{coluna}", "BLOQUEIO", "Coluna obrigatória não encontrada."));
                    essencialOk = false;
                }
            }
        }

        foreach (string status in StatusLancamentoObrigatorios)
        {
            if (!banco.DefinicoesConstraints.Contains(status, StringComparison.Ordinal))
            {
                itens.Add(Item($"STATUS_{status}", $"Status {status}", "BLOQUEIO", "Status não encontrado nas constraints do consumo."));
                essencialOk = false;
            }
        }

        if (!banco.DefinicoesConstraints.Contains("REGISTRADA_LOCALMENTE", StringComparison.Ordinal))
        {
            itens.Add(Item("STATUS_PESAGEM_REGISTRADA", "Status de pesagem REGISTRADA_LOCALMENTE", "BLOQUEIO", "Status de pesagem não encontrado nas constraints."));
            essencialOk = false;
        }

        VerificarTrigger(itens, banco.Triggers, "TRIGGER_LANCAMENTO_ATUALIZADO", "Trigger atualizado_em lançamento", "consumo_material_lancamento");
        VerificarTrigger(itens, banco.Triggers, "TRIGGER_ITEM_ATUALIZADO", "Trigger atualizado_em item", "consumo_material_item");

        foreach (string indice in new[] { "numero_ordem", "status", "lancamento", "item", "reserva", "documento" })
        {
            VerificarIndice(itens, banco.Indices, indice);
        }

        return essencialOk;
    }

    private bool VerificarSap(List<DiagnosticoConsumoSap261Item> itens)
    {
        ConfiguracaoSap sap = _carregarConfiguracaoSap();
        bool urlOk = !string.IsNullOrWhiteSpace(sap.MaterialDocumentBaseUrl);
        bool uriOk = Uri.TryCreate(sap.MaterialDocumentBaseUrl, UriKind.Absolute, out Uri? uri);
        bool httpsOk = uriOk && string.Equals(uri!.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        bool allowlistOk = uri is not null &&
                           sap.HostsPermitidos.Contains(uri.Host.TrimEnd('.').ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
        bool credenciaisOk = !string.IsNullOrWhiteSpace(sap.Usuario) && !string.IsNullOrWhiteSpace(sap.Senha);
        bool sapClientOk = !string.IsNullOrWhiteSpace(sap.SapClient);

        itens.Add(Item("SAP_URL_MDOC", "URL Material Document", urlOk ? "OK" : "BLOQUEIO",
            urlOk ? "URL Material Document configurada." : "URL Material Document ausente."));
        itens.Add(Item("SAP_HTTPS", "HTTPS SAP", httpsOk ? "OK" : "BLOQUEIO",
            httpsOk ? "URL SAP usa HTTPS." : "URL SAP deve usar HTTPS para envio."));
        itens.Add(Item("SAP_ALLOWLIST", "Host SAP na allowlist", allowlistOk ? "OK" : "BLOQUEIO",
            allowlistOk ? "Host SAP está na allowlist." : "Host SAP ausente ou fora da allowlist."));
        itens.Add(Item("SAP_CLIENT", "SAP client", sapClientOk ? "OK" : "ALERTA",
            sapClientOk ? "sap-client configurado." : "sap-client não configurado."));
        itens.Add(Item("SAP_BASIC_AUTH", "Credenciais SAP", credenciaisOk ? "OK" : "BLOQUEIO",
            credenciaisOk ? "Credenciais configuráveis por ambiente (não exibidas)." : "Usuário/senha SAP ausentes."));
        itens.Add(Item("SAP_WRITE_ENABLED", "FUGAPET_SAP_WRITE_ENABLED", "ALERTA",
            sap.EscritaHabilitada
                ? "Escrita SAP habilitada. Usar apenas com massa autorizada."
                : "Escrita SAP desabilitada: ambiente seguro, envio real bloqueado."));

        return urlOk && httpsOk && allowlistOk && credenciaisOk && sapClientOk && sap.EscritaHabilitada;
    }

    private static void VerificarTrigger(
        List<DiagnosticoConsumoSap261Item> itens,
        HashSet<string> valores,
        string codigo,
        string descricao,
        string trechoEsperado)
    {
        bool existe = valores.Any(valor => valor.Contains(trechoEsperado, StringComparison.OrdinalIgnoreCase));
        itens.Add(Item(codigo, descricao, existe ? "OK" : "ALERTA",
            existe ? "Encontrado." : "Não encontrado; verificar SQL 030/Gaia antes de carga real."));
    }

    private static void VerificarIndice(List<DiagnosticoConsumoSap261Item> itens, HashSet<string> indices, string trecho)
    {
        bool existe = indices.Any(indice => indice.Contains(trecho, StringComparison.OrdinalIgnoreCase));
        itens.Add(Item($"INDICE_{trecho.ToUpperInvariant()}", $"Índice por {trecho}", existe ? "OK" : "ALERTA",
            existe ? "Índice encontrado." : "Índice mínimo não localizado; alerta de performance."));
    }

    private static DiagnosticoConsumoSap261Item Item(string codigo, string descricao, string status, string mensagem)
        => new() { Codigo = codigo, Descricao = descricao, Status = status, Mensagem = mensagem };
}

internal interface IDiagnosticoConsumoSap261BancoProbe
{
    Task<DiagnosticoConsumoSap261BancoSnapshot> ObterSnapshotAsync(CancellationToken cancellationToken = default);
}

internal sealed class DiagnosticoConsumoSap261BancoSnapshot
{
    public HashSet<string> Tabelas { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, HashSet<string>> ColunasPorTabela { get; init; } = new(StringComparer.Ordinal);
    public string DefinicoesConstraints { get; init; } = string.Empty;
    public HashSet<string> Triggers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Indices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DiagnosticoConsumoSap261BancoProbe : IDiagnosticoConsumoSap261BancoProbe
{
    private const string Schema = "homologacao";
    private readonly IFabricaConexaoBanco _fabricaConexaoBanco;

    public DiagnosticoConsumoSap261BancoProbe(IFabricaConexaoBanco fabricaConexaoBanco)
    {
        _fabricaConexaoBanco = fabricaConexaoBanco;
    }

    public async Task<DiagnosticoConsumoSap261BancoSnapshot> ObterSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection conexao = await _fabricaConexaoBanco.CriarConexaoAbertaAsync(cancellationToken);
        return new DiagnosticoConsumoSap261BancoSnapshot
        {
            Tabelas = await LerTabelasAsync(conexao, cancellationToken),
            ColunasPorTabela = await LerColunasAsync(conexao, cancellationToken),
            DefinicoesConstraints = await LerConstraintsAsync(conexao, cancellationToken),
            Triggers = await LerTriggersAsync(conexao, cancellationToken),
            Indices = await LerIndicesAsync(conexao, cancellationToken)
        };
    }

    private static async Task<HashSet<string>> LerTabelasAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name
              FROM information_schema.tables
             WHERE table_schema = @schema
               AND table_name IN ('consumo_material_lancamento', 'consumo_material_item', 'consumo_material_pesagem');
            """;
        HashSet<string> tabelas = new(StringComparer.Ordinal);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            tabelas.Add(leitor.GetString(0));
        }

        return tabelas;
    }

    private static async Task<Dictionary<string, HashSet<string>>> LerColunasAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name, column_name
              FROM information_schema.columns
             WHERE table_schema = @schema
               AND table_name IN ('consumo_material_lancamento', 'consumo_material_item', 'consumo_material_pesagem');
            """;
        Dictionary<string, HashSet<string>> colunas = new(StringComparer.Ordinal);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            string tabela = leitor.GetString(0);
            if (!colunas.TryGetValue(tabela, out HashSet<string>? lista))
            {
                lista = new HashSet<string>(StringComparer.Ordinal);
                colunas[tabela] = lista;
            }

            lista.Add(leitor.GetString(1));
        }

        return colunas;
    }

    private static async Task<string> LerConstraintsAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(string_agg(pg_get_constraintdef(c.oid), ' '), '')
              FROM pg_constraint c
              JOIN pg_class t ON t.oid = c.conrelid
              JOIN pg_namespace n ON n.oid = t.relnamespace
             WHERE n.nspname = @schema
               AND t.relname IN ('consumo_material_lancamento', 'consumo_material_item', 'consumo_material_pesagem');
            """;
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        return Convert.ToString(await comando.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
    }

    private static async Task<HashSet<string>> LerTriggersAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
        => await LerStringSetAsync(conexao, """
            SELECT event_object_table || ':' || trigger_name
              FROM information_schema.triggers
             WHERE trigger_schema = @schema
               AND event_object_table IN ('consumo_material_lancamento', 'consumo_material_item');
            """, cancellationToken);

    private static async Task<HashSet<string>> LerIndicesAsync(NpgsqlConnection conexao, CancellationToken cancellationToken)
        => await LerStringSetAsync(conexao, """
            SELECT indexname || ':' || indexdef
              FROM pg_indexes
             WHERE schemaname = @schema
               AND tablename IN ('consumo_material_lancamento', 'consumo_material_item', 'consumo_material_pesagem');
            """, cancellationToken);

    private static async Task<HashSet<string>> LerStringSetAsync(NpgsqlConnection conexao, string sql, CancellationToken cancellationToken)
    {
        HashSet<string> valores = new(StringComparer.OrdinalIgnoreCase);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            valores.Add(leitor.GetString(0));
        }

        return valores;
    }
}
