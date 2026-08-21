namespace FugaPET_Dev.Modelo.Entrada;

public static class ValidadorEntradaProdutoArvoreLotes
{
    private const decimal ToleranciaPesoKg = 0.001m;
    private static readonly string[] OrigensValidas = ["BALANCA", "MANUAL"];
    private static readonly string[] StatusPesagemValidos = ["VALIDA", "CANCELADA", "ESTORNADA"];

    public static void Validar(EntradaProdutoLancamentoComLotesPersistencia? entrada, DateTime? dataReferencia = null)
    {
        if (entrada is null)
        {
            throw new ArgumentNullException(nameof(entrada));
        }

        DateTime referencia = (dataReferencia ?? DateTime.Today).Date;
        ValidadorDadosLoteEntrada validadorLote = new(() => referencia);

        if (entrada.Lancamento is null)
        {
            throw new InvalidOperationException("Lançamento de entrada com lotes é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(entrada.Lancamento.NumeroPedido))
        {
            throw new InvalidOperationException("Número do pedido é obrigatório para persistir a árvore de lotes.");
        }

        if (entrada.Itens is null)
        {
            throw new InvalidOperationException("Itens da entrada com lotes são obrigatórios.");
        }

        if (entrada.Itens.Count == 0)
        {
            throw new InvalidOperationException("Informe pelo menos um item para persistir a árvore de lotes.");
        }

        HashSet<string> itensSap = new(StringComparer.Ordinal);
        HashSet<Guid> codigosLote = [];
        HashSet<Guid> correlationIds = [];
        HashSet<Guid> codigosPesagem = [];

        foreach (EntradaProdutoItemComLotesPersistencia? itemPersistencia in entrada.Itens)
        {
            ValidarItem(itemPersistencia, itensSap, codigosLote, correlationIds, codigosPesagem, validadorLote);
        }
    }

    public static string ValidarLoginAuditoriaLote(string? login)
    {
        string loginNormalizado = (login ?? string.Empty).Trim();
        if (loginNormalizado.Length == 0)
        {
            throw new InvalidOperationException("Login do usuário autenticado é obrigatório para persistir lotes da entrada.");
        }

        if (loginNormalizado.Length > 80)
        {
            throw new InvalidOperationException("Login do usuário autenticado deve ter no máximo 80 caracteres para persistir lotes da entrada.");
        }

        return loginNormalizado;
    }

    public static decimal NormalizarPeso(decimal peso)
        => decimal.Round(peso, 3, MidpointRounding.AwayFromZero);

    public static void ConferirPesoConsolidado(Guid codigoLocalLote, decimal pesoAplicacao, decimal pesoBanco)
    {
        decimal diferenca = Math.Abs(NormalizarPeso(pesoAplicacao) - NormalizarPeso(pesoBanco));
        if (diferenca > ToleranciaPesoKg)
        {
            throw new InvalidOperationException(
                $"Peso consolidado divergente para lote local {codigoLocalLote}: aplicação={NormalizarPeso(pesoAplicacao):0.000} kg, banco={NormalizarPeso(pesoBanco):0.000} kg.");
        }
    }

    private static void ValidarItem(
        EntradaProdutoItemComLotesPersistencia? itemPersistencia,
        HashSet<string> itensSap,
        HashSet<Guid> codigosLote,
        HashSet<Guid> correlationIds,
        HashSet<Guid> codigosPesagem,
        ValidadorDadosLoteEntrada validadorLote)
    {
        if (itemPersistencia is null)
        {
            throw new InvalidOperationException("Item da entrada com lotes não pode ser nulo.");
        }

        if (itemPersistencia.Item is null)
        {
            throw new InvalidOperationException("Item da entrada com lotes é obrigatório.");
        }

        string numeroItemSap = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(itemPersistencia.NumeroItemSap);
        string numeroItem = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(itemPersistencia.Item.NumeroItem);
        if (!string.Equals(numeroItemSap, numeroItem, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Item SAP {numeroItemSap} inconsistente com o número do item {itemPersistencia.Item.NumeroItem}.");
        }

        if (!itensSap.Add(numeroItemSap))
        {
            throw new InvalidOperationException($"Item SAP duplicado na árvore de lotes: {numeroItemSap}.");
        }

        if (itemPersistencia.Lotes is null)
        {
            throw new InvalidOperationException($"Lotes do item SAP {numeroItemSap} são obrigatórios para persistência.");
        }

        if (itemPersistencia.Lotes.Count == 0)
        {
            throw new InvalidOperationException($"Item SAP {numeroItemSap} não possui lote para persistência.");
        }

        HashSet<int> sequenciasDoItem = [];
        HashSet<string> lotesFuncionaisDoItem = new(StringComparer.Ordinal);
        Dictionary<string, string> datasPorNumeroLote = new(StringComparer.Ordinal);
        foreach (EntradaProdutoLoteComPesagensPersistencia? lote in itemPersistencia.Lotes)
        {
            ValidarLote(
                numeroItemSap,
                lote,
                codigosLote,
                correlationIds,
                codigosPesagem,
                sequenciasDoItem,
                lotesFuncionaisDoItem,
                datasPorNumeroLote,
                validadorLote);
        }
    }

    private static void ValidarLote(
        string numeroItemSap,
        EntradaProdutoLoteComPesagensPersistencia? lote,
        HashSet<Guid> codigosLote,
        HashSet<Guid> correlationIds,
        HashSet<Guid> codigosPesagem,
        HashSet<int> sequenciasDoItem,
        HashSet<string> lotesFuncionaisDoItem,
        Dictionary<string, string> datasPorNumeroLote,
        ValidadorDadosLoteEntrada validadorLote)
    {
        if (lote is null)
        {
            throw new InvalidOperationException($"Lote do item SAP {numeroItemSap} não pode ser nulo.");
        }

        if (lote.CodigoLocal == Guid.Empty)
        {
            throw new InvalidOperationException($"Lote local vazio no item SAP {numeroItemSap}.");
        }

        if (!codigosLote.Add(lote.CodigoLocal))
        {
            throw new InvalidOperationException($"Lote local duplicado na árvore: {lote.CodigoLocal}.");
        }

        if (lote.CorrelationId == Guid.Empty)
        {
            throw new InvalidOperationException($"CorrelationId vazio no lote local {lote.CodigoLocal}.");
        }

        if (!correlationIds.Add(lote.CorrelationId))
        {
            throw new InvalidOperationException($"CorrelationId duplicado na árvore: {lote.CorrelationId}.");
        }

        ValidarEstadoOperacionalLote(numeroItemSap, lote);
        ValidarDadosLote(numeroItemSap, lote, lotesFuncionaisDoItem, datasPorNumeroLote, validadorLote);

        if (lote.Pesagens is null)
        {
            throw new InvalidOperationException($"Pesagens do lote local {lote.CodigoLocal} são obrigatórias para persistência.");
        }

        if (lote.Pesagens.Count == 0)
        {
            throw new InvalidOperationException($"Lote local {lote.CodigoLocal} não possui pesagem para persistência.");
        }

        decimal somaValida = 0m;
        foreach (EntradaProdutoPesagemComCodigoLocalPersistencia? pesagem in lote.Pesagens)
        {
            ValidarPesagem(numeroItemSap, lote, pesagem, codigosPesagem, sequenciasDoItem, ref somaValida);
        }

        if (somaValida <= 0m)
        {
            throw new InvalidOperationException($"Lote local {lote.CodigoLocal} não possui soma válida maior que zero.");
        }
    }

    private static void ValidarEstadoOperacionalLote(string numeroItemSap, EntradaProdutoLoteComPesagensPersistencia lote)
    {
        if (!Enum.IsDefined(lote.EstadoOperacional)
            || lote.EstadoOperacional != EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
        {
            throw new InvalidOperationException(
                $"Lote local {lote.CodigoLocal} do item SAP {numeroItemSap} deve estar finalizado em memória antes da persistência.");
        }
    }

    private static void ValidarDadosLote(
        string numeroItemSap,
        EntradaProdutoLoteComPesagensPersistencia lote,
        HashSet<string> lotesFuncionaisDoItem,
        Dictionary<string, string> datasPorNumeroLote,
        ValidadorDadosLoteEntrada validadorLote)
    {
        if (lote.Dados is null)
        {
            throw new InvalidOperationException($"Dados do lote são obrigatórios no item SAP {numeroItemSap}, lote local {lote.CodigoLocal}.");
        }

        ResultadoValidacaoLoteEntrada resultado = validadorLote.Validar(
            lote.Dados.NumeroLote,
            lote.Dados.DataFabricacao,
            lote.Dados.DataVencimento);
        if (!resultado.Sucesso)
        {
            throw new InvalidOperationException($"{resultado.Mensagem} Item SAP {numeroItemSap}, lote local {lote.CodigoLocal}.");
        }

        string numeroLote = resultado.DadosNormalizados!.NumeroLote;
        string chaveDatas = $"{resultado.DadosNormalizados!.DataFabricacao:yyyy-MM-dd}|{resultado.DadosNormalizados!.DataVencimento:yyyy-MM-dd}";
        string chaveFuncional = $"{numeroLote}|{chaveDatas}";
        if (!lotesFuncionaisDoItem.Add(chaveFuncional))
        {
            throw new InvalidOperationException(
                $"Lote {numeroLote} duplicado para o item SAP {numeroItemSap} com as mesmas datas.");
        }

        if (datasPorNumeroLote.TryGetValue(numeroLote, out string? datasExistentes)
            && !string.Equals(datasExistentes, chaveDatas, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Lote {numeroLote} já existe para o item SAP {numeroItemSap} com datas diferentes. Confira fabricação e vencimento.");
        }

        datasPorNumeroLote[numeroLote] = chaveDatas;
    }

    private static void ValidarPesagem(
        string numeroItemSap,
        EntradaProdutoLoteComPesagensPersistencia lote,
        EntradaProdutoPesagemComCodigoLocalPersistencia? pesagemLocal,
        HashSet<Guid> codigosPesagem,
        HashSet<int> sequenciasDoItem,
        ref decimal somaValida)
    {
        if (pesagemLocal is null)
        {
            throw new InvalidOperationException($"Pesagem local do lote {lote.CodigoLocal} não pode ser nula.");
        }

        if (pesagemLocal.CodigoLocalPesagem == Guid.Empty)
        {
            throw new InvalidOperationException($"Pesagem local vazia no lote local {lote.CodigoLocal}.");
        }

        if (!codigosPesagem.Add(pesagemLocal.CodigoLocalPesagem))
        {
            throw new InvalidOperationException($"Pesagem local duplicada na árvore: {pesagemLocal.CodigoLocalPesagem}.");
        }

        EntradaProdutoPesagem pesagem = pesagemLocal.Pesagem ?? throw new InvalidOperationException(
            $"Pesagem local {pesagemLocal.CodigoLocalPesagem} sem dados no lote local {lote.CodigoLocal}.");

        if (pesagem.Sequencia <= 0)
        {
            throw new InvalidOperationException($"Sequência inválida na pesagem local {pesagemLocal.CodigoLocalPesagem}, item SAP {numeroItemSap}.");
        }

        if (!sequenciasDoItem.Add(pesagem.Sequencia))
        {
            throw new InvalidOperationException($"Sequência duplicada no item SAP {numeroItemSap}: {pesagem.Sequencia}.");
        }

        ValidarOrigemStatus(numeroItemSap, pesagem);
        ValidarPesos(numeroItemSap, pesagem);

        if (pesagem.StatusPesagem == EntradaProdutoPesagemCalculos.StatusValida)
        {
            somaValida += pesagem.PesoLiquidoKg;
        }
    }

    private static void ValidarOrigemStatus(string numeroItemSap, EntradaProdutoPesagem pesagem)
    {
        if (!OrigensValidas.Contains(pesagem.Origem))
        {
            throw new InvalidOperationException($"Origem de peso inválida no item SAP {numeroItemSap}.");
        }

        if (!StatusPesagemValidos.Contains(pesagem.StatusPesagem))
        {
            throw new InvalidOperationException($"Status da pesagem inválido no item SAP {numeroItemSap}.");
        }
    }

    private static void ValidarPesos(string numeroItemSap, EntradaProdutoPesagem pesagem)
    {
        if (pesagem.PesoBrutoKg <= 0m)
        {
            throw new InvalidOperationException($"Peso bruto deve ser maior que zero (item SAP {numeroItemSap}).");
        }

        if (pesagem.PesoTaraKg < 0m)
        {
            throw new InvalidOperationException($"Peso de tara não pode ser negativo (item SAP {numeroItemSap}).");
        }

        if (pesagem.PesoBrutoKg <= pesagem.PesoTaraKg)
        {
            throw new InvalidOperationException($"Peso bruto deve ser maior que a tara (item SAP {numeroItemSap}).");
        }

        if (pesagem.PesoLiquidoKg <= 0m)
        {
            throw new InvalidOperationException($"Peso líquido deve ser maior que zero (item SAP {numeroItemSap}).");
        }

        if (Math.Abs(pesagem.PesoLiquidoKg - (pesagem.PesoBrutoKg - pesagem.PesoTaraKg)) > ToleranciaPesoKg)
        {
            throw new InvalidOperationException($"Peso líquido deve ser igual ao bruto menos a tara (item SAP {numeroItemSap}).");
        }
    }
}

