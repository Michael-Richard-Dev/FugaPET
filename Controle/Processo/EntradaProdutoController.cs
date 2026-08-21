using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Controle.Processo;

/// <summary>
/// Coordenador da tela de Entrada de Produto (ProcessoEntradaProdutoForm).
///
/// Refactor H9: este controller e o dono dos servicos usados pela tela e coordena o fluxo de
/// negocio, para que o Form apenas capture selecao e apresente dados/mensagens.
///
/// C12: a FINALIZACAO grava SOMENTE local (sem chamada automatica ao SAP). O envio ao SAP fica num
/// fluxo SEPARADO e controlado (<see cref="EnviarPesoEntradaParaSapHomologacaoAsync"/>), que cria o
/// documento de material 101, so roda em HOMOLOGACAO, com escrita habilitada e permissao ENVIAR_SAP.
/// </summary>
public sealed class EntradaProdutoController
{
    public IntegracaoEntradaSapServico Sap { get; }
    public EntradaProdutoServico EntradaProduto { get; }
    public BalancaLeituraServico BalancaLeitura { get; }
    public ImpressoraEtiquetaServico ImpressoraEtiqueta { get; }
    public ImpressaoEntradaServico Impressao { get; }
    public AutorizacaoCentroDepositoEntrada AutorizacaoCentroDeposito { get; }
    public TaraController Tara { get; }

    // Tarefa Entrada 24.1: serviço de tipo mestre (A_Product) LAZY — só construído ao consultar um pedido.
    private IProductMasterSapServico? _productMasterServico;
    private IProductMasterSapServico ProductMasterServico =>
        _productMasterServico ??= FabricaProductMasterSapServico.Criar();

    private readonly Func<bool> _ehAmbienteHomologacao;
    private readonly Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>> _carregarItensParaEnvio;

    // Seam de leitura A_ProductPlant (IsBatchManagementRequired por material × centro): produção delega à
    // fábrica; testes injetam um stub. Somente leitura; a Form/o controller nunca falam direto com HTTP.
    private readonly Func<string, string, CancellationToken, Task<ProdutoCentroSapMestre?>> _consultarProdutoCentroSap;
    private readonly Func<long, CancellationToken, Task<string?>> _obterStatusLancamento;
    private readonly Func<long, CancellationToken, Task<bool>> _reservarLancamentoParaEnvio;
    private readonly Func<CancellationToken, Task<DiagnosticoProntidaoIntegracaoSap>> _diagnosticarIntegracaoSap;
    private readonly Func<
        long,
        IReadOnlyList<ResultadoItemEnvioSap>,
        CenarioEnvioSapEntrada,
        RastreabilidadeDocumentoMaterialSap?,
        CancellationToken,
        Task<ResultadoOperacao>> _atualizarStatusAposEnvioSap;
    private readonly EntradaProdutoLotesOrquestrador _lotesOrquestrador;

    // Ctor padrao: a fabrica decide mock/real (a tela nao decide nem instancia servico SAP concreto).
    public EntradaProdutoController()
        : this(
            new IntegracaoEntradaSapServico(
                FabricaPedidoCompraSapServico.Criar(),
                FabricaMaterialDocumentSapServico.Criar()),
            new EntradaProdutoServico(),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            AutorizacaoCentroDepositoEntrada.CarregarDoAmbiente(),
            FabricaControladoresCadastro.CriarTaraController())
    {
    }

    internal EntradaProdutoController(
        IntegracaoEntradaSapServico sap,
        EntradaProdutoServico entradaProdutoServico,
        BalancaLeituraServico balancaLeituraServico,
        ImpressoraEtiquetaServico impressoraEtiquetaServico,
        AutorizacaoCentroDepositoEntrada autorizacaoCentroDeposito,
        TaraController taraController,
        Func<bool>? ehAmbienteHomologacao = null,
        Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>>? carregarItensParaEnvio = null,
        Func<string, string, CancellationToken, Task<ProdutoCentroSapMestre?>>? consultarProdutoCentroSap = null,
        Func<long, CancellationToken, Task<string?>>? obterStatusLancamento = null,
        Func<long, CancellationToken, Task<bool>>? reservarLancamentoParaEnvio = null,
        Func<CancellationToken, Task<DiagnosticoProntidaoIntegracaoSap>>? diagnosticarIntegracaoSap = null,
        Func<
            long,
            IReadOnlyList<ResultadoItemEnvioSap>,
            CenarioEnvioSapEntrada,
            RastreabilidadeDocumentoMaterialSap?,
            CancellationToken,
            Task<ResultadoOperacao>>? atualizarStatusAposEnvioSap = null,
        EntradaProdutoLotesOrquestrador? lotesOrquestrador = null)
    {
        Sap = sap ?? throw new ArgumentNullException(nameof(sap));
        EntradaProduto = entradaProdutoServico ?? throw new ArgumentNullException(nameof(entradaProdutoServico));
        BalancaLeitura = balancaLeituraServico ?? throw new ArgumentNullException(nameof(balancaLeituraServico));
        ImpressoraEtiqueta = impressoraEtiquetaServico ?? throw new ArgumentNullException(nameof(impressoraEtiquetaServico));
        Impressao = new ImpressaoEntradaServico(ImpressoraEtiqueta);
        AutorizacaoCentroDeposito = autorizacaoCentroDeposito ?? throw new ArgumentNullException(nameof(autorizacaoCentroDeposito));
        Tara = taraController ?? throw new ArgumentNullException(nameof(taraController));
        _ehAmbienteHomologacao = ehAmbienteHomologacao ?? AmbienteIntegracaoSap.EhHomologacao;
        _carregarItensParaEnvio = carregarItensParaEnvio
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ListarItensParaEnvioSapAsync(codigoLancamento, cancellationToken));
        _consultarProdutoCentroSap = consultarProdutoCentroSap
            ?? ((material, centro, cancellationToken) =>
                FabricaProductPlantSapServico.Criar().ObterProdutoCentroAsync(material, centro, cancellationToken));
        _obterStatusLancamento = obterStatusLancamento
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ObterStatusLancamentoAsync(codigoLancamento, cancellationToken));
        _reservarLancamentoParaEnvio = reservarLancamentoParaEnvio
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.TentarReservarLancamentoParaEnvioSapAsync(codigoLancamento, cancellationToken));
        _diagnosticarIntegracaoSap =
            diagnosticarIntegracaoSap ?? Sap.DiagnosticarProntidaoEscritaAsync;
        _atualizarStatusAposEnvioSap =
            atualizarStatusAposEnvioSap ?? EntradaProduto.AtualizarStatusAposEnvioSapAsync;
        _lotesOrquestrador = lotesOrquestrador ?? new EntradaProdutoLotesOrquestrador();
    }

    public EstadoOperacaoEntradaProdutoLotes IniciarOperacaoComLotes(
        ContextoOperacaoEntradaProdutoLotes contexto,
        IReadOnlyList<PedidoCompraSapItem> itensSap)
        => _lotesOrquestrador.IniciarOperacao(contexto, itensSap);

    public EstadoOperacaoEntradaProdutoLotes SelecionarItemOperacaoComLotes(long codigoSapPedidoCompraItem)
        => _lotesOrquestrador.SelecionarItem(codigoSapPedidoCompraItem);

    public EstadoOperacaoEntradaProdutoLotes ConfirmarLoteOperacaoComLotes(
        long codigoSapPedidoCompraItem,
        string? numeroLote,
        DateTime? dataFabricacao,
        DateTime? dataVencimento)
        => _lotesOrquestrador.ConfirmarLote(
            codigoSapPedidoCompraItem,
            numeroLote,
            dataFabricacao,
            dataVencimento);

    public EntradaProdutoPesagemEmMemoria RegistrarPesagemOperacaoComLotes(
        long codigoSapPedidoCompraItem,
        decimal pesoBrutoKg,
        decimal pesoTaraKg,
        long? codigoTara,
        string origem,
        long? codigoBalanca,
        string? leituraOriginal = null,
        DateTimeOffset? pesadoEm = null)
        => _lotesOrquestrador.RegistrarPesagemNoLoteAtivo(
            codigoSapPedidoCompraItem,
            pesoBrutoKg,
            pesoTaraKg,
            codigoTara,
            origem,
            codigoBalanca,
            leituraOriginal,
            pesadoEm);

    public int CancelarPesagensOperacaoComLotes(long codigoSapPedidoCompraItem)
        => _lotesOrquestrador.CancelarPesagensDoLoteAtivo(codigoSapPedidoCompraItem);

    public EntradaProdutoPesagemEmMemoria CancelarPesagemOperacaoComLotes(
        long codigoSapPedidoCompraItem,
        Guid codigoLocalPesagem)
        => _lotesOrquestrador.CancelarPesagemDoLoteAtivo(codigoSapPedidoCompraItem, codigoLocalPesagem);

    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> ObterPesagensItemOperacaoComLotes(long codigoSapPedidoCompraItem)
        => _lotesOrquestrador.ObterPesagensDoItem(codigoSapPedidoCompraItem);

    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> ObterPesagensLoteAtivoOperacaoComLotes(long codigoSapPedidoCompraItem)
        => _lotesOrquestrador.ObterPesagensDoLoteAtivo(codigoSapPedidoCompraItem);

    public EstadoOperacaoEntradaProdutoLotes FinalizarLoteOperacaoComLotes(long codigoSapPedidoCompraItem)
        => _lotesOrquestrador.FinalizarLoteAtivo(codigoSapPedidoCompraItem);

    public EntradaProdutoLancamentoComLotesPersistencia MontarLancamentoComLotesParaPersistencia()
        => _lotesOrquestrador.MontarLancamentoComLotesParaPersistencia();

    public Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia entrada,
        CancellationToken cancellationToken = default)
        => EntradaProduto.RegistrarOuRecuperarLancamentoComLotesAsync(entrada, cancellationToken);

    public EstadoOperacaoEntradaProdutoLotes ObterEstadoOperacaoComLotes()
        => _lotesOrquestrador.ObterEstado();

    public void LimparOperacaoComLotes()
        => _lotesOrquestrador.LimparOperacao();

    public async Task<DiagnosticoEnvioSapEntrada> DiagnosticarEnvioSapEntradaAsync(
        long? codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        bool ambienteHomologacao = _ehAmbienteHomologacao();
        bool usuarioTemPermissao =
            AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.EnviarSap);

        DiagnosticoProntidaoIntegracaoSap integracao =
            await _diagnosticarIntegracaoSap(cancellationToken);

        int totalItensPersistidos = 0;
        string? falhaItens = null;
        if (codigoLancamento is long codigo
            && codigo > 0
            && ambienteHomologacao
            && usuarioTemPermissao
            && integracao.AmbienteOperacional
            && integracao.IntegracaoAtiva
            && integracao.SapConfigurado
            && integracao.EscritaSapHabilitada
            && integracao.MaterialDocumentConfigurado)
        {
            try
            {
                totalItensPersistidos =
                    (await _carregarItensParaEnvio(codigo, cancellationToken)).Count;
            }
            catch
            {
                falhaItens = "Não foi possível validar os itens persistidos do lançamento.";
            }
        }

        string? motivoBloqueio = codigoLancamento is not long id || id <= 0
            ? "Finalize e grave o lançamento local antes do envio."
            : !ambienteHomologacao
                ? "O ambiente atual não é homologação."
                : !usuarioTemPermissao
                    ? "Usuário sem permissão ENVIAR_SAP."
                    : !integracao.AmbienteOperacional
                        ? integracao.MotivoBloqueio ?? "Integração SAP indisponível neste ambiente."
                        : !integracao.IntegracaoAtiva
                            ? integracao.MotivoBloqueio ?? "Integração SAP inativa."
                            : !integracao.SapConfigurado
                                ? "Configuração SAP indisponível."
                                : !integracao.EscritaSapHabilitada
                                    ? "Escrita SAP desabilitada."
                                    : !integracao.MaterialDocumentConfigurado
                                        ? ConfiguracaoSap.MensagemMaterialDocumentNaoConfigurado
                                        : falhaItens
                                            ?? (totalItensPersistidos == 0
                                                ? "O lançamento não possui itens persistidos elegíveis."
                                                : null);

        return new DiagnosticoEnvioSapEntrada
        {
            PodeEnviar = motivoBloqueio is null,
            MotivoBloqueio = motivoBloqueio,
            CodigoLancamento = codigoLancamento,
            TotalItensPersistidos = totalItensPersistidos,
            AmbienteHomologacao = ambienteHomologacao,
            UsuarioTemPermissao = usuarioTemPermissao,
            SapConfigurado = integracao.SapConfigurado,
            EscritaSapHabilitada = integracao.EscritaSapHabilitada,
            MaterialDocumentConfigurado = integracao.MaterialDocumentConfigurado,
            IntegracaoSapAtiva = integracao.IntegracaoAtiva
        };
    }

    /// <summary>
    /// C12: finaliza a leitura gravando SOMENTE local (rastreabilidade completa). NAO chama o SAP —
    /// a criacao do documento de material e um fluxo separado e controlado. Nao lanca: devolve um
    /// <see cref="ResultadoFinalizacaoEntrada"/> que a tela apenas apresenta.
    /// </summary>
    public async Task<ResultadoFinalizacaoEntrada> FinalizarLeituraAsync(
        EntradaProdutoLancamento lancamento,
        CancellationToken cancellationToken = default)
    {
        if (lancamento.Itens.Count == 0)
        {
            return new ResultadoFinalizacaoEntrada { Cenario = CenarioFinalizacaoEntrada.NenhumaLeitura };
        }

        try
        {
            ResultadoOperacao resultadoLancamento =
                await EntradaProduto.RegistrarLancamentoAsync(lancamento, cancellationToken);
            if (!resultadoLancamento.Sucesso)
            {
                return new ResultadoFinalizacaoEntrada
                {
                    Cenario = CenarioFinalizacaoEntrada.LancamentoNaoGravado,
                    MensagemFalhaLancamento = resultadoLancamento.Mensagem
                };
            }

            return new ResultadoFinalizacaoEntrada
            {
                Cenario = CenarioFinalizacaoEntrada.GravadoLocal,
                CodigoLancamento = resultadoLancamento.IdGerado ?? 0,
                Gravados = lancamento.Itens.Count
            };
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico($"ERRO ao gravar pesagens.{Environment.NewLine}{ex}");
            return new ResultadoFinalizacaoEntrada { Cenario = CenarioFinalizacaoEntrada.ErroAoGravar };
        }
    }

    /// <summary>
    /// Envio CONTROLADO da Entrada ao SAP de homologacao: criacao de documento de material 101
    /// (API_MATERIAL_DOCUMENT_SRV), SEPARADO da finalizacao local. So executa quando TODAS as travas
    /// estao habilitadas: ambiente HOMOLOGACAO, permissao ENVIAR_SAP, escrita habilitada
    /// (FUGAPET_SAP_WRITE_ENABLED) e Material Document configurado. A integracao ativa, a configuracao
    /// SAP e a chave de escrita sao reaplicadas pelo servico governado. Defesa de reenvio: lancamento
    /// ja CONFIRMADO_SAP ou CANCELADO aborta ANTES de carregar itens, montar payload ou fazer POST.
    /// Registra o resultado de forma sanitizada (sem segredo/payload).
    /// </summary>
    public async Task<ResultadoEnvioSapEntrada> EnviarPesoEntradaParaSapHomologacaoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        DiagnosticoEnvioSapEntrada diagnostico =
            await DiagnosticarEnvioSapEntradaAsync(codigoLancamento, cancellationToken);

        // Defesa de reenvio/duplicacao: o status atual do lancamento tem prioridade sobre qualquer
        // outro bloqueio. Se ja CONFIRMADO_SAP/CANCELADO, nao carrega itens, nao monta payload e nao
        // chama CriarDocumentoMaterialEntradaAsync.
        string? statusLancamento = await ObterStatusLancamentoSeguroAsync(codigoLancamento, cancellationToken);
        ResultadoEnvioSapEntrada? bloqueioStatus = ValidarStatusLancamento(statusLancamento);
        if (bloqueioStatus is not null)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): {bloqueioStatus.Mensagem}");
            return bloqueioStatus;
        }

        if (!diagnostico.PodeEnviar)
        {
            CenarioEnvioSapEntrada cenarioBloqueio = ObterCenarioBloqueio(diagnostico);
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): {diagnostico.MotivoBloqueio}");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = cenarioBloqueio,
                Total = diagnostico.TotalItensPersistidos,
                Mensagem = diagnostico.MotivoBloqueio
            };
        }

        IReadOnlyList<EntradaProdutoItemEnvioSap> itens;
        try
        {
            itens = await _carregarItensParaEnvio(codigoLancamento, cancellationToken);
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha ao carregar itens do lancamento {codigoLancamento}.{Environment.NewLine}{ex}");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.Falha };
        }

        if (itens.Count == 0)
        {
            Sap.RegistrarDiagnostico($"Envio SAP bloqueado: lancamento {codigoLancamento} sem itens gravados.");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.LancamentoSemItens };
        }

        // Pre-POST: prepara as posicoes 101 de forma CONDICIONAL a administracao de lote SAP
        // (A_ProductPlant / IsBatchManagementRequired, consultado 1x por material+centro). Bloqueia SEM POST
        // quando faltam dados/datas obrigatorios OU quando a administracao de lote e indeterminada (nunca
        // assume true/false). Material administrado por lote -> posicao por lote com Batch/datas; nao
        // administrado -> consolida sem Batch/datas. Nenhuma chamada Material Document acontece aqui.
        string numeroPedido = itens[0].NumeroPedido.Trim();
        ResultadoPreparacaoPayloadEntrada preparacao =
            await PreparadorPayloadMaterialDocumentEntrada.PrepararAsync(
                codigoLancamento, numeroPedido, itens, _consultarProdutoCentroSap, cancellationToken);
        if (preparacao.Bloqueio is not null)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): {preparacao.Bloqueio.Mensagem}");
            return preparacao.Bloqueio;
        }

        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes = preparacao.Posicoes!;

        // Tarefa Entrada 23.2 (Ajuste 5): coerência quantidade SAP × peso líquido ANTES da reserva/POST.
        ResultadoEnvioSapEntrada? bloqueioQuantidade = ValidarCoerenciaQuantidadeSap(codigoLancamento, itens);
        if (bloqueioQuantidade is not null)
        {
            return bloqueioQuantidade;
        }

        // Reserva/claim ATOMICO antes de montar o payload e antes do POST (concorrencia/idempotencia):
        // FINALIZADO_LOCAL/ERRO_SAP -> ENVIADO_SAP em um unico UPDATE condicional. Se 0 linhas, outro
        // envio ja reservou (ou o status mudou): aborta SEM POST.
        bool reservado;
        try
        {
            reservado = await _reservarLancamentoParaEnvio(codigoLancamento, cancellationToken);
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha ao reservar o lancamento {codigoLancamento}.{Environment.NewLine}{ex}");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.Falha,
                Total = itens.Count,
                Mensagem = "SAP HML: FALHA — não foi possível reservar o lançamento para envio."
            };
        }

        if (!reservado)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): reserva nao obtida (em processamento ou ja confirmado).");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.EnvioEmProcessamento,
                Total = itens.Count,
                Mensagem = "Envio já bloqueado ou em processamento. Aguarde a conclusão."
            };
        }

        // Um UNICO documento de material por lancamento (movimento 101), com as posicoes ja preparadas
        // (uma por lote quando administrado por lote; consolidada quando nao administrado).
        MaterialDocumentSapRequest requisicao =
            MontarRequisicaoMaterialDocument(numeroPedido, codigoLancamento, posicoes);
        string chaveNegocio = $"{numeroPedido}/{codigoLancamento}";
        RegistrarDiagnosticoEnvioKg(codigoLancamento, itens);

        // Apos a reserva, uma falha de POST e tratada como FALHA SAP (status volta a ERRO_SAP, abaixo)
        // para liberar reenvio futuro — em vez de deixar o lancamento preso em ENVIADO_SAP.
        ResultadoMaterialDocumentSap resultadoSap;
        try
        {
            resultadoSap = await Sap.CriarDocumentoMaterialEntradaAsync(
                requisicao, chaveNegocio, cancellationToken);
        }
        catch (Exception ex)
        {
            string mensagemTecnicaSanitizada = MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(ex);
            string mensagemFalhaTecnica = "SAP HML: FALHA — documento de material não criado. "
                + mensagemTecnicaSanitizada;
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha tecnica ao criar documento de material (lancamento {codigoLancamento}). "
                + mensagemTecnicaSanitizada);
            resultadoSap = ResultadoMaterialDocumentSap.Falha(null, mensagemFalhaTecnica);
        }

        // O documento e atomico: ou cria com todos os itens, ou nenhum. O status local usa o
        // numero_item ORIGINAL do banco (ex.: "10"), nao a forma SAP de 5 digitos ("00010").
        bool sucesso = resultadoSap.Sucesso;

        // 2xx SEM MaterialDocument/MaterialDocumentYear (etapa PARSE_RESPOSTA): o SAP provavelmente
        // CRIOU o documento, mas sem rastreabilidade confirmavel. NAO marcar ERRO_SAP (liberaria
        // reenvio/duplicacao): mantem o lancamento reservado (ENVIADO_SAP) e sinaliza divergencia
        // critica — operador nao deve reenviar sem suporte.
        if (!sucesso
            && string.Equals(resultadoSap.Etapa, MaterialDocumentSapApiClient.EtapaParse, StringComparison.Ordinal)
            && resultadoSap.StatusHttp is >= 200 and < 300)
        {
            await Sap.RegistrarFalhaStatusLocalAposSapAsync(
                codigoLancamento, resultadoSap.MensagemSanitizada, CancellationToken.None);
            Sap.RegistrarDiagnostico(
                $"CRITICO envio SAP lancamento {codigoLancamento}: SAP respondeu 2xx sem "
                + "MaterialDocument/MaterialDocumentYear. O documento pode ter sido criado. NAO reenviar sem suporte.");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.FalhaPersistenciaLocal,
                Total = itens.Count,
                StatusLocalAtualizado = false,
                MensagemCritica = resultadoSap.MensagemSanitizada,
                Mensagem = "SAP HML: FALHA CRÍTICA — resposta sem documento de material. Não reenviar sem suporte."
            };
        }

        // Status local e por ITEM (numero_item); como agora ha uma posicao por lote, varios itens do
        // payload compartilham o mesmo numero_item — deduplica para o UPDATE de status nao repetir item.
        List<ResultadoItemEnvioSap> resultados = itens
            .Select(item => item.NumeroItem)
            .Distinct(StringComparer.Ordinal)
            .Select(numeroItem => new ResultadoItemEnvioSap(
                numeroItem, sucesso, resultadoSap.MensagemSanitizada))
            .ToList();
        CenarioEnvioSapEntrada cenario =
            sucesso ? CenarioEnvioSapEntrada.Enviado : CenarioEnvioSapEntrada.Falha;

        // No sucesso, persiste a rastreabilidade do documento material na MESMA transacao do status.
        RastreabilidadeDocumentoMaterialSap? rastreabilidade = sucesso
            ? new RastreabilidadeDocumentoMaterialSap(
                resultadoSap.MaterialDocument,
                resultadoSap.MaterialDocumentYear,
                resultadoSap.ItensDocumento)
            : null;

        ResultadoOperacao atualizacaoLocal = await _atualizarStatusAposEnvioSap(
            codigoLancamento,
            resultados,
            cenario,
            rastreabilidade,
            cancellationToken);
        if (!atualizacaoLocal.Sucesso)
        {
            await Sap.RegistrarFalhaStatusLocalAposSapAsync(
                codigoLancamento,
                atualizacaoLocal.Mensagem,
                CancellationToken.None);
            Sap.RegistrarDiagnostico(
                $"CRITICO envio SAP lancamento {codigoLancamento}: documento de material respondido, "
                + "mas a atualizacao do status local falhou. NAO reenviar sem suporte.");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.FalhaPersistenciaLocal,
                Enviados = sucesso ? itens.Count : 0,
                Total = itens.Count,
                Itens = resultados,
                StatusLocalAtualizado = false,
                MaterialDocument = resultadoSap.MaterialDocument,
                MaterialDocumentYear = resultadoSap.MaterialDocumentYear,
                MensagemCritica = atualizacaoLocal.Mensagem
            };
        }

        if (sucesso)
        {
            // Rastreabilidade SAP (documento_material_sap / exercicio_documento_material_sap /
            // enviado_sap_em) gravada no lancamento na MESMA transacao do CONFIRMADO_SAP.
            Sap.RegistrarDiagnostico(
                $"Envio SAP confirmado: documento material {resultadoSap.MaterialDocument}/{resultadoSap.MaterialDocumentYear} "
                + $"gravado no lancamento {codigoLancamento}.");
        }

        string mensagemFalhaSap = string.IsNullOrWhiteSpace(resultadoSap.MensagemSanitizada)
            ? "SAP HML: FALHA — documento de material não criado."
            : resultadoSap.MensagemSanitizada;

        return new ResultadoEnvioSapEntrada
        {
            Cenario = cenario,
            Enviados = sucesso ? itens.Count : 0,
            Total = itens.Count,
            Itens = resultados,
            StatusLocalAtualizado = true,
            MaterialDocument = resultadoSap.MaterialDocument,
            MaterialDocumentYear = resultadoSap.MaterialDocumentYear,
            MensagemCritica = sucesso ? null : mensagemFalhaSap,
            Mensagem = sucesso
                ? $"SAP HML: ENVIADO — documento material {resultadoSap.MaterialDocument}/{resultadoSap.MaterialDocumentYear}"
                : mensagemFalhaSap
        };
    }

    // ---- Material Document (movimento 101): montagem e validacao pre-POST ----

    private async Task<string?> ObterStatusLancamentoSeguroAsync(
        long codigoLancamento,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _obterStatusLancamento(codigoLancamento, cancellationToken);
        }
        catch (Exception ex)
        {
            // Sem o status, NAO inferimos "ok": o filtro de status do SQL de itens
            // (ListarItensParaEnvioSapAsync) ainda impede itens confirmados/cancelados no payload.
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha ao ler status do lancamento {codigoLancamento}.{Environment.NewLine}{ex}");
            return null;
        }
    }

    /// <summary>Bloqueia reenvio quando o lancamento ja esta CONFIRMADO_SAP ou foi CANCELADO.</summary>
    private static ResultadoEnvioSapEntrada? ValidarStatusLancamento(string? statusLancamento)
    {
        string status = (statusLancamento ?? string.Empty).Trim().ToUpperInvariant();
        return status switch
        {
            "CONFIRMADO_SAP" => new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.LancamentoJaConfirmadoSap,
                Mensagem = "Lançamento já confirmado no SAP. Reenvio bloqueado."
            },
            "CANCELADO" => new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.LancamentoCancelado,
                Mensagem = "Lançamento cancelado. Envio bloqueado."
            },
            _ => null
        };
    }

    // A preparacao das posicoes e a validacao condicional (por administracao de lote SAP) ficam em
    // PreparadorPayloadMaterialDocumentEntrada — nao ha mais uma validacao unica que exige lote/datas
    // para todos os itens (isso quebrava material NAO administrado por lote: MM_IM_ODATA_API_MDOC/014).

    // Tarefa Entrada 23.2 (Ajuste 6): diagnóstico completo antes do POST 101 — peso bruto, tara (derivada
    // de bruto-líquido), tara em KG, peso líquido, QuantityInEntryUnit final e EntryUnit.
    private void RegistrarDiagnosticoEnvioKg(
        long codigoLancamento,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens)
    {
        foreach (EntradaProdutoItemEnvioSap item in itens)
        {
            string itemSap = NormalizarItemSap(item.NumeroItem);
            string unidadePedido = string.IsNullOrWhiteSpace(item.Unidade)
                ? "(não informada)"
                : item.Unidade.Trim().ToUpperInvariant();
            decimal taraKg = item.PesoBrutoKg - item.PesoLiquidoKg;
            string quantidadeSap = FormatarQuantidade(item.PesoLiquidoKg);

            Sap.RegistrarDiagnostico(
                $"Entrada 101 balanca lancamento {codigoLancamento}: Item {itemSap}; "
                + $"Material {item.Material?.Trim()}; Centro {item.Centro?.Trim()}; Deposito {item.Deposito?.Trim()}; "
                + $"Peso bruto {FormatarQuantidade(item.PesoBrutoKg)} KG; Tara {FormatarQuantidade(taraKg)} KG; "
                + $"Tara convertida {FormatarQuantidade(taraKg)} KG; Peso liquido {quantidadeSap} KG; "
                + $"QuantityInEntryUnit {quantidadeSap}; Quantidade SAP: {quantidadeSap} KG. EntryUnit SAP: KG. "
                + $"Unidade original do pedido {unidadePedido}.");
        }
    }

    // Tarefa Entrada 23.2 (Ajuste 5): guarda preventiva — a quantidade SAP formatada deve bater com o peso
    // líquido calculado (tolerância 0,001 KG). Protege contra qualquer regressão de conversão/formatação
    // (ex.: 1,5 virar 1500) ANTES da reserva/POST. Nenhuma chamada ao SAP acontece aqui.
    private ResultadoEnvioSapEntrada? ValidarCoerenciaQuantidadeSap(
        long codigoLancamento,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens)
    {
        foreach (EntradaProdutoItemEnvioSap item in itens)
        {
            string quantidadeTexto = FormatarQuantidade(item.PesoLiquidoKg);
            decimal quantidadeSapKg = decimal.Parse(
                quantidadeTexto, System.Globalization.CultureInfo.InvariantCulture);

            if (!EntradaProdutoQuantidadeSap.QuantidadeCoerente(quantidadeSapKg, item.PesoLiquidoKg))
            {
                Sap.RegistrarDiagnostico(
                    $"Entrada 101 BLOQUEADO (lancamento {codigoLancamento}): Item {NormalizarItemSap(item.NumeroItem)} "
                    + $"quantidade SAP {quantidadeTexto} divergente do peso liquido "
                    + $"{FormatarQuantidade(item.PesoLiquidoKg)} KG (bruto {FormatarQuantidade(item.PesoBrutoKg)} KG).");
                return new ResultadoEnvioSapEntrada
                {
                    Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                    Total = itens.Count,
                    Mensagem = "Quantidade SAP divergente do peso líquido calculado. "
                        + "Envio bloqueado para evitar entrada incorreta."
                };
            }
        }

        return null;
    }

    // Monta UMA posicao (item) do documento de material 101 a partir de um lote/representante. comLote
    // controla Batch/ManufactureDate/ShelfLifeExpirationDate (material ADMINISTRADO por lote no centro).
    // Os literais do movimento 101 (101/B/EntryUnit KG/QuantityInEntryUnit) vivem AQUI, no controller.
    internal static MaterialDocumentSapItemRequest MontarItemMaterialDocument(
        string numeroPedido, EntradaProdutoItemEnvioSap item, bool comLote)
        => new()
        {
            Material = item.Material!.Trim(),
            Plant = item.Centro!.Trim(),
            StorageLocation = item.Deposito!.Trim(),
            GoodsMovementType = "101",
            GoodsMovementRefDocType = "B", // referencia = Pedido de Compra (exigido pelo SAP no 101)
            QuantityInEntryUnit = FormatarQuantidade(item.PesoLiquidoKg),
            EntryUnit = "KG",
            PurchaseOrder = numeroPedido,
            PurchaseOrderItem = NormalizarItemSap(item.NumeroItem),
            // Batch/datas SOMENTE quando o material e administrado por lote no centro (A_ProductPlant).
            Batch = comLote && !string.IsNullOrWhiteSpace(item.NumeroLote) ? item.NumeroLote.Trim() : null,
            ManufactureDate = comLote ? item.DataFabricacao : null,
            ShelfLifeExpirationDate = comLote ? item.DataValidade : null
        };

    // Monta o documento 101 a partir das POSICOES ja preparadas (uma por lote quando administrado por
    // lote; consolidada quando nao administrado). Batch/datas e agrupamento ja foram decididos pelo
    // PreparadorPayloadMaterialDocumentEntrada com base em A_ProductPlant.
    private static MaterialDocumentSapRequest MontarRequisicaoMaterialDocument(
        string numeroPedido,
        long codigoLancamento,
        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes)
    {
        DateTime hoje = DateTime.Today;
        return new MaterialDocumentSapRequest
        {
            GoodsMovementCode = "01",
            PostingDate = hoje,
            DocumentDate = hoje,
            MaterialDocumentHeaderText = MontarTextoCabecalho(numeroPedido, codigoLancamento),
            Itens = posicoes.Select(posicao => posicao.Item).ToList()
        };
    }

    /// <summary>Item do pedido normalizado para o SAP: 5 digitos quando numerico ("10" -&gt; "00010").
    /// Nao inventa valor: nao-numerico ou com mais de 5 digitos e mantido (apenas trim).</summary>
    internal static string NormalizarItemSap(string numeroItem)
    {
        string valor = numeroItem.Trim();
        return valor.Length is > 0 and <= 5 && valor.All(char.IsDigit)
            ? valor.PadLeft(5, '0')
            : valor;
    }

    // Tarefa Entrada 23.2 (Ajuste 4): serialização INVARIANTE centralizada (nunca cultura pt-BR / milhar).
    private static string FormatarQuantidade(decimal pesoLiquidoKg)
        => EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(pesoLiquidoKg);

    // Facet SAP: MaterialDocumentHeaderText e Edm.String MaxLength=25. Texto curto, ASCII simples,
    // sem acentos nem caracteres especiais, com corte defensivo. Ex.: "FP 4500001253 L33".
    private const int TamanhoMaximoTextoCabecalho = 25;

    private static string MontarTextoCabecalho(string numeroPedido, long codigoLancamento)
    {
        string texto = $"FP {numeroPedido?.Trim()} L{codigoLancamento}".Trim();
        texto = RemoverAcentos(texto);
        texto = Regex.Replace(texto, "[^A-Za-z0-9 ]", string.Empty);
        texto = Regex.Replace(texto, @"\s+", " ").Trim();

        return texto.Length <= TamanhoMaximoTextoCabecalho
            ? texto
            : texto[..TamanhoMaximoTextoCabecalho].Trim();
    }

    private static string RemoverAcentos(string texto)
    {
        string normalizado = texto.Normalize(NormalizationForm.FormD);
        StringBuilder construtor = new(normalizado.Length);
        foreach (char caractere in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                construtor.Append(caractere);
            }
        }

        return construtor.ToString().Normalize(NormalizationForm.FormC);
    }

    private static CenarioEnvioSapEntrada ObterCenarioBloqueio(
        DiagnosticoEnvioSapEntrada diagnostico)
    {
        if (!diagnostico.AmbienteHomologacao)
        {
            return CenarioEnvioSapEntrada.AmbienteNaoHomologacao;
        }

        if (!diagnostico.UsuarioTemPermissao)
        {
            return CenarioEnvioSapEntrada.SemPermissao;
        }

        if (!diagnostico.IntegracaoSapAtiva)
        {
            return CenarioEnvioSapEntrada.IntegracaoInativa;
        }

        if (!diagnostico.SapConfigurado)
        {
            return CenarioEnvioSapEntrada.SapNaoConfigurado;
        }

        if (!diagnostico.EscritaSapHabilitada)
        {
            return CenarioEnvioSapEntrada.EscritaDesabilitada;
        }

        if (!diagnostico.MaterialDocumentConfigurado)
        {
            return CenarioEnvioSapEntrada.MaterialDocumentNaoConfigurado;
        }

        return CenarioEnvioSapEntrada.LancamentoSemItens;
    }

    /// <summary>
    /// Consulta um pedido especifico no SAP (sincroniza o cache local), le os dados de cabecalho e
    /// os itens, e aplica o filtro de escopo (centro/deposito autorizado). Devolve apenas dados; a
    /// concorrencia de UI (cancelar consulta anterior, gate, duplo clique) fica na tela.
    /// </summary>
    public async Task<ResultadoConsultaPedido> ConsultarPedidoAsync(
        string numeroPedido,
        FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo =
            FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.MateriaPrima,
        CancellationToken cancellationToken = default)
    {
        // 1. CACHE LOCAL primeiro: resposta rapida e SEM chamar o SAP quando o pedido ja foi
        //    pre-carregado em segundo plano (PreCarregamentoPedidosEntradaServico).
        PedidoCompraSapAgregado? pedido =
            await Sap.ObterPedidoAgregadoAsync(
                numeroPedido,
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string mensagemSucesso = "Pedido carregado do cache local.";

        // 2. CACHE MISS / pedido ausente: fallback de GET ESPECIFICO no SAP e nova leitura do cache.
        if (pedido is null)
        {
            ResultadoOperacao sincronizacao =
                await Sap.SincronizarPedidoAsync(numeroPedido, cancellationToken);
            if (!sincronizacao.Sucesso)
            {
                return new ResultadoConsultaPedido
                {
                    Sucesso = false,
                    Mensagem = "Pedido não encontrado no cache local e SAP indisponível no momento."
                };
            }

            mensagemSucesso = sincronizacao.Mensagem;
            pedido = await Sap.ObterPedidoAgregadoAsync(
                numeroPedido,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (pedido is null)
            {
                return new ResultadoConsultaPedido
                {
                    Sucesso = false,
                    Mensagem = "Pedido sincronizado, mas nao encontrado no cache local."
                };
            }
        }

        // Tarefa Entrada 23.1: valida APROVACAO/LIBERACAO no SAP (cabecalho FRESCO, sem confiar no cache
        // que nao guarda o status). Pedido nao liberado NAO carrega itens operacionais (Ajuste 6).
        PedidoCompraSap? cabecalhoSap =
            await Sap.ObterCabecalhoSapParaValidacaoAsync(numeroPedido, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ResultadoValidacaoPedidoCompra validacao = ValidadorLiberacaoPedidoCompra.Validar(cabecalhoSap);

        if (!validacao.Liberado)
        {
            return new ResultadoConsultaPedido
            {
                Sucesso = true,
                Mensagem = validacao.MotivoBloqueio,
                NumeroPedido = pedido.NumeroPedido,
                Fornecedor = pedido.Fornecedor,
                DataPedido = pedido.DataPedido,
                TipoPedido = pedido.TipoPedido,
                ItensAutorizados = [], // Ajuste 6: nada operacional para pedido nao liberado
                ItensOcultados = 0,
                PedidoLiberado = false,
                MotivoBloqueioLiberacao = validacao.MotivoBloqueio,
                StatusProcessamento = validacao.CodigoStatus,
                DescricaoStatusProcessamento = validacao.DescricaoStatus,
                LiberacaoNaoConcluida = validacao.LiberacaoNaoConcluida
            };
        }

        // Escopo Jales (H5): exibe apenas itens dentro do centro/deposito autorizado.
        IReadOnlyList<PedidoCompraSapItem> itensAutorizados = pedido.Itens
            .Where(item => AutorizacaoCentroDeposito.ItemAutorizado(item.Centro, item.Deposito))
            .ToList();

        // Tarefa Entrada 24.1: enriquece cada item com o ProductType (A_Product) e classifica; filtra pelo MODO.
        IReadOnlyList<PedidoCompraSapItem> itensClassificados =
            await EnriquecerEClassificarItensAsync(pedido.NumeroPedido, modo, itensAutorizados, cancellationToken);
        IReadOnlyList<PedidoCompraSapItem> itensDoModo =
            FugaPET_Dev.Modelo.Processo.FiltroItensEntradaMaterial.FiltrarItensPorModo(itensClassificados, modo);
        RegistrarDiagnosticoClassificacaoEntrada(pedido.NumeroPedido, modo, itensClassificados);

        // Ajuste 15: todos os itens Indefinidos (Product Master não classificou) → bloqueia por segurança.
        if (FugaPET_Dev.Modelo.Processo.FiltroItensEntradaMaterial.TodosIndefinidos(itensClassificados))
        {
            return BloquearPedidoPorModo(
                pedido, validacao,
                FugaPET_Dev.Modelo.Processo.FiltroItensEntradaMaterial.MensagemTodosIndefinidos);
        }

        // Ajuste 9: pedido sem NENHUM item do modo atual → alerta, não carrega grid vazia como sucesso.
        if (itensDoModo.Count == 0)
        {
            return BloquearPedidoPorModo(
                pedido, validacao,
                FugaPET_Dev.Modelo.Processo.FiltroItensEntradaMaterial.MontarMensagemSemItensDoModo(
                    modo, pedido.NumeroPedido, itensClassificados.Count));
        }

        return new ResultadoConsultaPedido
        {
            Sucesso = true,
            Mensagem = mensagemSucesso,
            NumeroPedido = pedido.NumeroPedido,
            Fornecedor = pedido.Fornecedor,
            DataPedido = pedido.DataPedido,
            TipoPedido = pedido.TipoPedido,
            ItensAutorizados = itensDoModo, // Ajuste 10/11: só os itens do modo entram na operação
            ItensOcultados = pedido.Itens.Count - itensDoModo.Count,
            PedidoLiberado = true,
            PedidoTemItensDoModo = true,
            StatusProcessamento = validacao.CodigoStatus,
            DescricaoStatusProcessamento = validacao.DescricaoStatus
        };
    }

    // Tarefa Entrada 24.1: bloqueio da abertura operacional por MODO (mantém pedido liberado; sem itens do modo).
    private static ResultadoConsultaPedido BloquearPedidoPorModo(
        PedidoCompraSapAgregado pedido,
        ResultadoValidacaoPedidoCompra validacao,
        string mensagemBloqueio)
        => new()
        {
            Sucesso = true,
            Mensagem = mensagemBloqueio,
            NumeroPedido = pedido.NumeroPedido,
            Fornecedor = pedido.Fornecedor,
            DataPedido = pedido.DataPedido,
            TipoPedido = pedido.TipoPedido,
            ItensAutorizados = [],
            ItensOcultados = 0,
            PedidoLiberado = true,
            PedidoTemItensDoModo = false,
            MotivoBloqueioModo = mensagemBloqueio,
            StatusProcessamento = validacao.CodigoStatus,
            DescricaoStatusProcessamento = validacao.DescricaoStatus
        };

    // Tarefa Entrada 24.1 (Ajustes 6/13/15): enriquece cada item com ProductType (A_Product) e classifica.
    // Cache por MATERIAL evita chamada repetida para o mesmo código. Falha/sem tipo → Indefinido (não libera por chute).
    private async Task<IReadOnlyList<PedidoCompraSapItem>> EnriquecerEClassificarItensAsync(
        string numeroPedido,
        FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo,
        IReadOnlyList<PedidoCompraSapItem> itens,
        CancellationToken cancellationToken)
    {
        Dictionary<string, ProdutoSapMestre?> cachePorMaterial = new(StringComparer.OrdinalIgnoreCase);
        List<PedidoCompraSapItem> resultado = new(itens.Count);

        foreach (PedidoCompraSapItem item in itens)
        {
            string material = (item.CodigoMaterial ?? string.Empty).Trim();
            ProdutoSapMestre? mestre = null;
            if (!string.IsNullOrWhiteSpace(material))
            {
                if (!cachePorMaterial.TryGetValue(material, out mestre))
                {
                    mestre = await ProductMasterServico.ObterProdutoAsync(material, cancellationToken);
                    cachePorMaterial[material] = mestre;
                }
            }

            string tipo = mestre?.TipoMaterialSap ?? string.Empty;
            FugaPET_Dev.Modelo.Processo.ClassificacaoEntradaMaterial classificacao =
                FugaPET_Dev.Modelo.Processo.ClassificadorItemEntradaMaterial.ClassificarPorProductType(tipo);

            resultado.Add(item with
            {
                TipoMaterialSap = tipo,
                GrupoMaterialSap = mestre?.GrupoMaterialSap ?? string.Empty,
                UnidadeBaseSap = mestre?.UnidadeBaseSap ?? string.Empty,
                DescricaoTipoMaterial =
                    FugaPET_Dev.Modelo.Processo.ClassificadorComponenteConsumo.ObterDescricaoTipoMaterialSap(tipo),
                ClassificacaoEntrada = classificacao
            });

            // Ajuste 14: diagnóstico por item.
            Sap.RegistrarDiagnostico(
                $"[Entrada][ClassificacaoItem] Pedido {numeroPedido}; Modo {modo}; Item {item.NumeroItem.Trim()}; "
                + $"Material {material}; ProductType {tipo}; ProductGroup {mestre?.GrupoMaterialSap}; "
                + $"BaseUnit {mestre?.UnidadeBaseSap}; Classificacao {classificacao}; Origem A_Product.ProductType.");
        }

        return resultado;
    }

    // Ajuste 14: totalizadores da classificação do pedido para o modo atual.
    private void RegistrarDiagnosticoClassificacaoEntrada(
        string numeroPedido,
        FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo,
        IReadOnlyList<PedidoCompraSapItem> itens)
    {
        FugaPET_Dev.Modelo.Processo.TotaisClassificacaoEntrada totais =
            FugaPET_Dev.Modelo.Processo.FiltroItensEntradaMaterial.ContarClassificacoes(itens, modo);

        Sap.RegistrarDiagnostico(
            $"[Entrada][ClassificacaoPedido] Pedido {numeroPedido}; Modo {modo}; "
            + $"Total de itens {totais.Total}; Itens Materia-Prima {totais.MateriaPrima}; "
            + $"Itens Quimicos {totais.Quimico}; Itens Outro/Indefinido {totais.OutroOuIndefinido}; "
            + $"Itens do modo {totais.DoModo}; "
            + $"Resultado {(totais.DoModo > 0 ? "Pedido aceito para o modo" : "Pedido bloqueado para o modo")}.");
    }
}

/// <summary>Cenarios da finalizacao LOCAL (sem chamada automatica ao SAP).</summary>
public enum CenarioFinalizacaoEntrada
{
    NenhumaLeitura,
    LancamentoNaoGravado,
    GravadoLocal,
    ErroAoGravar
}

/// <summary>Resultado da finalizacao local (somente dados; a tela formata a apresentacao).</summary>
public sealed class ResultadoFinalizacaoEntrada
{
    public CenarioFinalizacaoEntrada Cenario { get; init; }
    public long? CodigoLancamento { get; init; }
    public int Gravados { get; init; }
    public string? MensagemFalhaLancamento { get; init; }
}

/// <summary>Resultado do envio controlado da Entrada ao SAP (criacao de documento de material 101).</summary>
public sealed class ResultadoEnvioSapEntrada
{
    public CenarioEnvioSapEntrada Cenario { get; init; }
    public int Enviados { get; init; }
    public int Total { get; init; }
    public IReadOnlyList<ResultadoItemEnvioSap> Itens { get; init; } = [];
    public bool StatusLocalAtualizado { get; init; }
    public string? MensagemCritica { get; init; }

    /// <summary>Mensagem amigavel do resultado/bloqueio para a tela (sucesso, unidade, dados, etc.).</summary>
    public string? Mensagem { get; init; }

    /// <summary>Numero do documento de material criado no SAP (quando houver).</summary>
    public string? MaterialDocument { get; init; }

    /// <summary>Exercicio do documento de material criado no SAP (quando houver).</summary>
    public string? MaterialDocumentYear { get; init; }
}

public sealed class DiagnosticoEnvioSapEntrada
{
    public bool PodeEnviar { get; init; }
    public string? MotivoBloqueio { get; init; }
    public long? CodigoLancamento { get; init; }
    public int TotalItensPersistidos { get; init; }
    public bool AmbienteHomologacao { get; init; }
    public bool UsuarioTemPermissao { get; init; }
    public bool SapConfigurado { get; init; }
    public bool EscritaSapHabilitada { get; init; }
    public bool MaterialDocumentConfigurado { get; init; }
    public bool IntegracaoSapAtiva { get; init; }
}

/// <summary>Resultado da consulta de um pedido (dados ja filtrados pelo escopo; a tela so apresenta).</summary>
public sealed class ResultadoConsultaPedido
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string NumeroPedido { get; init; } = string.Empty;
    public string Fornecedor { get; init; } = string.Empty;
    public DateOnly? DataPedido { get; init; }
    public string TipoPedido { get; init; } = string.Empty;
    public IReadOnlyList<PedidoCompraSapItem> ItensAutorizados { get; init; } = [];
    public int ItensOcultados { get; init; }

    // Tarefa Entrada 23.1: aprovação/liberação do pedido no SAP. PedidoLiberado default true para não
    // afetar chamadas que não passam pela validação; o fluxo real sempre o define explicitamente.
    public bool PedidoLiberado { get; init; } = true;
    public string MotivoBloqueioLiberacao { get; init; } = string.Empty;
    public string StatusProcessamento { get; init; } = string.Empty;
    public string DescricaoStatusProcessamento { get; init; } = string.Empty;
    public bool LiberacaoNaoConcluida { get; init; }

    // Tarefa Entrada 24.1: separação por modo. PedidoTemItensDoModo default true para não afetar chamadas
    // que não passam pela classificação; o fluxo real sempre o define. MotivoBloqueioModo = alerta por modo.
    public bool PedidoTemItensDoModo { get; init; } = true;
    public string MotivoBloqueioModo { get; init; } = string.Empty;
}
