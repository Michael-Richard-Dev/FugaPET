using System.Collections.ObjectModel;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class EntradaProdutoLotesOrquestrador
{
    private readonly ValidadorDadosLoteEntrada _validadorDadosLote;
    private readonly Dictionary<long, EntradaProdutoItemSapSnapshot> _snapshotsPorCodigo = [];
    private readonly Dictionary<string, EntradaProdutoItemSapSnapshot> _snapshotsPorNumero = new(StringComparer.Ordinal);
    private EntradaProdutoOperacaoEmMemoria _operacao = new();
    private ContextoOperacaoEntradaProdutoLotes? _contexto;

    public EntradaProdutoLotesOrquestrador(ValidadorDadosLoteEntrada? validadorDadosLote = null)
    {
        _validadorDadosLote = validadorDadosLote ?? new ValidadorDadosLoteEntrada();
    }

    public EstadoOperacaoEntradaProdutoLotes IniciarOperacao(
        ContextoOperacaoEntradaProdutoLotes contexto,
        IReadOnlyList<PedidoCompraSapItem> itensSap)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(itensSap);

        if (_contexto is not null)
        {
            throw new InvalidOperationException("Operação de entrada por lotes já iniciada. Limpe a operação antes de iniciar outra.");
        }

        if (itensSap.Count == 0)
        {
            throw new InvalidOperationException("Informe ao menos um item SAP para iniciar a operação por lotes.");
        }

        Dictionary<long, EntradaProdutoItemSapSnapshot> snapshotsPorCodigoTemporario = [];
        Dictionary<string, EntradaProdutoItemSapSnapshot> snapshotsPorNumeroTemporario = new(StringComparer.Ordinal);
        foreach (PedidoCompraSapItem itemSap in itensSap)
        {
            EntradaProdutoItemSapSnapshot snapshot = EntradaProdutoItemSapSnapshot.Criar(itemSap);
            if (!snapshotsPorCodigoTemporario.TryAdd(snapshot.CodigoSapPedidoCompraItem, snapshot))
            {
                throw new InvalidOperationException($"Código local do item SAP duplicado: {snapshot.CodigoSapPedidoCompraItem}.");
            }

            if (!snapshotsPorNumeroTemporario.TryAdd(snapshot.NumeroItemSap, snapshot))
            {
                throw new InvalidOperationException($"Número do item SAP duplicado: {snapshot.NumeroItemSap}.");
            }
        }

        _contexto = contexto;
        _operacao = new EntradaProdutoOperacaoEmMemoria();
        _snapshotsPorCodigo.Clear();
        _snapshotsPorNumero.Clear();

        foreach (KeyValuePair<long, EntradaProdutoItemSapSnapshot> snapshot in snapshotsPorCodigoTemporario)
        {
            _snapshotsPorCodigo.Add(snapshot.Key, snapshot.Value);
        }

        foreach (KeyValuePair<string, EntradaProdutoItemSapSnapshot> snapshot in snapshotsPorNumeroTemporario)
        {
            _snapshotsPorNumero.Add(snapshot.Key, snapshot.Value);
        }

        return ObterEstado();
    }

    public EstadoOperacaoEntradaProdutoLotes SelecionarItem(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        _operacao.SelecionarItem(snapshot.NumeroItemSap, snapshot.Material);
        return ObterEstado();
    }

    public EstadoOperacaoEntradaProdutoLotes ConfirmarLote(
        long codigoSapPedidoCompraItem,
        string? numeroLote,
        DateTime? dataFabricacao,
        DateTime? dataVencimento)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        EntradaProdutoItemEmMemoria item = _operacao.SelecionarItem(snapshot.NumeroItemSap, snapshot.Material);
        EntradaProdutoLoteEmMemoria? loteAtivo = item.ObterLoteAtivo();
        if (loteAtivo is not null && loteAtivo.Estado != EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
        {
            throw new InvalidOperationException("Finalize ou cancele o lote ativo antes de informar um novo lote para o item.");
        }

        ResultadoValidacaoLoteEntrada validacao = _validadorDadosLote.Validar(numeroLote, dataFabricacao, dataVencimento, item);
        if (!validacao.Sucesso)
        {
            throw new InvalidOperationException(validacao.Mensagem);
        }

        if (validacao.LoteExistente is not null)
        {
            throw new InvalidOperationException("Lote já existe para este item com o mesmo número e datas.");
        }

        EntradaProdutoLoteEmMemoria lote = item.AdicionarLote(validacao.DadosNormalizados!);
        lote.Confirmar();
        item.DefinirLoteAtivo(lote.CodigoLocal);
        return ObterEstado();
    }

    public EntradaProdutoPesagemEmMemoria RegistrarPesagemNoLoteAtivo(
        long codigoSapPedidoCompraItem,
        decimal pesoBrutoKg,
        decimal pesoTaraKg,
        long? codigoTara,
        string origem,
        long? codigoBalanca,
        string? leituraOriginal = null,
        DateTimeOffset? pesadoEm = null)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        EntradaProdutoItemEmMemoria item = _operacao.SelecionarItem(snapshot.NumeroItemSap, snapshot.Material);
        EntradaProdutoLoteEmMemoria lote = item.ObterLoteAtivo()
            ?? throw new InvalidOperationException("Confirme um lote antes de registrar pesagem.");

        string origemNormalizada = NormalizarOrigem(origem);
        if (origemNormalizada == EntradaProdutoPesagemCalculos.OrigemManual)
        {
            codigoBalanca = null;
        }

        IReadOnlyList<EntradaProdutoPesagem> pesagensDoItem = item.Lotes.SelectMany(l => l.Pesagens).ToList();
        EntradaProdutoPesagem pesagem = EntradaProdutoPesagemCalculos.MontarLeitura(
            pesagensDoItem,
            pesoBrutoKg,
            pesoTaraKg,
            codigoTara,
            origemNormalizada,
            codigoBalanca,
            leituraOriginal ?? string.Empty,
            pesadoEm ?? DateTimeOffset.Now);

        return lote.RegistrarPesagem(pesagem);
    }

    public int CancelarPesagensDoLoteAtivo(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        EntradaProdutoItemEmMemoria item = _operacao.SelecionarItem(snapshot.NumeroItemSap, snapshot.Material);
        EntradaProdutoLoteEmMemoria lote = item.ObterLoteAtivo()
            ?? throw new InvalidOperationException("Nenhum lote ativo para cancelar pesagens.");

        return lote.CancelarPesagens();
    }

    public EntradaProdutoPesagemEmMemoria CancelarPesagemDoLoteAtivo(
        long codigoSapPedidoCompraItem,
        Guid codigoLocalPesagem)
    {
        EntradaProdutoLoteEmMemoria lote = ObterLoteAtivoDoItem(codigoSapPedidoCompraItem);
        return lote.CancelarPesagem(codigoLocalPesagem);
    }

    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> ObterPesagensDoItem(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        if (!_operacao.ItensPorNumeroItemSap.TryGetValue(snapshot.NumeroItemSap, out EntradaProdutoItemEmMemoria? item))
        {
            return Array.Empty<EntradaProdutoPesagemEmMemoria>();
        }

        return new ReadOnlyCollection<EntradaProdutoPesagemEmMemoria>(item.Lotes
            .SelectMany(lote => lote.PesagensComCodigoLocal)
            .OrderBy(pesagem => pesagem.Pesagem.Sequencia)
            .ToList());
    }

    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> ObterPesagensDoLoteAtivo(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoLoteEmMemoria lote = ObterLoteAtivoDoItem(codigoSapPedidoCompraItem);
        return new ReadOnlyCollection<EntradaProdutoPesagemEmMemoria>(lote.PesagensComCodigoLocal
            .OrderBy(pesagem => pesagem.Pesagem.Sequencia)
            .ToList());
    }

    public EstadoOperacaoEntradaProdutoLotes FinalizarLoteAtivo(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        EntradaProdutoItemEmMemoria item = _operacao.SelecionarItem(snapshot.NumeroItemSap, snapshot.Material);
        EntradaProdutoLoteEmMemoria lote = item.ObterLoteAtivo()
            ?? throw new InvalidOperationException("Nenhum lote ativo para finalizar.");

        lote.FinalizarEmMemoria();
        return ObterEstado();
    }

    public EntradaProdutoLancamentoComLotesPersistencia MontarLancamentoComLotesParaPersistencia()
    {
        ContextoOperacaoEntradaProdutoLotes contexto = ObterContexto();
        List<EntradaProdutoItemComLotesPersistencia> itensPersistencia = [];

        foreach (EntradaProdutoItemEmMemoria itemMemoria in _operacao.ItensPorNumeroItemSap.Values)
        {
            if (itemMemoria.Lotes.Count == 0)
            {
                continue;
            }

            EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorNumero(itemMemoria.NumeroItemSap);
            foreach (EntradaProdutoLoteEmMemoria lote in itemMemoria.Lotes)
            {
                if (lote.Estado != EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
                {
                    throw new InvalidOperationException($"Lote {lote.Dados.NumeroLote} do item SAP {itemMemoria.NumeroItemSap} ainda não foi finalizado em memória.");
                }
            }

            IReadOnlyList<EntradaProdutoPesagem> pesagensDoItem = itemMemoria.Lotes.SelectMany(l => l.Pesagens).ToList();
            EntradaProdutoItem itemPersistencia = new()
            {
                CodigoSapPedidoCompraItem = snapshot.CodigoSapPedidoCompraItem,
                NumeroItem = snapshot.NumeroItemSap,
                Material = snapshot.Material,
                Centro = snapshot.Centro,
                Deposito = snapshot.Deposito,
                Unidade = snapshot.Unidade,
                QuantidadePrevista = snapshot.QuantidadePrevista,
                QuantidadeRecebida = EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(pesagensDoItem),
                Pesagens = pesagensDoItem
            };

            itensPersistencia.Add(new EntradaProdutoItemComLotesPersistencia
            {
                Item = itemPersistencia,
                NumeroItemSap = snapshot.NumeroItemSap,
                Lotes = itemMemoria.Lotes.Select(EntradaProdutoLoteComPesagensPersistencia.CriarDeLoteFinalizado).ToList()
            });
        }

        EntradaProdutoLancamentoComLotesPersistencia arvore = new()
        {
            Lancamento = new EntradaProdutoLancamento
            {
                NumeroPedido = contexto.NumeroPedido,
                Fornecedor = contexto.Fornecedor,
                CodigoSetor = contexto.CodigoSetor,
                Terminal = contexto.Terminal,
                Itens = itensPersistencia.Select(i => i.Item).ToList()
            },
            Itens = itensPersistencia
        };

        ValidadorEntradaProdutoArvoreLotes.Validar(arvore);
        return arvore;
    }

    public EstadoOperacaoEntradaProdutoLotes ObterEstado()
        => new()
        {
            OperacaoIniciada = _contexto is not null,
            NumeroPedido = _contexto?.NumeroPedido,
            ModoEntradaMaterial = _contexto?.ModoEntradaMaterial,
            Itens = new ReadOnlyCollection<EstadoItemEntradaProdutoLotes>(_snapshotsPorNumero.Values
                .OrderBy(snapshot => snapshot.NumeroItemSap, StringComparer.Ordinal)
                .Select(MontarEstadoItem)
                .ToList()),
            NumeroItemSapSelecionado = _operacao.NumeroItemSapSelecionado
        };

    public void LimparOperacao()
    {
        _contexto = null;
        _operacao.Limpar();
        _snapshotsPorCodigo.Clear();
        _snapshotsPorNumero.Clear();
    }

    private ContextoOperacaoEntradaProdutoLotes ObterContexto()
        => _contexto ?? throw new InvalidOperationException("Operação de entrada por lotes ainda não foi iniciada.");

    private EntradaProdutoItemSapSnapshot ObterSnapshotPorCodigo(long codigoSapPedidoCompraItem)
    {
        ObterContexto();
        return _snapshotsPorCodigo.TryGetValue(codigoSapPedidoCompraItem, out EntradaProdutoItemSapSnapshot? snapshot)
            ? snapshot
            : throw new InvalidOperationException("Item SAP não pertence à operação por lotes atual.");
    }

    private EntradaProdutoLoteEmMemoria ObterLoteAtivoDoItem(long codigoSapPedidoCompraItem)
    {
        EntradaProdutoItemSapSnapshot snapshot = ObterSnapshotPorCodigo(codigoSapPedidoCompraItem);
        if (!_operacao.ItensPorNumeroItemSap.TryGetValue(snapshot.NumeroItemSap, out EntradaProdutoItemEmMemoria? item))
        {
            throw new InvalidOperationException("Nenhum lote ativo para o item informado.");
        }

        return item.ObterLoteAtivo()
            ?? throw new InvalidOperationException("Nenhum lote ativo para o item informado.");
    }

    private EntradaProdutoItemSapSnapshot ObterSnapshotPorNumero(string numeroItemSap)
    {
        string numeroNormalizado = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItemSap);
        return _snapshotsPorNumero.TryGetValue(numeroNormalizado, out EntradaProdutoItemSapSnapshot? snapshot)
            ? snapshot
            : throw new InvalidOperationException("Snapshot SAP do item não foi encontrado.");
    }

    private EstadoItemEntradaProdutoLotes MontarEstadoItem(EntradaProdutoItemSapSnapshot snapshot)
    {
        _operacao.ItensPorNumeroItemSap.TryGetValue(snapshot.NumeroItemSap, out EntradaProdutoItemEmMemoria? item);
        IReadOnlyList<EntradaProdutoLoteEmMemoria> lotes = item?.Lotes ?? [];
        EntradaProdutoLoteEmMemoria? loteAtivo = item?.ObterLoteAtivo();

        return new EstadoItemEntradaProdutoLotes
        {
            CodigoSapPedidoCompraItem = snapshot.CodigoSapPedidoCompraItem,
            NumeroItemSap = snapshot.NumeroItemSap,
            Material = snapshot.Material,
            Lotes = new ReadOnlyCollection<EstadoLoteEntradaProdutoLotes>(lotes.Select(MontarEstadoLote).ToList()),
            CodigoLoteAtivoLocal = loteAtivo?.CodigoLocal,
            PodeConfirmarNovoLote = loteAtivo is null || loteAtivo.Estado == EstadoOperacionalLoteEntrada.FinalizadoEmMemoria,
            PodePesar = loteAtivo?.PodeReceberPesagem == true,
            PodeFinalizarLote = loteAtivo is not null
                && loteAtivo.Estado == EstadoOperacionalLoteEntrada.Pesando
                && loteAtivo.PossuiPesagemValida
        };
    }

    private static EstadoLoteEntradaProdutoLotes MontarEstadoLote(EntradaProdutoLoteEmMemoria lote)
        => new()
        {
            CodigoLocal = lote.CodigoLocal,
            CorrelationId = lote.CorrelationId,
            Dados = lote.Dados,
            Estado = lote.Estado,
            QuantidadePesagens = lote.Pesagens.Count,
            QuantidadePesagensValidas = lote.Pesagens.Count(p =>
                string.Equals(p.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida, StringComparison.OrdinalIgnoreCase)),
            PesoLiquidoTotalMemoriaKg = lote.PesoLiquidoTotalMemoriaKg,
            PodeEditarDados = lote.PodeEditarDados,
            PodeReceberPesagem = lote.PodeReceberPesagem,
            PodeFinalizar = lote.Estado == EstadoOperacionalLoteEntrada.Pesando && lote.PossuiPesagemValida
        };

    private static string NormalizarOrigem(string? origem)
    {
        string origemNormalizada = (origem ?? string.Empty).Trim().ToUpperInvariant();
        if (origemNormalizada is EntradaProdutoPesagemCalculos.OrigemBalanca or EntradaProdutoPesagemCalculos.OrigemManual)
        {
            return origemNormalizada;
        }

        throw new ArgumentException("Origem da pesagem deve ser BALANCA ou MANUAL.", nameof(origem));
    }
}

public sealed record EstadoOperacaoEntradaProdutoLotes
{
    public bool OperacaoIniciada { get; init; }
    public string? NumeroPedido { get; init; }
    public ModoEntradaMaterial? ModoEntradaMaterial { get; init; }
    public string? NumeroItemSapSelecionado { get; init; }
    public IReadOnlyList<EstadoItemEntradaProdutoLotes> Itens { get; init; } = [];
}

public sealed record EstadoItemEntradaProdutoLotes
{
    public long CodigoSapPedidoCompraItem { get; init; }
    public string NumeroItemSap { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public Guid? CodigoLoteAtivoLocal { get; init; }
    public IReadOnlyList<EstadoLoteEntradaProdutoLotes> Lotes { get; init; } = [];
    public bool PodeConfirmarNovoLote { get; init; }
    public bool PodePesar { get; init; }
    public bool PodeFinalizarLote { get; init; }
}

public sealed record EstadoLoteEntradaProdutoLotes
{
    public Guid CodigoLocal { get; init; }
    public Guid CorrelationId { get; init; }
    public DadosLoteEntrada Dados { get; init; } = new();
    public EstadoOperacionalLoteEntrada Estado { get; init; }
    public int QuantidadePesagens { get; init; }
    public int QuantidadePesagensValidas { get; init; }
    public decimal PesoLiquidoTotalMemoriaKg { get; init; }
    public bool PodeEditarDados { get; init; }
    public bool PodeReceberPesagem { get; init; }
    public bool PodeFinalizar { get; init; }
}
