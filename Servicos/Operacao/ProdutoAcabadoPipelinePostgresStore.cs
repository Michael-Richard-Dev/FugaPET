using System.Collections.Concurrent;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Resultado da aquisiÃ§Ã£o de claim/recovery de etapa: token vem SOMENTE do retorno da funÃ§Ã£o (Â§3).</summary>
public sealed record ResultadoClaim045(bool Obtido, Guid? Token, int? Tentativa, string? CorrelationId = null)
{
    public static readonly ResultadoClaim045 NaoObtido = new(false, null, null);
}

/// <summary>
/// Store PostgreSQL REAL do pipeline 045 (261/101/HU/palete). Consome EXCLUSIVAMENTE as funÃ§Ãµes
/// <c>fn_pa_045_*</c> (SECURITY DEFINER) via <see cref="IProdutoAcabadoPipeline045Executor"/> â€” sem DML direto e
/// sem consultar tabelas para obter capability (Â§3/Â§4). A capability/token Ã© obtida SOMENTE do RETORNO das
/// funÃ§Ãµes de claim/recovery e mantida EM MEMÃ“RIA neste processo enquanto vigente; as funÃ§Ãµes de
/// sucesso/erro/timeout usam o token vigente em memÃ³ria. Atomicidade/locking multi-processo Ã© do banco (Â§8).
///
/// <para>Contrato consumido (nomes/ordem/colunas de retorno confirmados no pacote 045 REV5 CORRETIVA â€” PREFLIGHT
/// (to_regprocedure) e VALIDACAO): etapa â€” iniciar_fluxo, preparar_etapa, claim_etapa (retorna claim_token,
/// numero_tentativa), registrar_sucesso, registrar_erro, registrar_timeout, adquirir_recovery_etapa (retorna
/// recovery_claim_token, numero_tentativa), reassumir_claim_etapa, registrar_reconciliacao,
/// liberar_reprocessamento, registrar_261_historico_confirmado, guard_hu_pos_101; palete â€” palete_claim_envio,
/// palete_reassumir_claim, palete_adquirir_recovery, palete_registrar_sucesso, palete_registrar_erro,
/// palete_registrar_timeout, palete_registrar_reconciliacao, palete_liberar_reprocessamento,
/// palete_vincular_caixa.</para>
///
/// Implementa <see cref="IProdutoAcabadoPipelineStore"/>: <see cref="SuportaPersistenciaDefinitiva"/> reflete a
/// disponibilidade do executor (config PostgreSQL vÃ¡lida); <c>Obter/Salvar</c> operam sobre uma PROJEÃ‡ÃƒO em
/// memÃ³ria (o estado autoritativo Ã© o banco 045). Sem executor â‡’ fail-closed.
/// </summary>
public sealed class ProdutoAcabadoPipelinePostgresStore : IProdutoAcabadoPipelineStore, IProdutoAcabadoPipeline045Operacoes
{
    public const string Etapa261 = "261";
    public const string Etapa101 = "101";

    // Nomes das funÃ§Ãµes 045 (constantes internas; nunca entrada de usuÃ¡rio).
    private const string FnIniciarFluxo = "fn_pa_045_iniciar_fluxo";
    private const string FnPrepararEtapa = "fn_pa_045_preparar_etapa";
    private const string FnClaimEtapa = "fn_pa_045_claim_etapa";
    private const string FnRegistrarSucesso = "fn_pa_045_registrar_sucesso";
    private const string FnRegistrarErro = "fn_pa_045_registrar_erro";
    private const string FnRegistrarTimeout = "fn_pa_045_registrar_timeout";
    private const string FnAdquirirRecoveryEtapa = "fn_pa_045_adquirir_recovery_etapa";
    private const string FnReassumirClaimEtapa = "fn_pa_045_reassumir_claim_etapa";
    private const string FnRegistrarReconciliacao = "fn_pa_045_registrar_reconciliacao";
    private const string FnLiberarReprocessamento = "fn_pa_045_liberar_reprocessamento";
    private const string FnRegistrar261Historico = "fn_pa_045_registrar_261_historico_confirmado";
    private const string FnPaleteClaimEnvio = "fn_pa_045_palete_claim_envio";
    private const string FnPaleteReassumirClaim = "fn_pa_045_palete_reassumir_claim";
    private const string FnPaleteAdquirirRecovery = "fn_pa_045_palete_adquirir_recovery";
    private const string FnPaleteRegistrarSucesso = "fn_pa_045_palete_registrar_sucesso";
    private const string FnPaleteRegistrarErro = "fn_pa_045_palete_registrar_erro";
    private const string FnPaleteRegistrarTimeout = "fn_pa_045_palete_registrar_timeout";
    private const string FnPaleteRegistrarReconciliacao = "fn_pa_045_palete_registrar_reconciliacao";
    private const string FnPaleteLiberarReprocessamento = "fn_pa_045_palete_liberar_reprocessamento";
    private const string FnPaleteVincularCaixa = "fn_pa_045_palete_vincular_caixa";
    private const string FnPaleteCriar = "fn_pa_045_palete_criar";

    private static readonly object TraceSync = new();
    private static string TraceLogPath => System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "pa045_runtime_trace.log");

    private static void RegistrarTrace(long codigoCaixa, string etapa, string marco, string detalhe)
    {
        try
        {
            string linha = $"{DateTimeOffset.UtcNow:O}|cid=STORE|codigo_caixa={codigoCaixa}|etapa={etapa}|marco={marco}|{SanitizarTrace(detalhe)}" + Environment.NewLine;
            lock (TraceSync)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TraceLogPath)!);
                System.IO.File.AppendAllText(TraceLogPath, linha, System.Text.Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[PA045_STORE_TRACE] Falha ao gravar trace: {ex.GetType().Name}");
        }
    }

    private static string SanitizarTrace(string detalhe)
    {
        if (string.IsNullOrWhiteSpace(detalhe)) { return "detalhe=vazio"; }
        string limpo = detalhe.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return limpo.Length <= 1200 ? limpo : limpo[..1200] + "...";
    }

    private readonly IProdutoAcabadoPipeline045Executor _executor;

    // Tokens vigentes em memÃ³ria (Â§3): claim/recovery por (codigo,etapa) e por palete. NUNCA persistidos localmente.
    private readonly ConcurrentDictionary<string, (Guid token, int tentativa)> _claimEtapa = new();
    private readonly ConcurrentDictionary<string, (Guid token, int tentativa)> _recoveryEtapa = new();
    private readonly ConcurrentDictionary<long, (Guid token, int tentativa)> _claimPalete = new();
    private readonly ConcurrentDictionary<long, (Guid token, int tentativa)> _recoveryPalete = new();

    // ProjeÃ§Ã£o em memÃ³ria para Obter/Salvar do orquestrador (a verdade Ã© o banco 045).
    private readonly ConcurrentDictionary<long, ProdutoAcabadoPipelineSnapshot> _projecao = new();

    public ProdutoAcabadoPipelinePostgresStore(IProdutoAcabadoPipeline045Executor executor)
        => _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    // ---------------- IProdutoAcabadoPipelineStore ----------------
    public bool SuportaPersistenciaDefinitiva => _executor.Disponivel;

    public ProdutoAcabadoPipelineSnapshot? Obter(long codigoCaixa)
        => _projecao.TryGetValue(codigoCaixa, out ProdutoAcabadoPipelineSnapshot? s) ? s : null;

    public void Salvar(ProdutoAcabadoPipelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CodigoProdutoAcabadoCaixa is long c)
        {
            _projecao[c] = snapshot;
        }
    }

    // ---------------- Etapa 261/101 ----------------
    public async Task<bool> IniciarFluxoAsync(long codigo, long usuario, string terminal, string? origem = null, CancellationToken ct = default)
        => await FuncaoBoolAsync(FnIniciarFluxo,
            [P.Bigint(codigo), P.Bigint(usuario), P.Texto(terminal), P.Varchar("NOVA_ORQUESTRACAO"), P.Texto(origem)], ct);

    public Task<bool> PrepararEtapaAsync(long codigo, string etapa, string payloadJson, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnPrepararEtapa,
            [P.Bigint(codigo), P.Varchar(etapa), P.Jsonb(payloadJson), P.Bigint(usuario), P.Texto(terminal)], ct);

    /// <summary>Adquire o claim da etapa. Token vem SOMENTE do retorno (claim_token) e Ã© guardado em memÃ³ria.</summary>
    public async Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnClaimEtapa,
            [P.Bigint(codigo), P.Varchar(etapa), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "claim_token");
        if (r.Obtido) { _claimEtapa[Chave(codigo, etapa)] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    public Task<bool> RegistrarSucessoEtapaAsync(
        long codigo, string etapa, int http, string materialDocument, string materialDocumentYear,
        string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimEtapa.TryGetValue(Chave(codigo, etapa), out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarSucessoEtapaAsync(codigo, etapa, c.tentativa, c.token, http, materialDocument, materialDocumentYear, responseJson, endpoint, usuario, terminal, ct);
    }

    public Task<bool> RegistrarSucessoEtapaAsync(
        long codigo, string etapa, int tentativa, Guid claimToken, int http, string materialDocument, string materialDocumentYear,
        string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnRegistrarSucesso,
            [P.Bigint(codigo), P.Varchar(etapa), P.Integer(tentativa), P.Uuid(claimToken), P.Integer(http),
             P.Varchar(materialDocument), P.Varchar(materialDocumentYear), P.Jsonb(responseJson), P.Texto(endpoint),
             P.Bigint(usuario), P.Texto(terminal)], ct);

    public Task<bool> RegistrarErroEtapaAsync(
        long codigo, string etapa, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimEtapa.TryGetValue(Chave(codigo, etapa), out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarErroEtapaAsync(codigo, etapa, c.tentativa, c.token, http, responseJson, erro, endpoint, usuario, terminal, ct);
    }

    public Task<bool> RegistrarErroEtapaAsync(
        long codigo, string etapa, int tentativa, Guid claimToken, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnRegistrarErro,
            [P.Bigint(codigo), P.Varchar(etapa), P.Integer(tentativa), P.Uuid(claimToken), P.Integer(http),
             P.Jsonb(responseJson), P.Texto(erro), P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);

    public Task<bool> RegistrarTimeoutEtapaAsync(
        long codigo, string etapa, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimEtapa.TryGetValue(Chave(codigo, etapa), out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarTimeoutEtapaAsync(codigo, etapa, c.tentativa, c.token, erro, endpoint, usuario, terminal, ct);
    }

    public async Task<bool> RegistrarTimeoutEtapaAsync(
        long codigo, string etapa, int tentativa, Guid claimToken, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        RegistrarTrace(codigo, etapa, "M07", $"overload_explicito_store;tentativa={tentativa};claim_token={claimToken:D};ct_cancelado={ct.IsCancellationRequested}");
        try
        {
            RegistrarTrace(codigo, etapa, "M08", $"antes_FuncaoBoolAsync;funcao={FnRegistrarTimeout}");
            bool resultado = await FuncaoBoolAsync(FnRegistrarTimeout,
                [P.Bigint(codigo), P.Varchar(etapa), P.Integer(tentativa), P.Uuid(claimToken), P.Texto(erro),
                 P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);
            RegistrarTrace(codigo, etapa, resultado ? "M09A" : "M09B", $"FuncaoBoolAsync={resultado}");
            return resultado;
        }
        catch (Exception ex)
        {
            RegistrarTrace(codigo, etapa, "M09C", $"FuncaoBoolAsync_excecao;tipo={ex.GetType().Name};mensagem={ex.Message}");
            throw;
        }
    }

    /// <summary>Recovery da etapa apÃ³s restart. Nova capability vem SOMENTE do retorno (recovery_claim_token).</summary>
    public async Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnAdquirirRecoveryEtapa,
            [P.Bigint(codigo), P.Varchar(etapa), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "recovery_claim_token");
        if (r.Obtido) { _recoveryEtapa[Chave(codigo, etapa)] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    public async Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnReassumirClaimEtapa,
            [P.Bigint(codigo), P.Varchar(etapa), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "recovery_claim_token", "claim_token");
        if (r.Obtido) { _recoveryEtapa[Chave(codigo, etapa)] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    /// <summary>
    /// ReconciliaÃ§Ã£o de etapa (usa a capability de RECOVERY vigente). Contrato FINAL REV5 CORRETIVA 2: as posiÃ§Ãµes
    /// 7/8 sÃ£o <c>p_material_document</c> e <c>p_material_document_year</c> (varchar) â€” confirmadas no SQL canÃ´nico.
    /// </summary>
    public Task<bool> RegistrarReconciliacaoEtapaAsync(
        long codigo, string etapa, string resultado, int? http, string? materialDocument, string? materialDocumentYear,
        string responseJson, string? erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_recoveryEtapa.TryGetValue(Chave(codigo, etapa), out (Guid token, int tentativa) r)) { return Task.FromResult(false); }
        return FuncaoBoolAsync(FnRegistrarReconciliacao,
            [P.Bigint(codigo), P.Varchar(etapa), P.Integer(r.tentativa), P.Uuid(r.token), P.Varchar(resultado),
             P.Integer(http), P.Varchar(materialDocument), P.Varchar(materialDocumentYear), P.Jsonb(responseJson),
             P.Texto(erro), P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);
    }

    public Task<bool> LiberarReprocessamentoEtapaAsync(
        long codigo, string etapa, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_recoveryEtapa.TryGetValue(Chave(codigo, etapa), out (Guid token, int tentativa) r)) { return Task.FromResult(false); }
        return FuncaoBoolAsync(FnLiberarReprocessamento,
            [P.Bigint(codigo), P.Varchar(etapa), P.Uuid(r.token), P.Varchar(tipoLiberacao), P.Texto(motivo),
             P.Bigint(usuario), P.Texto(terminal)], ct);
    }

    public Task<bool> Registrar261HistoricoConfirmadoAsync(
        long codigo, string materialDocument, string materialDocumentYear, string origem, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnRegistrar261Historico,
            [P.Bigint(codigo), P.Varchar(materialDocument), P.Varchar(materialDocumentYear), P.Texto(origem),
             P.Bigint(usuario), P.Texto(terminal)], ct);

    // Â§2/Â§7: fn_pa_045_guard_hu_pos_101 Ã© funÃ§Ã£o de TRIGGER â€” atua automaticamente no banco quando o fluxo 044
    // altera status_hu_caixa. NUNCA Ã© chamada pelo C#. (Sem mÃ©todo/const para ela â€” proibiÃ§Ã£o explÃ­cita.)

    // ---------------- Snapshot por VIEWS runtime (sem tokens, Â§4) ----------------
    public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long codigo, CancellationToken ct = default)
        => _executor.LerViewRuntimeAsync("vw_pa_045_etapa_estado_runtime", "codigo_hu_caixa", P.Bigint(codigo), ct);

    public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long codigoPalete, CancellationToken ct = default)
        => _executor.LerViewRuntimeAsync("vw_pa_045_palete_estado_runtime", "codigo_hu_palete", P.Bigint(codigoPalete), ct);

    // ---------------- Palete ----------------
    public async Task<long?> CriarPaleteAsync(
        string codigoPaleteLocal,
        string plant,
        string storageLocation,
        decimal pesoBrutoKg,
        decimal pesoLiquidoKg,
        decimal taraKg,
        long usuario,
        string terminal,
        CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnPaleteCriar,
            [P.Texto(codigoPaleteLocal), P.Texto(plant), P.Texto(storageLocation), P.Numeric(pesoBrutoKg),
             P.Numeric(pesoLiquidoKg), P.Numeric(taraKg), P.Bigint(usuario), P.Texto(terminal)], ct);
        return linhas.Count == 0 ? null : linhas[0].ObterLong("codigo_hu_palete");
    }

    public async Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long codigoPalete, string requestJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnPaleteClaimEnvio,
            [P.Bigint(codigoPalete), P.Jsonb(requestJson), P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "claim_token");
        if (r.Obtido) { _claimPalete[codigoPalete] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    public async Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnPaleteAdquirirRecovery,
            [P.Bigint(codigoPalete), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "recovery_claim_token");
        if (r.Obtido) { _recoveryPalete[codigoPalete] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    public async Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(FnPaleteReassumirClaim,
            [P.Bigint(codigoPalete), P.Bigint(usuario), P.Texto(terminal)], ct);
        ResultadoClaim045 r = LerClaim(linhas, "claim_token");
        if (r.Obtido) { _claimPalete[codigoPalete] = (r.Token!.Value, r.Tentativa ?? 0); }
        return r;
    }

    public Task<bool> RegistrarSucessoPaleteAsync(
        long codigoPalete, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimPalete.TryGetValue(codigoPalete, out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarSucessoPaleteAsync(codigoPalete, c.tentativa, c.token, http, ucGerada, responseJson, endpoint, usuario, terminal, ct);
    }

    public Task<bool> RegistrarSucessoPaleteAsync(
        long codigoPalete, int tentativa, Guid claimToken, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnPaleteRegistrarSucesso,
            [P.Bigint(codigoPalete), P.Integer(tentativa), P.Uuid(claimToken), P.Varchar(ucGerada), P.Integer(http),
             P.Jsonb(responseJson), P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);

    public Task<bool> RegistrarErroPaleteAsync(
        long codigoPalete, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimPalete.TryGetValue(codigoPalete, out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarErroPaleteAsync(codigoPalete, c.tentativa, c.token, http, responseJson, erro, endpoint, usuario, terminal, ct);
    }

    public Task<bool> RegistrarErroPaleteAsync(
        long codigoPalete, int tentativa, Guid claimToken, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnPaleteRegistrarErro,
            [P.Bigint(codigoPalete), P.Integer(tentativa), P.Uuid(claimToken), P.Integer(http), P.Jsonb(responseJson),
             P.Texto(erro), P.Texto(endpoint), P.Bigint(usuario), P.Texto(terminal)], ct);

    public Task<bool> RegistrarTimeoutPaleteAsync(
        long codigoPalete, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_claimPalete.TryGetValue(codigoPalete, out (Guid token, int tentativa) c)) { return Task.FromResult(false); }
        return RegistrarTimeoutPaleteAsync(codigoPalete, c.tentativa, c.token, erro, endpoint, usuario, terminal, ct);
    }

    public Task<bool> RegistrarTimeoutPaleteAsync(
        long codigoPalete, int tentativa, Guid claimToken, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnPaleteRegistrarTimeout,
            [P.Bigint(codigoPalete), P.Integer(tentativa), P.Uuid(claimToken), P.Texto(erro), P.Texto(endpoint),
             P.Bigint(usuario), P.Texto(terminal)], ct);

    public Task<bool> RegistrarReconciliacaoPaleteAsync(
        long codigoPalete, string resultado, string? huPai, string evidenciaJson, string? erro, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_recoveryPalete.TryGetValue(codigoPalete, out (Guid token, int tentativa) r)) { return Task.FromResult(false); }
        return FuncaoBoolAsync(FnPaleteRegistrarReconciliacao,
            [P.Bigint(codigoPalete), P.Integer(r.tentativa), P.Uuid(r.token), P.Varchar(resultado), P.Varchar(huPai),
             P.Jsonb(evidenciaJson), P.Texto(erro), P.Bigint(usuario), P.Texto(terminal)], ct);
    }

    public Task<bool> LiberarReprocessamentoPaleteAsync(
        long codigoPalete, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default)
    {
        if (!_recoveryPalete.TryGetValue(codigoPalete, out (Guid token, int tentativa) r)) { return Task.FromResult(false); }
        return FuncaoBoolAsync(FnPaleteLiberarReprocessamento,
            [P.Bigint(codigoPalete), P.Uuid(r.token), P.Varchar(tipoLiberacao), P.Texto(motivo), P.Bigint(usuario), P.Texto(terminal)], ct);
    }

    public Task<bool> VincularCaixaPaleteAsync(long codigoPalete, long codigoCaixa, int sequencia, long usuario, string terminal, CancellationToken ct = default)
        => FuncaoBoolAsync(FnPaleteVincularCaixa,
            [P.Bigint(codigoPalete), P.Bigint(codigoCaixa), P.Integer(sequencia), P.Bigint(usuario), P.Texto(terminal)], ct);

    // GATE 046-E §3/§4: criação ATÔMICA — fn_pa_045_palete_criar + N × fn_pa_045_palete_vincular_caixa na MESMA
    // conexão/MESMA transação. COMMIT só depois de TODAS retornarem sucesso; qualquer false/erro/cancelamento
    // antes do COMMIT ⇒ ROLLBACK integral (o executor reverte criação e vínculos). Sem remoção/correção compensatória.
    public async Task<ResultadoCriacaoPaleteLocalAtomica> CriarPaleteComCaixasAsync(
        string materialEmbalagem, string plant, string storageLocation,
        decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg,
        IReadOnlyList<long> codigosCaixas, long usuario, string terminal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(codigosCaixas);
        if (string.IsNullOrWhiteSpace(materialEmbalagem))
        {
            return ResultadoCriacaoPaleteLocalAtomica.Bloqueado("Material de embalagem ausente: transação não iniciada.");
        }
        if (codigosCaixas.Count == 0 || codigosCaixas.Any(c => c <= 0))
        {
            return ResultadoCriacaoPaleteLocalAtomica.Bloqueado("Caixas inválidas para vínculo: transação não iniciada.");
        }

        try
        {
            long codigoHuPalete = await _executor.ExecutarEmTransacaoAsync(async (tx, ctx) =>
            {
                IReadOnlyList<Linha045> criacao = await tx.ExecutarFuncaoAsync(FnPaleteCriar,
                    [P.Texto(materialEmbalagem), P.Texto(plant), P.Texto(storageLocation),
                     P.Numeric(pesoBrutoKg), P.Numeric(pesoLiquidoKg), P.Numeric(taraKg),
                     P.Bigint(usuario), P.Texto(terminal)], ctx);
                long? codigo = criacao.Count == 0 ? null : criacao[0].ObterLong("codigo_hu_palete");
                if (codigo is not long codigoPalete || codigoPalete <= 0)
                {
                    throw new PaleteAtomicidadeException("Criação do palete negada pelo banco.");
                }

                for (int indice = 0; indice < codigosCaixas.Count; indice++)
                {
                    IReadOnlyList<Linha045> vinc = await tx.ExecutarFuncaoAsync(FnPaleteVincularCaixa,
                        [P.Bigint(codigoPalete), P.Bigint(codigosCaixas[indice]), P.Integer(indice + 1),
                         P.Bigint(usuario), P.Texto(terminal)], ctx);
                    if (!InterpretarBool(vinc, FnPaleteVincularCaixa))
                    {
                        throw new PaleteAtomicidadeException($"Vínculo da caixa {codigosCaixas[indice]} negado pelo banco.");
                    }
                }

                return codigoPalete;
            }, ct);

            return ResultadoCriacaoPaleteLocalAtomica.Ok(codigoHuPalete);
        }
        catch (PaleteAtomicidadeException ex)
        {
            // false → rollback já feito pelo executor. Fail-closed: nada persiste.
            return ResultadoCriacaoPaleteLocalAtomica.Bloqueado($"{ex.Message} Rollback integral: nenhum palete/vínculo persistido.");
        }
        catch (OperationCanceledException)
        {
            // §5/§14-F: cancelamento antes do COMMIT ⇒ rollback integral já efetuado. Fail-closed.
            return ResultadoCriacaoPaleteLocalAtomica.Bloqueado("Operação cancelada antes do commit. Rollback integral: nenhum palete/vínculo persistido.");
        }
        catch (Exception ex)
        {
            // §14-E: exceção PostgreSQL/infra ⇒ rollback já efetuado pelo executor. Fail-closed (não mascara sucesso).
            return ResultadoCriacaoPaleteLocalAtomica.Bloqueado($"Falha na transação do palete ({ex.GetType().Name}). Rollback integral: nenhum palete/vínculo persistido.");
        }
    }

    // GATE 046-E §7/§10: reload persistente — projeta a composição por OP/terminal e RECONSTRÓI palete + caixas
    // a partir do banco (autoritativo), sem depender de estado em memória. Ordena por codigo_hu_palete + ordem_item.
    public async Task<IReadOnlyList<ProdutoAcabadoPalete>> LerPaletesLocaisPorOrdemAsync(
        string numeroOrdemProducao, string? terminal, CancellationToken ct = default)
    {
        string? terminalFiltro = string.IsNullOrWhiteSpace(terminal) ? null : terminal;
        IReadOnlyList<Linha045> linhas = await _executor.LerComposicaoPaletesLocaisPorOrdemAsync(numeroOrdemProducao, terminalFiltro, ct);
        return ReconstruirPaletes(linhas);
    }

    // GATE 047-AB: reload por TERMINAL (sem OP) — usado ao ABRIR a Paletização. Terminal vazio ⇒ fail-closed (não lista tudo).
    public async Task<IReadOnlyList<ProdutoAcabadoPalete>> LerPaletesLocaisPorTerminalAsync(
        string? terminal, CancellationToken ct = default)
    {
        string terminalFiltro = (terminal ?? string.Empty).Trim();
        if (terminalFiltro.Length == 0) { return []; }
        IReadOnlyList<Linha045> linhas = await _executor.LerComposicaoPaletesLocaisPorTerminalAsync(terminalFiltro, ct);
        return ReconstruirPaletes(linhas);
    }

    // Reconstrução determinística (uma linha por caixa-em-palete). Agrupa por codigo_hu_palete preservando a ordem
    // de chegada (já ordenada por codigo_hu_palete, ordem_item). StatusSap reflete status_hu_palete (RASCUNHO local).
    private static IReadOnlyList<ProdutoAcabadoPalete> ReconstruirPaletes(IReadOnlyList<Linha045> linhas)
    {
        List<ProdutoAcabadoPalete> paletes = [];
        List<long> ordemPaletes = [];
        Dictionary<long, (Linha045 cabecalho, List<ProdutoAcabadoCaixa> caixas)> mapa = [];

        foreach (Linha045 linha in linhas)
        {
            if (linha.ObterLong("codigo_hu_palete") is not long codigoPalete) { continue; }
            if (!mapa.TryGetValue(codigoPalete, out (Linha045 cabecalho, List<ProdutoAcabadoCaixa> caixas) grupo))
            {
                grupo = (linha, []);
                mapa[codigoPalete] = grupo;
                ordemPaletes.Add(codigoPalete);
            }

            // GATE 046-AQ-F1: o estado de integração da caixa vem do banco (status_hu_caixa) via mapeador CANÔNICO.
            // Sem isso o modelo assumia o default EmPesagem e o preview rejeitava "precisa estar CONFIRMADA_SAP"
            // mesmo com a caixa persistida como CONFIRMADA_SAP. Status ausente ⇒ mantém EmPesagem (fail-closed:
            // continua reprovando no preview; NUNCA vira ConfirmadaSap por default).
            string? statusCaixaTexto = linha.ObterTexto("status_hu_caixa");
            StatusIntegracaoCaixa statusCaixa = string.IsNullOrWhiteSpace(statusCaixaTexto)
                ? StatusIntegracaoCaixa.EmPesagem
                : MapeadorStatusHuCaixa.DoTextoBanco(statusCaixaTexto);

            grupo.caixas.Add(new ProdutoAcabadoCaixa
            {
                CodigoProdutoAcabadoCaixa = linha.ObterLong("codigo_hu_caixa"),
                NumeroCaixa = linha.ObterInt("numero_caixa") ?? 0,
                NumeroOrdemProducao = linha.ObterTexto("numero_ordem_producao") ?? string.Empty,
                ItemOrdemProducao = linha.ObterTexto("item_ordem_producao") ?? string.Empty,
                Material = linha.ObterTexto("material") ?? string.Empty,
                Lote = linha.ObterTexto("lote") ?? string.Empty,
                HandlingUnitExternalId = linha.ObterTexto("handling_unit_external_id"),
                HandlingUnitCaixa = linha.ObterTexto("hu_caixa") ?? string.Empty,
                StatusIntegracao = statusCaixa
            });
        }

        foreach (long codigoPalete in ordemPaletes)
        {
            (Linha045 cabecalho, List<ProdutoAcabadoCaixa> caixas) = mapa[codigoPalete];
            int primeira = caixas.Count == 0 ? 0 : caixas.Min(c => c.NumeroCaixa);
            int ultima = caixas.Count == 0 ? 0 : caixas.Max(c => c.NumeroCaixa);
            string statusLocal = cabecalho.ObterTexto("status_hu_palete") ?? "RASCUNHO";
            paletes.Add(new ProdutoAcabadoPalete
            {
                CodigoHuPalete = codigoPalete,
                CodigoPaleteLocal = codigoPalete.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PrimeiraCaixa = primeira,
                UltimaCaixa = ultima,
                PesoBrutoKg = cabecalho.ObterDecimal("peso_bruto") ?? 0m,
                PesoLiquidoKg = cabecalho.ObterDecimal("peso_liquido") ?? 0m,
                TaraKg = cabecalho.ObterDecimal("peso_tara") ?? 0m,
                Plant = cabecalho.ObterTexto("centro") ?? string.Empty,
                StorageLocation = cabecalho.ObterTexto("deposito") ?? string.Empty,
                PackagingMaterial = cabecalho.ObterTexto("material_embalagem") ?? string.Empty,
                // GATE 047-AD: HU pai persistida (hu_palete.handling_unit_external_id, set no CONFIRMADO_SAP), lida da view
                // via to_jsonb (fail-safe: NULL quando ausente). RASCUNHO ⇒ NULL ⇒ grid "-"; CONFIRMADO ⇒ HU real.
                HandlingUnitPalete = cabecalho.ObterTexto("handling_unit_palete") ?? string.Empty,
                Caixas = caixas,
                StatusSap = statusLocal
            });
        }

        return paletes;
    }

    private static bool InterpretarBool(IReadOnlyList<Linha045> linhas, string funcao)
    {
        if (linhas.Count == 0) { return false; }
        object? bruto = linhas[0].Bruto(funcao) ?? PrimeiroValor(linhas[0]);
        return bruto is bool b ? b : bruto is not null && bruto is not DBNull;
    }

    // ---------------- helpers ----------------
    private static string Chave(long codigo, string etapa) => $"{codigo}:{etapa}";

    private static ResultadoClaim045 LerClaim(IReadOnlyList<Linha045> linhas, params string[] colunasToken)
    {
        if (linhas.Count == 0) { return ResultadoClaim045.NaoObtido; }
        Linha045 linha = linhas[0];
        Guid? token = null;
        foreach (string col in colunasToken)
        {
            token = linha.ObterUuid(col);
            if (token is not null) { break; }
        }
        if (token is null) { return ResultadoClaim045.NaoObtido; }
        int? tentativa = linha.ObterInt("numero_tentativa") ?? linha.ObterInt("tentativa") ?? linha.ObterInt("tentativas");
        string? correlationId = linha.ObterTexto("correlation_id") ?? linha.ObterTexto("correlation");
        return new ResultadoClaim045(true, token, tentativa, string.IsNullOrWhiteSpace(correlationId) ? null : correlationId);
    }

    private async Task<bool> FuncaoBoolAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken ct)
    {
        IReadOnlyList<Linha045> linhas = await _executor.ExecutarFuncaoAsync(funcao, parametros, ct);
        if (linhas.Count == 0) { return false; }
        object? bruto = linhas[0].Bruto(funcao) ?? PrimeiroValor(linhas[0]);
        return bruto is bool b ? b : bruto is not null && bruto is not DBNull;
    }

    private static object? PrimeiroValor(Linha045 linha)
        => linha.Bruto("guard") ?? linha.Bruto("ok") ?? null;

    /// <summary>Atalho para <see cref="Parametro045"/> (legibilidade das chamadas).</summary>
    private static class P
    {
        public static Parametro045 Bigint(long v) => Parametro045.Bigint(v);
        public static Parametro045 Integer(int? v) => Parametro045.Integer(v);
        public static Parametro045 Varchar(string? v) => Parametro045.Varchar(v);
        public static Parametro045 Texto(string? v) => Parametro045.Texto(v);
        public static Parametro045 Numeric(decimal? v) => Parametro045.Numeric(v);
        public static Parametro045 Uuid(Guid v) => Parametro045.Uuid(v);
        public static Parametro045 Jsonb(string? v) => Parametro045.Jsonb(v);
    }
}




