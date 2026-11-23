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

    /// <summary>
    /// Tolerancias de recebimento cadastradas no centro (incremento 069).
    /// <para>
    /// Devolve o CENTRO INTEIRO de uma vez, e nao material a material: a tela carrega isso uma vez por
    /// pedido e consulta em memoria a cada pesagem. Ir ao banco por pesagem colocaria o ritmo da
    /// balanca na dependencia da rede, e uma falha no meio do recebimento viraria bloqueio de material
    /// que ESTA cadastrado.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<Modelo.Cadastro.ToleranciaRecebimentoMaterial>> ListarToleranciasRecebimentoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _listarToleranciasRecebimento(centro, cancellationToken);

    // Tarefa Entrada 24.1: serviço de tipo mestre (A_Product) LAZY — só construído ao consultar um pedido.
    private IProductMasterSapServico? _productMasterServico;
    private IProductMasterSapServico ProductMasterServico =>
        _productMasterServico ??= FabricaProductMasterSapServico.Criar();

    private readonly Func<bool> _ehAmbienteHomologacao;
    private readonly Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>> _carregarItensParaEnvio;

    // Seam de leitura A_ProductPlant (IsBatchManagementRequired por material × centro): produção delega à
    // fábrica; testes injetam um stub. Somente leitura; a Form/o controller nunca falam direto com HTTP.
    private readonly Func<string, string, CancellationToken, Task<ProdutoCentroSapMestre?>> _consultarProdutoCentroSap;
    // GATE R2: ProductType do material (classificacao ROH/HIBE) e evidencia de recebimento ja confirmado.
    private readonly Func<string, CancellationToken, Task<ProdutoSapMestre?>> _consultarProdutoMestreSap;

    private readonly Func<string, CancellationToken, Task<IReadOnlyList<Modelo.Cadastro.ToleranciaRecebimentoMaterial>>>
        _listarToleranciasRecebimento;
    private readonly Func<string, string, long, CancellationToken, Task<bool?>> _existeRecebimentoConfirmadoItemPedido;
    private readonly Func<long, CancellationToken, Task<string?>> _obterStatusLancamento;
    // 12G-D: reidratação — localiza o lançamento local elegível por pedido (read-only). Seam p/ teste.
    private readonly Func<string, CancellationToken, Task<long?>> _recuperarLancamentoLocalPorPedido;
    // 054: EXCLUIR PESAGEM — cancelamento lógico local. Seam p/ teste (default = serviço real).
    private readonly Func<long, CancellationToken, Task<Modelo.Entrada.ResultadoExclusaoPesagem>> _excluirPesagemLocal;
    private readonly Func<long, CancellationToken, Task<Modelo.Entrada.ResultadoReservaEnvioSap>> _reservarLancamentoParaEnvio;
    // GATE 096D: loader PÓS-RESERVA (status ENVIADO_SAP) — revalida o snapshot sem se auto-invalidar.
    private readonly Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>> _carregarItensReservadosParaRevalidacao;
    // GATE 096D: libera a reserva (restaura StatusAnterior) num abort pré-POST — nunca via falha SAP.
    private readonly Func<long, string, CancellationToken, Task<bool>> _liberarReservaAposAbort;
    private readonly Func<CancellationToken, Task<DiagnosticoProntidaoIntegracaoSap>> _diagnosticarIntegracaoSap;
    private readonly Func<
        long,
        IReadOnlyList<ResultadoItemEnvioSap>,
        CenarioEnvioSapEntrada,
        RastreabilidadeDocumentoMaterialSap?,
        CancellationToken,
        Task<ResultadoOperacao>> _atualizarStatusAposEnvioSap;
    private readonly EntradaProdutoLotesOrquestrador _lotesOrquestrador;
    // Capability runtime de escrita SAP 101 (12E-E-B). Singleton em memoria por padrao; injetavel em teste.
    private readonly IRuntimeSapWriteCapabilityService _capability;

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
        Func<string, CancellationToken, Task<long?>>? recuperarLancamentoLocalPorPedido = null,
        Func<long, CancellationToken, Task<Modelo.Entrada.ResultadoReservaEnvioSap>>? reservarLancamentoParaEnvio = null,
        Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>>? carregarItensReservadosParaRevalidacao = null,
        Func<long, string, CancellationToken, Task<bool>>? liberarReservaAposAbort = null,
        Func<CancellationToken, Task<DiagnosticoProntidaoIntegracaoSap>>? diagnosticarIntegracaoSap = null,
        Func<
            long,
            IReadOnlyList<ResultadoItemEnvioSap>,
            CenarioEnvioSapEntrada,
            RastreabilidadeDocumentoMaterialSap?,
            CancellationToken,
            Task<ResultadoOperacao>>? atualizarStatusAposEnvioSap = null,
        EntradaProdutoLotesOrquestrador? lotesOrquestrador = null,
        IRuntimeSapWriteCapabilityService? capability = null,
        Func<long, CancellationToken, Task<Modelo.Entrada.ResultadoExclusaoPesagem>>? excluirPesagemLocal = null,
        // GATE R2: acrescentados NO FIM da lista para nao deslocar nenhum argumento posicional existente.
        Func<string, CancellationToken, Task<ProdutoSapMestre?>>? consultarProdutoMestreSap = null,
        Func<string, string, long, CancellationToken, Task<bool?>>? existeRecebimentoConfirmadoItemPedido = null,
        // Incremento 069: tolerancia de recebimento por material. NO FIM da lista, pela mesma razao
        // dos de cima — nao deslocar nenhum argumento posicional existente.
        Func<string, CancellationToken, Task<IReadOnlyList<Modelo.Cadastro.ToleranciaRecebimentoMaterial>>>?
            listarToleranciasRecebimento = null)
    {
        Sap = sap ?? throw new ArgumentNullException(nameof(sap));
        EntradaProduto = entradaProdutoServico ?? throw new ArgumentNullException(nameof(entradaProdutoServico));
        BalancaLeitura = balancaLeituraServico ?? throw new ArgumentNullException(nameof(balancaLeituraServico));
        ImpressoraEtiqueta = impressoraEtiquetaServico ?? throw new ArgumentNullException(nameof(impressoraEtiquetaServico));
        Impressao = new ImpressaoEntradaServico(ImpressoraEtiqueta);
        AutorizacaoCentroDeposito = autorizacaoCentroDeposito ?? throw new ArgumentNullException(nameof(autorizacaoCentroDeposito));
        Tara = taraController ?? throw new ArgumentNullException(nameof(taraController));
        // A regra do envio de peso deixou de ser so o nome do banco: e homologacao OU
        // liberacao explicita do ambiente (FUGAPET_SAP_ENVIO_PESO_LIBERADO), fail-closed.
        _ehAmbienteHomologacao = ehAmbienteHomologacao ?? AmbienteIntegracaoSap.EnvioPesoPermitido;
        _carregarItensParaEnvio = carregarItensParaEnvio
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ListarItensParaEnvioSapAsync(codigoLancamento, cancellationToken));
        _consultarProdutoCentroSap = consultarProdutoCentroSap
            ?? ((material, centro, cancellationToken) =>
                FabricaProductPlantSapServico.Criar().ObterProdutoCentroAsync(material, centro, cancellationToken));
        // GATE R2: ProductType do material (A_Product) — reuso do servico/fabrica JA existentes, sem DDL.
        _consultarProdutoMestreSap = consultarProdutoMestreSap
            ?? ((material, cancellationToken) =>
                FabricaProductMasterSapServico.Criar().ObterProdutoAsync(material, cancellationToken));
        // Incremento 069: a MESMA composicao que a tela de cadastro usa (FabricaControladoresCadastro).
        // Montar outra aqui abriria a porta para as duas lerem de lugares diferentes.
        _listarToleranciasRecebimento = listarToleranciasRecebimento
            ?? (async (centro, cancellationToken) =>
            {
                ResultadoListagemTolerancia listagem = await FabricaControladoresCadastro
                    .CriarToleranciaRecebimentoMaterialServico()
                    .ListarAsync(centro, cancellationToken);

                return [.. listagem.Linhas
                    .Where(l => l.Tolerancia is not null)
                    .Select(l => l.Tolerancia!)];
            });
        // GATE R2 (P5): evidencia de recebimento ja confirmado para o MESMO item do pedido. TRI-STATE:
        // true = ja houve (bloqueia), false = nao houve (libera), null = DESCONHECIDO (bloqueia).
        // GATE R5: o seam passou a ser atendido pelo provider OPERACIONAL, que compoe tres evidencias
        // (exclusividade duravel por pedido+item, recebimentos locais e historico 101/102 no SAP) e
        // delega a decisao ao motor puro. As fontes ainda nao aprovadas resolvem como INDISPONIVEIS,
        // portanto o comportamento efetivo permanece BLOQUEANDO — agora com cenario NOMEADO em
        // diagnostico, em vez de um null anonimo.
        _existeRecebimentoConfirmadoItemPedido = existeRecebimentoConfirmadoItemPedido
            ?? CriarProvedorP5Padrao(Sap.RegistrarDiagnostico).ExisteRecebimentoConfirmadoAsync;
        _obterStatusLancamento = obterStatusLancamento
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ObterStatusLancamentoAsync(codigoLancamento, cancellationToken));
        _recuperarLancamentoLocalPorPedido = recuperarLancamentoLocalPorPedido
            ?? ((numeroPedido, cancellationToken) =>
                EntradaProduto.ObterCodigoLancamentoLocalElegivelPorPedidoAsync(numeroPedido, cancellationToken));
        _reservarLancamentoParaEnvio = reservarLancamentoParaEnvio
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.TentarReservarLancamentoParaEnvioSapAsync(codigoLancamento, cancellationToken));
        _carregarItensReservadosParaRevalidacao = carregarItensReservadosParaRevalidacao
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ListarItensReservadosParaRevalidacaoSapAsync(codigoLancamento, cancellationToken));
        _liberarReservaAposAbort = liberarReservaAposAbort
            ?? ((codigoLancamento, statusAnterior, cancellationToken) =>
                EntradaProduto.LiberarReservaEnvioSapAposAbortPrePostAsync(codigoLancamento, statusAnterior, cancellationToken));
        _diagnosticarIntegracaoSap =
            diagnosticarIntegracaoSap ?? Sap.DiagnosticarProntidaoEscritaAsync;
        _atualizarStatusAposEnvioSap =
            atualizarStatusAposEnvioSap ?? EntradaProduto.AtualizarStatusAposEnvioSapAsync;
        _lotesOrquestrador = lotesOrquestrador ?? new EntradaProdutoLotesOrquestrador();
        _capability = capability ?? RuntimeSapWriteCapability.Instancia;
        _excluirPesagemLocal = excluirPesagemLocal
            ?? ((codigoPesagem, cancellationToken) =>
                new ExclusaoPesagemServico(
                    new AcessoDados.Repositorio.ExclusaoPesagemRepositorio(
                        new AcessoDados.Banco.FabricaConexaoPostgreSql(
                            AcessoDados.Banco.LeitorConfiguracaoBancoPostgreSql.Carregar())))
                    .ExcluirAsync(codigoPesagem, cancellationToken));
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

    /// <summary>12G-D: reidratação — código do lançamento local elegível persistido para o pedido, ou null.</summary>
    public Task<long?> RecuperarCodigoLancamentoLocalPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => _recuperarLancamentoLocalPorPedido(numeroPedido, cancellationToken);

    /// <summary>054: EXCLUIR PESAGEM — cancelamento lógico local da pesagem persistida (sem SAP, sem DELETE físico).</summary>
    public Task<Modelo.Entrada.ResultadoExclusaoPesagem> ExcluirPesagemLocalAsync(
        long codigoPesagem,
        CancellationToken cancellationToken = default)
        => _excluirPesagemLocal(codigoPesagem, cancellationToken);

    /// <summary>12G-D: itens persistidos elegíveis do lançamento (peso/lote/item), para reidratar a tela.</summary>
    public Task<IReadOnlyList<EntradaProdutoItemEnvioSap>> ListarItensPersistidosParaEnvioAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _carregarItensParaEnvio(codigoLancamento, cancellationToken);

    public async Task<DiagnosticoEnvioSapEntrada> DiagnosticarEnvioSapEntradaAsync(
        long? codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        bool ambienteHomologacao = _ehAmbienteHomologacao();
        bool usuarioTemPermissao =
            AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.EnviarSap);

        DiagnosticoProntidaoIntegracaoSap integracao =
            await _diagnosticarIntegracaoSap(cancellationToken);

        // C7 (12G-D): a HABILITAÇÃO da escrita 101 passou a ser DETALHE INTERNO do clique de envio (12G-B) —
        // a cerimônia arma a capability e o writer a consome (one-shot) antes do POST. Portanto a ELEGIBILIDADE
        // do botão NÃO depende mais da capability/env-write; depende de prontidão LOCAL + CONFIG. O campo abaixo
        // é mantido apenas para o chip de status (não gateia PodeEnviar).
        bool escritaSapHabilitada101 = integracao.EscritaSapHabilitada || _capability.EstaArmado101;

        int totalItensPersistidos = 0;
        string? falhaItens = null;
        if (codigoLancamento is long codigo
            && codigo > 0
            && ambienteHomologacao
            && usuarioTemPermissao
            && integracao.AmbienteOperacional
            && integracao.IntegracaoAtiva
            && integracao.SapConfigurado
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
                ? "Envio de peso não liberado neste ambiente."
                : !usuarioTemPermissao
                    ? "Usuário sem permissão ENVIAR_SAP."
                    : !integracao.AmbienteOperacional
                        ? integracao.MotivoBloqueio ?? "Integração SAP indisponível neste ambiente."
                        : !integracao.IntegracaoAtiva
                            ? integracao.MotivoBloqueio ?? "Integração SAP inativa."
                            : !integracao.SapConfigurado
                                ? "Configuração SAP indisponível."
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
            EscritaSapHabilitada = escritaSapHabilitada101,
            EnvEscritaSapHabilitada = integracao.EscritaSapHabilitada,
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
                codigoLancamento, numeroPedido, itens, _consultarProdutoCentroSap,
                _consultarProdutoMestreSap, cancellationToken);
        if (preparacao.Bloqueio is not null)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): {preparacao.Bloqueio.Mensagem}");
            return preparacao.Bloqueio;
        }

        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes = preparacao.Posicoes!;

        // GATE R2 (P5): a quantidade INTEGRAL do pedido só pode ser contabilizada UMA vez por item. Antes
        // da reserva/POST, exige evidência de que não há recebimento já confirmado para o mesmo item.
        // Desconhecido ⇒ BLOQUEIA (fail-closed). Vale só para posições ROH; Químicos não são afetados.
        ResultadoEnvioSapEntrada? bloqueioDuplicidade =
            await ValidarDuplicidadeRecebimentoItemPedidoAsync(
                codigoLancamento, numeroPedido, posicoes, itens.Count, cancellationToken);
        if (bloqueioDuplicidade is not null)
        {
            return bloqueioDuplicidade;
        }

        // GATE R2 (P7): coerência do PAYLOAD montado (cultura, escala e semântica dos dois campos).
        ResultadoEnvioSapEntrada? bloqueioPayload =
            ValidarCoerenciaPayload101(codigoLancamento, posicoes, itens, itens.Count);
        if (bloqueioPayload is not null)
        {
            return bloqueioPayload;
        }

        // Tarefa Entrada 23.2 (Ajuste 5): coerência quantidade SAP × peso líquido ANTES da reserva/POST.
        ResultadoEnvioSapEntrada? bloqueioQuantidade = ValidarCoerenciaQuantidadeSap(codigoLancamento, itens);
        if (bloqueioQuantidade is not null)
        {
            return bloqueioQuantidade;
        }

        // PEEK da capability 101 (12E-E-B) ANTES da reserva: quando o env write gate esta false (Q runtime),
        // a escrita 101 exige a capability ARMADA. Se nao esta armada (ex.: TTL expirou entre o diagnostico e
        // aqui), STOP sem alterar o status local. Nao consome. Se o env write gate estiver true (legado/teste),
        // a capability nao e exigida.
        if (!diagnostico.EnvEscritaSapHabilitada && !_capability.EstaArmado101)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP bloqueado (lancamento {codigoLancamento}): escrita SAP 101 nao habilitada em runtime.");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.EscritaDesabilitada,
                Total = itens.Count,
                Mensagem = "Escrita SAP 101 não habilitada. Habilite a escrita SAP Q antes do envio."
            };
        }

        MaterialDocumentSapRequest requisicao =
            MontarRequisicaoMaterialDocument(numeroPedido, codigoLancamento, posicoes);
        string? bloqueioPolitica = Sap.ValidarPoliticaAntesReserva(requisicao);
        if (bloqueioPolitica is not null)
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                Total = itens.Count,
                Mensagem = bloqueioPolitica
            };

        // Reserva/claim ATOMICO antes de montar o payload e antes do POST (concorrencia/idempotencia):
        // FINALIZADO_LOCAL/ERRO_SAP -> ENVIADO_SAP em um unico UPDATE condicional. Se 0 linhas, outro
        // envio ja reservou (ou o status mudou): aborta SEM POST.
        Modelo.Entrada.ResultadoReservaEnvioSap reserva;
        try
        {
            reserva = await _reservarLancamentoParaEnvio(codigoLancamento, cancellationToken);
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

        if (!reserva.Reservado)
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

        // GATE 096D: StatusAnterior EFETIVAMENTE consumido pela reserva — para liberar a reserva num abort
        // pré-POST (restaura FINALIZADO_LOCAL/ERRO_SAP), NUNCA classificando abort local como falha SAP.
        string statusAnteriorReserva = reserva.StatusAnterior ?? "FINALIZADO_LOCAL";

        // 054/§15 + 096D CORRETIVA: APÓS a reserva e ANTES do POST, RECARREGA pelo loader PÓS-RESERVA (status
        // ENVIADO_SAP + pesagens VÁLIDAS/ativas) e compara com o snapshot do payload. A transição
        // FINALIZADO_LOCAL/ERRO_SAP → ENVIADO_SAP feita pela PRÓPRIA reserva NÃO conta como mudança (o loader
        // pós-reserva já espera ENVIADO_SAP). Só um EXCLUIR PESAGEM concorrente muda o snapshot → aborta sem POST.
        IReadOnlyList<EntradaProdutoItemEnvioSap> itensRevalidados;
        try
        {
            itensRevalidados = await _carregarItensReservadosParaRevalidacao(codigoLancamento, cancellationToken);
        }
        catch (Exception ex)
        {
            CompensacaoReserva compensacao = await CompensarReservaAposAbortPrePostAsync(
                codigoLancamento, statusAnteriorReserva, "falha na recarga pós-reserva");
            Sap.RegistrarDiagnostico(
                $"Envio SAP abortado (lancamento {codigoLancamento}): recarga pós-reserva falhou. {compensacao.MensagemParaOperador}");
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.Falha,
                Total = itens.Count,
                Mensagem = $"Envio bloqueado antes do SAP. {compensacao.MensagemParaOperador}"
            };
        }

        if (!ItensElegiveisIguais(itens, itensRevalidados))
        {
            CompensacaoReserva compensacao = await CompensarReservaAposAbortPrePostAsync(
                codigoLancamento, statusAnteriorReserva, "snapshot pós-reserva alterado");
            Sap.RegistrarDiagnostico(
                $"Envio SAP abortado (lancamento {codigoLancamento}): snapshot alterado. ZERO POST. {compensacao.MensagemParaOperador}");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.Falha,
                Total = itensRevalidados.Count,
                Mensagem = $"O envio foi abortado porque o estado do lançamento mudou. {compensacao.MensagemParaOperador}"
            };
        }

        // GATE R3: releitura FRESCA do item do Pedido de Compra no SAP, APÓS a reserva e ANTES do POST.
        // Compara quantidade e unidade ATUAIS com as que compuseram o payload. Divergência,
        // indisponibilidade, identificação ambígua ou falha de consulta ⇒ ABORTA sem POST e LIBERA a
        // reserva pela compensação existente. Só executa quando há posição ROH (HIBE não consulta o SAP).
        ResultadoEnvioSapEntrada? bloqueioFrescor;
        try
        {
            bloqueioFrescor = await ValidarQuantidadePedidoFrescaAsync(
                codigoLancamento, numeroPedido, posicoes, itens.Count, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            // GATE R3B: cancelamento/timeout da reconsulta APÓS a reserva e ANTES do POST. Antes, a exceção
            // escapava e deixava o lançamento preso em ENVIADO_SAP sem compensação. A compensação usa
            // SEMPRE CancellationToken.None (o token cancelado nunca é reutilizado) e o StatusAnterior
            // EFETIVAMENTE consumido pela reserva. ZERO POST nos dois casos.
            bool cancelamentoSolicitado = cancellationToken.IsCancellationRequested;
            string natureza = cancelamentoSolicitado
                ? "cancelamento solicitado"
                : ex is TaskCanceledException { InnerException: TimeoutException }
                    ? "timeout da consulta ao Pedido de Compra"
                    : "consulta ao Pedido de Compra interrompida sem cancelamento solicitado";

            CompensacaoReserva compensacao = await CompensarReservaAposAbortPrePostAsync(
                codigoLancamento, statusAnteriorReserva, natureza);

            if (cancelamentoSolicitado)
            {
                // Semântica original PRESERVADA: cancelamento continua sendo cancelamento — nunca vira
                // confirmação nem falha SAP. A compensação já foi tentada acima.
                throw;
            }

            // Timeout/interrupção sem cancelamento do chamador é ABORT LOCAL pré-POST, não erro remoto.
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                Total = itens.Count,
                Mensagem = "Não foi possível reconsultar a quantidade do item no Pedido de Compra "
                    + $"({natureza}). Envio bloqueado antes do SAP. {compensacao.MensagemParaOperador}"
            };
        }

        if (bloqueioFrescor is not null)
        {
            CompensacaoReserva compensacao = await CompensarReservaAposAbortPrePostAsync(
                codigoLancamento, statusAnteriorReserva, "bloqueio na reconsulta fresca");
            Sap.RegistrarDiagnostico(
                $"Envio SAP abortado (lancamento {codigoLancamento}): {bloqueioFrescor.Mensagem} "
                + $"ZERO POST. {compensacao.MensagemParaOperador}");
            return new ResultadoEnvioSapEntrada
            {
                Cenario = bloqueioFrescor.Cenario,
                Total = bloqueioFrescor.Total,
                Mensagem = $"{bloqueioFrescor.Mensagem} {compensacao.MensagemParaOperador}"
            };
        }

        // Um UNICO documento de material por lancamento (movimento 101), com as posicoes ja preparadas
        // (uma por lote quando administrado por lote; consolidada quando nao administrado).
        string chaveNegocio = $"{numeroPedido}/{codigoLancamento}";
        RegistrarDiagnosticoEnvioKg(codigoLancamento, itens);

        // 12E-E-C: o CONSUMO ATÔMICO one-shot da capability 101 ocorre no ENFORCEMENT POINT FINAL — dentro
        // de MaterialDocumentSapServico (writer), imediatamente antes do POST. O controller NÃO consome aqui
        // (evita duplo consumo). Se a capability expirou/foi consumida por concorrente entre o PEEK e o writer,
        // o writer devolve FALHA (ZERO POST) e o fluxo de falha abaixo compensa a reserva (status -> ERRO_SAP).

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

            // TIMEOUT/indeterminado (exceção de transporte, sem StatusHttp conclusivo): o POST pode ter
            // chegado ao SAP. Capability permanece CONSUMIDA -> RECONCILIACAO_REQUERIDA. Sem auto-retry/rearm.
            _capability.MarcarReconciliacao();
        }

        // O documento e atomico: ou cria com todos os itens, ou nenhum. O status local usa o
        // numero_item ORIGINAL do banco (ex.: "10"), nao a forma SAP de 5 digitos ("00010").
        if (!resultadoSap.Sucesso && resultadoSap.PostTentado is false)
        {
            CompensacaoReserva compensacao = await CompensarReservaAposAbortPrePostAsync(
                codigoLancamento, statusAnteriorReserva, "writer bloqueou antes do POST");
            cancellationToken.ThrowIfCancellationRequested();
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                Total = itens.Count,
                Mensagem = $"{resultadoSap.MensagemSanitizada} {compensacao.MensagemParaOperador}"
            };
        }
        if (!resultadoSap.Sucesso && resultadoSap.StatusHttp is null or >= 500)
        {
            _capability.MarcarReconciliacao();
            return new ResultadoEnvioSapEntrada
            {
                Cenario = CenarioEnvioSapEntrada.FalhaPersistenciaLocal,
                Total = itens.Count,
                StatusLocalAtualizado = false,
                MensagemCritica = "Resultado remoto desconhecido. Reconciliação obrigatória; não reenviar.",
                Mensagem = "SAP HML: resultado indeterminado; não reenviar sem reconciliação."
            };
        }
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
            // 2xx sem documento = indeterminado: capability -> RECONCILIACAO_REQUERIDA (sem rearm/retry).
            _capability.MarcarReconciliacao();
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
            // Documento respondido mas status local falhou = indeterminado local: RECONCILIACAO_REQUERIDA.
            _capability.MarcarReconciliacao();
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

    /// <summary>
    /// GATE R2 (P5): impede que a quantidade integral do pedido seja contabilizada mais de uma vez para o
    /// mesmo PurchaseOrderItem em recebimentos distintos. Aplica-se SOMENTE a posições ROH (as que carregam
    /// os campos ZZ1). Evidência tri-state: true ⇒ já houve recebimento confirmado (bloqueia);
    /// false ⇒ não houve (libera); null ⇒ DESCONHECIDO (bloqueia, nunca presume ausência).
    /// </summary>
    private async Task<ResultadoEnvioSapEntrada?> ValidarDuplicidadeRecebimentoItemPedidoAsync(
        long codigoLancamento,
        string numeroPedido,
        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes,
        int total,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> itensComQuantidadeDoPedido = posicoes
            .Where(p => !string.IsNullOrWhiteSpace(p.Item.Zz1PesoEntradaMmi))
            .Select(p => (p.Item.PurchaseOrderItem ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal);

        foreach (string itemPedido in itensComQuantidadeDoPedido)
        {
            bool? jaRecebido;
            try
            {
                jaRecebido = await _existeRecebimentoConfirmadoItemPedido(
                    numeroPedido, itemPedido, codigoLancamento, cancellationToken);
            }
            catch (Exception ex)
            {
                Sap.RegistrarDiagnostico(
                    $"Envio SAP bloqueado (lancamento {codigoLancamento}): falha ao verificar duplicidade de "
                    + $"recebimento do item {itemPedido}.{Environment.NewLine}{ex}");
                jaRecebido = null;
            }

            if (jaRecebido is null)
            {
                Sap.RegistrarDiagnostico(
                    $"Envio SAP BLOQUEADO (lancamento {codigoLancamento}): item {itemPedido} usaria a "
                    + "quantidade integral do pedido, porem nao ha evidencia local de ausencia de recebimento "
                    + "ja confirmado para o mesmo item. Controle de duplicidade indisponivel.");
                return new ResultadoEnvioSapEntrada
                {
                    Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                    Total = total,
                    Mensagem = $"Item {itemPedido}: não foi possível comprovar que este item do pedido ainda "
                        + "não teve recebimento confirmado. Como o movimento 101 lançaria a quantidade "
                        + "integral do pedido, o envio foi bloqueado para evitar contabilização em duplicidade."
                };
            }

            if (jaRecebido == true)
            {
                Sap.RegistrarDiagnostico(
                    $"Envio SAP BLOQUEADO (lancamento {codigoLancamento}): item {itemPedido} ja possui "
                    + "recebimento confirmado; a quantidade integral do pedido nao sera lancada novamente.");
                return new ResultadoEnvioSapEntrada
                {
                    Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
                    Total = total,
                    Mensagem = $"Item {itemPedido} já possui recebimento confirmado no SAP. "
                        + "Envio bloqueado para não lançar a quantidade do pedido em duplicidade."
                };
            }
        }

        return null;
    }

    /// <summary>
    /// GATE R5: provider P5 padrão de produção. As três fontes de evidência resolvem como INDISPONÍVEIS
    /// enquanto não houver (a) a estrutura durável de exclusividade por pedido+item, (b) a consulta local
    /// aprovada e (c) o contrato confirmado de leitura de A_MaterialDocumentItem. Com qualquer uma
    /// indisponível, o P5 BLOQUEIA — fail-closed por construção, nunca liberação por omissão.
    /// </summary>
    /// <summary>
    /// P5/056: as três fontes passam a ser as operacionais. A exclusividade vem da chave primária
    /// por pedido+item (incremento 056); a evidência local, da consulta D2 aprovada; o histórico,
    /// do serviço real de A_MaterialDocumentItem, envolvido pela atestação humana de abrangência.
    /// <para>
    /// Nenhuma delas libera por omissão: tabela ausente, banco desabilitado, consulta falha ou
    /// atestação inexistente continuam resolvendo como INDISPONÍVEL, que bloqueia.
    /// </para>
    /// </summary>
    private static Servicos.Entrada.ProvedorEvidenciaRecebimentoItemPedido CriarProvedorP5Padrao(
        Action<string> registrarDiagnostico)
    {
        try
        {
            bool bancoHabilitado = AcessoDados.Banco.EstadoIntegracaoBanco.Habilitado;
            var fabricaConexao = new AcessoDados.Banco.FabricaConexaoPostgreSql();

            return new Servicos.Entrada.ProvedorEvidenciaRecebimentoItemPedido(
                exclusividade: new Servicos.Entrada.ExclusividadeItemPedidoDuravelServico(
                    fabricaConexao, bancoHabilitado, registrarDiagnostico),
                local: new Servicos.Entrada.EvidenciaRecebimentoLocalServico(
                    fabricaConexao, bancoHabilitado, registrarDiagnostico),
                historico: new Servicos.Entrada.HistoricoMaterialDocumentSapAtestadoServico(
                    new Servicos.IntegracaoSap.HistoricoMaterialDocumentSapServico(
                        Servicos.IntegracaoSap.LeitorConfiguracaoSap.Carregar()),
                    Servicos.Entrada.AtestacaoAbrangenciaRecebimento.Carregar(),
                    registrarDiagnostico),
                registrarDiagnostico: registrarDiagnostico);
        }
        catch (Exception ex)
        {
            // Falhar na composição não pode virar liberação: cai nos defaults fail-closed.
            registrarDiagnostico("P5: fontes operacionais indisponíveis (" + ex.GetType().Name
                + "); mantido o bloqueio.");
            return new Servicos.Entrada.ProvedorEvidenciaRecebimentoItemPedido(
                registrarDiagnostico: registrarDiagnostico);
        }
    }

    /// <summary>
    /// GATE R3B: desfecho da compensação da reserva num abort pré-POST. Nunca declara liberação fictícia:
    /// quando a compensação não é comprovada, o estado da reserva fica explicitamente INCERTO.
    /// </summary>
    private readonly record struct CompensacaoReserva(bool Comprovada, string MensagemParaOperador);

    /// <summary>
    /// GATE R3B: compensa a reserva num abort pré-POST, UMA única vez, reutilizando
    /// <c>_liberarReservaAposAbort</c> — que preserva as verificações de propriedade/estado (só restaura a
    /// partir de ENVIADO_SAP e só para FINALIZADO_LOCAL/ERRO_SAP), de modo que uma transição concorrente
    /// legítima NÃO é sobrescrita.
    /// <para>
    /// Usa SEMPRE <see cref="CancellationToken.None"/>: o token cancelado do chamador nunca é reutilizado,
    /// senão a própria compensação abortaria. Falha ou não-comprovação ⇒ estado INCERTO declarado, nunca
    /// liberação fictícia.
    /// </para>
    /// </summary>
    private async Task<CompensacaoReserva> CompensarReservaAposAbortPrePostAsync(
        long codigoLancamento,
        string statusAnteriorReserva,
        string natureza)
    {
        try
        {
            bool liberada = await _liberarReservaAposAbort(
                codigoLancamento, statusAnteriorReserva, CancellationToken.None);

            if (liberada)
            {
                Sap.RegistrarDiagnostico(
                    $"Envio SAP abortado (lancamento {codigoLancamento}): {natureza}. ZERO POST; reserva "
                    + $"liberada para {statusAnteriorReserva}.");
                return new CompensacaoReserva(true, string.Empty);
            }

            // Não liberou: pode ser transição concorrente legítima OU reserva ainda presa. Não afirmamos
            // qual — declaramos a incerteza.
            Sap.RegistrarDiagnostico(
                $"Envio SAP abortado (lancamento {codigoLancamento}): {natureza}. ZERO POST; compensacao da "
                + "reserva NAO comprovada (nenhuma linha restaurada). Estado da reserva INCERTO.");
            return new CompensacaoReserva(false,
                "Atenção: não foi possível confirmar a liberação da reserva deste lançamento; "
                + "verifique o status antes de tentar novamente.");
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP abortado (lancamento {codigoLancamento}): {natureza}. ZERO POST; a compensacao "
                + $"da reserva FALHOU. Estado da reserva INCERTO.{Environment.NewLine}{ex}");
            return new CompensacaoReserva(false,
                "Atenção: a liberação da reserva deste lançamento falhou; "
                + "verifique o status antes de tentar novamente.");
        }
    }

    /// <summary>
    /// GATE R3: valida a quantidade e a unidade do item do Pedido de Compra contra uma leitura FRESCA do
    /// SAP, reutilizando o contrato já existente <c>ObterCabecalhoSapParaValidacaoAsync</c> (que consulta
    /// A_PurchaseOrder direto, com a navegação de itens, sem cache local).
    /// <para>
    /// Aplica-se SOMENTE às posições ROH — as que carregam os campos ZZ1 e portanto lançam a quantidade
    /// integral do pedido. Fluxos HIBE/legado não disparam nenhuma consulta SAP adicional.
    /// </para>
    /// FAIL-CLOSED: cabeçalho nulo, exceção, item não encontrado, item duplicado (ambíguo), quantidade
    /// ausente, unidade divergente ou quantidade divergente ⇒ bloqueio. Nunca presume o valor do SAP.
    /// </summary>
    private async Task<ResultadoEnvioSapEntrada?> ValidarQuantidadePedidoFrescaAsync(
        long codigoLancamento,
        string numeroPedido,
        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes,
        int total,
        CancellationToken cancellationToken)
    {
        MaterialDocumentSapItemRequest[] posicoesRoh = [.. posicoes
            .Select(p => p.Item)
            .Where(i => !string.IsNullOrWhiteSpace(i.Zz1PesoEntradaMmi))];

        if (posicoesRoh.Length == 0)
        {
            return null; // Nenhuma posição usa a quantidade do pedido: nada a revalidar, nenhuma consulta.
        }

        PedidoCompraSap? pedidoFresco;
        try
        {
            pedidoFresco = await Sap.ObterCabecalhoSapParaValidacaoAsync(numeroPedido, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha ao reconsultar o pedido {numeroPedido} para revalidar a quantidade."
                + $"{Environment.NewLine}{ex}");
            return BloqueioFrescor(total,
                "Não foi possível reconsultar a quantidade do item no Pedido de Compra antes do envio. "
                + "Envio bloqueado.");
        }

        if (pedidoFresco is null || pedidoFresco.Itens.Count == 0)
        {
            return BloqueioFrescor(total,
                $"O Pedido de Compra {numeroPedido} não retornou itens na reconsulta ao SAP. Envio bloqueado.");
        }

        foreach (MaterialDocumentSapItemRequest posicao in posicoesRoh)
        {
            string itemPedido = (posicao.PurchaseOrderItem ?? string.Empty).Trim();

            PedidoCompraSapItem[] correspondentes = [.. pedidoFresco.Itens
                .Where(i => string.Equals(
                    NormalizarItemSap(i.NumeroItem), itemPedido, StringComparison.Ordinal))];

            if (correspondentes.Length == 0)
            {
                return BloqueioFrescor(total,
                    $"Item {itemPedido} não foi localizado na reconsulta do Pedido de Compra {numeroPedido}. "
                    + "Envio bloqueado.");
            }

            if (correspondentes.Length > 1)
            {
                return BloqueioFrescor(total,
                    $"Item {itemPedido} retornou {correspondentes.Length} ocorrências na reconsulta do Pedido "
                    + "de Compra (identificação ambígua). Envio bloqueado.");
            }

            PedidoCompraSapItem itemSap = correspondentes[0];

            if (itemSap.Quantidade is not decimal quantidadeSap)
            {
                return BloqueioFrescor(total,
                    $"Item {itemPedido}: o SAP não informou a quantidade do pedido na reconsulta. "
                    + "Envio bloqueado.");
            }

            string unidadeSap = (itemSap.UnidadeMedida ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.Equals(unidadeSap, (posicao.EntryUnit ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                return BloqueioFrescor(total,
                    $"Item {itemPedido}: unidade da quantidade divergente entre o payload "
                    + $"({posicao.EntryUnit}) e o SAP ({(unidadeSap.Length == 0 ? "não informada" : unidadeSap)}). "
                    + "Envio bloqueado.");
            }

            if (!decimal.TryParse(
                    posicao.QuantityInEntryUnit,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal quantidadePayload))
            {
                return BloqueioFrescor(total,
                    $"Item {itemPedido}: quantidade do payload ilegível na revalidação. Envio bloqueado.");
            }

            if (!EntradaProdutoQuantidadeSap.QuantidadeCoerente(quantidadePayload, quantidadeSap))
            {
                Sap.RegistrarDiagnostico(
                    $"Envio SAP BLOQUEADO (lancamento {codigoLancamento}): item {itemPedido} com quantidade "
                    + $"do pedido OBSOLETA. Payload {posicao.QuantityInEntryUnit}; SAP atual "
                    + $"{EntradaProdutoAlocacao101.FormatarDecimalContratoSap(quantidadeSap)}.");
                return BloqueioFrescor(total,
                    $"Item {itemPedido}: a quantidade do Pedido de Compra mudou no SAP "
                    + $"({EntradaProdutoAlocacao101.FormatarDecimalContratoSap(quantidadeSap)} "
                    + $"{unidadeSap}) e não corresponde à usada no envio ({posicao.QuantityInEntryUnit}). "
                    + "Atualize os dados do pedido antes de enviar. Envio bloqueado.");
            }
        }

        return null;
    }

    private static ResultadoEnvioSapEntrada BloqueioFrescor(int total, string mensagem)
        => new()
        {
            Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
            Total = total,
            Mensagem = mensagem
        };

    /// <summary>
    /// GATE R2 (P6/P7): coerência do PAYLOAD já montado. NÃO exige mais igualdade entre
    /// QuantityInEntryUnit e o peso líquido — os dois campos passaram a ter semânticas distintas.
    /// Valida, por posição: parse invariante dos dois campos; no caminho ROH, quantidade padrão igual à
    /// quantidade do pedido e ZZ1 igual ao peso líquido da POSIÇÃO; par ZZ1 tudo-ou-nada; e, por item do
    /// pedido, a soma dos ZZ1 igual à soma dos pesos líquidos (antiduplicação estrutural).
    /// </summary>
    private ResultadoEnvioSapEntrada? ValidarCoerenciaPayload101(
        long codigoLancamento,
        IReadOnlyList<EntradaProdutoPosicaoMaterialDocument> posicoes,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens,
        int total)
    {
        foreach (EntradaProdutoPosicaoMaterialDocument posicao in posicoes)
        {
            MaterialDocumentSapItemRequest item = posicao.Item;
            string itemPedido = (item.PurchaseOrderItem ?? string.Empty).Trim();

            if (!decimal.TryParse(
                    item.QuantityInEntryUnit,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal quantidadePadrao)
                || quantidadePadrao <= 0m)
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: quantidade do movimento 101 inválida ou não positiva.");
            }

            bool temPeso = !string.IsNullOrWhiteSpace(item.Zz1PesoEntradaMmi);
            bool temUnidadePeso = !string.IsNullOrWhiteSpace(item.Zz1PesoEntradaMmiu);
            if (temPeso != temUnidadePeso)
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: campos de peso de entrada incompletos (valor e unidade são "
                    + "obrigatórios juntos).");
            }

            if (!temPeso)
            {
                continue; // Químicos e demais tipos: caminho legado, nada mais a validar aqui.
            }

            if (!string.Equals(item.Zz1PesoEntradaMmiu, EntradaProdutoAlocacao101.UnidadeSuportada, StringComparison.Ordinal))
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: unidade do peso de entrada diferente de "
                    + $"{EntradaProdutoAlocacao101.UnidadeSuportada}.");
            }

            if (!string.Equals(item.EntryUnit, EntradaProdutoAlocacao101.UnidadeSuportada, StringComparison.Ordinal))
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: unidade da quantidade diferente de "
                    + $"{EntradaProdutoAlocacao101.UnidadeSuportada}.");
            }

            if (!decimal.TryParse(
                    item.Zz1PesoEntradaMmi,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal pesoPayload)
                || pesoPayload <= 0m)
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: peso de entrada inválido ou não positivo.");
            }

            // O peso da POSIÇÃO é a soma dos lotes locais que a compõem: 1 lote no CASO A, N lotes
            // consolidados no CASO B. Nunca um total do item nem do documento.
            decimal pesoPosicao = SomarPesoLiquidoDosLotes(itens, posicao.CodigosLotesLocais, pesoPayload);

            if (!EntradaProdutoQuantidadeSap.QuantidadeCoerente(pesoPayload, pesoPosicao))
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: peso de entrada {item.Zz1PesoEntradaMmi} divergente do peso líquido "
                    + $"da posição {FormatarQuantidade(pesoPosicao)} KG.");
            }

            decimal quantidadeEsperada = itens
                .Where(i => string.Equals(NormalizarItemSap(i.NumeroItem), itemPedido, StringComparison.Ordinal))
                .Select(i => i.QuantidadePedidoItem)
                .FirstOrDefault(q => q.HasValue) ?? 0m;

            if (quantidadeEsperada <= 0m
                || !EntradaProdutoQuantidadeSap.QuantidadeCoerente(quantidadePadrao, quantidadeEsperada))
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {itemPedido}: quantidade do movimento 101 {item.QuantityInEntryUnit} divergente da "
                    + "quantidade do item do Pedido de Compra.");
            }
        }

        // Antiduplicação estrutural: por item do pedido, a soma dos ZZ1 das posições tem de reproduzir a
        // soma dos pesos líquidos daquele item — nunca um total repetido.
        foreach (IGrouping<string, EntradaProdutoPosicaoMaterialDocument> grupo in posicoes
            .Where(p => !string.IsNullOrWhiteSpace(p.Item.Zz1PesoEntradaMmi))
            .GroupBy(p => (p.Item.PurchaseOrderItem ?? string.Empty).Trim(), StringComparer.Ordinal))
        {
            decimal somaPayload = grupo.Sum(p => decimal.Parse(
                p.Item.Zz1PesoEntradaMmi!, System.Globalization.CultureInfo.InvariantCulture));
            decimal somaLocal = itens
                .Where(i => string.Equals(NormalizarItemSap(i.NumeroItem), grupo.Key, StringComparison.Ordinal))
                .Sum(i => i.PesoLiquidoKg);

            if (!EntradaProdutoQuantidadeSap.QuantidadeCoerente(somaPayload, somaLocal))
            {
                return BloqueioPayload(codigoLancamento, total,
                    $"Item {grupo.Key}: soma dos pesos de entrada do payload divergente do peso líquido "
                    + "apurado do item. Envio bloqueado (possível duplicação).");
            }
        }

        return null;
    }

    private static decimal SomarPesoLiquidoDosLotes(
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens,
        IReadOnlyList<long> codigosLotes,
        decimal padrao)
    {
        decimal soma = itens
            .Where(i => codigosLotes.Contains(i.CodigoEntradaProdutoLote))
            .Sum(i => i.PesoLiquidoKg);
        return soma > 0m ? soma : padrao;
    }

    private ResultadoEnvioSapEntrada BloqueioPayload(long codigoLancamento, int total, string mensagem)
    {
        Sap.RegistrarDiagnostico($"Envio SAP BLOQUEADO (lancamento {codigoLancamento}): {mensagem}");
        return new ResultadoEnvioSapEntrada
        {
            Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
            Total = total,
            Mensagem = mensagem
        };
    }

    // Monta UMA posicao (item) do documento de material 101 a partir de um lote/representante. comLote
    // controla Batch/ManufactureDate/ShelfLifeExpirationDate (material ADMINISTRADO por lote no centro).
    // Os literais do movimento 101 (101/B/EntryUnit KG/QuantityInEntryUnit) vivem AQUI, no controller.
    /// <summary>
    /// GATE R2: a alocacao decide o par (campo padrao x campos customizados).
    /// ROH ...... QuantityInEntryUnit = quantidade INTEGRAL do item do Pedido de Compra, na unidade do
    ///            pedido (somente KG neste incremento); ZZ1_PESO_ENTRADA_MMI/MMIU = peso liquido pesado.
    /// Demais ... comportamento LEGADO (quantidade = peso liquido), SEM campos customizados.
    /// </summary>
    internal static MaterialDocumentSapItemRequest MontarItemMaterialDocument(
        string numeroPedido, EntradaProdutoItemEnvioSap item, bool comLote, ResultadoAlocacao101 alocacao,
        int linha = 1)
        => new()
        {
            Material = item.Material!.Trim(),
            Plant = item.Centro!.Trim(),
            StorageLocation = item.Deposito!.Trim(),
            GoodsMovementType = "101",
            GoodsMovementRefDocType = "B", // referencia = Pedido de Compra (exigido pelo SAP no 101)
            // Line-ID exigido pelo SAP ao postar via contexto de negocio MaterialDocumentItem
            // (MM_IM_ODATA_API_MDOC/027). Sequencial dentro da requisicao, a partir de 1.
            MaterialDocumentLine = linha.ToString(CultureInfo.InvariantCulture),
            QuantityInEntryUnit = alocacao.QuantidadePadrao,
            EntryUnit = alocacao.UnidadePadrao,
            Zz1PesoEntradaMmi = alocacao.PesoLiquidoCustomizado,
            Zz1PesoEntradaMmiu = alocacao.UnidadePesoCustomizado,
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
    // 054/§15: itens elegíveis (numero_item → soma peso líquido) idênticos entre snapshot e recarga pós-reserva.
    // depois vazio => false (nada elegível restou). Qualquer divergência de item/peso => false (payload stale).
    private static bool ItensElegiveisIguais(
        IReadOnlyList<EntradaProdutoItemEnvioSap> antes,
        IReadOnlyList<EntradaProdutoItemEnvioSap> depois)
    {
        if (depois.Count == 0)
        {
            return false;
        }

        // GATE R3: o snapshot deixou de comparar SOMENTE item -> soma do peso. Agora inclui a IDENTIDADE
        // (pedido, item, material) e os dados DOCUMENTAIS que alimentam o campo padrão do 101
        // (QuantidadePedidoItem e a unidade). Sem isso, uma quantidade do pedido OBSOLETA passaria pela
        // revalidação pós-reserva apenas porque o peso não mudou.
        static Dictionary<string, AssinaturaItemElegivel> PorItem(IReadOnlyList<EntradaProdutoItemEnvioSap> itens)
            => itens
                .GroupBy(i => NormalizarItemSap(i.NumeroItem))
                .ToDictionary(
                    g => g.Key,
                    g => new AssinaturaItemElegivel(
                        Pedido: Normalizar(g.Select(x => x.NumeroPedido)),
                        Material: Normalizar(g.Select(x => x.Material)),
                        Unidade: Normalizar(g.Select(x => x.Unidade)).ToUpperInvariant(),
                        QuantidadePedido: Unica(g.Select(x => x.QuantidadePedidoItem)),
                        SomaPesoLiquido: g.Sum(x => x.PesoLiquidoKg)));

        Dictionary<string, AssinaturaItemElegivel> a = PorItem(antes);
        Dictionary<string, AssinaturaItemElegivel> b = PorItem(depois);
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, AssinaturaItemElegivel> par in a)
        {
            if (!b.TryGetValue(par.Key, out AssinaturaItemElegivel? assinatura) || assinatura != par.Value)
            {
                return false;
            }
        }

        return true;

        static string Normalizar(IEnumerable<string?> valores)
        {
            string[] distintos = [.. valores
                .Select(v => (v ?? string.Empty).Trim())
                .Distinct(StringComparer.Ordinal)];
            // Divergência DENTRO do mesmo item (ex.: dois materiais) é, por si, snapshot inconsistente:
            // devolve um marcador que nunca casa, em vez de escolher um dos valores.
            return distintos.Length == 1 ? distintos[0] : "\u0000DIVERGENTE";
        }

        static decimal? Unica(IEnumerable<decimal?> valores)
        {
            decimal?[] distintos = [.. valores.Distinct()];
            return distintos.Length == 1 ? distintos[0] : decimal.MinValue;
        }
    }

    /// <summary>
    /// GATE R3: assinatura comparável de um item elegível entre o snapshot do payload e a recarga
    /// pós-reserva. Record ⇒ igualdade estrutural por valor, incluindo os campos documentais.
    /// </summary>
    private sealed record AssinaturaItemElegivel(
        string Pedido,
        string Material,
        string Unidade,
        decimal? QuantidadePedido,
        decimal SomaPesoLiquido);

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
            // GATE 105G: o cache miss usa o MESMO contrato técnico da validação fresca. Uma falha
            // classificada (401/403/404/timeout/rede/TLS/5xx/resposta inválida/config) chega tipada e
            // preserva o cenário até a UI — nunca mais a mensagem genérica "SAP indisponível no momento".
            ResultadoOperacao sincronizacao;
            try
            {
                sincronizacao = await Sap.SincronizarPedidoAsync(numeroPedido, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // cancelamento do operador: sem modal técnico falso.
            }
            catch (ConsultaSapException falha)
            {
                return MontarFalhaConsultaSap(
                    pedido, numeroPedido, falha.Cenario, falha.HttpStatus, falha.CorrelationId);
            }
            catch (IntegracaoSapBloqueadaException)
            {
                return MontarFalhaConsultaSap(
                    pedido, numeroPedido, CenarioFalhaConsultaSap.ConfiguracaoInvalida, null, null);
            }

            if (!sincronizacao.Sucesso)
            {
                // Integração sem configuração válida (ex.: arquivo malformado) é detectada de forma
                // ESTRUTURAL (SapConfigurado), nunca por texto da mensagem. Modo simulado é exceção.
                if (!Sap.SapConfigurado && !Sap.EhSimulado)
                {
                    return MontarFalhaConsultaSap(
                        pedido, numeroPedido, CenarioFalhaConsultaSap.ConfiguracaoInvalida, null, null);
                }

                // Falha NÃO técnica (sem itens / fora do escopo / validação operacional): a consulta
                // ocorreu, então preserva a mensagem funcional REAL — sem inventar cenário HTTP.
                return new ResultadoConsultaPedido
                {
                    Sucesso = false,
                    Mensagem = sincronizacao.Mensagem,
                    TituloFalha = "Consulta de pedido",
                    NumeroPedido = (numeroPedido ?? string.Empty).Trim(),
                    ItensAutorizados = [],
                    ItensOcultados = 0,
                    ConsultaTecnicaOk = true,
                    CenarioFalhaSap = CenarioFalhaConsultaSap.Nenhum
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
        // GATE 105D: a consulta técnica é avaliada ANTES do status de negócio. Falha técnica NUNCA vira
        // "pedido não liberado" — o validador só roda com CONSULTA_TECNICA_OK e pedido SAP válido.
        PedidoCompraSap? cabecalhoSap;
        try
        {
            cabecalhoSap = await Sap.ObterCabecalhoSapParaValidacaoAsync(numeroPedido, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // cancelamento do operador: sem modal técnico falso.
        }
        catch (ConsultaSapException falha)
        {
            return MontarFalhaConsultaSap(pedido, numeroPedido, falha.Cenario, falha.HttpStatus, falha.CorrelationId);
        }
        catch (IntegracaoSapBloqueadaException)
        {
            return MontarFalhaConsultaSap(
                pedido, numeroPedido, CenarioFalhaConsultaSap.ConfiguracaoInvalida, null, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // null aqui significa EXCLUSIVAMENTE "não encontrado" (404) — não mais "algum erro técnico".
        if (cabecalhoSap is null)
        {
            return MontarFalhaConsultaSap(
                pedido, numeroPedido, CenarioFalhaConsultaSap.NaoEncontrado, 404, null);
        }

        ResultadoValidacaoPedidoCompra validacao = ValidadorLiberacaoPedidoCompra.Validar(cabecalhoSap);

        if (!validacao.Liberado)
        {
            // GATE 105D: status indeterminado (vazio/não mapeado) tem cenário e texto próprios; os demais
            // permanecem como bloqueio FUNCIONAL real (03/04/08/liberação não concluída).
            (string tituloBloqueio, string mensagemBloqueio) = validacao.StatusDesconhecido
                ? MensagensFalhaConsultaSap.Descrever(
                    CenarioFalhaConsultaSap.StatusNegocioDesconhecido, pedido.NumeroPedido)
                : (string.Empty, validacao.MotivoBloqueio);

            return new ResultadoConsultaPedido
            {
                Sucesso = true,
                Mensagem = mensagemBloqueio,
                NumeroPedido = pedido.NumeroPedido,
                Fornecedor = pedido.Fornecedor,
                DataPedido = pedido.DataPedido,
                TipoPedido = pedido.TipoPedido,
                ItensAutorizados = [], // Ajuste 6: nada operacional para pedido nao liberado
                ItensOcultados = 0,
                PedidoLiberado = false,
                MotivoBloqueioLiberacao = mensagemBloqueio,
                StatusProcessamento = validacao.CodigoStatus,
                DescricaoStatusProcessamento = validacao.DescricaoStatus,
                LiberacaoNaoConcluida = validacao.LiberacaoNaoConcluida,
                // Consulta técnica OK: este é um bloqueio FUNCIONAL, não uma falha de integração.
                ConsultaTecnicaOk = true,
                CenarioFalhaSap = validacao.StatusDesconhecido
                    ? CenarioFalhaConsultaSap.StatusNegocioDesconhecido
                    : CenarioFalhaConsultaSap.Nenhum,
                TituloFalha = tituloBloqueio
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

    /// <summary>
    /// GATE 105D: resultado de FALHA DE CONSULTA (técnica) ou de NÃO ENCONTRADO. Nunca marca o pedido como
    /// "não liberado": PedidoLiberado permanece true e MotivoBloqueioLiberacao vazio, para que a UI não
    /// apresente causa de negócio. O validador de liberação NÃO roda neste caminho.
    /// </summary>
    /// <remarks>internal static para teste direto (InternalsVisibleTo), sem instanciar o controller/DB.</remarks>
    internal static ResultadoConsultaPedido MontarFalhaConsultaSap(
        PedidoCompraSapAgregado? pedido,
        string numeroPedido,
        CenarioFalhaConsultaSap cenario,
        int? httpStatus,
        string? correlationId)
    {
        string numero = string.IsNullOrWhiteSpace(pedido?.NumeroPedido)
            ? (numeroPedido ?? string.Empty).Trim()
            : pedido!.NumeroPedido;
        (string titulo, string mensagem) = MensagensFalhaConsultaSap.Descrever(cenario, numero);

        return new ResultadoConsultaPedido
        {
            // Sucesso=false: a tela trata como consulta não concluída (sem abrir operação).
            Sucesso = false,
            Mensagem = mensagem,
            TituloFalha = titulo,
            NumeroPedido = numero,
            ItensAutorizados = [],
            ItensOcultados = 0,
            ConsultaTecnicaOk = false,
            CenarioFalhaSap = cenario,
            HttpStatusFalha = httpStatus,
            CorrelationId = correlationId ?? string.Empty,
            // Não afirmar nada sobre o status de negócio: ele não foi avaliado.
            PedidoLiberado = true,
            MotivoBloqueioLiberacao = string.Empty,
            StatusProcessamento = string.Empty,
            DescricaoStatusProcessamento = string.Empty
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
    // Env write gate BRUTO (FUGAPET_Q_SAP_WRITE_ENABLED). Distinto de EscritaSapHabilitada, que ja e efetivo
    // (env OU capability). No Q runtime este e sempre false; a capability 101 e a fonte de autorizacao.
    public bool EnvEscritaSapHabilitada { get; init; }
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

    // GATE 105D: diagnóstico da CONSULTA SAP, separado do status de NEGÓCIO. Permite à UI distinguir
    // SUCESSO × NAO_ENCONTRADO × FALHA_TECNICA × BLOQUEIO_FUNCIONAL sem conhecer HTTP.
    /// <summary>Cenário classificado da consulta (Nenhum quando a consulta técnica foi bem-sucedida).</summary>
    public CenarioFalhaConsultaSap CenarioFalhaSap { get; init; } = CenarioFalhaConsultaSap.Nenhum;

    /// <summary>
    /// Consulta SAP concluída tecnicamente. Default true para não afetar chamadas que não passam pela
    /// validação; o fluxo real sempre o define. Só com true o validador de liberação pode ter rodado.
    /// </summary>
    public bool ConsultaTecnicaOk { get; init; } = true;

    /// <summary>Título de aplicação para o cenário (vazio quando não há falha a apresentar).</summary>
    public string TituloFalha { get; init; } = string.Empty;

    /// <summary>Status HTTP da falha, quando houve resposta (apenas diagnóstico; a UI não o interpreta).</summary>
    public int? HttpStatusFalha { get; init; }

    /// <summary>Correlação para rastrear a falha no diagnóstico técnico.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Falha TÉCNICA da consulta (nunca deve ser apresentada como status de negócio).</summary>
    public bool FalhaTecnicaSap => !ConsultaTecnicaOk;
}
