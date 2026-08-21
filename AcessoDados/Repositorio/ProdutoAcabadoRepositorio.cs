using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistência PRODUTIVA da caixa individual de Produto Acabado (Handling Unit) sobre o incremental 044
/// (REV11). Consome EXCLUSIVAMENTE a superfície autorizada ao app: INSERT por colunas concedidas +
/// funções fn_hu_caixa_* (nunca UPDATE direto; nunca DML no log de auditoria). Npgsql assíncrono,
/// parametrizado, JSONB/uuid/timestamptz tipados. A identidade (numero_caixa/codigo_caixa_local/hu_caixa)
/// é gerada pelo banco. Nenhum segredo é lido/gravado/logado.
/// </summary>
public sealed class ProdutoAcabadoRepositorio : IProdutoAcabadoRepositorio
{
    private readonly IFabricaConexaoBanco _fabricaConexao;

    // Colunas hidratadas em ProdutoAcabadoCaixa (SELECT explícito — nunca SELECT *).
    private const string ColunasCaixa = """
        codigo_hu_caixa, numero_caixa, codigo_caixa_local, hu_caixa,
        numero_ordem_producao, item_ordem_producao, material, lote, centro, deposito,
        material_embalagem, origem_material_embalagem,
        peso_bruto, peso_liquido, peso_tara, unidade_peso, quantidade, unidade_quantidade,
        origem_pesagem, codigo_balanca, codigo_usuario, terminal,
        correlation_id, status_hu_caixa, handling_unit_external_id,
        request_json_sanitizado::text AS request_json_sanitizado,
        response_json_sanitizado::text AS response_json_sanitizado,
        erro_sanitizado, http_status, tentativas, claim_token,
        criado_em, atualizado_em, enviado_sap_em, confirmado_sap_em
        """;

    // GATE 047-V: projeção ESPECÍFICA das capabilities 047. O RETURNS TABLE físico de
    // fn_pa_047_caixa_buscar_hu_exata e fn_pa_047_caixa_listar_intervalo_hu expõe SOMENTE estas 15 colunas.
    // Selecionar ColunasCaixa (tabela hu_caixa completa) contra a function causa 42703 (coluna inexistente).
    // Estas 15 cobrem o contrato da Paletização; os demais campos ficam no default do modelo (não são auditoria real).
    private const string ColunasCaixa047 = """
        codigo_hu_caixa, handling_unit_external_id, numero_ordem_producao, material, lote,
        numero_caixa, hu_caixa, codigo_caixa_local, peso_bruto, peso_liquido, peso_tara,
        unidade_peso, centro, deposito, status_hu_caixa
        """;

    public ProdutoAcabadoRepositorio(IFabricaConexaoBanco fabricaConexao)
        => _fabricaConexao = fabricaConexao ?? throw new ArgumentNullException(nameof(fabricaConexao));

    public async Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caixa);
        if (caixa.CodigoUsuario is not long usuario)
        {
            throw new InvalidOperationException("Caixa sem codigo_usuario: obrigatório para o INSERT autorizado.");
        }

        const string sql = $"""
            INSERT INTO hu_caixa (
                numero_ordem_producao, item_ordem_producao, correlation_id,
                material, lote, centro, deposito, material_embalagem, origem_material_embalagem,
                peso_bruto, peso_liquido, peso_tara, unidade_peso, quantidade, unidade_quantidade,
                origem_pesagem, codigo_balanca, codigo_usuario, terminal
            ) VALUES (
                @numero_op, @item_op, @correlation,
                @material, @lote, @centro, @deposito, @material_emb, @origem_emb,
                @peso_bruto, @peso_liquido, @peso_tara, @unidade_peso, @quantidade, @unidade_qtd,
                @origem_pesagem, @codigo_balanca, @codigo_usuario, @terminal
            )
            RETURNING {ColunasCaixa};
            """;

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("numero_op", NpgsqlDbType.Varchar, caixa.NumeroOrdemProducao.Trim());
        cmd.Parameters.AddWithValue("item_op", NpgsqlDbType.Varchar, caixa.ItemOrdemProducao.Trim());
        AdicionarUuidOuNulo(cmd, "correlation", caixa.CorrelationId == Guid.Empty ? null : caixa.CorrelationId);
        cmd.Parameters.AddWithValue("material", NpgsqlDbType.Varchar, caixa.Material.Trim());
        cmd.Parameters.AddWithValue("lote", NpgsqlDbType.Varchar, caixa.Lote.Trim());
        cmd.Parameters.AddWithValue("centro", NpgsqlDbType.Varchar, caixa.Centro.Trim());
        cmd.Parameters.AddWithValue("deposito", NpgsqlDbType.Varchar, caixa.Deposito.Trim());
        cmd.Parameters.AddWithValue("material_emb", NpgsqlDbType.Varchar, caixa.MaterialEmbalagem.Trim());
        cmd.Parameters.AddWithValue("origem_emb", NpgsqlDbType.Varchar, OrigemEmbalagemTexto(caixa.OrigemMaterialEmbalagem));
        cmd.Parameters.AddWithValue("peso_bruto", NpgsqlDbType.Numeric, caixa.PesoBrutoKg);
        cmd.Parameters.AddWithValue("peso_liquido", NpgsqlDbType.Numeric, caixa.PesoLiquidoKg);
        cmd.Parameters.AddWithValue("peso_tara", NpgsqlDbType.Numeric, caixa.TaraKg);
        cmd.Parameters.AddWithValue("unidade_peso", NpgsqlDbType.Varchar, UnidadeOuPadrao(caixa.UnidadePeso, "KG"));
        cmd.Parameters.AddWithValue("quantidade", NpgsqlDbType.Numeric, (decimal)caixa.QuantidadeProdutos);
        cmd.Parameters.AddWithValue("unidade_qtd", NpgsqlDbType.Varchar, UnidadeOuPadrao(caixa.UnidadeQuantidade, "UN"));
        cmd.Parameters.AddWithValue("origem_pesagem", NpgsqlDbType.Varchar, caixa.OrigemPesagem.Trim().ToUpperInvariant());
        AdicionarBigintOuNulo(cmd, "codigo_balanca", caixa.CodigoBalanca);
        cmd.Parameters.AddWithValue("codigo_usuario", NpgsqlDbType.Bigint, usuario);
        cmd.Parameters.AddWithValue("terminal", NpgsqlDbType.Varchar, caixa.Terminal.Trim());

        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("INSERT de hu_caixa não retornou o snapshot esperado.");
        }

        return Hidratar(leitor);
    }

    public async Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa pesagem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pesagem);
        string origem = pesagem.OrigemPesagem.Trim().ToUpperInvariant();
        if (origem == "BALANCA" && pesagem.CodigoBalanca is null)
        {
            throw new InvalidOperationException("Pesagem BALANCA exige codigo_balanca.");
        }
        if (origem == "MANUAL" && pesagem.CodigoBalanca is not null)
        {
            throw new InvalidOperationException("Pesagem MANUAL não deve ter codigo_balanca.");
        }

        const string sql = """
            INSERT INTO hu_caixa_pesagem (
                codigo_hu_caixa, codigo_balanca, origem_pesagem, peso_lido,
                peso_bruto, peso_liquido, peso_tara, unidade_peso, payload_balanca, codigo_usuario
            ) VALUES (
                @codigo_hu, @codigo_balanca, @origem, @peso_lido,
                @peso_bruto, @peso_liquido, @peso_tara, @unidade, @payload, @codigo_usuario
            )
            RETURNING codigo_hu_caixa_pesagem;
            """;

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("codigo_hu", NpgsqlDbType.Bigint, pesagem.CodigoHuCaixa);
        AdicionarBigintOuNulo(cmd, "codigo_balanca", pesagem.CodigoBalanca);
        cmd.Parameters.AddWithValue("origem", NpgsqlDbType.Varchar, origem);
        cmd.Parameters.AddWithValue("peso_lido", NpgsqlDbType.Numeric, pesagem.PesoLido);
        cmd.Parameters.AddWithValue("peso_bruto", NpgsqlDbType.Numeric, pesagem.PesoBruto);
        cmd.Parameters.AddWithValue("peso_liquido", NpgsqlDbType.Numeric, pesagem.PesoLiquido);
        cmd.Parameters.AddWithValue("peso_tara", NpgsqlDbType.Numeric, pesagem.PesoTara);
        cmd.Parameters.AddWithValue("unidade", NpgsqlDbType.Varchar, UnidadeOuPadrao(pesagem.UnidadePeso, "KG"));
        AdicionarJsonbOuNulo(cmd, "payload", pesagem.PayloadBalancaJson);
        cmd.Parameters.AddWithValue("codigo_usuario", NpgsqlDbType.Bigint, pesagem.CodigoUsuario);

        object? resultado = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(resultado);
    }

    public Task<bool> FinalizarLocalAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_finalizar_local(@c,@u,@t)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
                cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
            },
            cancellationToken);

    public Task<bool> SalvarPreviewAsync(long codigo, string requestJsonSanitizado, string endpointSanitizado, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_salvar_preview(@c,@req,@end)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("req", NpgsqlDbType.Jsonb, (object?)requestJsonSanitizado ?? DBNull.Value);
                cmd.Parameters.AddWithValue("end", NpgsqlDbType.Text, endpointSanitizado ?? string.Empty);
            },
            cancellationToken);

    public Task<bool> AguardarAutorizacaoAsync(long codigo, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_aguardar_autorizacao(@c)",
            cmd => cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo),
            cancellationToken);

    public Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_autorizar_envio(@c,@u,@t)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
                cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
            },
            cancellationToken);

    public async Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        string sql = $"SELECT {ColunasCaixa} FROM fn_hu_caixa_claim_envio(@c,@u,@t)";
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
        cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
        cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);

        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? Hidratar(leitor) : null;
    }

    public Task<bool> RegistrarSucessoAsync(
        long codigo, int numeroTentativa, Guid claimToken, string handlingUnitExternalId, string? warehouse,
        int httpStatus, string? responseJsonSanitizado, string? sapMessagesJson, string? etag,
        string? createdByUserSap, DateTimeOffset? creationDatetimeSap, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_registrar_sucesso(@c,@tent,@claim,@hu,@wh,@http,@resp,@sap,@etag,@by,@creation)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("tent", NpgsqlDbType.Integer, numeroTentativa);
                cmd.Parameters.AddWithValue("claim", NpgsqlDbType.Uuid, claimToken);
                cmd.Parameters.AddWithValue("hu", NpgsqlDbType.Varchar, handlingUnitExternalId ?? string.Empty);
                cmd.Parameters.AddWithValue("wh", NpgsqlDbType.Varchar, (object?)warehouse ?? string.Empty);
                cmd.Parameters.AddWithValue("http", NpgsqlDbType.Integer, httpStatus);
                AdicionarJsonbOuNulo(cmd, "resp", responseJsonSanitizado);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
                cmd.Parameters.AddWithValue("etag", NpgsqlDbType.Text, (object?)etag ?? DBNull.Value);
                cmd.Parameters.AddWithValue("by", NpgsqlDbType.Varchar, (object?)createdByUserSap ?? DBNull.Value);
                cmd.Parameters.AddWithValue("creation", NpgsqlDbType.TimestampTz, (object?)creationDatetimeSap ?? DBNull.Value);
            },
            cancellationToken);

    public Task<bool> RegistrarErroAsync(
        long codigo, int numeroTentativa, Guid claimToken, int? httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, ResultadoErroHu resultado, bool podeReprocessar,
        CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_registrar_erro(@c,@tent,@claim,@http,@resp,@sap,@erro,@res,@repro)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("tent", NpgsqlDbType.Integer, numeroTentativa);
                cmd.Parameters.AddWithValue("claim", NpgsqlDbType.Uuid, claimToken);
                cmd.Parameters.AddWithValue("http", NpgsqlDbType.Integer, (object?)httpStatus ?? DBNull.Value);
                AdicionarJsonbOuNulo(cmd, "resp", responseJsonSanitizado);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
                cmd.Parameters.AddWithValue("erro", NpgsqlDbType.Text, erroSanitizado ?? string.Empty);
                cmd.Parameters.AddWithValue("res", NpgsqlDbType.Varchar, ResultadoErroHuTexto.ParaBanco(resultado));
                cmd.Parameters.AddWithValue("repro", NpgsqlDbType.Boolean, podeReprocessar);
            },
            cancellationToken);

    public Task<bool> RegistrarTimeoutAsync(
        long codigo, int numeroTentativa, Guid claimToken, string? sapMessagesJson, string erroSanitizado,
        CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_registrar_timeout(@c,@tent,@claim,@sap,@erro)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("tent", NpgsqlDbType.Integer, numeroTentativa);
                cmd.Parameters.AddWithValue("claim", NpgsqlDbType.Uuid, claimToken);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
                cmd.Parameters.AddWithValue("erro", NpgsqlDbType.Text, erroSanitizado ?? string.Empty);
            },
            cancellationToken);

    public Task<bool> ConfirmarReconciliacaoAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string? etag, string? createdByUserSap, DateTimeOffset? creationDatetimeSap,
        bool comparacaoAprovada, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_confirmar_reconciliacao(@c,@hu,@wh,@http,@resp,@sap,@etag,@by,@creation,@ok)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("hu", NpgsqlDbType.Varchar, handlingUnitExternalId ?? string.Empty);
                cmd.Parameters.AddWithValue("wh", NpgsqlDbType.Varchar, (object?)warehouse ?? string.Empty);
                cmd.Parameters.AddWithValue("http", NpgsqlDbType.Integer, httpStatus);
                AdicionarJsonbOuNulo(cmd, "resp", responseJsonSanitizado);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
                cmd.Parameters.AddWithValue("etag", NpgsqlDbType.Text, (object?)etag ?? DBNull.Value);
                cmd.Parameters.AddWithValue("by", NpgsqlDbType.Varchar, (object?)createdByUserSap ?? DBNull.Value);
                cmd.Parameters.AddWithValue("creation", NpgsqlDbType.TimestampTz, (object?)creationDatetimeSap ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ok", NpgsqlDbType.Boolean, comparacaoAprovada);
            },
            cancellationToken);

    public Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_registrar_reconciliacao_nao_encontrada(@c,@hu,@wh,@http,@resp,@sap,@erro)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("hu", NpgsqlDbType.Varchar, handlingUnitExternalId ?? string.Empty);
                cmd.Parameters.AddWithValue("wh", NpgsqlDbType.Varchar, (object?)warehouse ?? string.Empty);
                cmd.Parameters.AddWithValue("http", NpgsqlDbType.Integer, httpStatus);
                AdicionarJsonbOuNulo(cmd, "resp", responseJsonSanitizado);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
                cmd.Parameters.AddWithValue("erro", NpgsqlDbType.Text, erroSanitizado ?? string.Empty);
            },
            cancellationToken);

    public Task<bool> BloquearConfiguracaoAsync(
        long codigo, long usuario, string terminal, string erroSanitizado, string? sapMessagesJson,
        CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_bloquear_configuracao(@c,@u,@t,@erro,@sap)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
                cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
                cmd.Parameters.AddWithValue("erro", NpgsqlDbType.Text, erroSanitizado ?? string.Empty);
                AdicionarJsonbOuNulo(cmd, "sap", sapMessagesJson);
            },
            cancellationToken);

    public Task<bool> CancelarAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_cancelar(@c,@u,@t,@m)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
                cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
                cmd.Parameters.AddWithValue("m", NpgsqlDbType.Text, motivo ?? string.Empty);
            },
            cancellationToken);

    public Task<bool> LiberarReprocessamentoAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => ExecutarFuncaoBoolAsync(
            "SELECT fn_hu_caixa_liberar_reprocessamento(@c,@u,@t,@m)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
                cmd.Parameters.AddWithValue("u", NpgsqlDbType.Bigint, usuario);
                cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
                cmd.Parameters.AddWithValue("m", NpgsqlDbType.Text, motivo ?? string.Empty);
            },
            cancellationToken);

    public async Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
    {
        string sql = $"SELECT {ColunasCaixa} FROM hu_caixa WHERE codigo_hu_caixa=@c";
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, codigo);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? Hidratar(leitor) : null;
    }

    public async Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(
        string numeroOrdemProducao, string terminal, CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {ColunasCaixa}
              FROM hu_caixa
             WHERE numero_ordem_producao=@op
               AND upper(btrim(terminal))=upper(btrim(@t))
             ORDER BY numero_caixa
            """;
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("op", NpgsqlDbType.Varchar, (numeroOrdemProducao ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        List<ProdutoAcabadoCaixa> caixas = [];
        while (await leitor.ReadAsync(cancellationToken))
        {
            caixas.Add(Hidratar(leitor));
        }

        return caixas;
    }

    // REV4-§12 / REV5-§3: recuperação por CONTEXTO INEQUÍVOCO (OP + item + material + lote + terminal),
    // FAIL-CLOSED: todos os campos são igualdade OBRIGATÓRIA (nenhum filtro opcional que amplie o contexto).
    // Se qualquer campo obrigatório vier vazio, NÃO amplia para OP+terminal — retorna lista vazia.
    public async Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(
        string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal,
        CancellationToken cancellationToken = default)
    {
        string op = (numeroOrdemProducao ?? string.Empty).Trim();
        string item = (itemOrdemProducao ?? string.Empty).Trim();
        string mat = (material ?? string.Empty).Trim();
        string lt = (lote ?? string.Empty).Trim();
        string term = (terminal ?? string.Empty).Trim();
        if (op.Length == 0 || item.Length == 0 || mat.Length == 0 || lt.Length == 0 || term.Length == 0)
        {
            // Contexto incompleto ⇒ não consulta (fail-closed). Diagnóstico sanitizado (sem valores sensíveis).
            System.Diagnostics.Trace.TraceWarning(
                "[ProdutoAcabado] Recuperação por contexto bloqueada: contexto incompleto (OP/item/material/lote/terminal obrigatórios).");
            return [];
        }

        string sql = $"""
            SELECT {ColunasCaixa}
              FROM hu_caixa
             WHERE numero_ordem_producao=@op
               AND upper(btrim(terminal))=upper(btrim(@t))
               AND btrim(item_ordem_producao)=btrim(@item)
               AND btrim(material)=btrim(@material)
               AND btrim(lote)=btrim(@lote)
             ORDER BY numero_caixa
            """;
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("op", NpgsqlDbType.Varchar, op);
        cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, term);
        cmd.Parameters.AddWithValue("item", NpgsqlDbType.Text, item);
        cmd.Parameters.AddWithValue("material", NpgsqlDbType.Text, mat);
        cmd.Parameters.AddWithValue("lote", NpgsqlDbType.Text, lt);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        List<ProdutoAcabadoCaixa> caixas = [];
        while (await leitor.ReadAsync(cancellationToken))
        {
            caixas.Add(Hidratar(leitor));
        }

        return caixas;
    }

    // INC-047: leitura por HU externo (seleção manual). Usa capability Gaia 047-B; sem POST/SAP.
    public async Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(
        IReadOnlyList<string> husExternais, CancellationToken cancellationToken = default)
    {
        string[] hus = (husExternais ?? [])
            .Select(h => (h ?? string.Empty).Trim())
            .Where(h => h.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (hus.Length == 0)
        {
            return [];
        }

        const string sql = $"""
            SELECT {ColunasCaixa047}
              FROM fn_pa_047_caixa_buscar_hu_exata(@hu)
            """;
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        List<ProdutoAcabadoCaixa> caixas = [];
        foreach (string hu in hus)
        {
            await using NpgsqlCommand cmd = new(sql, con);
            cmd.Parameters.AddWithValue("hu", NpgsqlDbType.Text, hu);
            await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await leitor.ReadAsync(cancellationToken))
            {
                caixas.Add(Hidratar047(leitor));
            }
        }

        return caixas;
    }

    // INC-047: leitura por INTERVALO de HU externo (sequência). Usa capability Gaia 047-B; sem POST/SAP.
    public async Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(
        string huInicial, string huFinal, string? material, CancellationToken cancellationToken = default)
    {
        string ini = (huInicial ?? string.Empty).Trim();
        string fim = (huFinal ?? string.Empty).Trim();
        string mat = (material ?? string.Empty).Trim();
        if (ini.Length == 0 || fim.Length == 0 || mat.Length == 0)
        {
            return [];
        }

        const string sql = $"""
            SELECT {ColunasCaixa047}
              FROM fn_pa_047_caixa_listar_intervalo_hu(p_material => @mat, p_hu_inicial => @ini, p_hu_final => @fim)
            """;
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("ini", NpgsqlDbType.Text, ini);
        cmd.Parameters.AddWithValue("fim", NpgsqlDbType.Text, fim);
        cmd.Parameters.AddWithValue("mat", NpgsqlDbType.Text, mat);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        List<ProdutoAcabadoCaixa> caixas = [];
        while (await leitor.ReadAsync(cancellationToken))
        {
            caixas.Add(Hidratar047(leitor));
        }

        return caixas;
    }

    public async Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {ColunasCaixa}
              FROM hu_caixa
             WHERE upper(btrim(terminal))=upper(btrim(@t))
               AND status_hu_caixa NOT IN ('CONFIRMADA_SAP','CANCELADA')
             ORDER BY codigo_hu_caixa DESC
             LIMIT 1
            """;
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.AddWithValue("t", NpgsqlDbType.Text, terminal ?? string.Empty);
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? Hidratar(leitor) : null;
    }

    // ---------- infra ----------

    private async Task<bool> ExecutarFuncaoBoolAsync(
        string sql, Action<NpgsqlCommand> configurar, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        configurar(cmd);
        object? resultado = await cmd.ExecuteScalarAsync(cancellationToken);
        return resultado is bool b && b;
    }

    private static void AdicionarUuidOuNulo(NpgsqlCommand cmd, string nome, Guid? valor)
        => cmd.Parameters.AddWithValue(nome, NpgsqlDbType.Uuid, (object?)valor ?? DBNull.Value);

    private static void AdicionarBigintOuNulo(NpgsqlCommand cmd, string nome, long? valor)
        => cmd.Parameters.AddWithValue(nome, NpgsqlDbType.Bigint, (object?)valor ?? DBNull.Value);

    private static void AdicionarJsonbOuNulo(NpgsqlCommand cmd, string nome, string? json)
        => cmd.Parameters.AddWithValue(nome, NpgsqlDbType.Jsonb, string.IsNullOrWhiteSpace(json) ? DBNull.Value : json);

    private static string UnidadeOuPadrao(string? valor, string padrao)
        => string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim().ToUpperInvariant();

    private static string OrigemEmbalagemTexto(OrigemMaterialEmbalagemCaixa origem)
        => origem switch
        {
            OrigemMaterialEmbalagemCaixa.Sap => "SAP",
            OrigemMaterialEmbalagemCaixa.Operador => "OPERADOR",
            OrigemMaterialEmbalagemCaixa.Configuracao => "CONFIGURACAO",
            OrigemMaterialEmbalagemCaixa.FallbackControlado => "FALLBACK_CONTROLADO",
            _ => "NAO_INFORMADA"
        };

    private static OrigemMaterialEmbalagemCaixa OrigemEmbalagemEnum(string? texto)
        => (texto ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "SAP" => OrigemMaterialEmbalagemCaixa.Sap,
            "OPERADOR" => OrigemMaterialEmbalagemCaixa.Operador,
            "CONFIGURACAO" => OrigemMaterialEmbalagemCaixa.Configuracao,
            "FALLBACK_CONTROLADO" => OrigemMaterialEmbalagemCaixa.FallbackControlado,
            _ => OrigemMaterialEmbalagemCaixa.NaoInformada
        };

    private static ProdutoAcabadoCaixa Hidratar(NpgsqlDataReader r)
    {
        decimal quantidade = r.GetFieldValue<decimal>(r.GetOrdinal("quantidade"));
        return new ProdutoAcabadoCaixa
        {
            CodigoProdutoAcabadoCaixa = r.GetInt64(r.GetOrdinal("codigo_hu_caixa")),
            NumeroCaixa = r.GetInt32(r.GetOrdinal("numero_caixa")),
            CodigoCaixaLocal = r.GetString(r.GetOrdinal("codigo_caixa_local")),
            HandlingUnitCaixa = r.GetString(r.GetOrdinal("hu_caixa")),
            NumeroOrdemProducao = r.GetString(r.GetOrdinal("numero_ordem_producao")),
            ItemOrdemProducao = r.GetString(r.GetOrdinal("item_ordem_producao")),
            Material = r.GetString(r.GetOrdinal("material")),
            Lote = r.GetString(r.GetOrdinal("lote")),
            Centro = r.GetString(r.GetOrdinal("centro")),
            Deposito = r.GetString(r.GetOrdinal("deposito")),
            MaterialEmbalagem = r.GetString(r.GetOrdinal("material_embalagem")),
            OrigemMaterialEmbalagem = OrigemEmbalagemEnum(r.GetString(r.GetOrdinal("origem_material_embalagem"))),
            PesoBrutoKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_bruto")),
            PesoLiquidoKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_liquido")),
            TaraKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_tara")),
            UnidadePeso = r.GetString(r.GetOrdinal("unidade_peso")),
            QuantidadeProdutos = (int)Math.Round(quantidade, MidpointRounding.AwayFromZero),
            UnidadeQuantidade = r.GetString(r.GetOrdinal("unidade_quantidade")),
            OrigemPesagem = r.GetString(r.GetOrdinal("origem_pesagem")),
            CodigoBalanca = LerBigintOuNulo(r, "codigo_balanca"),
            CodigoUsuario = r.GetInt64(r.GetOrdinal("codigo_usuario")),
            Terminal = r.GetString(r.GetOrdinal("terminal")),
            CorrelationId = r.GetFieldValue<Guid>(r.GetOrdinal("correlation_id")),
            StatusIntegracao = MapeadorStatusHuCaixa.DoTextoBanco(r.GetString(r.GetOrdinal("status_hu_caixa"))),
            HandlingUnitExternalId = LerTextoOuNulo(r, "handling_unit_external_id"),
            RequestPayload = LerTextoOuNulo(r, "request_json_sanitizado"),
            ResponsePayload = LerTextoOuNulo(r, "response_json_sanitizado"),
            ErroSanitizado = LerTextoOuNulo(r, "erro_sanitizado"),
            HttpStatus = LerIntOuNulo(r, "http_status"),
            Tentativas = r.GetInt32(r.GetOrdinal("tentativas")),
            ClaimToken = LerUuidOuNulo(r, "claim_token"),
            CriadoEm = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("criado_em")),
            AtualizadoEm = r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("atualizado_em")),
            EnviadoSapEm = LerDateTimeOffsetOuNulo(r, "enviado_sap_em"),
            ConfirmadoSapEm = LerDateTimeOffsetOuNulo(r, "confirmado_sap_em")
        };
    }

    // GATE 047-V: hidratação restrita às 15 colunas do RETURNS TABLE das capabilities 047. NÃO lê colunas
    // ausentes (item_ordem_producao, material_embalagem, quantidade, correlation_id, tentativas, auditoria, etc.):
    // elas ficam no default do modelo. Cobre o contrato da Paletização (HU, OP, material, lote, pesos, status, centro/depósito).
    private static ProdutoAcabadoCaixa Hidratar047(NpgsqlDataReader r) => new()
    {
        CodigoProdutoAcabadoCaixa = r.GetInt64(r.GetOrdinal("codigo_hu_caixa")),
        HandlingUnitExternalId = LerTextoOuNulo(r, "handling_unit_external_id"),
        NumeroOrdemProducao = r.GetString(r.GetOrdinal("numero_ordem_producao")),
        Material = r.GetString(r.GetOrdinal("material")),
        Lote = r.GetString(r.GetOrdinal("lote")),
        NumeroCaixa = r.GetInt32(r.GetOrdinal("numero_caixa")),
        HandlingUnitCaixa = r.GetString(r.GetOrdinal("hu_caixa")),
        CodigoCaixaLocal = r.GetString(r.GetOrdinal("codigo_caixa_local")),
        PesoBrutoKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_bruto")),
        PesoLiquidoKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_liquido")),
        TaraKg = r.GetFieldValue<decimal>(r.GetOrdinal("peso_tara")),
        UnidadePeso = r.GetString(r.GetOrdinal("unidade_peso")),
        Centro = r.GetString(r.GetOrdinal("centro")),
        Deposito = r.GetString(r.GetOrdinal("deposito")),
        StatusIntegracao = MapeadorStatusHuCaixa.DoTextoBanco(r.GetString(r.GetOrdinal("status_hu_caixa")))
    };

    private static string? LerTextoOuNulo(NpgsqlDataReader r, string coluna)
    {
        int i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    private static int? LerIntOuNulo(NpgsqlDataReader r, string coluna)
    {
        int i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    private static long? LerBigintOuNulo(NpgsqlDataReader r, string coluna)
    {
        int i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetInt64(i);
    }

    private static Guid? LerUuidOuNulo(NpgsqlDataReader r, string coluna)
    {
        int i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetFieldValue<Guid>(i);
    }

    private static DateTimeOffset? LerDateTimeOffsetOuNulo(NpgsqlDataReader r, string coluna)
    {
        int i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetFieldValue<DateTimeOffset>(i);
    }
}


