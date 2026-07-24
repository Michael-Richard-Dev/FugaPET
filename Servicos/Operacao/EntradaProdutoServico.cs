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

        // Tara x setor: a tara usada deve ser ativa e do setor do lancamento.
        if (pesagem.CodigoTara is long codigoTara && codigoTara > 0
            && tarasDoSetor.Count > 0 && !tarasDoSetor.Contains(codigoTara))
        {
            await NegarAsync(usuario, $"Tara selecionada nao pertence ao setor autorizado (item {item.NumeroItem}).", cancellationToken);
        }

        // Balanca: quando informada, deve estar ativa e pertencer ao setor do lancamento.
        if (pesagem.CodigoBalanca is long codigoBalanca && codigoBalanca > 0 && _balancaRepositorio is not null)
        {
            BalancaCadastro? balanca = await _balancaRepositorio.ObterPorIdAsync(codigoBalanca, cancellationToken);
            if (balanca is null || !balanca.SituacaoBalanca)
            {
                await NegarAsync(usuario, $"Balanca informada nao encontrada ou inativa (item {item.NumeroItem}).", cancellationToken);
            }
            if (codigoSetor is long setorLancamento && balanca!.CodigoSetor != setorLancamento)
            {
                await NegarAsync(usuario, $"Balanca nao pertence ao setor do lancamento (item {item.NumeroItem}).", cancellationToken);
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
