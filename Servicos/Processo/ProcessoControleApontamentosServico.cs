using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Orquestrador do Controle de Apontamentos. Interpreta o código, consulta a OP pela integração
/// EXISTENTE (sem novo cliente HTTP), resolve a operação sem ambiguidade, valida a sequência técnica,
/// resolve a tela de destino por CONFIGURAÇÃO (nunca por descrição) e aplica a máquina de estados.
///
/// Transições reais (as únicas implementadas) — ver <see cref="StatusApontamentoOperacao"/>:
///   (nenhum) --início confirmado--> EM_ANDAMENTO
///   EM_ANDAMENTO --retomada------> EM_ANDAMENTO (só auditoria; iniciado_em inalterado)
///   EM_ANDAMENTO --atividade OK--> AGUARDANDO_FINALIZACAO
///   AGUARDANDO_FINALIZACAO --término--> CONCLUIDA
///   EM_ANDAMENTO --recusa/cancelamento--> CANCELADA
///
/// Erros operacionais viram <see cref="ResultadoLeituraApontamento"/> — não exceções.
/// TODA leitura relevante é auditada em operacao_producao_evento quando a estrutura existe.
/// </summary>
public sealed class ProcessoControleApontamentosServico
{
    public const string MensagemEstruturaNaoAplicada =
        "A configuração da operação ainda não foi aplicada no banco DEV.";
    private const string MarcadorPerfilResultadoNaoResolvido = "PERFIL_RESULTADO_NAO_RESOLVIDO";

    private readonly IProductionOrderSapServico _ordemProducaoServico;
    private readonly Func<IControleApontamentosRepositorio> _criarRepositorio;
    private readonly CodigoBarrasOperacaoServico _parser;
    private readonly IControleApontamentosAutorizacaoServico _autorizacao;

    // GATE 048-E: resolucao READ-ONLY do roteiro (marcador PP_FORM). Fail-closed por padrao.
    private readonly IProductionRoutingSapServico _roteiroServico;

    public ProcessoControleApontamentosServico()
        : this(
            FabricaProductionOrderSapServico.Criar(),
            () => new ControleApontamentosRepositorio(
                new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())),
            new CodigoBarrasOperacaoServico(),
            autorizacao: null,
            roteiroServico: FabricaProductionRoutingSapServico.Criar())
    {
    }

    internal ProcessoControleApontamentosServico(
        IProductionOrderSapServico ordemProducaoServico,
        Func<IControleApontamentosRepositorio> criarRepositorio,
        CodigoBarrasOperacaoServico? parser = null,
        IControleApontamentosAutorizacaoServico? autorizacao = null,
        IProductionRoutingSapServico? roteiroServico = null)
    {
        _ordemProducaoServico = ordemProducaoServico ?? throw new ArgumentNullException(nameof(ordemProducaoServico));
        _criarRepositorio = criarRepositorio ?? throw new ArgumentNullException(nameof(criarRepositorio));
        _parser = parser ?? new CodigoBarrasOperacaoServico();
        _autorizacao = autorizacao ?? new ControleApontamentosAutorizacaoServico();
        _roteiroServico = roteiroServico ?? new ProductionRoutingSapFailClosedServico();
    }

    /// <summary>Interpreta o código sem tocar em SAP/banco (eco imediato na tela e testes).</summary>
    public CodigoBarrasOperacao Interpretar(string? codigoLido) => _parser.Interpretar(codigoLido);

    /// <summary>
    /// Processa uma leitura completa. <paramref name="confirmar"/> é chamado ANTES de efetivar o estado;
    /// se devolver false, nada é registrado (e o evento de recusa é auditado).
    /// </summary>
    public async Task<ResultadoLeituraApontamento> ProcessarLeituraAsync(
        string? codigoLido,
        string usuario,
        string estacao,
        Func<ConfirmacaoApontamento, bool> confirmar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmar);

        CodigoBarrasOperacao codigo = _parser.Interpretar(codigoLido);
        if (!codigo.Valido)
        {
            await AuditarAsync(codigo, usuario, estacao, "CODIGO_INVALIDO", codigo.MensagemValidacao, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.CodigoInvalido, codigo.MensagemValidacao, codigo);
        }

        if (string.IsNullOrWhiteSpace(usuario))
        {
            const string mensagem = "Não há sessão de usuário autenticada. Refaça o login para registrar apontamentos.";
            await AuditarAsync(codigo, usuario, estacao, "SEM_SESSAO", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.SemSessaoUsuario, mensagem, codigo);
        }

        // Permissão POR AÇÃO, antes de qualquer consulta SAP, confirmação ou alteração de banco.
        bool inicio = codigo.TipoEvento == TipoEventoOperacao.Inicio;
        string acaoExigida = inicio ? PermissoesSistema.Acoes.Iniciar : PermissoesSistema.Acoes.Finalizar;
        bool autorizado = inicio ? _autorizacao.PodeIniciar() : _autorizacao.PodeFinalizar();
        if (!autorizado)
        {
            string mensagem =
                $"Usuário sem permissão para {(inicio ? "iniciar" : "finalizar")} apontamentos "
                + $"(ação {acaoExigida} da rotina {PermissoesSistema.Rotinas.ControleApontamentos}).";
            await AuditarAsync(
                codigo, usuario, estacao, $"SEM_PERMISSAO_{acaoExigida}", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.SemPermissaoAcao, mensagem, codigo);
        }

        return inicio
            ? await ProcessarInicioAsync(codigo, usuario, estacao, confirmar, cancellationToken)
            : await ProcessarTerminoAsync(codigo, usuario, estacao, confirmar, cancellationToken);
    }

    // =========================== INÍCIO ===========================
    // O início SEMPRE consulta o SAP (precisa da lista de operações para resolver e validar sequência).

    private async Task<ResultadoLeituraApontamento> ProcessarInicioAsync(
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        Func<ConfirmacaoApontamento, bool> confirmar,
        CancellationToken cancellationToken)
    {
        (OrdemProducaoSap? ordem, ResultadoLeituraApontamento? falha) =
            await ConsultarOrdemAsync(codigo, usuario, estacao, cancellationToken);
        if (falha is not null)
        {
            return falha;
        }

        OrdemProducaoSap ordemSap = ordem!;

        if (FalhaMapeamentoOperacoesSap(ordemSap))
        {
            string mensagem = "O SAP retornou operações para a OP, mas o campo ManufacturingOrderOperation não foi identificado. Verifique o payload e o mapeamento da entidade A_ProductionOrderOperation_2.";
            await AuditarAsync(codigo, usuario, estacao, "FALHA_MAPEAMENTO_OPERACOES_SAP", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.FalhaMapeamentoOperacoesSap, mensagem, codigo, ordemSap);
        }

        ResultadoResolucaoOperacao resolucao = ResolverOperacao(ordemSap, codigo.Operacao);
        if (resolucao.Ambigua)
        {
            string mensagem =
                $"A operação {codigo.Operacao} aparece mais de uma vez na OP. "
                + "Não foi possível identificar a sequência/suboperação de forma segura.";
            await AuditarAsync(codigo, usuario, estacao, "OPERACAO_AMBIGUA", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.OperacaoAmbigua, mensagem, codigo, ordemSap);
        }

        if (resolucao.Operacao is null)
        {
            string mensagem = $"Operação {codigo.Operacao} não existe na OP {ordemSap.NumeroOrdem}.";
            await AuditarAsync(codigo, usuario, estacao, "OPERACAO_INEXISTENTE", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.OperacaoNaoEncontrada, mensagem, codigo, ordemSap);
        }

        OperacaoOrdemProducaoSap operacao = resolucao.Operacao;

        // GATE 048-E: SOMENTE operações com marcador PP_FORM (OperationStandardTextCode, via roteiro
        // AUTORITATIVO da ProductionVersion) são manuais. O serviço é o DONO ÚNICO da regra; o roteiro
        // não resolvido/ambíguo é fail-closed; a operação automática não gera apontamento nem abre tela.
        // GATE 095F (095D/095E-R1): roteiro V3 COMPLETO da OP (API_PRODUCTION_ROUTING;v=3; sem ProductionVersion).
        RoteiroProducaoSap? roteiro = await _roteiroServico.ResolverRoteiroDaOrdemAsync(ordemSap, cancellationToken);
        // GATE 095F-R1 (Hermes): a manualidade de CADA ocorrência é decidida SÓ APÓS a correlação exata
        // (Operation+Plant+WorkCenter) — o Marcador NUNCA recebe o roteiro completo no fluxo produtivo.
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoesManuais =
            FiltrarOperacoesManuaisCorrelacionadas(ordemSap, roteiro);
        OrdemProducaoSap ordemManual = ordemSap with { Operacoes = operacoesManuais };

        const string mensagemRoteiroNaoResolvido =
            "Não foi possível resolver o roteiro (marcador PP_FORM) desta operação no SAP. "
            + "O apontamento manual está bloqueado até a regularização.";

        // Seam autoritativo 095E-R1: correlaciona a OCORRÊNCIA corrente (Operation+Plant+WorkCenter) ANTES do
        // Marcador. Plant efetivo = operacao.Centro ?? ordemSap.Centro (mesmo do routing local). O Marcador NÃO
        // recebe o roteiro completo — recebe o roteiro REDUZIDO a uma operação. Fail-closed em 0/>1 exatos.
        OperacaoOrdemProducaoSap ocorrenciaParaCorrelacao = operacao with
        {
            Centro = string.IsNullOrWhiteSpace(operacao.Centro) ? ordemSap.Centro : operacao.Centro
        };
        ResultadoCorrelacaoOcorrencia correlacao =
            CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, ocorrenciaParaCorrelacao);
        if (correlacao.Estado != EstadoCorrelacaoOcorrencia.Correlacionada)
        {
            string codigoDiagnostico = correlacao.Estado == EstadoCorrelacaoOcorrencia.Ambigua
                ? "OPERATION_OCCURRENCE_AMBIGUOUS"
                : "OPERATION_OCCURRENCE_NOT_FOUND";
            await AuditarAsync(codigo, usuario, estacao, codigoDiagnostico, mensagemRoteiroNaoResolvido, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagemRoteiroNaoResolvido, codigo, ordemManual);
        }

        // Roteiro reduzido a EXATAMENTE uma operação; o Marcador (Op-only, PP_FORM) decide a manualidade.
        ClassificacaoOperacaoManual classificacao =
            MarcadorOperacaoManualSap.ClassificarOperacao(correlacao.RoteiroReduzido, operacao.Operacao);

        if (classificacao == ClassificacaoOperacaoManual.ContratoNaoResolvido)
        {
            // Correlação já passou (1 operação): o único ContratoNaoResolvido aqui é StandardTextCode não obtido.
            await AuditarAsync(codigo, usuario, estacao, "STANDARD_TEXT_NAO_OBTIDO", mensagemRoteiroNaoResolvido, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagemRoteiroNaoResolvido, codigo, ordemManual);
        }

        if (classificacao == ClassificacaoOperacaoManual.Automatica)
        {
            string mensagem =
                $"A operação {operacao.Operacao} é automática no SAP e não exige apontamento manual no FugaPET.";
            await AuditarAsync(codigo, usuario, estacao, "OPERACAO_AUTOMATICA", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.OperacaoAutomatica, mensagem, codigo, ordemManual);
        }

        // Manual: a partir daqui a OP usada (grid, sequência anterior/próxima) contém SÓ operações manuais;
        // a operação lida carrega o marcador PP_FORM. A tela/Controller nunca conhecem essa regra.
        ordemSap = ordemManual;
        operacao = operacao with { CodigoTextoPadrao = MarcadorOperacaoManualSap.CodigoTextoPadraoManual };

        IControleApontamentosRepositorio repositorio = _criarRepositorio();
        if (!await EstruturaDisponivelSeguraAsync(repositorio, cancellationToken))
        {
            return new ResultadoLeituraApontamento
            {
                Cenario = CenarioLeituraApontamento.EstruturaNaoAplicada,
                Mensagem = MensagemEstruturaNaoAplicada,
                Codigo = codigo,
                Ordem = ordemSap,
                Operacao = operacao
            };
        }

        // Estado completo da OP (grid + validação de sequência), sempre a partir do persistido.
        IReadOnlyList<OperacaoProducaoApontamento> apontamentosOrdem =
            await repositorio.ListarApontamentosDaOrdemAsync(ordemSap.NumeroOrdem, cancellationToken);
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoesOrdem =
            await repositorio.ListarConfiguracoesAtivasAsync(ordemSap.Centro, ordemSap.TipoOrdem, cancellationToken);

        // ---- Retomada / duplicidade: um apontamento ativo desta operação muda o significado da leitura ----
        OperacaoProducaoApontamento? ativo = await repositorio.ObterApontamentoAtivoAsync(
            ordemSap.NumeroOrdem, operacao.Sequencia, operacao.Operacao, operacao.Suboperacao, cancellationToken);

        if (ativo is not null)
        {
            return await TratarLeituraDeInicioComAtivoAsync(
                repositorio, codigo, ordemSap, operacao, ativo, usuario, estacao, confirmar,
                apontamentosOrdem, configuracoesOrdem, cancellationToken);
        }

        // Código de início já consumido por um apontamento encerrado (não ativo).
        string idempotencyKey = CodigoBarrasOperacaoServico.MontarIdempotencyKey(
            codigo.CodigoOriginal, codigo.FormatoVersao);
        if (await repositorio.CodigoJaUtilizadoAsync(idempotencyKey, cancellationToken))
        {
            const string mensagem = "Este código de início já foi utilizado.";
            await AuditarAsync(codigo, usuario, estacao, "INICIO_DUPLICADO", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.InicioDuplicado, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // ---- Destino: SEMPRE por rota Plant + WorkCenter (GATE 093D) ----
        // Operation/Sequence/SubOperation NÃO participam da decisão da rota; o WorkCenter é usado literalmente.
        string plantRota = string.IsNullOrWhiteSpace(operacao.Centro) ? ordemSap.Centro : operacao.Centro;
        ResultadoConfiguracaoOperacao resultadoConfig =
            await repositorio.ObterConfiguracaoRotaPorWorkCenterAsync(plantRota, operacao.CentroTrabalho, cancellationToken);

        if (resultadoConfig.Ambigua)
        {
            string mensagem =
                $"Há {resultadoConfig.Empatadas} configurações de mesma especificidade para a operação "
                + $"{operacao.Operacao}. Ajuste o cadastro para que apenas uma se aplique.";
            await AuditarAsync(codigo, usuario, estacao, "CONFIGURACAO_AMBIGUA", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(
                    CenarioLeituraApontamento.ConfiguracaoAmbigua, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        ConfiguracaoOperacaoProcesso? configuracao = resultadoConfig.Configuracao;
        if (configuracao is null || configuracao.TipoProcesso == TipoProcessoOperacao.SemDestinoConfigurado)
        {
            string mensagem =
                $"A operação {operacao.Operacao} não possui tela de destino configurada. "
                + "Cadastre a configuração da operação antes de iniciar.";
            await AuditarAsync(codigo, usuario, estacao, "MAPEAMENTO_AUSENTE", mensagem, cancellationToken);
            return ComEstado(
                new ResultadoLeituraApontamento
                {
                    Cenario = CenarioLeituraApontamento.MapeamentoNaoConfigurado,
                    Mensagem = mensagem,
                    Codigo = codigo,
                    Ordem = ordemSap,
                    Operacao = operacao
                },
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // ---- Sequência: a operação anterior (ordenação técnica) precisa estar CONCLUIDA (GATE 093D: WC03) ----
        OperacaoOrdemProducaoSap? anterior = ObterOperacaoAnterior(ordemSap, operacao);
        if (anterior is not null && !OperacaoConcluida(apontamentosOrdem, anterior))
        {
            string mensagem =
                $"A operação {anterior.Operacao} (sequência {DescreverSequencia(anterior)}) precisa estar "
                + $"concluída antes de iniciar a operação {operacao.Operacao}.";
            await AuditarAsync(codigo, usuario, estacao, "SEQUENCIA_BLOQUEADA", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(
                    CenarioLeituraApontamento.OperacaoAnteriorNaoConcluida, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // ---- Ocorrência do WorkCenter (chave do perfil por rota + ocorrência) ----
        int ordemOcorrenciaWorkCenter = CalcularOrdemOcorrenciaWorkCenter(ordemSap, operacao);
        if (ordemOcorrenciaWorkCenter <= 0)
        {
            const string mensagem = "Não foi possível calcular a ocorrência da operação neste WorkCenter.";
            await AuditarAsync(codigo, usuario, estacao, "ORDEM_WORKCENTER_NAO_RESOLVIDA", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(
                    CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // ---- Perfil de resultado (somente RESULTADO_APONTAMENTO): normalizado por rota + ocorrência, fail-closed ----
        long? codigoPerfilResultado = null;
        if (string.Equals(configuracao.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))
        {
            ResultadoPerfilResultado perfil = await repositorio.ObterPerfilResultadoAsync(
                configuracao.CodigoConfiguracao, ordemOcorrenciaWorkCenter, cancellationToken);
            if (perfil.Ambiguo || !perfil.CodigoPerfilResultado.HasValue)
            {
                string mensagem = perfil.Ambiguo
                    ? $"Há {perfil.Encontrados} perfis de resultado ativos para esta rota/ocorrência de WorkCenter."
                    : "Não há perfil de resultado ativo para esta rota/ocorrência de WorkCenter.";
                await AuditarAsync(codigo, usuario, estacao, "PERFIL_RESULTADO_NAO_RESOLVIDO", mensagem, cancellationToken);
                return ComEstado(
                    ResultadoLeituraApontamento.Falha(
                        CenarioLeituraApontamento.MapeamentoNaoConfigurado, mensagem, codigo, ordemSap),
                    apontamentosOrdem, configuracoesOrdem, operacao);
            }
            codigoPerfilResultado = perfil.CodigoPerfilResultado;
        }

        ItemOrdemProducaoSap? item = ordemSap.Itens.FirstOrDefault();
        OperacaoProducaoApontamento novo = new()
        {
            NumeroOrdem = ordemSap.NumeroOrdem,
            ItemOrdem = item?.ItemOrdem ?? string.Empty,
            Produto = item?.Material ?? ordemSap.MaterialProduzido,
            Sequencia = operacao.Sequencia,
            Operacao = operacao.Operacao,
            Suboperacao = operacao.Suboperacao,
            DescricaoOperacao = operacao.Descricao,
            CentroTrabalho = operacao.CentroTrabalho,
            TipoProcesso = configuracao.TipoProcesso,
            TelaDestino = configuracao.TelaDestino,
            CodigoPerfilResultado = codigoPerfilResultado,
            UsuarioInicio = usuario,
            EstacaoInicio = estacao,
            CodigoBarrasInicio = codigo.CodigoOriginal,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Status = StatusApontamentoOperacao.EmAndamento
        };

        if (!confirmar(ConfirmacaoApontamento.ParaInicio(codigo, ordemSap, operacao, usuario, estacao)))
        {
            const string mensagem = "Início cancelado pelo operador.";
            await AuditarAsync(codigo, usuario, estacao, "CONFIRMACAO_RECUSADA", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(
                    CenarioLeituraApontamento.ConfirmacaoPendente, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // Claim atômico: quem perde a corrida recebe null (conflito funcional, nunca erro de suporte).
        OperacaoProducaoApontamento? criado =
            await repositorio.TentarIniciarApontamentoAsync(novo, codigo, cancellationToken);
        if (criado is null)
        {
            const string mensagem = "Outra estação iniciou esta operação neste instante. Leitura ignorada.";
            await AuditarAsync(codigo, usuario, estacao, "FALHA_CONCORRENTE", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.InicioDuplicado, mensagem, codigo, ordemSap),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // Snapshot do perfil também no objeto em memória (o RETURNING já traz a coluna; garante consistência).
        criado.CodigoPerfilResultado = codigoPerfilResultado;

        // Recarrega o estado da OP já com o apontamento criado (grid a partir do persistido).
        apontamentosOrdem = await repositorio.ListarApontamentosDaOrdemAsync(ordemSap.NumeroOrdem, cancellationToken);

        return new ResultadoLeituraApontamento
        {
            Cenario = CenarioLeituraApontamento.SucessoInicio,
            Mensagem = $"Operação {operacao.Operacao} iniciada.",
            Codigo = codigo,
            Ordem = ordemSap,
            Operacao = operacao,
            Configuracao = configuracao,
            Apontamento = criado,
            Contexto = MontarContexto(criado, configuracao.TipoProcesso),
            ApontamentosDaOrdem = apontamentosOrdem,
            ConfiguracoesDaOrdem = configuracoesOrdem
        };
    }

    /// <summary>
    /// Leitura do código de INÍCIO quando já existe apontamento ativo: define retomada, bloqueio por
    /// outro usuário ou orientação de término. NUNCA cria um segundo apontamento nem altera iniciado_em.
    /// </summary>
    private async Task<ResultadoLeituraApontamento> TratarLeituraDeInicioComAtivoAsync(
        IControleApontamentosRepositorio repositorio,
        CodigoBarrasOperacao codigo,
        OrdemProducaoSap ordem,
        OperacaoOrdemProducaoSap operacao,
        OperacaoProducaoApontamento ativo,
        string usuario,
        string estacao,
        Func<ConfirmacaoApontamento, bool> confirmar,
        IReadOnlyList<OperacaoProducaoApontamento> apontamentosOrdem,
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoesOrdem,
        CancellationToken cancellationToken)
    {
        // Atividade já concluída: não reabre; o operador deve ler o término.
        if (ativo.Status == StatusApontamentoOperacao.AguardandoFinalizacao)
        {
            string mensagem =
                $"A atividade da operação {operacao.Operacao} já foi concluída. "
                + "Leia o código de TÉRMINO para finalizar.";
            await AuditarAsync(codigo, usuario, estacao, "JA_AGUARDANDO_TERMINO", mensagem, cancellationToken);
            return ComEstado(
                new ResultadoLeituraApontamento
                {
                    Cenario = CenarioLeituraApontamento.OperacaoJaAguardandoTermino,
                    Mensagem = mensagem,
                    Codigo = codigo,
                    Ordem = ordem,
                    Operacao = operacao,
                    Apontamento = ativo
                },
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // EM_ANDAMENTO por OUTRO usuário: bloqueia (retomar exigiria ação administrativa).
        if (!string.Equals(ativo.UsuarioInicio, usuario, StringComparison.OrdinalIgnoreCase))
        {
            string mensagem =
                $"Esta operação está em andamento por {ativo.UsuarioInicio} "
                + $"(estação {ativo.EstacaoInicio}, início {FormatarData(ativo.IniciadoEm)}).";
            await AuditarAsync(codigo, usuario, estacao, "EM_ANDAMENTO_OUTRO_USUARIO", mensagem, cancellationToken);
            return ComEstado(
                new ResultadoLeituraApontamento
                {
                    Cenario = CenarioLeituraApontamento.OperacaoEmAndamentoPorOutroUsuario,
                    Mensagem = mensagem,
                    Codigo = codigo,
                    Ordem = ordem,
                    Operacao = operacao,
                    Apontamento = ativo
                },
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // GATE 093D — RESULTADO_APONTAMENTO: reidrata rota/ocorrência/perfil e valida consistência do
        // WorkCenter persistido ANTES de reabrir. Fail-closed em divergência; não cria novo apontamento.
        ResultadoLeituraApontamento? reidratacao = await ReidratarContextoResultadoApontamentoRetomadaAsync(
            repositorio, codigo, ordem, operacao, ativo, usuario, estacao,
            apontamentosOrdem, configuracoesOrdem, cancellationToken);
        if (reidratacao is not null)
        {
            return reidratacao;
        }

        // RETOMADA do mesmo usuário: reabre a MESMA tela com os dados PERSISTIDOS.
        if (!confirmar(ConfirmacaoApontamento.ParaRetomada(codigo, ordem, operacao, ativo)))
        {
            const string mensagem = "Retomada cancelada pelo operador.";
            await AuditarAsync(codigo, usuario, estacao, "CONFIRMACAO_RECUSADA", mensagem, cancellationToken);
            return ComEstado(
                ResultadoLeituraApontamento.Falha(
                    CenarioLeituraApontamento.ConfirmacaoPendente, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // Retomada é EM_ANDAMENTO → EM_ANDAMENTO: só evento, sem alterar o apontamento.
        await AuditarAsync(
            codigo, usuario, estacao, "RETOMADA",
            $"Retomada da operação {operacao.Operacao} (início original {FormatarData(ativo.IniciadoEm)}).",
            cancellationToken, ativo.CodigoApontamento,
            StatusApontamentoOperacao.EmAndamento, StatusApontamentoOperacao.EmAndamento, ativo.CorrelationId);

        return new ResultadoLeituraApontamento
        {
            Cenario = CenarioLeituraApontamento.RetomadaDisponivel,
            Mensagem = $"Retomando a operação {operacao.Operacao} (iniciada em {FormatarData(ativo.IniciadoEm)}).",
            Codigo = codigo,
            Ordem = ordem,
            Operacao = operacao,
            Apontamento = ativo,
            // Contexto reconstruído a partir do PERSISTIDO (sobrevive a reinício da aplicação).
            Contexto = MontarContexto(ativo, ativo.TipoProcesso),
            ApontamentosDaOrdem = apontamentosOrdem,
            ConfiguracoesDaOrdem = configuracoesOrdem
        };
    }

    /// <summary>
    /// GATE 093D — retomada de RESULTADO_APONTAMENTO: re-resolve Plant/WorkCenter, valida o WorkCenter
    /// persistido, re-resolve a rota, recalcula a ocorrência e re-resolve o perfil normalizado, gravando o
    /// snapshot no apontamento ativo. Retorna FALHA (fail-closed) em qualquer divergência; retorna null
    /// quando não se aplica ou quando reidratou com sucesso. NÃO cria novo apontamento/resultado/item/vínculo.
    /// ZERO SAP (LOCAL_ONLY).
    /// </summary>
    private async Task<ResultadoLeituraApontamento?> ReidratarContextoResultadoApontamentoRetomadaAsync(
        IControleApontamentosRepositorio repositorio,
        CodigoBarrasOperacao codigo,
        OrdemProducaoSap ordem,
        OperacaoOrdemProducaoSap operacao,
        OperacaoProducaoApontamento ativo,
        string usuario,
        string estacao,
        IReadOnlyList<OperacaoProducaoApontamento> apontamentosOrdem,
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoesOrdem,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(ativo.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))
        {
            return null;
        }

        string plantRota = string.IsNullOrWhiteSpace(operacao.Centro) ? ordem.Centro : operacao.Centro;
        if (string.IsNullOrWhiteSpace(plantRota) || string.IsNullOrWhiteSpace(operacao.CentroTrabalho))
        {
            const string mensagem =
                "Não foi possível resolver Plant/WorkCenter para retomar o resultado de apontamento.";
            await AuditarAsync(codigo, usuario, estacao, "ROTA_WORKCENTER_NAO_RESOLVIDA", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // WorkCenter persistido deve bater com o WorkCenter atual da operação.
        if (!string.IsNullOrWhiteSpace(ativo.CentroTrabalho)
            && !string.Equals(NormalizarChave(ativo.CentroTrabalho), NormalizarChave(operacao.CentroTrabalho),
                StringComparison.OrdinalIgnoreCase))
        {
            string mensagem =
                $"WorkCenter persistido do apontamento diverge do WorkCenter atual da operação {operacao.Operacao}.";
            await AuditarAsync(codigo, usuario, estacao, "ROTA_WORKCENTER_INCONSISTENTE", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        ResultadoConfiguracaoOperacao resultadoConfig =
            await repositorio.ObterConfiguracaoRotaPorWorkCenterAsync(plantRota, operacao.CentroTrabalho, cancellationToken);
        if (resultadoConfig.Ambigua)
        {
            string mensagem =
                $"Há {resultadoConfig.Empatadas} configurações de mesma especificidade para retomar a operação "
                + $"{operacao.Operacao}. Ajuste o cadastro para que apenas uma se aplique.";
            await AuditarAsync(codigo, usuario, estacao, "CONFIGURACAO_AMBIGUA", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ConfiguracaoAmbigua, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        ConfiguracaoOperacaoProcesso? configuracao = resultadoConfig.Configuracao;
        if (configuracao is null
            || configuracao.TipoProcesso == TipoProcessoOperacao.SemDestinoConfigurado
            || !string.Equals(configuracao.TipoProcesso, ativo.TipoProcesso, StringComparison.Ordinal))
        {
            string mensagem =
                $"A rota Plant/WorkCenter da operação {operacao.Operacao} não resolve o destino persistido do apontamento.";
            await AuditarAsync(codigo, usuario, estacao, "MAPEAMENTO_RETOMADA_INCONSISTENTE", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.MapeamentoNaoConfigurado, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        int ordemOcorrenciaWorkCenter = CalcularOrdemOcorrenciaWorkCenter(ordem, operacao);
        if (ordemOcorrenciaWorkCenter <= 0)
        {
            const string mensagem = "Não foi possível calcular a ocorrência da operação neste WorkCenter.";
            await AuditarAsync(codigo, usuario, estacao, "ORDEM_WORKCENTER_NAO_RESOLVIDA", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        ResultadoPerfilResultado perfil = await repositorio.ObterPerfilResultadoAsync(
            configuracao.CodigoConfiguracao, ordemOcorrenciaWorkCenter, cancellationToken);
        if (perfil.Ambiguo || !perfil.CodigoPerfilResultado.HasValue)
        {
            string mensagem = perfil.Ambiguo
                ? $"Há {perfil.Encontrados} perfis de resultado ativos para esta rota/ocorrência de WorkCenter."
                : "Não há perfil de resultado ativo para esta rota/ocorrência de WorkCenter.";
            await AuditarAsync(codigo, usuario, estacao, "PERFIL_RESULTADO_NAO_RESOLVIDO", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.MapeamentoNaoConfigurado, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        // §12 — snapshot histórico divergente do perfil re-resolvido = FAIL_CLOSED (não troca silenciosamente).
        // Snapshot NULL (legado pré-054) apenas reidrata, conforme comportamento golden.
        if (ativo.CodigoPerfilResultado.HasValue
            && ativo.CodigoPerfilResultado.Value != perfil.CodigoPerfilResultado.Value)
        {
            const string mensagem = "O perfil de resultado atual diverge do perfil registrado neste apontamento.";
            await AuditarAsync(codigo, usuario, estacao, "PERFIL_RESULTADO_DIVERGENTE", mensagem, cancellationToken);
            return ComEstado(ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.MapeamentoNaoConfigurado, mensagem, codigo, ordem),
                apontamentosOrdem, configuracoesOrdem, operacao);
        }

        ativo.CodigoPerfilResultado = perfil.CodigoPerfilResultado;
        return null;
    }

    // =========================== TÉRMINO ===========================
    // O término NÃO depende de nova consulta SAP: resolve pelo apontamento local ativo (OP + operação).

    private async Task<ResultadoLeituraApontamento> ProcessarTerminoAsync(
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        Func<ConfirmacaoApontamento, bool> confirmar,
        CancellationToken cancellationToken)
    {
        IControleApontamentosRepositorio repositorio = _criarRepositorio();
        if (!await EstruturaDisponivelSeguraAsync(repositorio, cancellationToken))
        {
            return new ResultadoLeituraApontamento
            {
                Cenario = CenarioLeituraApontamento.EstruturaNaoAplicada,
                Mensagem = MensagemEstruturaNaoAplicada,
                Codigo = codigo
            };
        }

        string idempotencyKey = CodigoBarrasOperacaoServico.MontarIdempotencyKey(
            codigo.CodigoOriginal, codigo.FormatoVersao);
        if (await repositorio.CodigoJaUtilizadoAsync(idempotencyKey, cancellationToken))
        {
            const string mensagem = "Este código de término já foi utilizado.";
            await AuditarAsync(codigo, usuario, estacao, "TERMINO_DUPLICADO", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.TerminoDuplicado, mensagem, codigo);
        }

        // Fonte da verdade do término: o apontamento local. Se o SAP cair, o término continua possível.
        IReadOnlyList<OperacaoProducaoApontamento> ativos =
            await repositorio.ListarApontamentosAtivosPorOperacaoAsync(
                codigo.OrdemProducao, codigo.Operacao, cancellationToken);

        if (ativos.Count == 0)
        {
            string mensagem = $"Não há apontamento em andamento para a operação {codigo.Operacao} da OP {codigo.OrdemProducao}.";
            await AuditarAsync(codigo, usuario, estacao, "TERMINO_SEM_INICIO", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.TerminoSemInicio, mensagem, codigo);
        }

        if (ativos.Count > 1)
        {
            string mensagem =
                $"Há {ativos.Count} apontamentos ativos para a operação {codigo.Operacao} desta OP. "
                + "Não é possível identificar qual finalizar com segurança.";
            await AuditarAsync(codigo, usuario, estacao, "TERMINO_AMBIGUO", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(CenarioLeituraApontamento.TerminoAmbiguo, mensagem, codigo);
        }

        OperacaoProducaoApontamento ativo = ativos[0];

        if (ativo.Status != StatusApontamentoOperacao.AguardandoFinalizacao)
        {
            const string mensagem =
                "A atividade desta operação ainda não foi concluída na tela operacional. "
                + "Conclua o processo antes de ler o código de término.";
            await AuditarAsync(
                codigo, usuario, estacao, "TERMINO_NAO_LIBERADO", mensagem, cancellationToken, ativo.CodigoApontamento);
            return new ResultadoLeituraApontamento
            {
                Cenario = CenarioLeituraApontamento.TerminoNaoLiberado,
                Mensagem = mensagem,
                Codigo = codigo,
                Apontamento = ativo
            };
        }

        // Consulta SAP é COMPLEMENTAR: enriquece a tela, mas não bloqueia o término se estiver indisponível.
        OrdemProducaoSap? ordemComplementar = await ConsultarOrdemComplementarAsync(codigo, cancellationToken);

        if (!confirmar(ConfirmacaoApontamento.ParaTermino(codigo, ativo)))
        {
            const string mensagem = "Término cancelado pelo operador.";
            await AuditarAsync(
                codigo, usuario, estacao, "CONFIRMACAO_RECUSADA", mensagem, cancellationToken, ativo.CodigoApontamento);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ConfirmacaoPendente, mensagem, codigo, ordemComplementar);
        }

        DateTimeOffset? terminadoEm = await repositorio.TentarConcluirApontamentoAsync(
            ativo.CodigoApontamento, usuario, estacao, codigo, idempotencyKey, cancellationToken);
        if (terminadoEm is null)
        {
            const string mensagem = "Esta operação já foi finalizada por outra estação.";
            await AuditarAsync(
                codigo, usuario, estacao, "FALHA_CONCORRENTE", mensagem, cancellationToken, ativo.CodigoApontamento);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.TerminoDuplicado, mensagem, codigo, ordemComplementar);
        }

        ativo.Status = StatusApontamentoOperacao.Concluida;
        ativo.TerminadoEm = terminadoEm;
        ativo.UsuarioTermino = usuario;
        ativo.EstacaoTermino = estacao;
        ativo.CodigoBarrasTermino = codigo.CodigoOriginal;

        IReadOnlyList<OperacaoProducaoApontamento> apontamentosOrdem =
            await repositorio.ListarApontamentosDaOrdemAsync(codigo.OrdemProducao, cancellationToken);
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoesOrdem = ordemComplementar is null
            ? []
            : await repositorio.ListarConfiguracoesAtivasAsync(
                ordemComplementar.Centro, ordemComplementar.TipoOrdem, cancellationToken);

        return new ResultadoLeituraApontamento
        {
            Cenario = CenarioLeituraApontamento.SucessoTermino,
            Mensagem = $"Operação {codigo.Operacao} concluída.",
            Codigo = codigo,
            Ordem = ordemComplementar,
            Apontamento = ativo,
            ApontamentosDaOrdem = apontamentosOrdem,
            ConfiguracoesDaOrdem = configuracoesOrdem
        };
    }

    // =========================== Conclusão operacional ===========================

    /// <summary>
    /// EM_ANDAMENTO → AGUARDANDO_FINALIZACAO após a tela operacional concluir a atividade, gravando o
    /// vínculo com o registro criado (ex.: codigo_lancamento do Consumo). Só avança quando exatamente
    /// uma linha é atualizada; erro/divergência SAP e fechamento simples NÃO avançam.
    /// </summary>
    public async Task<bool> RegistrarConclusaoOperacionalAsync(
        long codigoApontamento,
        ResultadoExecucaoProcesso resultado,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        string tipoProcesso = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (!resultado.AtividadeConcluida)
        {
            return false;
        }

        try
        {
            IControleApontamentosRepositorio repositorio = _criarRepositorio();
            OperacaoProducaoApontamento? apontamento = new() { TipoProcesso = tipoProcesso };

            if (resultado.CodigoRegistroProcesso is long codigoRegistroProcesso && codigoRegistroProcesso > 0)
            {
                await repositorio.RegistrarVinculoProcessoAsync(
                    codigoApontamento,
                    apontamento?.TipoProcesso ?? string.Empty,
                    codigoRegistroProcesso,
                    cancellationToken);
            }

            if (string.Equals(apontamento.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))
            {
                IReadOnlyList<ApontamentoProcesso> vinculos =
                    await repositorio.ListarProcessosVinculadosAsync(codigoApontamento, cancellationToken);

                EstadoConclusaoAgregada conclusaoResultado =
                    AvaliarConclusaoResultadoApontamento(apontamento, vinculos);

                if (conclusaoResultado != EstadoConclusaoAgregada.Concluida)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        $"[Apontamento][{MarcadorPerfilResultadoNaoResolvido}] Resultado do apontamento sem registro terminal para a ocorrência atual.");
                    return false;
                }
            }

            return await repositorio.TentarMarcarAguardandoFinalizacaoAsync(
                codigoApontamento, resultado, usuario, estacao, codigo, cancellationToken);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            // Banco indisponível/estado incompatível: não avança e não mente para a tela.
            System.Diagnostics.Trace.TraceWarning(
                $"[Apontamento] Falha ao registrar conclusão operacional: {ex.GetType().Name}");
            return false;
        }
    }

    public async Task<bool> RecuperarConsumoConfirmadoAsync(
        ContextoApontamentoProcesso contexto,
        long codigoLancamento,
        string materialEsperado,
        string reservaEsperada,
        string itemReservaEsperado,
        string loteEsperado,
        string usuario,
        string estacao,
        string codigoBarrasInicio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (codigoLancamento <= 0
            || !string.Equals(contexto.TipoProcesso, TipoProcessoOperacao.ConsumoMateriaPrima, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(materialEsperado)
            || string.IsNullOrWhiteSpace(reservaEsperada)
            || string.IsNullOrWhiteSpace(itemReservaEsperado)
            || string.IsNullOrWhiteSpace(loteEsperado)
            || string.IsNullOrWhiteSpace(usuario)
            || string.IsNullOrWhiteSpace(estacao))
        {
            return false;
        }

        CodigoBarrasOperacao codigo = Interpretar(codigoBarrasInicio);
        if (!codigo.Valido
            || codigo.TipoEvento != TipoEventoOperacao.Inicio
            || !string.Equals(codigo.OrdemProducao, contexto.NumeroOrdem, StringComparison.Ordinal)
            || !string.Equals(codigo.Operacao, contexto.Operacao, StringComparison.Ordinal))
        {
            return false;
        }

        ResultadoExecucaoProcesso resultado = new(
            ResultadoExecucaoProcessoApontamento.ConfirmadoSap,
            codigoLancamento,
            "Consumo já confirmado no SAP recuperado sem novo envio.",
            true);

        try
        {
            return await _criarRepositorio().TentarRecuperarConsumoConfirmadoAsync(
                contexto,
                codigoLancamento,
                materialEsperado.Trim(),
                reservaEsperada.Trim(),
                itemReservaEsperado.Trim(),
                loteEsperado.Trim(),
                resultado,
                usuario.Trim(),
                estacao.Trim(),
                codigo,
                cancellationToken);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[Apontamento] Falha ao recuperar consumo confirmado: {ex.GetType().Name}");
            return false;
        }
    }

    private static EstadoConclusaoAgregada AvaliarConclusaoResultadoApontamento(
        OperacaoProducaoApontamento apontamento,
        IReadOnlyList<ApontamentoProcesso> vinculos)
    {
        if (string.Equals(apontamento.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))
        {
            return AvaliadorConclusaoAgregada.AvaliarResultadoApontamento(vinculos);
        }

        return EstadoConclusaoAgregada.Pendente;
    }

    private Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemParaConclusaoAgregadaAsync(
        OperacaoProducaoApontamento apontamento,
        CancellationToken cancellationToken)
        => _ordemProducaoServico.ConsultarOrdemAsync(apontamento.NumeroOrdem, cancellationToken);

    // =========================== Sequência técnica ===========================

    /// <summary>
    /// Ordenação TÉCNICA das operações: Sequencia, depois Operacao, depois Suboperacao e, como
    /// desempate final, OrderOperationInternalId. Comparação numérica quando os dois lados são
    /// numéricos com zeros à esquerda não sejam ordenados por texto puro; nunca por descrição.
    /// </summary>
    internal static IReadOnlyList<OperacaoOrdemProducaoSap> OrdenarTecnicamente(OrdemProducaoSap ordem)
        => ordem.Operacoes
            .OrderBy(o => o.Sequencia, ComparadorCampoSap.Instancia)
            .ThenBy(o => o.Operacao, ComparadorCampoSap.Instancia)
            .ThenBy(o => o.Suboperacao, ComparadorCampoSap.Instancia)
            .ThenBy(o => o.OrderOperationInternalId, ComparadorCampoSap.Instancia)
            .ToList();

    /// <summary>
    /// GATE 093D — ordinal 1-based da OCORRÊNCIA da operação dentro do MESMO Plant+WorkCenter, na ordenação
    /// técnica global da OP. Desambigua o MESMO WorkCenter repetido na rota (1ª ocorrência=1, 2ª=2, ...).
    /// Comparação apenas por Trim/case (NUNCA transforma o código do WorkCenter). 0 quando Plant/WC vazios
    /// ou a operação não é localizada.
    /// </summary>
    internal static int CalcularOrdemOcorrenciaWorkCenter(OrdemProducaoSap ordem, OperacaoOrdemProducaoSap atual)
    {
        string plantAlvo = NormalizarChave(string.IsNullOrWhiteSpace(atual.Centro) ? ordem.Centro : atual.Centro);
        string wcAlvo = NormalizarChave(atual.CentroTrabalho);
        if (string.IsNullOrWhiteSpace(plantAlvo) || string.IsNullOrWhiteSpace(wcAlvo))
        {
            return 0;
        }

        int ocorrencia = 0;
        foreach (OperacaoOrdemProducaoSap op in OrdenarTecnicamente(ordem))
        {
            string plant = NormalizarChave(string.IsNullOrWhiteSpace(op.Centro) ? ordem.Centro : op.Centro);
            string wc = NormalizarChave(op.CentroTrabalho);
            if (string.Equals(plant, plantAlvo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(wc, wcAlvo, StringComparison.OrdinalIgnoreCase))
            {
                ocorrencia++;
            }

            if (MesmaOperacao(op, atual))
            {
                return ocorrencia;
            }
        }

        return 0;
    }

    private static string NormalizarChave(string? valor) => (valor ?? string.Empty).Trim();

    /// <summary>
    /// GATE 095F-R1 (seam 095E-R1) — operações MANUAIS da OP com a manualidade decidida por OCORRÊNCIA e
    /// SEMPRE após a correlação exata (Operation+Plant+WorkCenter): para cada operação da OP, correlaciona o
    /// roteiro V3 completo à ocorrência, reduz a UMA operação e só então aplica o Marcador (PP_FORM) sobre o
    /// roteiro REDUZIDO. Correlação 0/&gt;1 exatos ⇒ a ocorrência não é manual resolvida (fail-closed por item).
    /// O Marcador NUNCA recebe o roteiro completo. Plant efetivo = operacao.Centro ?? ordemSap.Centro.
    /// </summary>
    private static IReadOnlyList<OperacaoOrdemProducaoSap> FiltrarOperacoesManuaisCorrelacionadas(
        OrdemProducaoSap ordemSap, RoteiroProducaoSap? roteiro)
    {
        if (roteiro is null || roteiro.Operacoes.Count == 0)
        {
            return [];
        }

        List<OperacaoOrdemProducaoSap> manuais = [];
        foreach (OperacaoOrdemProducaoSap op in ordemSap.Operacoes)
        {
            OperacaoOrdemProducaoSap ocorrencia = op with
            {
                Centro = string.IsNullOrWhiteSpace(op.Centro) ? ordemSap.Centro : op.Centro
            };
            ResultadoCorrelacaoOcorrencia correlacao =
                CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, ocorrencia);
            if (correlacao.Estado != EstadoCorrelacaoOcorrencia.Correlacionada)
            {
                continue; // 0 ou >1 correlação exata: não é manual resolvida.
            }

            if (MarcadorOperacaoManualSap.ClassificarOperacao(correlacao.RoteiroReduzido, op.Operacao)
                == ClassificacaoOperacaoManual.Manual)
            {
                manuais.Add(op with { CodigoTextoPadrao = MarcadorOperacaoManualSap.CodigoTextoPadraoManual });
            }
        }

        return manuais;
    }

    /// <summary>Operação imediatamente anterior na ordenação técnica; null quando é a primeira.</summary>
    internal static OperacaoOrdemProducaoSap? ObterOperacaoAnterior(
        OrdemProducaoSap ordem, OperacaoOrdemProducaoSap atual)
    {
        IReadOnlyList<OperacaoOrdemProducaoSap> ordenadas = OrdenarTecnicamente(ordem);
        int indice = IndiceDe(ordenadas, atual);
        return indice > 0 ? ordenadas[indice - 1] : null;
    }

    /// <summary>Próxima operação na ordenação técnica; null quando é a última.</summary>
    internal static OperacaoOrdemProducaoSap? ObterProximaOperacao(
        OrdemProducaoSap ordem, OperacaoOrdemProducaoSap atual)
    {
        IReadOnlyList<OperacaoOrdemProducaoSap> ordenadas = OrdenarTecnicamente(ordem);
        int indice = IndiceDe(ordenadas, atual);
        return indice >= 0 && indice + 1 < ordenadas.Count ? ordenadas[indice + 1] : null;
    }

    private static int IndiceDe(IReadOnlyList<OperacaoOrdemProducaoSap> ordenadas, OperacaoOrdemProducaoSap alvo)
    {
        for (int i = 0; i < ordenadas.Count; i++)
        {
            if (MesmaOperacao(ordenadas[i], alvo))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool MesmaOperacao(OperacaoOrdemProducaoSap a, OperacaoOrdemProducaoSap b)
        => string.Equals(a.Sequencia, b.Sequencia, StringComparison.Ordinal)
           && string.Equals(a.Operacao, b.Operacao, StringComparison.Ordinal)
           && string.Equals(a.Suboperacao, b.Suboperacao, StringComparison.Ordinal);

    /// <summary>True quando existe apontamento CONCLUIDA para a operação (comparação técnica, sem descrição).</summary>
    internal static bool OperacaoConcluida(
        IReadOnlyList<OperacaoProducaoApontamento> apontamentos, OperacaoOrdemProducaoSap operacao)
        => apontamentos.Any(a =>
            a.Status == StatusApontamentoOperacao.Concluida
            && string.Equals(a.Sequencia, operacao.Sequencia, StringComparison.Ordinal)
            && string.Equals(a.Operacao, operacao.Operacao, StringComparison.Ordinal)
            && string.Equals(a.Suboperacao ?? string.Empty, operacao.Suboperacao ?? string.Empty, StringComparison.Ordinal));

    private static string DescreverSequencia(OperacaoOrdemProducaoSap operacao)
        => string.IsNullOrWhiteSpace(operacao.Sequencia) ? "(padrão)" : operacao.Sequencia;

    /// <summary>
    /// Resolve a operação por OP + número da operação. Considera Sequencia/Suboperacao para desempatar.
    /// NUNCA usa First() em resultado ambíguo — devolve a ambiguidade para o chamador bloquear.
    /// </summary>
    internal static ResultadoResolucaoOperacao ResolverOperacao(OrdemProducaoSap ordem, string operacaoLida)
    {
        string alvo = (operacaoLida ?? string.Empty).Trim();

        List<OperacaoOrdemProducaoSap> candidatas = ordem.Operacoes
            .Where(op => OperacaoEquivalente(op.Operacao, alvo))
            .ToList();

        return candidatas.Count switch
        {
            0 => new ResultadoResolucaoOperacao(null, false, candidatas),
            1 => new ResultadoResolucaoOperacao(candidatas[0], false, candidatas),
            _ => new ResultadoResolucaoOperacao(null, true, candidatas)
        };
    }

    /// <summary>Compara operação tolerando zeros à esquerda (ignorando zeros à esquerda), sem converter para número.</summary>
    private static bool OperacaoEquivalente(string? daOrdem, string lida)
    {
        string a = (daOrdem ?? string.Empty).Trim().TrimStart('0');
        string b = (lida ?? string.Empty).Trim().TrimStart('0');
        return a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool FalhaMapeamentoOperacoesSap(OrdemProducaoSap ordem)
        => ordem.Operacoes.Count > 0
            && ordem.Operacoes.All(op => string.IsNullOrWhiteSpace(op.Operacao));

    // =========================== Apoio ===========================

    private async Task<(OrdemProducaoSap? Ordem, ResultadoLeituraApontamento? Falha)> ConsultarOrdemAsync(
        CodigoBarrasOperacao codigo, string usuario, string estacao, CancellationToken cancellationToken)
    {
        ResultadoConsultaOrdemProducaoSap consulta;
        try
        {
            consulta = await _ordemProducaoServico.ConsultarOrdemAsync(codigo.OrdemProducao, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[Apontamento] Falha ao consultar OP: {ex.GetType().Name}");
            const string mensagem = "Não foi possível consultar a OP no SAP no momento.";
            await AuditarAsync(codigo, usuario, estacao, "FALHA_SAP", mensagem, cancellationToken);
            return (null, ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.FalhaConsultaSap, mensagem, codigo));
        }

        if (consulta.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || consulta.Ordem is null)
        {
            string mensagem = $"OP {codigo.OrdemProducao} não encontrada no SAP.";
            await AuditarAsync(codigo, usuario, estacao, "OP_INEXISTENTE", mensagem, cancellationToken);
            return (null, ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.OrdemNaoEncontrada, mensagem, codigo));
        }

        OrdemProducaoSap ordem = consulta.Ordem;
        if (!ordem.Liberada || ordem.Excluida)
        {
            string mensagem = $"OP {ordem.NumeroOrdem} não está liberada para apontamento.";
            await AuditarAsync(codigo, usuario, estacao, "OP_NAO_LIBERADA", mensagem, cancellationToken);
            return (null, ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.OrdemNaoLiberada, mensagem, codigo, ordem));
        }

        return (ordem, null);
    }

    /// <summary>Consulta SAP COMPLEMENTAR do término: falha/indisponibilidade não bloqueia a conclusão.</summary>
    private async Task<OrdemProducaoSap?> ConsultarOrdemComplementarAsync(
        CodigoBarrasOperacao codigo, CancellationToken cancellationToken)
    {
        try
        {
            ResultadoConsultaOrdemProducaoSap consulta =
                await _ordemProducaoServico.ConsultarOrdemAsync(codigo.OrdemProducao, cancellationToken);
            return consulta.Cenario == CenarioConsultaOrdemProducaoSap.Encontrada ? consulta.Ordem : null;
        }
        catch (Exception ex)
        {
            // A OP pode ter mudado de status (ou o SAP estar fora): o apontamento local não fica preso.
            System.Diagnostics.Trace.TraceInformation(
                $"[Apontamento] Consulta SAP complementar do término indisponível: {ex.GetType().Name}. "
                + "Término segue pelo apontamento local.");
            return null;
        }
    }

    private async Task<bool> EstruturaDisponivelSeguraAsync(
        IControleApontamentosRepositorio repositorio, CancellationToken cancellationToken)
    {
        try
        {
            return await repositorio.EstruturaDisponivelAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            return false;
        }
    }

    // GATE 093D: o Contexto carrega o SNAPSHOT do perfil já persistido/reidratado no apontamento
    // (apontamento.CodigoPerfilResultado) — nunca a coluna legada da configuração.
    private static ContextoApontamentoProcesso MontarContexto(
        OperacaoProducaoApontamento apontamento, string tipoProcesso)
        => new()
        {
            CodigoApontamento = apontamento.CodigoApontamento,
            NumeroOrdem = apontamento.NumeroOrdem,
            ItemOrdem = apontamento.ItemOrdem,
            Produto = apontamento.Produto,
            Sequencia = apontamento.Sequencia,
            Operacao = apontamento.Operacao,
            Suboperacao = apontamento.Suboperacao,
            DescricaoOperacao = apontamento.DescricaoOperacao,
            CentroTrabalho = apontamento.CentroTrabalho,
            TipoProcesso = tipoProcesso,
            Usuario = apontamento.UsuarioInicio,
            Estacao = apontamento.EstacaoInicio,
            IniciadoEm = apontamento.IniciadoEm?.LocalDateTime ?? DateTime.Now,
            CodigoBarrasInicio = apontamento.CodigoBarrasInicio,
            CodigoPerfilResultado = apontamento.CodigoPerfilResultado
        };

    private static ResultadoLeituraApontamento ComEstado(
        ResultadoLeituraApontamento resultado,
        IReadOnlyList<OperacaoProducaoApontamento> apontamentos,
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoes,
        OperacaoOrdemProducaoSap? operacao)
        => new()
        {
            Cenario = resultado.Cenario,
            Mensagem = resultado.Mensagem,
            Codigo = resultado.Codigo,
            Ordem = resultado.Ordem,
            Operacao = resultado.Operacao ?? operacao,
            Configuracao = resultado.Configuracao,
            Apontamento = resultado.Apontamento,
            Contexto = resultado.Contexto,
            ApontamentosDaOrdem = apontamentos,
            ConfiguracoesDaOrdem = configuracoes
        };

    /// <summary>
    /// Audita a leitura. Falha de auditoria NÃO derruba a operação, mas também não é silenciosa: fica
    /// registrada no Trace com marcador dedicado. As transições de estado bem-sucedidas gravam o evento
    /// dentro da MESMA transação no repository — este caminho é só para tentativas que não alteram estado.
    /// </summary>
    private async Task AuditarAsync(
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        string resultado,
        string mensagem,
        CancellationToken cancellationToken,
        long? codigoApontamento = null,
        string statusAnterior = "",
        string statusNovo = "",
        string correlationId = "")
    {
        try
        {
            IControleApontamentosRepositorio repositorio = _criarRepositorio();
            if (!await EstruturaDisponivelSeguraAsync(repositorio, cancellationToken))
            {
                return; // sem estrutura não há onde auditar (a tela já informa o bloqueio)
            }

            await repositorio.RegistrarEventoAsync(
                codigoApontamento, codigo, usuario, estacao,
                statusAnterior, statusNovo, resultado, mensagem, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[Apontamento][AUDITORIA_NAO_REGISTRADA] resultado={resultado}; "
                + $"codigo={codigo.CodigoOriginal}; erro={ex.GetType().Name}");
        }
    }

    private static string FormatarData(DateTimeOffset? data)
        => data?.LocalDateTime.ToString("dd/MM/yyyy HH:mm") ?? "-";
}

/// <summary>Comparador técnico de campos SAP: numérico quando ambos são numéricos, senão ordinal.</summary>
internal sealed class ComparadorCampoSap : IComparer<string>
{
    public static ComparadorCampoSap Instancia { get; } = new();

    public int Compare(string? x, string? y)
    {
        string a = (x ?? string.Empty).Trim();
        string b = (y ?? string.Empty).Trim();

        // Comparar como número evita a ordem errada de texto puro em campos numéricos com zeros à esquerda.
        if (a.Length > 0 && b.Length > 0 && a.All(char.IsAsciiDigit) && b.All(char.IsAsciiDigit)
            && long.TryParse(a, out long na) && long.TryParse(b, out long nb))
        {
            return na.CompareTo(nb);
        }

        return string.CompareOrdinal(a, b);
    }
}

/// <summary>Resolução da operação: única, ambígua (>1) ou inexistente (0).</summary>
internal sealed record ResultadoResolucaoOperacao(
    OperacaoOrdemProducaoSap? Operacao,
    bool Ambigua,
    IReadOnlyList<OperacaoOrdemProducaoSap> Candidatas);

/// <summary>Dados da confirmação pedida ao operador antes de efetivar início/retomada/término.</summary>
public sealed class ConfirmacaoApontamento
{
    public required TipoConfirmacaoApontamento Tipo { get; init; }
    public required CodigoBarrasOperacao Codigo { get; init; }
    public OrdemProducaoSap? Ordem { get; init; }
    public OperacaoOrdemProducaoSap? Operacao { get; init; }
    public OperacaoProducaoApontamento? Ativo { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Estacao { get; init; } = string.Empty;

    public static ConfirmacaoApontamento ParaInicio(
        CodigoBarrasOperacao codigo, OrdemProducaoSap ordem, OperacaoOrdemProducaoSap operacao,
        string usuario, string estacao)
        => new()
        {
            Tipo = TipoConfirmacaoApontamento.Inicio,
            Codigo = codigo,
            Ordem = ordem,
            Operacao = operacao,
            Usuario = usuario,
            Estacao = estacao
        };

    public static ConfirmacaoApontamento ParaRetomada(
        CodigoBarrasOperacao codigo, OrdemProducaoSap ordem, OperacaoOrdemProducaoSap operacao,
        OperacaoProducaoApontamento ativo)
        => new()
        {
            Tipo = TipoConfirmacaoApontamento.Retomada,
            Codigo = codigo,
            Ordem = ordem,
            Operacao = operacao,
            Ativo = ativo,
            Usuario = ativo.UsuarioInicio,
            Estacao = ativo.EstacaoInicio
        };

    public static ConfirmacaoApontamento ParaTermino(CodigoBarrasOperacao codigo, OperacaoProducaoApontamento ativo)
        => new()
        {
            Tipo = TipoConfirmacaoApontamento.Termino,
            Codigo = codigo,
            Ativo = ativo,
            Usuario = ativo.UsuarioInicio,
            Estacao = ativo.EstacaoInicio
        };
}

public enum TipoConfirmacaoApontamento
{
    Inicio,
    Retomada,
    Termino
}

