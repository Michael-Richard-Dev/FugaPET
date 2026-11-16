using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Ambiente;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Terminal;

namespace FugaPET_Dev.Controle.Processo;

public sealed class ProdutoAcabadoController
{
    public const string EndpointConsultaOpProdutoAcabado =
        "GET /sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/A_ProductionOrder_2('<OP>')?$format=json&$expand=to_ProductionOrderItem,to_ProductionOrderOperation,to_ProductionOrderStatus&sap-client=110";

    private readonly IProductionOrderSapServico _productionOrderSapServico;
    private readonly TaraController _taraController;
    private readonly ProdutoAcabadoPaletePayloadBuilder _paletePayloadBuilder;

    // Fluxo de caixa individual (Handling Unit): orquestraÃ§Ã£o persistente HU/SAP via Service (injetÃ¡vel).
    private readonly ProdutoAcabadoHuService _huService;
    private readonly ProdutoAcabadoHandlingUnitCaixaRequestBuilder _requestBuilder = new();

    // Consulta REAL (somente leitura) da norma de embalagem â€” substitui o antigo fallback em memÃ³ria.
    private readonly IProdutoAcabadoNormaEmbalagemSapServico _normaEmbalagemSapServico;

    // REV4-Â§3/Â§5: composiÃ§Ã£o do NOVO pipeline (261â†’101â†’HU) a partir da configuraÃ§Ã£o. Fail-closed por padrÃ£o:
    // gate FUGAPET_SAP_PA_PIPELINE_ENABLED=false â‡’ HU-only permanece; gate true sem store persistente â‡’
    // DEPENDENCIA_GAIA. O orquestrador sÃ³ existe quando ambos sÃ£o verdadeiros.
    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline _composicaoPipeline;
    private readonly IProdutoAcabadoPaleteInt012Gateway _paleteInt012Gateway;
    private readonly ProdutoAcabadoPaleteInt012Orquestrador? _paleteInt012Orquestrador;
    private readonly ProdutoAcabadoPaleteLocalPersistenteOrquestrador? _paleteLocalPersistenteOrquestrador;
    private readonly IProdutoAcabadoPipeline045Operacoes? _pipeline045Operacoes;

    public ProdutoAcabadoController()
        : this(FabricaProductionOrderSapServico.Criar())
    {
    }

    public ProdutoAcabadoController(
        IProductionOrderSapServico productionOrderSapServico,
        TaraController? taraController = null,
        ProdutoAcabadoPaletePayloadBuilder? paletePayloadBuilder = null,
        IProdutoAcabadoRepositorio? repositorio = null,
        IProdutoAcabadoHandlingUnitSapServico? huSapServico = null,
        IProdutoAcabadoNormaEmbalagemSapServico? normaEmbalagemSapServico = null,
        ProdutoAcabadoHuService? huService = null,
        ConfiguracaoSap? configuracaoSap = null,
        IProdutoAcabadoPipelineStore? pipelineStore = null,
        IProdutoAcabadoPaleteInt012Gateway? paleteInt012Gateway = null)
    {
        _productionOrderSapServico = productionOrderSapServico ?? throw new ArgumentNullException(nameof(productionOrderSapServico));
        _taraController = taraController ?? FabricaControladoresCadastro.CriarTaraController();
        _paletePayloadBuilder = paletePayloadBuilder ?? new ProdutoAcabadoPaletePayloadBuilder();
        // PersistÃªncia produtiva real quando o banco estÃ¡ habilitado; senÃ£o fail-closed (nÃ£o finge banco).
        // Gateway SAP fail-closed por padrÃ£o (sem POST real). Testes injetam Service/fakes controlados.
        _normaEmbalagemSapServico = normaEmbalagemSapServico ?? FabricaProdutoAcabadoNormaEmbalagemSapServico.Criar();

        // ConfiguraÃ§Ã£o para o gate do pipeline: injetÃ¡vel nos testes; em runtime lÃª env/arquivo (gate default false).
        _configuracaoSap = configuracaoSap ?? CarregarConfiguracaoSapSeguro();
        // REV5-Â§9: store REAL PostgreSQL/045 por padrÃ£o (NUNCA MemoryStore produtivo). SuportaPersistenciaDefinitiva
        // reflete a disponibilidade do executor Npgsql (banco configurado). Banco ausente â‡’ executor indisponÃ­vel
        // â‡’ fail-closed na composiÃ§Ã£o. Testes injetam store/memÃ³ria controlados.
        IProdutoAcabadoPipelineStore store = pipelineStore
            ?? new ProdutoAcabadoPipelinePostgresStore(FabricaProdutoAcabadoPipeline045Executor.Criar());
        _pipeline045Operacoes = store as IProdutoAcabadoPipeline045Operacoes;

        // GATE 113J (§2/§3): o HuService passa a ser composto DEPOIS da configuraÃ§Ã£o/store, porque no modo
        // PIPELINE_045 ele recebe o EARLY BINDING do fluxo 045 (executado na finalizaÃ§Ã£o local, com a caixa
        // em FINALIZADA_LOCAL). No modo HU_ONLY o binding Ã© null e a finalizaÃ§Ã£o cria ZERO linhas 045 â€”
        // comportamento histÃ³rico preservado. A reordenaÃ§Ã£o Ã© segura: configuraÃ§Ã£o e store nÃ£o dependem
        // do HuService; sÃ³ a composiÃ§Ã£o do pipeline depende, e ela continua por Ãºltimo.
        _huService = huService ?? new ProdutoAcabadoHuService(
            repositorio ?? FabricaProdutoAcabadoRepositorio.Criar(),
            huSapServico ?? FabricaProdutoAcabadoHandlingUnitSapServico.Criar(),
            requestBuilder: null,
            estabelecerBindingPipeline045: MontarBindingPipeline045(_configuracaoSap, _pipeline045Operacoes));

        _composicaoPipeline = FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(
            _configuracaoSap, _huService, store);

        // REV4-Â§15: composiÃ§Ã£o runtime do INT012. Default permanece fail-closed; nenhum POST Ã© disparado aqui.
        _paleteInt012Gateway = paleteInt012Gateway ?? FabricaProdutoAcabadoPaleteInt012Gateway.Criar(_configuracaoSap);
        _paleteLocalPersistenteOrquestrador = store is IProdutoAcabadoPipeline045Operacoes opsPaleteLocal && opsPaleteLocal.SuportaPersistenciaDefinitiva
            ? new ProdutoAcabadoPaleteLocalPersistenteOrquestrador(opsPaleteLocal)
            : null;
        _paleteInt012Orquestrador = store is IProdutoAcabadoPipeline045Operacoes opsPalete && opsPalete.SuportaPersistenciaDefinitiva
            ? new ProdutoAcabadoPaleteInt012Orquestrador(opsPalete, _paleteInt012Gateway)
            : null;
    }

    private static ConfiguracaoSap CarregarConfiguracaoSapSeguro()
    {
        try
        {
            return LeitorConfiguracaoSap.Carregar();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            // ConfiguraÃ§Ã£o malformada â‡’ trata como nÃ£o habilitado (gate false); nunca lanÃ§a na inicializaÃ§Ã£o da UI.
            return new ConfiguracaoSap();
        }
    }

    // ============================ Descricao do material para a ETIQUETA ============================

    // GATE 126E: criado sob demanda e reaproveitado. A fabrica nunca lanca: configuracao ausente ou
    // malformada devolve uma implementacao que responde null, entao a etiqueta segue saindo.
    private readonly Lazy<IProductDescriptionSapServico> _descricaoProdutoSap =
        new(FabricaProductDescriptionSapServico.Criar);

    /// <summary>
    /// GATE 126E: descricao do material para a ETIQUETA, em melhor esforco. Le A_ProductDescription
    /// pelo servico ja existente (o mesmo que a tela de Consumo usa), escolhendo o idioma PT quando ha.
    /// <para>
    /// BEST-EFFORT POR DECISAO, nao por descuido: a etiqueta e impressa na gravacao do palete, que e
    /// efeito local. SAP indisponivel, material sem descricao cadastrada ou integracao desligada
    /// devolvem vazio e a etiqueta sai com o CODIGO do material, como saia antes. Falhar a impressao
    /// por causa do texto seria trocar uma etiqueta incompleta por nenhuma etiqueta.
    /// </para>
    /// <para>
    /// Nunca propaga excecao e nunca inventa texto: sem resposta utilizavel, devolve string vazia.
    /// </para>
    /// </summary>
    public async Task<string> ObterDescricaoMaterialParaEtiquetaAsync(
        string? material, CancellationToken cancellationToken = default)
    {
        string codigo = (material ?? string.Empty).Trim();
        if (codigo.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            ProdutoSapMestre? mestre = await _descricaoProdutoSap.Value
                .ObterDescricaoAsync(codigo, cancellationToken);
            return (mestre?.DescricaoProdutoSap ?? string.Empty).Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[ProdutoAcabado] Descricao do material {codigo} nao obtida ({ex.GetType().Name}); "
                + "a etiqueta sai com o codigo.");
            return string.Empty;
        }
    }

    // GATE 126F: servico de lotes, criado sob demanda. A URL e DERIVADA da que ja esta configurada
    // (troca do segmento API_*_SRV), entao nao exige cadastro novo no configuracao.sap.json.
    private readonly Lazy<IBatchSapServico> _lotesSap =
        new(() => new BatchSapServico(CarregarConfiguracaoSapSeguro()));

    /// <summary>
    /// GATE 126F: datas de fabricacao e validade dos lotes do palete, para a ETIQUETA, em melhor
    /// esforco. Le API_BATCH_SRV (colecao <c>Batch</c>), um lote por consulta.
    /// <para>
    /// BEST-EFFORT PELO MESMO MOTIVO DA DESCRICAO: a etiqueta sai na gravacao do palete, que e efeito
    /// local. Lote sem datas no cadastro, SAP fora do ar ou integracao desligada resultam em lote
    /// AUSENTE do mapa, e a linha correspondente sai "NAO INFORMADA" — como saia antes. Nenhuma data
    /// e deduzida: uma validade errada na etiqueta de expedicao e pior que etiqueta sem validade.
    /// </para>
    /// <para>
    /// Consulta em SERIE e nao em paralelo: um palete tem poucos lotes distintos, e disparar
    /// requisicoes concorrentes contra o SAP para ganhar milissegundos na impressao nao se paga.
    /// Lotes repetidos sao consultados UMA vez.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, LoteSapDatas>> ObterDatasDeLotesParaEtiquetaAsync(
        string? material,
        IEnumerable<string?> lotes,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, LoteSapDatas> mapa = [];

        string codigoMaterial = (material ?? string.Empty).Trim();
        if (codigoMaterial.Length == 0 || lotes is null)
        {
            return mapa;
        }

        HashSet<string> jaConsultados = [];
        foreach (string? lote in lotes)
        {
            string chave = LoteSapDatas.NormalizarLote(lote);
            if (chave.Length == 0 || !jaConsultados.Add(chave))
            {
                continue;
            }

            try
            {
                LoteSapDatas? datas = await _lotesSap.Value
                    .ObterDatasAsync(codigoMaterial, (lote ?? string.Empty).Trim(), cancellationToken);

                if (datas is not null)
                {
                    mapa[chave] = datas;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[ProdutoAcabado] Datas do lote nao obtidas ({ex.GetType().Name}); "
                    + "a linha sai NAO INFORMADA.");
            }
        }

        return mapa;
    }

    // ============================ Pipeline PA (261 â†’ 101 â†’ HU) â€” REV4 ============================

    /// <summary>REV4-Â§4: true quando o gate FUGAPET_SAP_PA_PIPELINE_ENABLED estÃ¡ ligado (independe de store/disponibilidade).</summary>
    public bool PipelinePaGateHabilitado => _configuracaoSap.ProdutoAcabadoPipelineHabilitado;

    /// <summary>REV4-Â§5: true SOMENTE quando gate ligado E store persistente definitivo disponÃ­vel (orquestrador composto).</summary>
    public bool PipelinePaDisponivel => _composicaoPipeline.Disponivel;

    /// <summary>Motivo da (in)disponibilidade do pipeline: PIPELINE_DESABILITADO / DEPENDENCIA_GAIA_PERSISTENCIA_PIPELINE / PIPELINE_COMPOSTO.</summary>
    public string PipelinePaMotivo => _composicaoPipeline.Motivo;

    /// <summary>REV4-Â§15: gateway INT012 composto no runtime, mas envio real permanece governado por gate prÃ³prio.</summary>
    public bool PaleteInt012EnvioAutorizado => _paleteInt012Gateway.EnvioAutorizado;

    /// <summary>
    /// GATE 113J (§2/§3): devolve o EARLY BINDING do fluxo 045 SOMENTE no modo PIPELINE_045 — gate ligado E
    /// operações 045 com persistência definitiva. Fora desse modo devolve null, e a finalização de caixa
    /// permanece HU_ONLY com ZERO linhas 045. Exposto internamente para teste do critério de composição.
    /// </summary>
    internal static Func<long, long, string, CancellationToken, Task<bool>>? MontarBindingPipeline045(
        ConfiguracaoSap configuracao,
        IProdutoAcabadoPipeline045Operacoes? operacoes)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (!configuracao.ProdutoAcabadoPipelineHabilitado
            || operacoes is null
            || !operacoes.SuportaPersistenciaDefinitiva)
        {
            return null;
        }

        return (codigo, usuario, terminal, ct) => operacoes.IniciarFluxoAsync(
            codigo, usuario, terminal, origem: "EARLY_BINDING_FINALIZADA_LOCAL", ct);
    }

    /// <summary>
    /// GATE 113J (§6) + 113M (B2): estado do vínculo 045 da caixa. NÃO depende do gate do pipeline — o
    /// binding 045 é persistente e IRREVERSÍVEL, então uma caixa com etapas 045 nunca volta a ser tratada
    /// como HU-only, inclusive após restart/erro/timeout com o runtime em modo HU_ONLY.
    ///
    /// 113M (B2): a AUSÊNCIA de consulta deixou de ser confundida com AUSÊNCIA DE VÍNCULO. Operações 045
    /// nulas, sem persistência definitiva, ou exceção de leitura ⇒ <see cref="EstadoVinculo045.ConsultaIndisponivel"/>,
    /// que BLOQUEIA a HU direta (fail-closed) — antes retornava false e liberava o envio.
    /// ZERO etapas materialmente consultadas continuam significando HU-only legítimo (113G).
    /// </summary>
    public async Task<EstadoVinculo045> ConsultarVinculoPipeline045Async(
        long codigoCaixa, CancellationToken cancellationToken = default)
    {
        if (_pipeline045Operacoes is null || !_pipeline045Operacoes.SuportaPersistenciaDefinitiva)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[ProdutoAcabado] 113M: consulta do vinculo 045 da caixa {codigoCaixa} INDISPONIVEL "
                + "(operacoes 045 ausentes ou sem persistencia definitiva); HU direta bloqueada.");
            return EstadoVinculo045.ConsultaIndisponivel;
        }

        try
        {
            IReadOnlyList<Linha045> etapas =
                await _pipeline045Operacoes.LerEstadoEtapasAsync(codigoCaixa, cancellationToken);
            return etapas.Count > 0 ? EstadoVinculo045.ComVinculo : EstadoVinculo045.SemVinculo;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[ProdutoAcabado] 113M: consulta do vinculo 045 da caixa {codigoCaixa} falhou "
                + $"({ex.GetType().Name}); tratando como CONSULTA_INDISPONIVEL (fail-closed).");
            return EstadoVinculo045.ConsultaIndisponivel;
        }
    }

    /// <summary>
    /// GATE 113M (B2): true quando o envio HU DIRETO deve ser bloqueado. Bloqueia COM_VINCULO (pertence ao
    /// pipeline) e CONSULTA_INDISPONIVEL (não se pode comprovar a ausência de vínculo). Puro e testável.
    /// </summary>
    internal static bool BloqueiaHuDireta(EstadoVinculo045 estado)
        => estado is EstadoVinculo045.ComVinculo or EstadoVinculo045.ConsultaIndisponivel;

    public async Task<ResultadoBloqueioPipeline045> VerificarBloqueioPipeline045Async(long codigoCaixa, CancellationToken cancellationToken = default)
    {
        if (!PipelinePaGateHabilitado || _pipeline045Operacoes is null || !_pipeline045Operacoes.SuportaPersistenciaDefinitiva)
        {
            return ResultadoBloqueioPipeline045.Liberado;
        }

        IReadOnlyList<Linha045> linhas;
        try
        {
            linhas = await _pipeline045Operacoes.LerEstadoEtapasAsync(codigoCaixa, cancellationToken);
        }
        catch (Exception ex)
        {
            return ResultadoBloqueioPipeline045.CriarBloqueio(
                $"Não foi possível consultar o estado 045 da caixa ({ex.GetType().Name}). Não tente enviar novamente até reconciliação.");
        }

        return AvaliarBloqueioPipeline045(linhas);
    }

    /// <summary>
    /// GATE 117G: decisão PRE-POST do pipeline 045. PENDENTE só é liberado quando a linha é materialmente
    /// virgem. Qualquer evidência de tentativa/claim/HTTP/documento/request/response/erro/reconciliação mantém
    /// o bloqueio fail-closed. correlation_id NÃO é evidência de execução: o early binding já o materializa
    /// antes da primeira tentativa.
    /// </summary>
    internal static ResultadoBloqueioPipeline045 AvaliarBloqueioPipeline045(IReadOnlyList<Linha045> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);

        Linha045? bloqueante = linhas.FirstOrDefault(LinhaPipeline045BloqueiaEnvio);
        if (bloqueante is null)
        {
            return ResultadoBloqueioPipeline045.Liberado;
        }

        string etapa = PrimeiroTexto(bloqueante, "etapa_sap", "etapa", "codigo_etapa", "tipo_etapa") ?? "?";
        string status = PrimeiroTexto(bloqueante, "status_etapa", "status", "status_movimento", "status_pipeline") ?? "?";
        return ResultadoBloqueioPipeline045.CriarBloqueio(
            $"Envio interrompido em estado que exige reconciliação. Etapa {etapa} está em {status}. Não tente enviar novamente.");
    }

    private static bool LinhaPipeline045BloqueiaEnvio(Linha045 linha)
    {
        string? status = PrimeiroTexto(linha, "status_etapa", "status", "status_movimento", "status_pipeline");
        if (string.IsNullOrWhiteSpace(status))
        {
            return true; // estado ilegível/incompatível => fail-closed
        }

        string normalizado = status.Trim().ToUpperInvariant();

        if (normalizado is "CONFIRMADO" or "CONFIRMADO_SAP" or "CONCLUIDO" or "CONCLUÍDO" or "CANCELADO" or "CANCELADA")
        {
            return false;
        }

        if (normalizado == "PENDENTE")
        {
            return !LinhaPipeline045PendenteVirgem(linha);
        }

        // PROCESSANDO / ERRO / INDETERMINADO / TIMEOUT / estado desconhecido:
        // preserva o comportamento anterior de bloqueio e a proteção contra retry cego.
        return true;
    }

    private static bool LinhaPipeline045PendenteVirgem(Linha045 linha)
    {
        int tentativa = linha.ObterInt("numero_tentativa") ?? 0;
        if (tentativa != 0)
        {
            return false;
        }

        if (TemValorPipeline045(
            linha,
            "lease_obtido_em",
            "lease_expira_em",
            "recovery_lease_obtido_em",
            "recovery_lease_expira_em",
            "material_document",
            "material_document_year",
            "http_status",
            "iniciado_em",
            "confirmado_em",
            "falhou_em",
            "indeterminado_em",
            "request_sanitizado",
            "response_sanitizado",
            "erro_sanitizado",
            "reconciliado_em"))
        {
            return false;
        }

        string? statusReconciliacao = PrimeiroTexto(linha, "status_reconciliacao");
        if (!string.IsNullOrWhiteSpace(statusReconciliacao)
            && !string.Equals(statusReconciliacao.Trim(), "NAO_NECESSARIA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? podeReprocessarTexto = PrimeiroTexto(linha, "pode_reprocessar");
        if (bool.TryParse(podeReprocessarTexto, out bool podeReprocessar) && podeReprocessar)
        {
            return false;
        }

        return true;
    }

    private static bool TemValorPipeline045(Linha045 linha, params string[] colunas)
    {
        foreach (string coluna in colunas)
        {
            object? valor = linha.Bruto(coluna);
            if (valor is null || valor is DBNull)
            {
                continue;
            }

            if (valor is string texto && string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static string? PrimeiroTexto(Linha045 linha, params string[] colunas)
    {
        foreach (string coluna in colunas)
        {
            string? valor = linha.ObterTexto(coluna);
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return null;
    }
    public async Task<ResultadoPaleteInt012> EnviarPaleteInt012Async(
        ProdutoAcabadoPalete palete,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(palete);
        cancellationToken.ThrowIfCancellationRequested();

        ResultadoPreviewProdutoAcabadoPalete preview = GerarPreviewPalete(palete);
        if (!preview.Sucesso)
        {
            return ResultadoPaleteInt012.NaoEnviado(preview.Mensagem);
        }

        // GATE 047-CH: contrato MVP CONGELADO — só PackagingMaterialMvp.Permitido (PALLET01) é homologado no SAP.
        // Material diferente/vazio/NULL (ex.: PALLET02/03/05 inexistentes no A_Product) ⇒ BLOQUEIA LOCALMENTE ANTES
        // de qualquer claim/tentativa/POST_FORMACAO_INICIADO/CPI/SAP. Evita HTTP 500 e HUs filhas sem parent.
        if (!PackagingMaterialMvp.Autorizado(palete.PackagingMaterial))
        {
            return ResultadoPaleteInt012.NaoEnviado("Material de embalagem não autorizado para integração SAP.");
        }

        // GATE 047-Z / 047-N (Ares): guard TEMPORÁRIO de HOMOGENEIDADE (exigia 1 OP + 1 material + 1 lote) REMOVIDO.
        // Contrato redefinido: MESMA_OP_OBRIGATORIA=NAO, MESMO_MATERIAL_UNIVERSAL=NAO, MODO_MANUAL_MULTIPLOS_PRODUTOS=PERMITIDO,
        // sem guard de lote inventado. Os guards REAIS de integridade (HU individual, status elegível, não-duplicidade,
        // write gate, claim, tentativa, idempotência, fail-closed) permanecem no preview/orquestrador/gateway.
        if (!PaleteInt012EnvioAutorizado)
        {
            return ResultadoPaleteInt012.NaoEnviado(
                "Envio INT012 bloqueado: gate/base/allowlist CPI não autorizados.");
        }

        if (_paleteInt012Orquestrador is null)
        {
            return ResultadoPaleteInt012.NaoEnviado(
                "Envio INT012 bloqueado: DEPENDENCIA_GAIA persistência/auditoria do palete indisponível. Nenhum POST executado.");
        }

        IReadOnlyList<long> codigosCaixas = palete.Caixas
            .Where(caixa => caixa.CodigoProdutoAcabadoCaixa.HasValue)
            .Select(caixa => caixa.CodigoProdutoAcabadoCaixa!.Value)
            .ToArray();
        // GATE 046-AQ-C1: o envio reutiliza a identidade persistida do palete (codigo_hu_palete), evitando que o
        // orquestrador recrie/revincule um palete que já existe (o que violava constraint e lançava exceção antes do claim).
        ResultadoPalete045 resultado = await _paleteInt012Orquestrador.ExecutarAsync(
            codigosCaixas,
            preview.Payload!,
            EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario ?? 0,
            EstadoTerminalLocalAtual.Contexto.NomeTerminal,
            palete.CodigoHuPalete,
            cancellationToken);

        return resultado.Executou
            ? new ResultadoPaleteInt012
            {
                Estado = resultado.Estado,
                UcGerada = resultado.UcGerada,
                HttpStatus = null,
                MensagemSanitizada = resultado.Mensagem
            }
            : ResultadoPaleteInt012.NaoEnviado(resultado.Mensagem);
    }
    // GATE 047-Z / 047-N: ValidarHomogeneidadePaleteInt012 + PossuiUmValorDistintoObrigatorio REMOVIDOS
    // (guard temporário de mesma OP/material/lote superseded; proposta Gaia 047-M CANCELADA/NÃO INSTALADA).
    /// <summary>
    /// REV4-Â§3/Â§6: executa o pipeline 261â†’101â†’HU para UMA caixa. Fail-closed: pipeline indisponÃ­vel â‡’ Bloqueada
    /// (nenhum POST); commands incompletos (DEPENDENCIA_ARES/GAIA) â‡’ Bloqueada, zero HTTP. Nunca envia HU isolada.
    /// </summary>
    public async Task<ResultadoPipelineProdutoAcabado> EnviarCaixaPipelineAsync(
        ProdutoAcabadoPipelineOrigem origem, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origem);

        if (!_composicaoPipeline.Disponivel || _composicaoPipeline.Orquestrador is null)
        {
            return new ResultadoPipelineProdutoAcabado(
                false, EtapaPipelineProdutoAcabado.Bloqueada, $"Pipeline indisponÃ­vel: {_composicaoPipeline.Motivo}.", null);
        }

        ResultadoComandosPipeline comandos = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        if (!comandos.Sucesso || comandos.Comando261 is null || comandos.Comando101 is null)
        {
            return new ResultadoPipelineProdutoAcabado(
                false, EtapaPipelineProdutoAcabado.Bloqueada, comandos.Mensagem, null);
        }

        return await _composicaoPipeline.Orquestrador.ExecutarAsync(
            origem.CodigoCaixa, comandos.Comando261, comandos.Comando101, usuario, terminal, cancellationToken);
    }

    /// <summary>
    /// GATE 118B (§5): releitura FRESCA da OP no SAP (STEP 1 item, STEP 2 componentes) + validacao
    /// (§5 STEP 3) + calculo do alocador cumulativo (STEP 4). NAO envia nada: devolve os DELTAS por
    /// componente para que o chamador monte a origem do pipeline (STEP 5). Nenhum snapshot atomico e
    /// alegado; a consistencia cruzada entre as duas leituras e verificada pelo alocador.
    /// Qualquer indisponibilidade/indeterminacao => Bloqueado com ZERO itens (nenhum POST possivel).
    /// </summary>
    public async Task<Resultado261Cumulativo> CalcularAlocacao261FrescaAsync(
        ProdutoAcabadoCaixa caixa,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caixa);

        string numeroOrdem = caixa.NumeroOrdemProducao?.Trim() ?? string.Empty;
        if (numeroOrdem.Length == 0)
        {
            return Resultado261Cumulativo.Bloqueado(
                "Caixa sem OP: alocacao 261 bloqueada (nenhum POST).");
        }

        LeituraFrescaOrdem261 fresca =
            await _productionOrderSapServico.ConsultarEstadoFresco261Async(numeroOrdem, cancellationToken);

        if (!fresca.Disponivel || fresca.Item is null)
        {
            return Resultado261Cumulativo.Bloqueado(
                string.IsNullOrWhiteSpace(fresca.Mensagem)
                    ? "Releitura fresca da OP indisponivel: alocacao 261 bloqueada (nenhum POST)."
                    : fresca.Mensagem);
        }

        // GATE 124E: acumulado REAL ja consumido, para o componente KG/PP05 — soma do peso liquido das
        // caixas que JA fecharam o pipeline (CONFIRMADA_SAP), excluindo a corrente e as canceladas.
        // GATE 124H: a leitura passou a ser a FOCAL SEM TERMINAL. O WithdrawnQuantity do SAP e
        // cumulativo por componente (Reservation+ReservationItem) e NAO conhece terminal; o ledger
        // anterior, particionado por terminal, comparava escopos diferentes e podia SUBCONSUMIR em
        // silencio quando a mesma OP fosse produzida em mais de um terminal.
        // Falha de leitura => null => o alocador BLOQUEIA o componente elegivel (nunca cai para
        // teorico, nunca estima historico).
        decimal? pesoConfirmadoAnterior = null;
        try
        {
            IReadOnlyList<ProdutoAcabadoCaixa> caixasContexto =
                await _huService.ListarConfirmadasPorContextoSemTerminalAsync(
                    caixa.NumeroOrdemProducao, caixa.ItemOrdemProducao, caixa.Material, caixa.Lote,
                    cancellationToken);

            pesoConfirmadoAnterior = ProdutoAcabado261AllocatorCumulativo
                .SomarPesoLiquidoConfirmadoAnterior(caixasContexto, caixa.CodigoProdutoAcabadoCaixa);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Acumulado real NAO comprovado: permanece null e o alocador bloqueia o KG/PP05.
            pesoConfirmadoAnterior = null;
        }

        // GATE 118B-R4: a montagem da entrada e o SEAM UNICO compartilhado com os testes de wiring.
        // NumeroOrdemItemFresco sai de fresca.Item.NumeroOrdem (OP retornada pelo SAP) e NAO de
        // fresca.NumeroOrdemConsultada (eco da request) — eco tornava a checagem tautologica.
        Entrada261Cumulativa entrada = ProdutoAcabado261AllocatorCumulativo.MontarEntrada(
            fresca, caixa, numeroOrdem, pesoConfirmadoAnterior);

        return ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);
    }

    public bool SapSimulado => _productionOrderSapServico.EhSimulado;

    public async Task<ResultadoConsultaProdutoAcabado> ConsultarOrdemProducaoAsync(
        string numeroOp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroOp))
        {
            return ResultadoConsultaProdutoAcabado.Falha("Informe a OP para consulta.");
        }

        ResultadoConsultaOrdemProducaoSap resultadoSap =
            await _productionOrderSapServico.ConsultarOrdemAsync(numeroOp.Trim(), cancellationToken);
        if (resultadoSap.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || resultadoSap.Ordem is null)
        {
            string mensagem = string.IsNullOrWhiteSpace(resultadoSap.MensagemSanitizada)
                ? "NÃ£o foi possÃ­vel consultar a OP no SAP."
                : resultadoSap.MensagemSanitizada;
            return ResultadoConsultaProdutoAcabado.Falha(mensagem);
        }

        OrdemProducaoSap ordemSap = resultadoSap.Ordem;
        if (!ordemSap.Liberada)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas ainda nÃ£o estÃ¡ liberada para produto acabado.");
        }

        if (ordemSap.Confirmada || ordemSap.Excluida)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas estÃ¡ encerrada, confirmada ou marcada para exclusÃ£o.");
        }

        ProdutoAcabadoOrdem ordem = MapearOrdem(ordemSap);

        // Defesa 1 (Hera): Produto Acabado HU sÃ³ para OPs PET do centro 3007. Sem gate por OP; nenhuma escrita.
        if (!RegraCentroPetProdutoAcabado.CentroPermitido(ordem.Centro))
        {
            return ResultadoConsultaProdutoAcabado.Falha(RegraCentroPetProdutoAcabado.MensagemCentroNaoPermitido);
        }

        if (string.IsNullOrWhiteSpace(ordem.MaterialProduzido))
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas sem item produzido para produto acabado.");
        }

        if (ordem.QuantidadePendente <= 0m)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP sem saldo pendente para entrada de produto acabado.");
        }

        ProdutoAcabadoNormaEmbalagem norma = await ConsultarNormaParaTelaAsync(ordem.MaterialProduzido, cancellationToken);
        return ResultadoConsultaProdutoAcabado.Ok(ordem, norma, resultadoSap.MensagemSanitizada);
    }

    /// <summary>
    /// Consulta REAL (somente leitura) da norma de embalagem pelo material acabado (contrato INT012) +
    /// interpretaÃ§Ã£o defensiva (Â§5/Â§6). NÃƒO executa POST. SÃ³ Ã© bem-sucedida no cenÃ¡rio Encontrada com
    /// interpretaÃ§Ã£o vÃ¡lida; qualquer outro cenÃ¡rio â‡’ Sucesso=false com o motivo sanitizado (nunca fallback).
    /// </summary>
    public async Task<ResultadoNormaEmbalagemProdutoAcabado> ConsultarNormaEmbalagemAsync(
        string material,
        CancellationToken cancellationToken = default)
    {
        ResultadoConsultaNormaEmbalagemSap consulta =
            await _normaEmbalagemSapServico.ObterNormaAsync(material, cancellationToken);

        if (consulta.Cenario != CenarioConsultaNormaEmbalagem.Encontrada || consulta.Norma is null)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(
                (material ?? string.Empty).Trim(), consulta.MensagemSanitizada);
        }

        return InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(material ?? string.Empty, consulta.Norma);
    }

    private async Task<ProdutoAcabadoNormaEmbalagem> ConsultarNormaParaTelaAsync(
        string material,
        CancellationToken cancellationToken)
        => MapearNormaParaTela(
            material,
            await _normaEmbalagemSapServico.ObterNormaAsync(material, cancellationToken));

    /// <summary>RÃ³tulo de STATUS NORMA por cenÃ¡rio (Â§9). Nunca "SEM NORMA CADASTRADA" para todo erro.</summary>
    internal static string RotuloStatusNorma(CenarioConsultaNormaEmbalagem cenario)
        => cenario switch
        {
            CenarioConsultaNormaEmbalagem.Encontrada => "CONSULTADA SAP",
            CenarioConsultaNormaEmbalagem.NaoConfigurada => "NAO CONFIGURADA",
            CenarioConsultaNormaEmbalagem.CredencialAusente => "NAO CONFIGURADA",
            CenarioConsultaNormaEmbalagem.HostNaoPermitido => "NAO CONFIGURADA",
            CenarioConsultaNormaEmbalagem.NaoEncontrada => "SEM NORMA CADASTRADA",
            CenarioConsultaNormaEmbalagem.ErroAutenticacao => "ERRO DE AUTENTICACAO",
            CenarioConsultaNormaEmbalagem.ErroAutorizacao => "ACESSO NAO AUTORIZADO",
            _ => "ERRO NA CONSULTA"
        };

    /// <summary>
    /// Mapeia o resultado TIPADO da consulta (cenÃ¡rio) para o modelo da Tela (Â§9). Encontrada + interpretaÃ§Ã£o
    /// vÃ¡lida â‡’ campos reais + itens (P/I ou M) + NormaValida=true. Encontrada mas interpretaÃ§Ã£o bloqueia â‡’
    /// "SEM NORMA CADASTRADA". Demais cenÃ¡rios â‡’ rÃ³tulo prÃ³prio (NAO CONFIGURADA / ERRO DE AUTENTICACAO /
    /// ACESSO NAO AUTORIZADO / ERRO NA CONSULTA), NormaValida=false, sem material inventado.
    /// </summary>
    internal static ProdutoAcabadoNormaEmbalagem MapearNormaParaTela(
        string material,
        ResultadoConsultaNormaEmbalagemSap consulta)
    {
        string materialConsultado = (material ?? string.Empty).Trim();

        if (consulta.Cenario == CenarioConsultaNormaEmbalagem.Encontrada && consulta.Norma is not null)
        {
            ResultadoNormaEmbalagemProdutoAcabado interpretacao =
                InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(materialConsultado, consulta.Norma);

            if (interpretacao.Sucesso && interpretacao.Norma is not null)
            {
                return MapearNormaValida(interpretacao.Norma, consulta);
            }

            // API respondeu, porÃ©m a estrutura nÃ£o Ã© utilizÃ¡vel â‡’ sem cadastro vÃ¡lido.
            return new ProdutoAcabadoNormaEmbalagem
            {
                Material = materialConsultado,
                MaterialCaixa = string.Empty,
                QuantidadeProdutosPorCaixa = 0,
                Status = "SEM NORMA CADASTRADA",
                NormaValida = false,
                DiagnosticoSanitizado = interpretacao.Mensagem
            };
        }

        return new ProdutoAcabadoNormaEmbalagem
        {
            Material = materialConsultado,
            MaterialCaixa = string.Empty,
            QuantidadeProdutosPorCaixa = 0,
            Status = RotuloStatusNorma(consulta.Cenario),
            NormaValida = false,
            DiagnosticoSanitizado = consulta.MensagemSanitizada
        };
    }

    private static ProdutoAcabadoNormaEmbalagem MapearNormaValida(
        NormaEmbalagemProdutoAcabado n,
        ResultadoConsultaNormaEmbalagemSap consulta)
        => new()
        {
            Material = n.MaterialProduto,
            PackagingInstruction = n.CodigoNorma,
            MaterialCaixa = n.MaterialCaixa,
            QuantidadeProdutosPorCaixa = (int)Math.Round(n.QuantidadePorCaixa, MidpointRounding.AwayFromZero),
            Unidade = n.UnidadeQuantidade,
            Status = RotuloStatusNorma(CenarioConsultaNormaEmbalagem.Encontrada),
            NormaValida = true,
            DiagnosticoSanitizado = consulta.MensagemSanitizada,
            Itens =
            [
                new ProdutoAcabadoNormaItem
                {
                    Material = n.MaterialCaixa, TipoMaterial = "P",
                    Quantidade = n.QuantidadeEmbalagem, Unidade = n.UnidadeEmbalagem, Item = "P"
                },
                new ProdutoAcabadoNormaItem
                {
                    Material = n.MaterialProduto, TipoMaterial = "I",
                    Quantidade = n.QuantidadePorCaixa, Unidade = n.UnidadeQuantidade, Item = "I"
                }
            ]
        };

    public ProdutoAcabadoCaixa MontarCaixa(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        int numeroCaixa,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        string terminal = "",
        long? codigoUsuario = null,
        long? codigoBalanca = null)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(norma);

        if (norma.QuantidadeProdutosPorCaixa <= 0)
        {
            throw new InvalidOperationException("Quantidade por caixa deve ser maior que zero.");
        }

        decimal pesoLiquidoKg = pesoBrutoKg - taraKg;
        if (pesoLiquidoKg <= 0m)
        {
            throw new InvalidOperationException("Peso lÃ­quido da caixa deve ser maior que zero.");
        }

        // Â§16: material de embalagem da CAIXA â€” nunca PALLET01 (material de palete). Vem da norma
        // (MaterialCaixa) ou de um valor controlado informado; a origem Ã© sempre explÃ­cita.
        string embalagem = !string.IsNullOrWhiteSpace(materialEmbalagem)
            ? materialEmbalagem.Trim()
            : (norma.MaterialCaixa ?? string.Empty).Trim();

        return new ProdutoAcabadoCaixa
        {
            NumeroCaixa = numeroCaixa,
            CodigoCaixaLocal = $"CX-{ordem.NumeroOrdem}-{numeroCaixa:0000}",
            NumeroOrdemProducao = ordem.NumeroOrdem.Trim(),
            ItemOrdemProducao = ordem.ItemOrdem.Trim(),
            Material = ordem.MaterialProduzido.Trim(),
            Lote = ordem.Lote.Trim(),
            Centro = ordem.Centro.Trim(),
            Deposito = ordem.DepositoDestino.Trim(),
            MaterialEmbalagem = embalagem,
            OrigemMaterialEmbalagem = origemMaterialEmbalagem,
            PesoBrutoKg = pesoBrutoKg,
            TaraKg = taraKg,
            PesoLiquidoKg = pesoLiquidoKg,
            UnidadePeso = "KG",
            QuantidadeProdutos = norma.QuantidadeProdutosPorCaixa,
            UnidadeQuantidade = (norma.Unidade ?? string.Empty).Trim().ToUpperInvariant(),
            OrigemPesagem = origemPesagem,
            CodigoBalanca = string.Equals(origemPesagem, "BALANCA", StringComparison.OrdinalIgnoreCase)
                ? codigoBalanca : null,
            CorrelationId = Guid.NewGuid(),
            StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal,
            Terminal = terminal ?? string.Empty,
            CodigoUsuario = codigoUsuario
        };
    }

    /// <summary>
    /// Monta uma caixa sem nÃºmero/cÃ³digo definitivos. A identidade sequencial sÃ³ pode ser atribuÃ­da
    /// atomicamente por IProdutoAcabadoRepositorio.RegistrarCaixaAsync.
    /// </summary>
    public ProdutoAcabadoCaixa MontarCaixaSemIdentidadeSequencial(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        string terminal = "",
        long? codigoUsuario = null,
        long? codigoBalanca = null)
    {
        ProdutoAcabadoCaixa caixa = MontarCaixa(
            ordem,
            norma,
            0,
            pesoBrutoKg,
            taraKg,
            origemPesagem,
            materialEmbalagem,
            origemMaterialEmbalagem,
            terminal,
            codigoUsuario,
            codigoBalanca);
        caixa.CodigoCaixaLocal = string.Empty;
        return caixa;
    }

    public ProdutoAcabadoPalete MontarPalete(
        ProdutoAcabadoOrdem ordem,
        IReadOnlyList<ProdutoAcabadoCaixa> caixas,
        IReadOnlyList<ProdutoAcabadoPalete> paletesExistentes,
        int primeiraCaixa,
        int ultimaCaixa,
        string packagingMaterial)
    {
        ArgumentNullException.ThrowIfNull(paletesExistentes);
        if (primeiraCaixa > ultimaCaixa)
        {
            throw new InvalidOperationException("Intervalo de caixas invÃ¡lido.");
        }

        ProdutoAcabadoCaixa[] selecionadas = caixas
            .Where(caixa => caixa.NumeroCaixa >= primeiraCaixa && caixa.NumeroCaixa <= ultimaCaixa)
            .OrderBy(caixa => caixa.NumeroCaixa)
            .ToArray();
        if (selecionadas.Length != (ultimaCaixa - primeiraCaixa + 1))
        {
            throw new InvalidOperationException("Todas as caixas do intervalo precisam existir.");
        }

        // Â§9/Â§10: regras LOCAIS centralizadas no validador ÃšNICO (CONFIRMADA_SAP + HU obrigatÃ³ria, sem
        // duplicidade/NumeroCaixa duplicado/jÃ¡-paletizada/OP incompatÃ­vel, agregaÃ§Ã£o determinÃ­stica). Evita
        // duas implementaÃ§Ãµes divergentes. Tentativa invÃ¡lida â‡’ exceÃ§Ã£o ANTES de qualquer mutaÃ§Ã£o.
        // REV4-Â§13: os paletes JÃ montados sÃ£o passados de verdade (nÃ£o mais lista vazia) â€” caixa presente em
        // palete existente â‡’ novo palete BLOQUEADO (sem mutar CodigoPaleteLocal).
        Servicos.Operacao.ResultadoPaleteLocal validacao =
            Servicos.Operacao.ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar(ordem.NumeroOrdem, selecionadas, paletesExistentes);
        if (!validacao.Sucesso)
        {
            throw new InvalidOperationException(validacao.Mensagem);
        }

        string codigoPalete = $"PLT-{ordem.NumeroOrdem}-{primeiraCaixa:0000}-{ultimaCaixa:0000}";
        ProdutoAcabadoPalete palete = new()
        {
            CodigoPaleteLocal = codigoPalete,
            PrimeiraCaixa = primeiraCaixa,
            UltimaCaixa = ultimaCaixa,
            PesoBrutoKg = validacao.PesoBrutoKg,
            PesoLiquidoKg = validacao.PesoLiquidoKg,
            TaraKg = validacao.TaraKg,
            Plant = ordem.Centro,
            StorageLocation = ordem.DepositoDestino,
            PackagingMaterial = packagingMaterial,
            Caixas = selecionadas
        };

        return palete;
    }

    /// <summary>
    /// INC-047 (Paletização por HU): monta um palete LOCAL a partir de uma SELEÇÃO arbitrária de caixas (não por
    /// intervalo de numero_caixa). REUTILIZA o validador ÚNICO (ProdutoAcabadoPaleteLocalValidador) — mesmas regras
    /// (CONFIRMADA_SAP, HU obrigatória, sem duplicidade/já-paletizada, mesma OP sob auditoria Gaia, agregação
    /// determinística). NÃO duplica regra na Form. Tentativa inválida ⇒ exceção antes de qualquer persistência.
    /// </summary>
    public ProdutoAcabadoPalete MontarPaletePorSelecao(
        IReadOnlyList<ProdutoAcabadoCaixa> caixasSelecionadas,
        IReadOnlyList<ProdutoAcabadoPalete> paletesExistentes,
        string packagingMaterial)
    {
        ArgumentNullException.ThrowIfNull(caixasSelecionadas);
        ArgumentNullException.ThrowIfNull(paletesExistentes);
        if (caixasSelecionadas.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma caixa selecionada para o palete.");
        }

        ProdutoAcabadoCaixa[] selecionadas = caixasSelecionadas.OrderBy(c => c.NumeroCaixa).ToArray();
        string numeroOrdem = (selecionadas[0].NumeroOrdemProducao ?? string.Empty).Trim();
        Servicos.Operacao.ResultadoPaleteLocal validacao = Servicos.Operacao.ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar(
            numeroOrdem,
            selecionadas,
            paletesExistentes,
            exigirMesmaOp: false);
        if (!validacao.Sucesso)
        {
            throw new InvalidOperationException(validacao.Mensagem);
        }

        int primeira = selecionadas.Min(c => c.NumeroCaixa);
        int ultima = selecionadas.Max(c => c.NumeroCaixa);
        bool opUnica = selecionadas
            .Select(c => (c.NumeroOrdemProducao ?? string.Empty).Trim())
            .Where(op => op.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Count() == 1;
        string codigoContexto = opUnica && numeroOrdem.Length > 0 ? numeroOrdem : "MULTIOP";
        return new ProdutoAcabadoPalete
        {
            CodigoPaleteLocal = $"PLT-{codigoContexto}-{primeira:0000}-{ultima:0000}",
            PrimeiraCaixa = primeira,
            UltimaCaixa = ultima,
            PesoBrutoKg = validacao.PesoBrutoKg,
            PesoLiquidoKg = validacao.PesoLiquidoKg,
            TaraKg = validacao.TaraKg,
            Plant = selecionadas[0].Centro,
            StorageLocation = selecionadas[0].Deposito,
            PackagingMaterial = packagingMaterial,
            Caixas = selecionadas
        };
    }

    public async Task<ResultadoPaletePersistenciaLocal> CriarPaleteLocalPersistenteAsync(
        ProdutoAcabadoPalete palete,
        long? codigoUsuario,
        string terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(palete);
        if (codigoUsuario is not long usuario || usuario <= 0)
        {
            return ResultadoPaletePersistenciaLocal.Bloqueado("Usuário não identificado para criar o palete local.");
        }

        if (_paleteLocalPersistenteOrquestrador is null)
        {
            return ResultadoPaletePersistenciaLocal.Bloqueado(
                "Persistência local de palete indisponível neste ambiente. Nenhum palete foi criado.");
        }

        ResultadoPaletePersistenciaLocal resultado = await _paleteLocalPersistenteOrquestrador.CriarAsync(
            palete,
            usuario,
            terminal,
            cancellationToken);

        if (!resultado.Sucesso || resultado.CodigoHuPalete is not long codigoHuPalete)
        {
            return resultado;
        }

        palete.CodigoHuPalete = codigoHuPalete;
        palete.StatusSap = "RASCUNHO";
        ConfirmarVinculoPaleteLocal(palete);
        return resultado;
    }

    /// <summary>
    /// GATE 046-E §7/§10/§12: BANCO = fonte autoritativa. Recarrega os paletes locais persistidos da OP (e do
    /// terminal, quando informado) reconstruindo palete + caixas a partir do banco. Fail-closed: persistência
    /// indisponível ⇒ lista vazia (nunca inventa palete em memória). Zero SAP/INT012/claim.
    /// </summary>
    public async Task<IReadOnlyList<ProdutoAcabadoPalete>> RecarregarPaletesLocaisAsync(
        string numeroOrdem, string? terminal, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroOrdem)
            || _pipeline045Operacoes is not { SuportaPersistenciaDefinitiva: true } ops)
        {
            return [];
        }

        IReadOnlyList<ProdutoAcabadoPalete> paletes = await ops.LerPaletesLocaisPorOrdemAsync(
            numeroOrdem.Trim(), terminal, cancellationToken);

        // Coerência local: marca as caixas reconstruídas como já paletizadas (status RASCUNHO vindo do banco).
        foreach (ProdutoAcabadoPalete palete in paletes)
        {
            foreach (ProdutoAcabadoCaixa caixa in palete.Caixas)
            {
                caixa.CodigoPaleteLocal = palete.CodigoPaleteLocal;
            }
        }

        return paletes;
    }

    /// <summary>
    /// GATE 047-AB: recarrega os paletes locais persistidos do TERMINAL (sem OP), para popular PALETES FORMADOS ao
    /// ABRIR a Paletização (incl. RASCUNHO). Fail-closed: persistência indisponível ⇒ lista vazia. Zero SAP/INT012/claim.
    /// </summary>
    public async Task<IReadOnlyList<ProdutoAcabadoPalete>> RecarregarPaletesLocaisPorTerminalAsync(
        string? terminal, CancellationToken cancellationToken = default)
    {
        if (_pipeline045Operacoes is not { SuportaPersistenciaDefinitiva: true } ops)
        {
            return [];
        }

        IReadOnlyList<ProdutoAcabadoPalete> paletes = await ops.LerPaletesLocaisPorTerminalAsync(terminal, cancellationToken);
        foreach (ProdutoAcabadoPalete palete in paletes)
        {
            foreach (ProdutoAcabadoCaixa caixa in palete.Caixas)
            {
                caixa.CodigoPaleteLocal = palete.CodigoPaleteLocal;
            }
        }

        return paletes;
    }
    public void ConfirmarVinculoPaleteLocal(ProdutoAcabadoPalete palete)
    {
        ArgumentNullException.ThrowIfNull(palete);
        if (string.IsNullOrWhiteSpace(palete.CodigoPaleteLocal))
        {
            throw new InvalidOperationException("Palete sem cÃ³digo local para confirmaÃ§Ã£o.");
        }

        foreach (ProdutoAcabadoCaixa caixa in palete.Caixas)
        {
            caixa.CodigoPaleteLocal = palete.CodigoPaleteLocal;
        }
    }

    // ============================ Caixa individual (Handling Unit) ============================

    /// <summary>Gateway autorizado a executar o POST real (fail-closed por padrÃ£o nesta frente).</summary>
    public bool EnvioHuAutorizado => _huService.EnvioAutorizado;

    /// <summary>
    /// Registra e finaliza UMA caixa (uma por vez por terminal): INSERT EM_PESAGEM + pesagem + finalizar
    /// local + preview (request tipado) + AGUARDANDO_AUTORIZACAO_SAP â€” tudo persistido via Service/Repository
    /// (banco = fonte da verdade). Exige usuÃ¡rio. NÃƒO envia ao SAP e NÃƒO cria palete.
    /// </summary>
    public async Task<ResultadoFinalizacaoCaixa> FinalizarCaixaLocalAsync(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string terminal,
        long? codigoUsuario = null,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        CancellationToken cancellationToken = default,
        long? codigoBalanca = null,
        Action<Servicos.Diagnostico.EtapaRegistroCaixa120E>? diagnostico = null)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(norma);
        if (codigoUsuario is not long usuario)
        {
            return ResultadoFinalizacaoCaixa.Bloqueada("UsuÃ¡rio nÃ£o identificado para registrar a caixa.", null);
        }

        diagnostico?.Invoke(Servicos.Diagnostico.EtapaRegistroCaixa120E.MONTAR_CAIXA);
        if (string.Equals(origemPesagem.Trim(), "BALANCA", StringComparison.OrdinalIgnoreCase)
            && codigoBalanca is not > 0)
        {
            throw new InvalidOperationException("Pesagem BALANCA exige codigo_balanca resolvido.");
        }

        ProdutoAcabadoCaixa caixa = MontarCaixaSemIdentidadeSequencial(
            ordem, norma, pesoBrutoKg, taraKg, origemPesagem,
            materialEmbalagem, origemMaterialEmbalagem, terminal, codigoUsuario, codigoBalanca);

        ResultadoFinalizacaoHu resultado = await _huService.RegistrarEFinalizarCaixaAsync(
            caixa, usuario, terminal, cancellationToken, diagnostico);

        return resultado.Sucesso && resultado.Caixa is not null
            ? ResultadoFinalizacaoCaixa.Ok(resultado.Caixa, resultado.RequestJson, EnvioHuAutorizado)
            : ResultadoFinalizacaoCaixa.Bloqueada(resultado.Mensagem, resultado.Caixa);
    }

    /// <summary>Autoriza o envio (AGUARDANDO_AUTORIZACAO_SAP â†’ PRONTA_PARA_ENVIO) via funÃ§Ã£o de banco.</summary>
    public Task<bool> AutorizarEnvioCaixaAsync(long codigoProdutoAcabadoCaixa, long usuario, string terminal, CancellationToken cancellationToken = default)
        => _huService.AutorizarEnvioAsync(codigoProdutoAcabadoCaixa, usuario, terminal, cancellationToken);

    /// <summary>ProntidÃ£o do envio manual: sÃ³ quando o gateway estÃ¡ autorizado e a caixa estÃ¡ PRONTA_PARA_ENVIO.</summary>
    public async Task<ResultadoDiagnosticoEnvioCaixa> DiagnosticarProntidaoEnvioCaixaAsync(
        long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
    {
        ProdutoAcabadoCaixa? caixa = await _huService.ObterPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        if (caixa is null)
        {
            return new ResultadoDiagnosticoEnvioCaixa(false, "Caixa nÃ£o encontrada.", null);
        }

        if (!EnvioHuAutorizado)
        {
            return new ResultadoDiagnosticoEnvioCaixa(false,
                "Envio ao SAP nÃ£o autorizado nesta frente (gateway fail-closed).", caixa.StatusIntegracao);
        }

        bool pronta = caixa.StatusIntegracao == StatusIntegracaoCaixa.ProntaParaEnvio;
        return new ResultadoDiagnosticoEnvioCaixa(
            pronta, pronta ? "Caixa apta para envio manual." : $"Caixa em {MapeadorStatusHuCaixa.ParaTextoBanco(caixa.StatusIntegracao)}.",
            caixa.StatusIntegracao);
    }

    /// <summary>
    /// Envio manual da caixa Ã  HU SAP (UMA tentativa): fail-closed sem claim/POST; caso contrÃ¡rio claim
    /// atÃ´mico â†’ usa sÃ³ o snapshot â†’ POST â†’ classifica sucesso/erro/timeout. Delegado ao Service.
    /// </summary>
    public async Task<ResultadoEnvioCaixaHu> EnviarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        ResultadoEnvioHu resultado = await _huService.EnviarAsync(codigoProdutoAcabadoCaixa, usuario, terminal, cancellationToken);
        return resultado.Cenario switch
        {
            CenarioEnvioHu.Confirmado => ResultadoEnvioCaixaHu.Confirmado(resultado.HandlingUnitExternalId ?? string.Empty, resultado.Caixa!),
            CenarioEnvioHu.Timeout => ResultadoEnvioCaixaHu.Timeout(resultado.Mensagem, resultado.Caixa),
            CenarioEnvioHu.NaoAutorizado => ResultadoEnvioCaixaHu.NaoAutorizado(resultado.Mensagem, resultado.Caixa),
            CenarioEnvioHu.ClaimNaoObtido => ResultadoEnvioCaixaHu.Bloqueado(resultado.Mensagem, resultado.Caixa),
            // REV4-Â§6/Â§8/Â§9: SAP respondeu mas a persistÃªncia local nÃ£o pÃ´de ser comprovada â‡’ Falha (nunca
            // Sucesso/ReprocessÃ¡vel). A mensagem segura chega Ã  Form; o botÃ£o nÃ£o reabilita (estado nÃ£o elegÃ­vel).
            CenarioEnvioHu.ConfirmacaoLocalNaoComprovada => ResultadoEnvioCaixaHu.Falha(resultado.Mensagem, resultado.Caixa),
            _ => ResultadoEnvioCaixaHu.Falha(resultado.Mensagem, resultado.Caixa)
        };
    }

    /// <summary>
    /// ReconciliaÃ§Ã£o apÃ³s INDETERMINADO_TIMEOUT. Exige a HU SAP REALMENTE conhecida (nÃ£o o CodigoCaixaLocal).
    /// Em DEV nÃ£o hÃ¡ essa chave (contrato pendente), entÃ£o o padrÃ£o null resulta em Indeterminada com a
    /// incompatibilidade documentada â€” nunca fabrica a chave.
    /// </summary>
    public async Task<ResultadoEnvioCaixaHu> ReconciliarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, string? handlingUnitExternalIdConhecido = null, CancellationToken cancellationToken = default)
    {
        ResultadoReconciliacaoHu resultado = await _huService.ReconciliarAsync(
            codigoProdutoAcabadoCaixa, handlingUnitExternalIdConhecido, cancellationToken);
        return resultado.Cenario switch
        {
            CenarioReconciliacaoHu.Confirmada => ResultadoEnvioCaixaHu.Confirmado(resultado.HandlingUnitExternalId ?? string.Empty, resultado.Caixa!),
            CenarioReconciliacaoHu.NaoEncontrada => ResultadoEnvioCaixaHu.Timeout(resultado.Mensagem, resultado.Caixa),
            _ => ResultadoEnvioCaixaHu.Bloqueado(resultado.Mensagem, resultado.Caixa)
        };
    }

    /// <summary>Reprocessamento controlado: ERRO_SAP reprocessÃ¡vel â†’ PRONTA_PARA_ENVIO (via funÃ§Ã£o de banco).</summary>
    public async Task<ResultadoEnvioCaixaHu> ReprocessarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
    {
        bool liberado = await _huService.LiberarReprocessamentoAsync(codigoProdutoAcabadoCaixa, usuario, terminal, motivo, cancellationToken);
        ProdutoAcabadoCaixa? caixa = await _huService.ObterPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        return liberado
            ? ResultadoEnvioCaixaHu.ProntaParaReenvio(caixa!)
            : ResultadoEnvioCaixaHu.Bloqueado("Reprocessamento nÃ£o liberado (nÃ£o elegÃ­vel pelo contrato de banco).", caixa);
    }

    /// <summary>Cancelamento controlado da caixa (via funÃ§Ã£o de banco). A UI reflete o snapshot recarregado.</summary>
    public async Task<ResultadoEnvioCaixaHu> CancelarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
    {
        bool cancelada = await _huService.CancelarAsync(codigoProdutoAcabadoCaixa, usuario, terminal, motivo, cancellationToken);
        ProdutoAcabadoCaixa? caixa = await _huService.ObterPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        return cancelada
            ? ResultadoEnvioCaixaHu.Bloqueado("Caixa cancelada.", caixa)
            // INC-063: o banco devolve apenas true/false, entao a mensagem enumera as causas
            // possiveis em vez de afirmar uma. A de MOVIMENTO SAP CONFIRMADO vem primeiro porque
            // e a unica que exige acao fora do sistema: cancelar por cima de um 261 ja lancado
            // deixa consumo orfao no SAP e trava toda caixa seguinte da OP em reconciliacao.
            : ResultadoEnvioCaixaHu.Bloqueado(
                "Cancelamento não aplicado. Possíveis causas: a caixa tem movimento SAP confirmado "
                + "(261 ou 101) e exige ESTORNO no SAP antes de ser cancelada; já possui HU criada; "
                + "o estado não permite cancelamento; ou o terminal não confere.",
                caixa);
    }

    public Task<ProdutoAcabadoCaixa?> ObterCaixaAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        => _huService.ObterAtivaPorTerminalAsync(terminal, cancellationToken);

    public Task<ProdutoAcabadoCaixa?> ObterCaixaPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
        => _huService.ObterPorCodigoAsync(codigo, cancellationToken);

    /// <summary>Recupera (read-only) as caixas persistidas da OP+terminal para reconstruir a grid ao reabrir.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPersistidasAsync(
        string numeroOrdemProducao, string terminal, CancellationToken cancellationToken = default)
        => _huService.ListarCaixasPersistidasAsync(numeroOrdemProducao, terminal, cancellationToken);

    /// <summary>REV4-Â§12: recupera a grid por CONTEXTO completo (OP+item+material+lote+terminal), isolando contexto.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPersistidasPorContextoAsync(
        string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal,
        CancellationToken cancellationToken = default)
        => _huService.ListarCaixasPersistidasPorContextoAsync(numeroOrdemProducao, itemOrdemProducao, material, lote, terminal, cancellationToken);

    /// <summary>INC-047: caixas por HU externo (Paletização — seleção MANUAL). Read-only, sem SAP/POST.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPorHandlingUnitsAsync(
        IReadOnlyList<string> husExternais, CancellationToken cancellationToken = default)
        => _huService.ListarCaixasPorHandlingUnitsAsync(husExternais, cancellationToken);

    /// <summary>INC-047: caixas por intervalo de HU externo (Paletização — SEQUÊNCIA). Read-only, sem SAP/POST.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPorIntervaloHandlingUnitAsync(
        string huInicial, string huFinal, string? material, CancellationToken cancellationToken = default)
        => _huService.ListarCaixasPorIntervaloHandlingUnitAsync(huInicial, huFinal, material, cancellationToken);

    /// <summary>Request tipado da HU (puro, sem persistÃªncia) â€” valida a caixa para exibiÃ§Ã£o/diagnÃ³stico.</summary>
    public ResultadoRequestHandlingUnitCaixa GerarRequestHandlingUnitCaixa(ProdutoAcabadoCaixa caixa)
        => _requestBuilder.Montar(caixa);

    /// <summary>
    /// GATE 126V: resolve a tara FISICA do material de embalagem (configuracao operacional, por mandante +
    /// centro + material) ANTES de montar o DTO e entrega o resultado ao builder. Preview e POST consomem
    /// EXATAMENTE este mesmo payload (<see cref="EnviarPaleteInt012Async"/> chama este metodo).
    /// </summary>
    public ResultadoPreviewProdutoAcabadoPalete GerarPreviewPalete(ProdutoAcabadoPalete palete)
    {
        ArgumentNullException.ThrowIfNull(palete);
        return _paletePayloadBuilder.MontarPreview(palete, ResolverTaraFisicaEmbalagemPalete(palete));
    }

    /// <summary>
    /// GATE 126V: resolucao PURA da tara fisica a partir da configuracao ja carregada. Nenhuma leitura de
    /// hu_palete, nenhuma constante de peso no codigo. Ausencia/invalidez/ambiguidade ⇒ fail-closed.
    /// </summary>
    internal ResultadoTaraFisicaEmbalagem ResolverTaraFisicaEmbalagemPalete(ProdutoAcabadoPalete palete)
    {
        // GATE 126X: a identidade QAS110 vem da configuracao EFETIVAMENTE CARREGADA pelo runtime (perfil
        // local + destino SAP validado pelo contrato de startup). Nao confirmada ⇒ sem tara ⇒ sem payload.
        ResultadoIdentidadeQas110 identidade = ValidadorIdentidadeQas110.Resolver(_configuracaoSap);
        return ResolvedorTaraFisicaEmbalagemPalete.Resolver(
            _configuracaoSap.TarasFisicasEmbalagemPalete,
            identidade.Identidade,
            palete.Plant,
            palete.PackagingMaterial);
    }

    public Task<IReadOnlyList<TaraCadastro>> ListarTarasAtivasPorSetorAsync(
        long codigoSetor,
        CancellationToken cancellationToken = default)
        => _taraController.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    private static ProdutoAcabadoOrdem MapearOrdem(OrdemProducaoSap ordemSap)
    {
        ItemOrdemProducaoSap? item = ordemSap.Itens.FirstOrDefault();
        decimal planejada = item?.QuantidadePrevista ?? ordemSap.QuantidadePrevista;

        // GATE 118B: a producao JA recebida vem de MfgOrderItemGoodsReceiptQty em TRI-STATE.
        // O campo antigo (MfgOrderItemActualDeliveryQty) NAO existe no contrato real e por isso valia
        // sempre 0. Aqui o null e PRESERVADO ate a camada de exibicao; nenhuma decisao de envio usa
        // este valor — o alocador 261 le o tri-state direto da leitura FRESCA.
        decimal? recebida = item?.QuantidadeRecebidaSap;

        return new ProdutoAcabadoOrdem
        {
            NumeroOrdem = ordemSap.NumeroOrdem.Trim(),
            MaterialProduzido = PrimeiroTexto(item?.Material, ordemSap.MaterialProduzido),
            // Tarefa 21.6.4 (Ajuste 2): o SAP nÃ£o retorna descriÃ§Ã£o do material aqui â€” NÃƒO usar o cÃ³digo
            // como descriÃ§Ã£o (senÃ£o o card Produto Acabado duplica). Fica vazio atÃ© haver texto real.
            DescricaoMaterial = string.Empty,
            Centro = PrimeiroTexto(item?.Centro, ordemSap.Centro),
            DepositoDestino = PrimeiroTexto(item?.Deposito, ordemSap.Deposito),
            QuantidadePlanejada = planejada,
            QuantidadeRecebidaSap = recebida,
            // Pendente so e calculavel quando o recebido e CONHECIDO; indeterminado => null.
            QuantidadePendente = recebida is decimal r ? Math.Max(planejada - r, 0m) : null,
            Unidade = PrimeiroTexto(item?.Unidade, ordemSap.Unidade).ToUpperInvariant(),
            Lote = PrimeiroTexto(item?.Lote, ordemSap.Lote),
            ItemOrdem = item?.ItemOrdem?.Trim() ?? string.Empty,
            Operacao = ordemSap.Operacoes.FirstOrDefault()?.Operacao ?? string.Empty,
            StatusOrdem = ordemSap.Liberada ? "LIBERADA" : "NAO_LIBERADA",
            Liberada = ordemSap.Liberada,
            EncerradaOuDeletada = ordemSap.Confirmada || ordemSap.Excluida,
            Componentes = ordemSap.Componentes
                .Select(componente => new ProdutoAcabadoComponenteOrdem
                {
                    Material = componente.Material.Trim(),
                    Centro = PrimeiroTexto(componente.Centro, ordemSap.Centro),
                    Deposito = componente.Deposito.Trim(),
                    QuantidadeNecessaria = componente.QuantidadeNecessaria,
                    Unidade = componente.UnidadeBase.Trim().ToUpperInvariant(),
                    Reserva = componente.Reserva.Trim(),
                    ItemReserva = componente.ItemReserva.Trim(),
                    Lote = componente.Lote.Trim(),
                    TipoMovimento = componente.TipoMovimento.Trim()
                })
                .ToArray()
        };
    }

    private static string PrimeiroTexto(params string?[] valores)
        => valores.FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))?.Trim() ?? string.Empty;
}

public sealed class ResultadoConsultaProdutoAcabado
{
    private ResultadoConsultaProdutoAcabado(bool sucesso, string mensagem, ProdutoAcabadoOrdem? ordem, ProdutoAcabadoNormaEmbalagem? norma)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Ordem = ordem;
        NormaEmbalagem = norma;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public ProdutoAcabadoOrdem? Ordem { get; }
    public ProdutoAcabadoNormaEmbalagem? NormaEmbalagem { get; }

    public static ResultadoConsultaProdutoAcabado Ok(ProdutoAcabadoOrdem ordem, ProdutoAcabadoNormaEmbalagem norma, string mensagem)
        => new(true, string.IsNullOrWhiteSpace(mensagem) ? "OP consultada para produto acabado." : mensagem, ordem, norma);

    public static ResultadoConsultaProdutoAcabado Falha(string mensagem)
        => new(false, mensagem, null, null);
}

public enum CenarioFinalizacaoCaixa { Ok, Bloqueada }

/// <summary>Resultado do registro+finalizaÃ§Ã£o persistente da caixa (uma por vez), com o request JSON gerado.</summary>
public sealed record ResultadoFinalizacaoCaixa(
    CenarioFinalizacaoCaixa Cenario,
    string Mensagem,
    ProdutoAcabadoCaixa? Caixa,
    string RequestJson,
    bool PodeEnviar)
{
    public bool Sucesso => Cenario == CenarioFinalizacaoCaixa.Ok;

    public static ResultadoFinalizacaoCaixa Ok(ProdutoAcabadoCaixa caixa, string requestJson, bool podeEnviar)
        => new(CenarioFinalizacaoCaixa.Ok,
            "Caixa registrada e finalizada; aguardando autorizaÃ§Ã£o de envio ao SAP.",
            caixa, requestJson, podeEnviar);

    public static ResultadoFinalizacaoCaixa Bloqueada(string mensagem, ProdutoAcabadoCaixa? caixaAtiva)
        => new(CenarioFinalizacaoCaixa.Bloqueada, mensagem, caixaAtiva, string.Empty, false);
}

/// <summary>DiagnÃ³stico de prontidÃ£o do envio manual da caixa.</summary>
public sealed record ResultadoDiagnosticoEnvioCaixa(bool PodeEnviar, string Mensagem, StatusIntegracaoCaixa? Status);
public sealed record ResultadoBloqueioPipeline045(bool Bloqueado, string Mensagem)
{
    public static readonly ResultadoBloqueioPipeline045 Liberado = new(false, string.Empty);

    public static ResultadoBloqueioPipeline045 CriarBloqueio(string mensagem)
        => new(true, mensagem);
}

public enum CenarioEnvioCaixaHu { Confirmado, Falha, Bloqueado, NaoAutorizado, ProntaParaReenvio, Timeout }

/// <summary>Resultado do envio manual da caixa Ã  HU SAP (nunca cria palete/Material Document).</summary>
public sealed record ResultadoEnvioCaixaHu(
    CenarioEnvioCaixaHu Cenario,
    string Mensagem,
    string? HandlingUnitExternalId,
    ProdutoAcabadoCaixa? Caixa)
{
    public bool Sucesso => Cenario == CenarioEnvioCaixaHu.Confirmado;

    /// <summary>True quando a caixa apenas foi preparada para reenvio (nenhum POST executado, HU nÃ£o confirmada).</summary>
    public bool Reprocessavel => Cenario == CenarioEnvioCaixaHu.ProntaParaReenvio;

    public static ResultadoEnvioCaixaHu Confirmado(string handlingUnitExternalId, ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.Confirmado,
            $"Handling Unit {handlingUnitExternalId} confirmada no SAP.",
            handlingUnitExternalId, caixa);

    public static ResultadoEnvioCaixaHu Falha(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioCaixaHu.Falha, mensagem, null, caixa);

    public static ResultadoEnvioCaixaHu Bloqueado(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioCaixaHu.Bloqueado, mensagem, null, caixa);

    public static ResultadoEnvioCaixaHu NaoAutorizado(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioCaixaHu.NaoAutorizado, mensagem, null, caixa);

    /// <summary>Timeout apÃ³s possÃ­vel POST: caixa em INDETERMINADO_TIMEOUT; requer reconciliaÃ§Ã£o.</summary>
    public static ResultadoEnvioCaixaHu Timeout(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioCaixaHu.Timeout, mensagem, null, caixa);

    /// <summary>Â§5: caixa em ERRO_SAP foi preparada (PRONTA_PARA_ENVIO) para novo envio â€” sem POST, HU nÃ£o confirmada.</summary>
    public static ResultadoEnvioCaixaHu ProntaParaReenvio(ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.ProntaParaReenvio,
            "Caixa preparada para reenvio (PRONTA_PARA_ENVIO). Nenhum POST executado; HU ainda nÃ£o confirmada.",
            null, caixa);
}
















