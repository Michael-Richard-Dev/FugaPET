using System.Collections.Concurrent;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Orquestrador PRODUTIVO do pipeline Produto Acabado sobre o contrato 045 REV5 CORRETIVA 2 FINAL. A AUTORIDADE
/// de persistÃªncia Ã© o PostgreSQL 045 (via <see cref="IProdutoAcabadoPipeline045Operacoes"/>): para cada etapa
/// executa <c>preparar_etapa â†’ claim_etapa â†’ POST (gateway homologado) â†’ registrar_sucesso/erro/timeout</c>.
/// Ordem ESTRITA: 261 confirmado libera 101; 101 confirmado libera HU (044). Sem claim â‡’ sem POST. Timeout/erro
/// â‡’ para o pipeline, zero retry cego. O guard de HU pÃ³s-101 Ã© TRIGGER de banco â€” nunca invocado aqui. O
/// snapshot devolvido Ã© PROJEÃ‡ÃƒO para UI/teste (nÃ£o Ã© autoridade). ProteÃ§Ã£o local de duplo clique por caixa
/// (a autoridade multi-processo Ã© o claim do banco).
/// </summary>
public sealed class ProdutoAcabadoPipeline045Orquestrador
{
    private const string EndpointMaterialDocument = "/A_MaterialDocumentHeader";

    private static class Pa045RuntimeTrace
    {
        private static readonly object Sync = new();
        public static string LogPath => System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "pa045_runtime_trace.log");

        public static void Registrar(Guid correlationId, long codigoCaixa, string etapa, string marco, string detalhe)
        {
            try
            {
                string linha = $"{DateTimeOffset.UtcNow:O}|cid={correlationId}|codigo_caixa={codigoCaixa}|etapa={etapa}|marco={marco}|{Sanitizar(detalhe)}" + Environment.NewLine;
                lock (Sync)
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
                    System.IO.File.AppendAllText(LogPath, linha, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"[PA045_RUNTIME_TRACE] Falha ao gravar trace: {ex.GetType().Name}");
            }
        }

        private static string Sanitizar(string detalhe)
        {
            if (string.IsNullOrWhiteSpace(detalhe)) { return "detalhe=vazio"; }
            string limpo = detalhe.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            return limpo.Length <= 1200 ? limpo : limpo[..1200] + "...";
        }
    }

    private readonly IProdutoAcabadoPipeline045Operacoes _ops;
    private readonly IProdutoAcabadoMovimento261Gateway _gateway261;
    private readonly IProdutoAcabadoMovimento101Gateway _gateway101;
    private readonly IProdutoAcabadoHuEnvio _huEnvio;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _travas = new();

    public ProdutoAcabadoPipeline045Orquestrador(
        IProdutoAcabadoPipeline045Operacoes ops,
        IProdutoAcabadoMovimento261Gateway gateway261,
        IProdutoAcabadoMovimento101Gateway gateway101,
        IProdutoAcabadoHuEnvio huEnvio)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        _gateway261 = gateway261 ?? throw new ArgumentNullException(nameof(gateway261));
        _gateway101 = gateway101 ?? throw new ArgumentNullException(nameof(gateway101));
        _huEnvio = huEnvio ?? throw new ArgumentNullException(nameof(huEnvio));
    }

    public async Task<ResultadoPipelineProdutoAcabado> ExecutarAsync(
        long codigoCaixa,
        ProdutoAcabadoMovimento261Command comando261,
        ProdutoAcabadoMovimento101Command comando101,
        long usuario,
        string terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando261);
        ArgumentNullException.ThrowIfNull(comando101);

        SemaphoreSlim trava = _travas.GetOrAdd(codigoCaixa, _ => new SemaphoreSlim(1, 1));
        if (!await trava.WaitAsync(0, cancellationToken))
        {
            return new ResultadoPipelineProdutoAcabado(false, EtapaPipelineProdutoAcabado.Bloqueada,
                "Pipeline jÃ¡ em execuÃ§Ã£o para esta caixa.", null);
        }

        ProdutoAcabadoPipelineSnapshot snapshot = new() { CodigoProdutoAcabadoCaixa = codigoCaixa };
        try
        {
            await _ops.IniciarFluxoAsync(codigoCaixa, usuario, terminal, origem: null, cancellationToken);

            // ---------------- 261 ----------------
            ResultadoEtapa etapa261 = await ExecutarEtapaAsync(
                codigoCaixa, ProdutoAcabadoPipelinePostgresStore.Etapa261, """{"GoodsMovementType":"261"}""",
                (claim, ct) => _gateway261.EnviarAsync(GarantirCorrelationId261(codigoCaixa, claim, comando261), ct), usuario, terminal, cancellationToken);
            Aplicar(snapshot, ProdutoAcabadoPipelinePostgresStore.Etapa261, etapa261);
            // Â§5: 261 sÃ³ libera 101 quando Confirmado NO SAP E persistÃªncia 045 comprovada. registrar_sucesso=false â‡’ zero 101, zero HU.
            if (!etapa261.Avancavel)
            {
                return Parar(snapshot, EtapaPipelineProdutoAcabado.Movimento261, etapa261.Mensagem);
            }

            // ---------------- 101 (sÃ³ apÃ³s 261 confirmado) ----------------
            ResultadoEtapa etapa101 = await ExecutarEtapaAsync(
                codigoCaixa, ProdutoAcabadoPipelinePostgresStore.Etapa101, """{"GoodsMovementType":"101"}""",
                (_, ct) => _gateway101.EnviarAsync(comando101, ct), usuario, terminal, cancellationToken);
            Aplicar(snapshot, ProdutoAcabadoPipelinePostgresStore.Etapa101, etapa101);
            // Â§5: 101 sÃ³ libera HU quando Confirmado NO SAP E persistÃªncia 045 comprovada. registrar_sucesso=false â‡’ zero HU.
            if (!etapa101.Avancavel)
            {
                return Parar(snapshot, EtapaPipelineProdutoAcabado.Movimento101, etapa101.Mensagem);
            }

            // ---------------- HU 044 (sÃ³ apÃ³s 101 confirmado) ----------------
            // Â§2 (ponte 045â†’044): o adaptador consulta o estado autoritativo da HU e sÃ³ POSTa se AGUARDANDO
            // (autoriza+comprova PRONTA) ou jÃ¡ PRONTA; qualquer outro estado â‡’ zero POST. O guard pÃ³s-101 Ã© trigger de banco.
            StatusIntegracaoCaixa estadoHu = await _huEnvio.EnviarHuAsync(codigoCaixa, usuario, terminal, cancellationToken);
            snapshot.EstadoHu = estadoHu;

            // Â§4: SOMENTE HU ConfirmadaSap conclui o pipeline. Qualquer outro estado (ProntaParaEnvio/EnviandoSap/
            // ErroSap/IndeterminadoTimeout/Bloqueada/...) â‡’ NÃƒO Concluido, zero retry cego.
            if (estadoHu == StatusIntegracaoCaixa.ConfirmadaSap)
            {
                return new ResultadoPipelineProdutoAcabado(true, EtapaPipelineProdutoAcabado.Concluido,
                    "Pipeline 045 concluÃ­do: 261 + 101 confirmados e HU ConfirmadaSap.", snapshot);
            }

            return Parar(snapshot, EtapaPipelineProdutoAcabado.HandlingUnit,
                $"Pipeline nÃ£o concluÃ­do: HU em {estadoHu} (nÃ£o ConfirmadaSap). Sem retry cego.");
        }
        finally
        {
            trava.Release();
        }
    }

    private async Task<ResultadoEtapa> ExecutarEtapaAsync(
        long codigo, string etapa, string requestJson,
        Func<ResultadoClaim045, CancellationToken, Task<ResultadoMovimentoSap>> postar, long usuario, string terminal, CancellationToken ct)
    {
        Guid traceId = Guid.NewGuid();
        Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M01", $"antes_preparar;ct_cancelado={ct.IsCancellationRequested}");
        await _ops.PrepararEtapaAsync(codigo, etapa, requestJson, usuario, terminal, ct);

        // Â§6: sem claim â‡’ NÃƒO POSTAR. A capability vem sÃ³ do retorno de claim_etapa (nunca SELECT).
        ResultadoClaim045 claim = await _ops.AdquirirClaimEtapaAsync(codigo, etapa, usuario, terminal, ct);
        Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M02", $"claim_obtido={claim.Obtido};tentativa={claim.Tentativa};claim_token={(claim.Token.HasValue ? claim.Token.Value.ToString("D") : "null")}");
        if (!claim.Obtido)
        {
            // Claim negado pelo banco (estado nÃ£o permite, lease vigente de outro ator, etc.) â‡’ nenhum POST.
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M10", "retorno_claim_negado;avancavel=false");
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M11", "saida_etapa;estado=Erro;avancavel=false");
            return new ResultadoEtapa(EstadoMovimentoSap.Erro, false, null, null, null,
                $"Etapa {etapa}: claim nÃ£o obtido (banco negou). Nenhum POST executado.");
        }

        ResultadoMovimentoSap r;
        try
        {
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M03", $"antes_postar;ct_cancelado={ct.IsCancellationRequested}");
            r = await postar(claim, ct); // UM POST, zero retry cego
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M04A", $"retorno_postar;estado={r.Estado};http={r.HttpStatus};material_document_presente={!string.IsNullOrWhiteSpace(r.MaterialDocument)};mensagem={r.MensagemSanitizada}");
        }
        catch (Exception ex)
        {
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M04B", $"excecao_postar;tipo={ex.GetType().Name};ct_cancelado={ct.IsCancellationRequested};mensagem={ex.Message}");
            ResultadoEtapa resultadoExcecao = await RegistrarExcecaoPosClaimComoIndeterminadaAsync(traceId, codigo, etapa, claim, ex, usuario, terminal);
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M10", $"retorno_excecao;estado={resultadoExcecao.Estado};avancavel={resultadoExcecao.Avancavel};mensagem={resultadoExcecao.Mensagem}");
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M11", $"saida_etapa;estado={resultadoExcecao.Estado};avancavel={resultadoExcecao.Avancavel}");
            return resultadoExcecao;
        }

        if (r.Estado == EstadoMovimentoSap.Confirmado)
        {
            // Â§5: o POST foi confirmado no SAP; a persistÃªncia 045 Ã© a AUTORIDADE. registrar_sucesso=false â‡’ o
            // estado local nÃ£o foi comprovado â‡’ NÃƒO avanÃ§ar (Avancavel=false), zero retry do POST jÃ¡ confirmado.
            string responseJson = $$"""{"MaterialDocument":"{{r.MaterialDocument}}","MaterialDocumentYear":"{{r.MaterialDocumentYear}}"}""";
            bool persistiu;
            try
            {
                persistiu = await _ops.RegistrarSucessoEtapaAsync(codigo, etapa, claim.Tentativa ?? 0, claim.Token!.Value, r.HttpStatus ?? 0,
                    r.MaterialDocument ?? string.Empty, r.MaterialDocumentYear ?? string.Empty,
                    responseJson, EndpointMaterialDocument, usuario, terminal, CancellationToken.None);
            }
            catch (Exception ex)
            {
                return ResultadoPersistenciaDesfechoFalhou(etapa, "registrar_sucesso", ex, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus);
            }

            string msg = persistiu
                ? r.MensagemSanitizada
                : $"Etapa {etapa}: POST confirmado no SAP porÃ©m persistÃªncia 045 NÃƒO comprovada (registrar_sucesso=false). ReconciliaÃ§Ã£o necessÃ¡ria; sem avanÃ§o, sem retry.";
            ResultadoEtapa resultadoConfirmado = new(r.Estado, persistiu, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus, msg);
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M10", $"retorno_confirmado;persistiu={persistiu};avancavel={resultadoConfirmado.Avancavel}");
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M11", $"saida_etapa;estado={resultadoConfirmado.Estado};avancavel={resultadoConfirmado.Avancavel}");
            return resultadoConfirmado;
        }

        if (r.Estado == EstadoMovimentoSap.IndeterminadoTimeout)
        {
            // Â§6: timeout jÃ¡ nÃ£o avanÃ§a; se a persistÃªncia do timeout nÃ£o for comprovada, mantÃ©m sem avanÃ§o/retry.
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M05", "entrada_timeout_pos_claim");
            bool persistiu;
            try
            {
                Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M06", $"antes_registrar_timeout;tentativa={claim.Tentativa ?? 0};claim_token={claim.Token!.Value:D};ct_persistencia=CancellationToken.None");
                persistiu = await _ops.RegistrarTimeoutEtapaAsync(codigo, etapa, claim.Tentativa ?? 0, claim.Token!.Value, r.MensagemSanitizada, EndpointMaterialDocument, usuario, terminal, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M09C", $"registrar_timeout_excecao;tipo={ex.GetType().Name};mensagem={ex.Message}");
                ResultadoEtapa falhaTimeout = ResultadoPersistenciaDesfechoFalhou(etapa, "registrar_timeout", ex, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus);
                Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M10", $"retorno_timeout_excecao;estado={falhaTimeout.Estado};avancavel={falhaTimeout.Avancavel}");
                Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M11", $"saida_etapa;estado={falhaTimeout.Estado};avancavel={falhaTimeout.Avancavel}");
                return falhaTimeout;
            }

            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, persistiu ? "M09A" : "M09B", $"registrar_timeout_resultado={persistiu}");
            string msg = persistiu ? r.MensagemSanitizada
                : $"Etapa {etapa}: timeout com persistÃªncia 045 nÃ£o comprovada (registrar_timeout=false). Sem avanÃ§o, sem retry.";
            ResultadoEtapa resultadoTimeout = new(r.Estado, false, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus, msg);
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M10", $"retorno_timeout;persistiu={persistiu};avancavel={resultadoTimeout.Avancavel}");
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M11", $"saida_etapa;estado={resultadoTimeout.Estado};avancavel={resultadoTimeout.Avancavel}");
            return resultadoTimeout;
        }

        // Â§6: erro conhecido; nÃ£o avanÃ§a. PersistÃªncia do erro nÃ£o comprovada nÃ£o Ã© mascarada como sucesso.
        bool erroPersistido;
        try
        {
            erroPersistido = await _ops.RegistrarErroEtapaAsync(codigo, etapa, claim.Tentativa ?? 0, claim.Token!.Value, r.HttpStatus ?? 0, "{}", r.MensagemSanitizada, EndpointMaterialDocument, usuario, terminal, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return ResultadoPersistenciaDesfechoFalhou(etapa, "registrar_erro", ex, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus);
        }

        string msgErro = erroPersistido ? r.MensagemSanitizada
            : $"Etapa {etapa}: erro com persistÃªncia 045 nÃ£o comprovada (registrar_erro=false). Sem avanÃ§o, sem retry.";
        return new ResultadoEtapa(r.Estado, false, r.MaterialDocument, r.MaterialDocumentYear, r.HttpStatus, msgErro);
    }

    private async Task<ResultadoEtapa> RegistrarExcecaoPosClaimComoIndeterminadaAsync(
        Guid traceId, long codigo, string etapa, ResultadoClaim045 claim, Exception ex, long usuario, string terminal)
    {
        string mensagem = $"Etapa {etapa}: envio interrompido apÃ³s claim em estado que exige reconciliaÃ§Ã£o ({ex.GetType().Name}). NÃ£o tente enviar novamente.";
        Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M05", $"entrada_excecao_pos_claim;tipo={ex.GetType().Name}");
        try
        {
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M06", $"antes_registrar_timeout_excecao;tentativa={claim.Tentativa ?? 0};claim_token={claim.Token!.Value:D};ct_persistencia=CancellationToken.None");
            bool persistiu = await _ops.RegistrarTimeoutEtapaAsync(codigo, etapa, claim.Tentativa ?? 0, claim.Token!.Value, mensagem, EndpointMaterialDocument, usuario, terminal, CancellationToken.None);
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, persistiu ? "M09A" : "M09B", $"registrar_timeout_excecao_resultado={persistiu}");
            string msg = persistiu
                ? mensagem
                : $"Etapa {etapa}: exceÃ§Ã£o pÃ³s-claim com persistÃªncia 045 NÃƒO comprovada (registrar_timeout=false). ReconciliaÃ§Ã£o obrigatÃ³ria; sem avanÃ§o, sem retry.";
            return new ResultadoEtapa(EstadoMovimentoSap.IndeterminadoTimeout, false, null, null, null, msg);
        }
        catch (Exception persistencia)
        {
            System.Diagnostics.Trace.TraceWarning($"[ProdutoAcabadoPipeline045] Falha ao persistir desfecho indeterminado da etapa {etapa}: {persistencia.GetType().Name}");
            Pa045RuntimeTrace.Registrar(traceId, codigo, etapa, "M09C", $"registrar_timeout_excecao_falhou;tipo={persistencia.GetType().Name};mensagem={persistencia.Message}");
            return ResultadoPersistenciaDesfechoFalhou(etapa, "registrar_timeout", persistencia, null, null, null);
        }
    }


    private static ProdutoAcabadoMovimento261Command GarantirCorrelationId261(
        long codigoCaixa,
        ResultadoClaim045 claim,
        ProdutoAcabadoMovimento261Command comando)
    {
        if (!string.IsNullOrWhiteSpace(comando.CorrelationId))
        {
            return comando;
        }

        string correlationId = !string.IsNullOrWhiteSpace(claim.CorrelationId)
            ? claim.CorrelationId!
            : $"PA045-{codigoCaixa}-261-{claim.Tentativa ?? 0}-{claim.Token!.Value:N}";

        return comando with { CorrelationId = correlationId };
    }
    private static ResultadoEtapa ResultadoPersistenciaDesfechoFalhou(
        string etapa, string operacao, Exception ex, string? materialDocument, string? materialDocumentYear, int? http)
    {
        string mensagem = $"Etapa {etapa}: falha em {operacao} apÃ³s claim ({ex.GetType().Name}). Envio interrompido em estado que exige reconciliaÃ§Ã£o. NÃ£o tente enviar novamente.";
        return new ResultadoEtapa(EstadoMovimentoSap.IndeterminadoTimeout, false, materialDocument, materialDocumentYear, http, mensagem);
    }
    private static void Aplicar(ProdutoAcabadoPipelineSnapshot s, string etapa, ResultadoEtapa r)
    {
        if (etapa == ProdutoAcabadoPipelinePostgresStore.Etapa261)
        {
            s.Estado261 = r.Estado; s.MaterialDocument261 = r.MaterialDocument; s.MaterialDocumentYear261 = r.MaterialDocumentYear;
            s.HttpStatus261 = r.Http; s.Mensagem261Sanitizada = r.Mensagem;
        }
        else
        {
            s.Estado101 = r.Estado; s.MaterialDocument101 = r.MaterialDocument; s.MaterialDocumentYear101 = r.MaterialDocumentYear;
            s.HttpStatus101 = r.Http; s.Mensagem101Sanitizada = r.Mensagem;
        }
    }

    private static ResultadoPipelineProdutoAcabado Parar(ProdutoAcabadoPipelineSnapshot s, EtapaPipelineProdutoAcabado etapa, string mensagem)
        => new(true, etapa, mensagem, s);

    // Avancavel: sÃ³ true quando o POST foi Confirmado NO SAP E a persistÃªncia 045 foi comprovada (registrar_sucesso=true).
    private sealed record ResultadoEtapa(EstadoMovimentoSap Estado, bool Avancavel, string? MaterialDocument, string? MaterialDocumentYear, int? Http, string Mensagem);
}







