using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Portao de seguranca e persistencia do lancamento de Entrada de Produto (rastreabilidade
/// completa: lancamento + itens + pesagens). Valida autenticacao, permissao (FINALIZAR ou
/// EXECUTAR), origem, pesos, situacao do pedido/item, setor, tara x setor e balanca x setor
/// ANTES de gravar. Tentativas negadas sao auditadas (best-effort) sem que falha de auditoria
/// libere a operacao. Retorna ResultadoOperacao amigavel — excecoes de infraestrutura propagam.
/// </summary>
public sealed class EntradaProdutoServico
{
    private const string OrigemManual = "MANUAL";
    private static readonly string[] OrigensValidas = ["BALANCA", "MANUAL"];
    private static readonly string[] StatusPesagemValidos = ["VALIDA", "CANCELADA", "ESTORNADA"];
    private const string TelaAuditoria = "Entrada de Produto";

    // Tolerancia para liquido = bruto - tara, alinhada a regra endurecida aprovada pelo Gaia (script 022 consolidado).
    private const decimal ToleranciaPesoKg = 0.001m;

    private readonly EntradaProdutoRepositorio _repositorio;
    private readonly PesagemEntradaItemRepositorio _itemRepositorio;
    private readonly TaraRepositorio _taraRepositorio;
    private readonly BalancaRepositorio? _balancaRepositorio;
    private readonly AutorizacaoCentroDepositoEntrada _autorizacaoCentroDeposito;
    private readonly AuditoriaServico? _auditoria;

    public EntradaProdutoServico()
        : this(
            new EntradaProdutoRepositorio(Fabrica()),
            new PesagemEntradaItemRepositorio(Fabrica()),
            new TaraRepositorio(Fabrica()),
            new BalancaRepositorio(Fabrica()),
            AutorizacaoCentroDepositoEntrada.CarregarDoAmbiente(),
            CriarAuditoriaPadrao())
    {
    }

    public EntradaProdutoServico(
        EntradaProdutoRepositorio repositorio,
        PesagemEntradaItemRepositorio itemRepositorio,
        TaraRepositorio taraRepositorio,
        BalancaRepositorio? balancaRepositorio,
        AutorizacaoCentroDepositoEntrada autorizacaoCentroDeposito,
        AuditoriaServico? auditoria)
    {
        _repositorio = repositorio;
        _itemRepositorio = itemRepositorio;
        _taraRepositorio = taraRepositorio;
        _balancaRepositorio = balancaRepositorio;
        _autorizacaoCentroDeposito = autorizacaoCentroDeposito;
        _auditoria = auditoria;
    }

    /// <summary>
    /// Valida e grava um lancamento completo. Retorna <see cref="ResultadoOperacao.Falha"/> com
    /// mensagem amigavel quando alguma regra de negocio falha. Excecoes de infraestrutura (DB,
    /// null de repositorio em testes) propagam normalmente para o chamador.
    /// </summary>
    public async Task<ResultadoOperacao> RegistrarLancamentoAsync(
        EntradaProdutoLancamento lancamento, CancellationToken cancellationToken = default)
    {
        try
        {
            long usuario = ExigirUsuarioAutenticado();

            // Permissao: FINALIZAR ou EXECUTAR sao suficientes para registrar a pesagem.
            bool temPermissao =
                AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Finalizar)
                || AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Executar);
            if (!temPermissao)
            {
                await NegarAsync(usuario,
                    AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.Finalizar),
                    cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(lancamento.NumeroPedido))
            {
                throw new ErroOperacionalEsperadoException("Pedido obrigatorio para o lancamento de entrada.");
            }

            IReadOnlyList<EntradaProdutoItem> itensComPesagem = lancamento.Itens
                .Where(item => item.Pesagens.Count > 0)
                .ToList();
            if (itensComPesagem.Count == 0)
            {
                throw new ErroOperacionalEsperadoException("Nenhuma pesagem informada para gravar.");
            }

            bool possuiManual = itensComPesagem
                .SelectMany(item => item.Pesagens)
                .Any(p => string.Equals(p.Origem?.Trim(), OrigemManual, StringComparison.OrdinalIgnoreCase));
            if (possuiManual && !AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual))
            {
                await NegarAsync(usuario,
                    AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.PesoManual),
                    cancellationToken);
            }

            // Setor: quando o lancamento especifica setor e o usuario tem setor padrao, devem coincidir.
            if (lancamento.CodigoSetor is long setorLancamento
                && EstadoSessaoUsuarioAtual.SessaoAtual?.IdSetorPadrao is long setorUsuario
                && setorLancamento != setorUsuario)
            {
                await NegarAsync(usuario, "Setor do lancamento nao autorizado para o usuario.", cancellationToken);
            }

            IReadOnlySet<long> tarasDoSetor = await CarregarTarasDoSetorAsync(lancamento.CodigoSetor, cancellationToken);

            foreach (EntradaProdutoItem item in itensComPesagem)
            {
                await ValidarItemAsync(usuario, lancamento.CodigoSetor, item, tarasDoSetor, cancellationToken);
            }

            long id = await _repositorio.SalvarLancamentoAsync(
                lancamento with { Itens = itensComPesagem }, cancellationToken);

            return ResultadoOperacao.Ok($"Lancamento {id} gravado com sucesso.", idGerado: id);
        }
        catch (ErroOperacionalEsperadoException ex)
        {
            return ResultadoOperacao.Falha(ex.Message);
        }
    }


    public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia entrada,
        CancellationToken cancellationToken = default)
    {
        (EntradaProdutoLancamentoComLotesPersistencia snapshot, ContextoAuditoriaEntradaLotes contexto) =
            await PrepararLancamentoComLotesAsync(entrada, cancellationToken);

        return await _repositorio.RegistrarLancamentoComLotesAsync(snapshot, contexto, cancellationToken);
    }

    /// <summary>
    /// Ação de idempotência auditada quando a árvore informada diverge da árvore já persistida sob a mesma
    /// correlation_id. Constante estável para consultas na trilha de auditoria.
    /// </summary>
    internal const string AcaoAuditoriaIdempotenciaDivergente = "ENTRADA_LOTES_IDEMPOTENCIA_DIVERGENTE";

    public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia entrada,
        CancellationToken cancellationToken = default)
    {
        (EntradaProdutoLancamentoComLotesPersistencia snapshot, ContextoAuditoriaEntradaLotes contexto) =
            await PrepararLancamentoComLotesAsync(entrada, cancellationToken);

        try
        {
            return await _repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(snapshot, contexto, cancellationToken);
        }
        catch (ConflitoPersistenciaEntradaLotesException ex)
        {
            // §10/§11: audita (best-effort) o diagnóstico SANITIZADO e mapeia para a exceção operacional
            // com a mensagem pública genérica. Encapsulado para ser testável sem banco.
            throw await MapearConflitoIdempotenciaAsync(ex, cancellationToken);
        }
    }

    /// <summary>
    /// §10/§11 — audita o conflito (best-effort, diagnóstico técnico sanitizado) e devolve a exceção
    /// operacional a ser lançada, com a mensagem pública genérica e o conflito preservado como
    /// InnerException. A falha da auditoria NUNCA transforma o conflito em sucesso.
    /// </summary>
    internal async Task<ErroOperacionalEsperadoException> MapearConflitoIdempotenciaAsync(
        ConflitoPersistenciaEntradaLotesException conflito,
        CancellationToken cancellationToken)
    {
        await AuditarConflitoIdempotenciaAsync(conflito, cancellationToken);
        return new ErroOperacionalEsperadoException(conflito.MensagemUsuario, conflito);
    }

    private async Task AuditarConflitoIdempotenciaAsync(
        ConflitoPersistenciaEntradaLotesException conflito,
        CancellationToken cancellationToken)
    {
        if (_auditoria is null)
        {
            return;
        }

        string mensagem = conflito.DivergenciaTecnica;
        if (conflito.CorrelationId is Guid correlationId)
        {
            mensagem += $" | correlation_id={correlationId}";
        }

        if (!string.IsNullOrWhiteSpace(conflito.NumeroItemSap))
        {
            mensagem += $" | item={conflito.NumeroItemSap}";
        }

        try
        {
            await _auditoria.RegistrarErroAsync(
                AcaoAuditoriaIdempotenciaDivergente,
                mensagem,
                TelaAuditoria,
                cancellationToken);
        }
        catch
        {
            // Best-effort: a impossibilidade de auditar não pode liberar nem alterar o resultado do conflito.
        }
    }

    private async Task<(EntradaProdutoLancamentoComLotesPersistencia Snapshot, ContextoAuditoriaEntradaLotes Contexto)>
        PrepararLancamentoComLotesAsync(
            EntradaProdutoLancamentoComLotesPersistencia entrada,
            CancellationToken cancellationToken)
    {
        ContextoAuditoriaEntradaLotes contexto = CapturarContextoAuditoriaEntradaLotes();
        EntradaProdutoLancamentoComLotesPersistencia snapshot = EntradaProdutoArvoreLotesSnapshot.Criar(entrada);
        ValidadorEntradaProdutoArvoreLotes.Validar(snapshot, contexto.DataReferencia);
        ValidadorEntradaProdutoPersistenciaLotes.ValidarSetorObrigatorio(snapshot.Lancamento.CodigoSetor, contexto);

        bool temPermissao =
            AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Finalizar)
            || AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Executar);
        if (!temPermissao)
        {
            await NegarAsync(
                contexto.CodigoUsuario,
                AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.Finalizar),
                cancellationToken);
        }

        IReadOnlyList<EntradaProdutoItem> itensComPesagem = snapshot.Itens
            .Select(item => item.Item with
            {
                Pesagens = item.Lotes
                    .SelectMany(lote => lote.Pesagens)
                    .Select(pesagemLocal => pesagemLocal.Pesagem)
                    .ToList()
            })
            .Where(item => item.Pesagens.Count > 0)
            .ToList();

        if (itensComPesagem.Count == 0)
        {
            throw new ErroOperacionalEsperadoException("Nenhuma pesagem informada para gravar.");
        }

        bool possuiManual = itensComPesagem
            .SelectMany(item => item.Pesagens)
            .Any(pesagem => string.Equals(pesagem.Origem, OrigemManual, StringComparison.Ordinal));
        if (possuiManual && !AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual))
        {
            await NegarAsync(
                contexto.CodigoUsuario,
                AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.PesoManual),
                cancellationToken);
        }

        IReadOnlySet<long> tarasDoSetor = await CarregarTarasDoSetorAsync(snapshot.Lancamento.CodigoSetor, cancellationToken);
        foreach (EntradaProdutoItem item in itensComPesagem)
        {
            await ValidarItemComLotesAsync(contexto, snapshot.Lancamento.NumeroPedido, item, tarasDoSetor, cancellationToken);
        }

        return (snapshot, contexto);
    }
    public Task<EntradaProdutoItemPersistido?> ObterItemPersistidoAsync(
        long codigoLancamento,
        long codigoSapPedidoCompraItem,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterItemPersistidoAsync(
            codigoLancamento,
            codigoSapPedidoCompraItem,
            cancellationToken);

    /// <summary>Pesagens INDIVIDUAIS persistidas de um item (para detalhe/reimpressão por pesagem, não SUM).</summary>
    public Task<IReadOnlyList<EntradaProdutoPesagem>> ListarPesagensPersistidasAsync(
        long codigoLancamento,
        long codigoSapPedidoCompraItem,
        CancellationToken cancellationToken = default)
        => _repositorio.ListarPesagensPersistidasAsync(
            codigoLancamento,
            codigoSapPedidoCompraItem,
            cancellationToken);

    /// <summary>Itens ja persistidos do lancamento, com pesos consolidados, para envio controlado ao SAP.</summary>
    public Task<IReadOnlyList<EntradaProdutoItemEnvioSap>> ListarItensParaEnvioSapAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _repositorio.ListarItensParaEnvioSapAsync(codigoLancamento, cancellationToken);

    /// <summary>Status atual do lancamento (defesa de reenvio antes da criacao do documento de material).</summary>
    public Task<string?> ObterStatusLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterStatusLancamentoAsync(codigoLancamento, cancellationToken);

    /// <summary>Reserva atomica do lancamento (FINALIZADO_LOCAL/ERRO_SAP -&gt; ENVIADO_SAP) antes do POST.
    /// Retorna false quando outro envio ja reservou (concorrencia/idempotencia).</summary>
    public Task<bool> TentarReservarLancamentoParaEnvioSapAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _repositorio.TentarReservarLancamentoParaEnvioSapAsync(codigoLancamento, cancellationToken);

    public async Task<ResultadoOperacao> AtualizarStatusAposEnvioSapAsync(
        long codigoLancamento,
        IReadOnlyList<ResultadoItemEnvioSap> resultados,
        CenarioEnvioSapEntrada cenario,
        RastreabilidadeDocumentoMaterialSap? rastreabilidade = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _repositorio.AtualizarStatusAposEnvioSapAsync(
                codigoLancamento,
                resultados,
                cenario,
                rastreabilidade,
                cancellationToken);
            return ResultadoOperacao.Ok("Status local do envio SAP atualizado.");
        }
        catch
        {
            return ResultadoOperacao.Falha(
                "O SAP respondeu ao envio, mas o status local não pôde ser atualizado.");
        }
    }

    private async Task ValidarItemComLotesAsync(
        ContextoAuditoriaEntradaLotes contexto,
        string numeroPedido,
        EntradaProdutoItem item,
        IReadOnlySet<long> tarasDoSetor,
        CancellationToken cancellationToken)
    {
        ValidadorEntradaProdutoPersistenciaLotes.ValidarItemObrigatorio(item);

        if (!_autorizacaoCentroDeposito.ItemAutorizado(item.Centro, item.Deposito))
        {
            await NegarAsync(contexto.CodigoUsuario, $"Item {item.NumeroItem} fora do centro/deposito autorizado para a entrada.", cancellationToken);
        }

        long codigoSap = item.CodigoSapPedidoCompraItem!.Value;
        ValidacaoItemPesagem validacao = await _itemRepositorio.ValidarItemAsync(codigoSap, cancellationToken);
        if (!validacao.Existe)
        {
            await NegarAsync(contexto.CodigoUsuario, $"Item {item.NumeroItem} nao encontrado no cache do pedido.", cancellationToken);
        }
        if (!validacao.PedidoAtivo)
        {
            await NegarAsync(contexto.CodigoUsuario, "Pedido de compra inativo. Entrada nao permitida.", cancellationToken);
        }
        if (!validacao.ItemAtivo)
        {
            await NegarAsync(contexto.CodigoUsuario, $"Item {item.NumeroItem} inativo. Entrada nao permitida.", cancellationToken);
        }
        if (!validacao.MaterialPresente)
        {
            await NegarAsync(contexto.CodigoUsuario, $"Item {item.NumeroItem} sem material valido. Entrada nao permitida.", cancellationToken);
        }

        ValidadorEntradaProdutoPersistenciaLotes.ValidarCoerenciaCacheSap(numeroPedido, item, validacao);

        foreach (EntradaProdutoPesagem pesagem in item.Pesagens)
        {
            await ValidarPesagemAsync(contexto.CodigoUsuario, contexto.CodigoSetorUsuario, item, pesagem, tarasDoSetor, cancellationToken);
        }
    }
    private async Task ValidarItemAsync(
        long usuario, long? codigoSetor, EntradaProdutoItem item,
        IReadOnlySet<long> tarasDoSetor, CancellationToken cancellationToken)
    {
        // H5: centro/deposito autorizado.
        if (!_autorizacaoCentroDeposito.ItemAutorizado(item.Centro, item.Deposito))
        {
            await NegarAsync(usuario, $"Item {item.NumeroItem} fora do centro/deposito autorizado para a entrada.", cancellationToken);
        }

        // Pedido/item ativo + material valido (quando vinculado ao item SAP).
        if (item.CodigoSapPedidoCompraItem is long codigoSap && codigoSap > 0)
        {
            ValidacaoItemPesagem validacao = await _itemRepositorio.ValidarItemAsync(codigoSap, cancellationToken);
            if (!validacao.Existe)
            {
                await NegarAsync(usuario, $"Item {item.NumeroItem} nao encontrado no cache do pedido.", cancellationToken);
            }
            if (!validacao.PedidoAtivo)
            {
                await NegarAsync(usuario, "Pedido de compra inativo. Entrada nao permitida.", cancellationToken);
            }
            if (!validacao.ItemAtivo)
            {
                await NegarAsync(usuario, $"Item {item.NumeroItem} inativo. Entrada nao permitida.", cancellationToken);
            }
            if (!validacao.MaterialPresente)
            {
                await NegarAsync(usuario, $"Item {item.NumeroItem} sem material valido. Entrada nao permitida.", cancellationToken);
            }
        }

        foreach (EntradaProdutoPesagem pesagem in item.Pesagens)
        {
            await ValidarPesagemAsync(usuario, codigoSetor, item, pesagem, tarasDoSetor, cancellationToken);
        }
    }

    private async Task ValidarPesagemAsync(
        long usuario, long? codigoSetor, EntradaProdutoItem item, EntradaProdutoPesagem pesagem,
        IReadOnlySet<long> tarasDoSetor, CancellationToken cancellationToken)
    {
        string origem = pesagem.Origem?.Trim() ?? string.Empty;
        if (!OrigensValidas.Contains(origem, StringComparer.OrdinalIgnoreCase))
        {
            await NegarAsync(usuario, $"Origem de peso invalida no item {item.NumeroItem}.", cancellationToken);
        }

        if (!StatusPesagemValidos.Contains(
                pesagem.StatusPesagem?.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            await NegarAsync(usuario, $"Status da pesagem invalido no item {item.NumeroItem}.", cancellationToken);
        }

        if (pesagem.PesoBrutoKg <= 0m)
        {
            await NegarAsync(usuario, $"Peso bruto deve ser maior que zero (item {item.NumeroItem}).", cancellationToken);
        }
        if (pesagem.PesoTaraKg < 0m)
        {
            await NegarAsync(usuario, $"Peso de tara nao pode ser negativo (item {item.NumeroItem}).", cancellationToken);
        }
        if (pesagem.PesoBrutoKg <= pesagem.PesoTaraKg)
        {
            await NegarAsync(usuario, $"Peso bruto deve ser maior que a tara (item {item.NumeroItem}).", cancellationToken);
        }
        if (pesagem.PesoLiquidoKg <= 0m)
        {
            await NegarAsync(usuario, $"Peso liquido deve ser maior que zero (item {item.NumeroItem}).", cancellationToken);
        }
        // Liquido deve bater com bruto - tara (tolerancia 0.001 kg), conforme regra endurecida do Gaia.
        if (Math.Abs(pesagem.PesoLiquidoKg - (pesagem.PesoBrutoKg - pesagem.PesoTaraKg)) > ToleranciaPesoKg)
        {
            await NegarAsync(usuario, $"Peso liquido deve ser igual ao bruto menos a tara (item {item.NumeroItem}).", cancellationToken);
        }
        try
        {
            ValidadorEntradaProdutoPersistenciaLotes.ValidarTaraDoSetor(
                pesagem.CodigoTara,
                tarasDoSetor,
                item.NumeroItem);
        }
        catch (InvalidOperationException ex)
        {
            await NegarAsync(usuario, ex.Message, cancellationToken);
        }

        if (pesagem.CodigoBalanca is long codigoBalanca && codigoBalanca > 0)
        {
            BalancaCadastro? balanca = _balancaRepositorio is null
                ? null
                : await _balancaRepositorio.ObterPorIdAsync(codigoBalanca, cancellationToken);
            try
            {
                ValidadorEntradaProdutoPersistenciaLotes.ValidarBalancaDoSetor(
                    pesagem.CodigoBalanca,
                    balanca,
                    _balancaRepositorio is not null,
                    codigoSetor ?? 0,
                    item.NumeroItem);
            }
            catch (InvalidOperationException ex)
            {
                await NegarAsync(usuario, ex.Message, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlySet<long>> CarregarTarasDoSetorAsync(long? codigoSetor, CancellationToken cancellationToken)
    {
        if (codigoSetor is not long setor || setor <= 0)
        {
            return new HashSet<long>();
        }

        IReadOnlyList<TaraCadastro> taras = await _taraRepositorio.ListarAtivasPorSetorAsync(setor, cancellationToken);
        return taras.Select(tara => tara.CodigoTara).ToHashSet();
    }

    private static ContextoAuditoriaEntradaLotes CapturarContextoAuditoriaEntradaLotes()
    {
        SessaoUsuarioAplicacao? sessao = EstadoSessaoUsuarioAtual.SessaoAtual;
        return ContextoAuditoriaEntradaLotes.Criar(
            sessao?.IdUsuario,
            sessao?.Login,
            sessao?.IdSetorPadrao,
            DateTime.Today.Date);
    }
    private static long ExigirUsuarioAutenticado()
    {
        long? usuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        return usuario ?? throw new ErroOperacionalEsperadoException(
            "Usuario nao autenticado. Faca login para registrar a entrada.");
    }

    private async Task NegarAsync(long usuario, string mensagem, CancellationToken cancellationToken)
    {
        if (_auditoria is not null)
        {
            try
            {
                await _auditoria.RegistrarAcessoNegadoAsync(usuario, mensagem, TelaAuditoria, cancellationToken);
            }
            catch
            {
                // Auditoria e best-effort; sua falha nao libera a operacao.
            }
        }

        throw new ErroOperacionalEsperadoException(mensagem);
    }

    private static IFabricaConexaoBanco Fabrica()
        => new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar());

    private static AuditoriaServico? CriarAuditoriaPadrao()
    {
        try
        {
            return new AuditoriaServico(new AuditoriaAcaoUsuarioServico(new AuditoriaAcaoUsuarioRepositorio(Fabrica())));
        }
        catch
        {
            return null;
        }
    }
}




