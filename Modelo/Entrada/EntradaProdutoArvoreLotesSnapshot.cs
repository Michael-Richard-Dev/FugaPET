namespace FugaPET_Dev.Modelo.Entrada;

public static class EntradaProdutoArvoreLotesSnapshot
{
    public static EntradaProdutoLancamentoComLotesPersistencia Criar(EntradaProdutoLancamentoComLotesPersistencia? entrada)
    {
        if (entrada is null)
        {
            throw new ArgumentNullException(nameof(entrada));
        }

        IReadOnlyList<EntradaProdutoItemComLotesPersistencia> itens = entrada.Itens?
            .Select(CopiarItemComLotes)
            .ToList()
            ?? throw new InvalidOperationException("Itens da entrada com lotes são obrigatórios.");

        IReadOnlyList<EntradaProdutoItem> itensLancamento = itens
            .Select(item => item.Item)
            .ToList();

        return new EntradaProdutoLancamentoComLotesPersistencia
        {
            Lancamento = CopiarLancamento(entrada.Lancamento, itensLancamento),
            Itens = itens
        };
    }

    private static EntradaProdutoLancamento CopiarLancamento(
        EntradaProdutoLancamento? lancamento,
        IReadOnlyList<EntradaProdutoItem> itens)
    {
        if (lancamento is null)
        {
            throw new InvalidOperationException("Lançamento de entrada com lotes é obrigatório.");
        }

        return lancamento with
        {
            NumeroPedido = NormalizarTexto(lancamento.NumeroPedido),
            Fornecedor = NormalizarTextoOuNulo(lancamento.Fornecedor),
            Terminal = NormalizarTextoOuNulo(lancamento.Terminal),
            Itens = itens.ToList()
        };
    }

    private static EntradaProdutoItemComLotesPersistencia CopiarItemComLotes(
        EntradaProdutoItemComLotesPersistencia? itemPersistencia)
    {
        if (itemPersistencia is null)
        {
            throw new InvalidOperationException("Item da entrada com lotes não pode ser nulo.");
        }

        IReadOnlyList<EntradaProdutoLoteComPesagensPersistencia> lotes = itemPersistencia.Lotes?
            .Select(CopiarLote)
            .ToList()
            ?? throw new InvalidOperationException("Lotes do item são obrigatórios para persistência.");

        IReadOnlyList<EntradaProdutoPesagem> pesagens = lotes
            .SelectMany(lote => lote.Pesagens)
            .Select(pesagemLocal => pesagemLocal.Pesagem)
            .ToList();

        return new EntradaProdutoItemComLotesPersistencia
        {
            Item = CopiarItem(itemPersistencia.Item, pesagens),
            NumeroItemSap = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(itemPersistencia.NumeroItemSap),
            Lotes = lotes
        };
    }

    private static EntradaProdutoItem CopiarItem(
        EntradaProdutoItem? item,
        IReadOnlyList<EntradaProdutoPesagem> pesagens)
    {
        if (item is null)
        {
            throw new InvalidOperationException("Item da entrada com lotes é obrigatório.");
        }

        return item with
        {
            NumeroItem = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(item.NumeroItem),
            Material = NormalizarTexto(item.Material),
            Centro = NormalizarTexto(item.Centro),
            Deposito = NormalizarTexto(item.Deposito),
            Unidade = NormalizarTexto(item.Unidade),
            Pesagens = pesagens.ToList()
        };
    }

    private static EntradaProdutoLoteComPesagensPersistencia CopiarLote(EntradaProdutoLoteComPesagensPersistencia? lote)
    {
        if (lote is null)
        {
            throw new InvalidOperationException("Lote da entrada não pode ser nulo.");
        }

        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens = lote.Pesagens?
            .Select(CopiarPesagemLocal)
            .ToList()
            ?? throw new InvalidOperationException("Pesagens do lote são obrigatórias para persistência.");

        return new EntradaProdutoLoteComPesagensPersistencia
        {
            CodigoLocal = lote.CodigoLocal,
            Dados = CopiarDadosLote(lote.Dados),
            CorrelationId = lote.CorrelationId,
            EstadoOperacional = lote.EstadoOperacional,
            Pesagens = pesagens
        };
    }

    private static DadosLoteEntrada CopiarDadosLote(DadosLoteEntrada? dados)
    {
        if (dados is null)
        {
            throw new InvalidOperationException("Dados do lote são obrigatórios para persistência.");
        }

        return new DadosLoteEntrada(dados.NumeroLote, dados.DataFabricacao, dados.DataVencimento);
    }

    private static EntradaProdutoPesagemComCodigoLocalPersistencia CopiarPesagemLocal(
        EntradaProdutoPesagemComCodigoLocalPersistencia? pesagemLocal)
    {
        if (pesagemLocal is null)
        {
            throw new InvalidOperationException("Pesagem local do lote não pode ser nula.");
        }

        return new EntradaProdutoPesagemComCodigoLocalPersistencia
        {
            CodigoLocalPesagem = pesagemLocal.CodigoLocalPesagem,
            Pesagem = CopiarPesagem(pesagemLocal.Pesagem)
        };
    }

    private static EntradaProdutoPesagem CopiarPesagem(EntradaProdutoPesagem? pesagem)
    {
        if (pesagem is null)
        {
            throw new InvalidOperationException("Dados da pesagem são obrigatórios para persistência.");
        }

        return pesagem with
        {
            Origem = NormalizarCodigo(pesagem.Origem),
            StatusPesagem = NormalizarCodigo(pesagem.StatusPesagem)
        };
    }

    private static string NormalizarCodigo(string? valor)
        => (valor ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizarTexto(string? valor)
        => (valor ?? string.Empty).Trim();

    private static string? NormalizarTextoOuNulo(string? valor)
    {
        string normalizado = NormalizarTexto(valor);
        return normalizado.Length == 0 ? null : normalizado;
    }
}

