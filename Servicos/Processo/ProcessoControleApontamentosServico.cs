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
        RoteiroProducaoSap? roteiro = await _roteiroServico.ResolverRoteiroDaOrdemAsync(ordemSap, cancellationToken);
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoesManuais =
            MarcadorOperacaoManualSap.FiltrarOperacoesManuais(ordemSap, roteiro);
        OrdemProducaoSap ordemManual = ordemSap with { Operacoes = operacoesManuais };
        ClassificacaoOperacaoManual classificacao =
            MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, operacao.Operacao);

        if (classificacao == ClassificacaoOperacaoManual.ContratoNaoResolvido)
        {
            const string mensagem =
                "Não foi possível resolver o roteiro (marcador PP_FORM) desta operação no SAP. "
                + "O apontamento manual está bloqueado até a regularização.";
            await AuditarAsync(codigo, usuario, estacao, "ROTEIRO_NAO_RESOLVIDO", mensagem, cancellationToken);
            return ResultadoLeituraApontamento.Falha(
                CenarioLeituraApontamento.ContratoRoteiroNaoResolvido, mensagem, codigo, ordemManual);
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

        // ---- Destino: SEMPRE por configuração ----
        ResultadoConfiguracaoOperacao resultadoConfig = await repositorio.ObterConfiguracaoOperacaoAsync(
            ordemSap.Centro, ordemSap.TipoOrdem, operacao.Sequencia, operacao.Operacao,
            operacao.Suboperacao, operacao.CentroTrabalho, cancellationToken);

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

        // ---- Sequência: a operação anterior obrigatória precisa estar CONCLUIDA ----
        if (configuracao.ExigeOperacaoAnterior)
        {
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
            CodigoBarrasInicio = apontamento.CodigoBarrasInicio
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
