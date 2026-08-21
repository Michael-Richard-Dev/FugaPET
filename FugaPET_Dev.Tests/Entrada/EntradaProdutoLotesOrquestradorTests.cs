using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Entrada;

public sealed class EntradaProdutoLotesOrquestradorTests
{
    private static readonly DateTime Hoje = new(2026, 7, 24);

    [Fact]
    public void IniciarOperacao_DeveCriarSnapshotSemReferenciarListaOriginal()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();
        List<PedidoCompraSapItem> itens = [ItemSap(10, "3500027")];

        EstadoOperacaoEntradaProdutoLotes estado = orquestrador.IniciarOperacao(Contexto(), itens);
        itens.Clear();

        Assert.True(estado.OperacaoIniciada);
        Assert.Equal("4500001489", estado.NumeroPedido);
        Assert.Single(orquestrador.ObterEstado().Itens);
        Assert.Equal("00010", orquestrador.ObterEstado().Itens[0].NumeroItemSap);
    }

    [Fact]
    public void ConfirmarLote_DeveGerarGuidLocalECorrelationIdUmaUnicaVez()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        EstadoOperacaoEntradaProdutoLotes estado = orquestrador.ConfirmarLote(10, " lote-a ", Hoje, Hoje.AddDays(30));
        EstadoLoteEntradaProdutoLotes lote = estado.Itens.Single().Lotes.Single();

        Assert.NotEqual(Guid.Empty, lote.CodigoLocal);
        Assert.NotEqual(Guid.Empty, lote.CorrelationId);
        Assert.Equal("LOTE-A", lote.Dados.NumeroLote);
        Assert.True(estado.Itens.Single().PodePesar);
    }

    [Fact]
    public void ConfirmarLote_DeveBloquearNovoLoteEnquantoAnteriorNaoFinalizado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => orquestrador.ConfirmarLote(10, "LOTE-B", Hoje, Hoje.AddDays(30)));

        Assert.Contains("lote ativo", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistrarPesagem_DeveSequenciarPorItemMesmoEmLotesDiferentes()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria primeira = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 5m, 1m, 4, EntradaProdutoPesagemCalculos.OrigemBalanca, 7, "BAL=5", Hoje);
        orquestrador.FinalizarLoteAtivo(10);
        orquestrador.ConfirmarLote(10, "LOTE-B", Hoje, Hoje.AddDays(40));
        EntradaProdutoPesagemEmMemoria segunda = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, 99, "3", Hoje);

        Assert.Equal(1, primeira.Pesagem.Sequencia);
        Assert.Equal(2, segunda.Pesagem.Sequencia);
        Assert.Null(segunda.Pesagem.CodigoBalanca);
        Assert.NotEqual(primeira.CodigoLocalPesagem, segunda.CodigoLocalPesagem);
    }

    [Fact]
    public void CancelarPesagens_DevePreservarGuidEBloquearFinalizacaoSemValida()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria pesagem = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);

        int canceladas = orquestrador.CancelarPesagensDoLoteAtivo(10);
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.FinalizarLoteAtivo(10));

        Assert.Equal(1, canceladas);
        Assert.Contains("pesagem", erro.Message, StringComparison.OrdinalIgnoreCase);
        EstadoLoteEntradaProdutoLotes lote = orquestrador.ObterEstado().Itens.Single().Lotes.Single();
        Assert.Equal(0, lote.QuantidadePesagensValidas);
        Assert.Equal(0m, lote.PesoLiquidoTotalMemoriaKg);
        Assert.NotEqual(Guid.Empty, pesagem.CodigoLocalPesagem);
    }

    [Fact]
    public void CancelarPesagens_AposFinalizacaoDeveBloquearEPreservarPesagem()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria pesagem = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 4m, 1m, 6, EntradaProdutoPesagemCalculos.OrigemBalanca, 9, "BAL=4", Hoje);
        Guid codigoPesagem = pesagem.CodigoLocalPesagem;
        int sequencia = pesagem.Pesagem.Sequencia;
        string status = pesagem.Pesagem.StatusPesagem;
        decimal peso = pesagem.Pesagem.PesoLiquidoKg;

        orquestrador.FinalizarLoteAtivo(10);
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.CancelarPesagensDoLoteAtivo(10));
        EntradaProdutoLancamentoComLotesPersistencia arvore = orquestrador.MontarLancamentoComLotesParaPersistencia();
        EntradaProdutoPesagemComCodigoLocalPersistencia persistida = arvore.Itens.Single().Lotes.Single().Pesagens.Single();

        Assert.Contains("finalizado", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, arvore.Itens.Single().Lotes.Single().EstadoOperacional);
        Assert.Equal(codigoPesagem, persistida.CodigoLocalPesagem);
        Assert.Equal(sequencia, persistida.Pesagem.Sequencia);
        Assert.Equal(status, persistida.Pesagem.StatusPesagem);
        Assert.Equal(peso, persistida.Pesagem.PesoLiquidoKg);
        Assert.Equal(3m, arvore.Itens.Single().Item.QuantidadeRecebida);
    }

    [Fact]
    public void CancelarPesagens_EmLoteConfirmadoSemPesagensDeveRetornarZeroSemMudarEstado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));

        int canceladas = orquestrador.CancelarPesagensDoLoteAtivo(10);

        Assert.Equal(0, canceladas);
        Assert.Equal(EstadoOperacionalLoteEntrada.LoteConfirmado, orquestrador.ObterEstado().Itens.Single().Lotes.Single().Estado);
    }

    [Fact]
    public void MontarLancamento_DeveExigirTodosOsLotesFinalizados()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            orquestrador.MontarLancamentoComLotesParaPersistencia);

        Assert.Contains("ainda não foi finalizado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MontarLancamento_DeveCriarArvorePersistenciaComLotesPesagensEGuid()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0.5m, 3, EntradaProdutoPesagemCalculos.OrigemBalanca, 8, "BAL=2", Hoje);
        orquestrador.FinalizarLoteAtivo(10);

        EntradaProdutoLancamentoComLotesPersistencia arvore = orquestrador.MontarLancamentoComLotesParaPersistencia();

        Assert.Equal("4500001489", arvore.Lancamento.NumeroPedido);
        Assert.Single(arvore.Itens);
        Assert.Equal("00010", arvore.Itens[0].NumeroItemSap);
        Assert.Equal(1.5m, arvore.Itens[0].Item.QuantidadeRecebida);
        Assert.Single(arvore.Itens[0].Lotes);
        Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, arvore.Itens[0].Lotes[0].EstadoOperacional);
        Assert.NotEqual(Guid.Empty, arvore.Itens[0].Lotes[0].CodigoLocal);
        Assert.NotEqual(Guid.Empty, arvore.Itens[0].Lotes[0].Pesagens[0].CodigoLocalPesagem);
    }

    [Fact]
    public void IniciarOperacao_CodigoSapDuplicadoDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.Throws<InvalidOperationException>(() => orquestrador.IniciarOperacao(
            Contexto(), [ItemSap(10, "3500027"), ItemSap(10, "3500028", numeroItem: "20")]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void IniciarOperacao_NumeroItemDuplicadoAposNormalizacaoDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.Throws<InvalidOperationException>(() => orquestrador.IniciarOperacao(
            Contexto(), [ItemSap(10, "3500027", numeroItem: "10"), ItemSap(20, "3500028", numeroItem: "00010")]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void IniciarOperacao_SegundoItemComMaterialVazioDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.ThrowsAny<Exception>(() => orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027"), ItemSap(20, string.Empty, numeroItem: "20")]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void IniciarOperacao_SegundoItemComCentroVazioDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.ThrowsAny<Exception>(() => orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027"), ItemSap(20, "3500028", numeroItem: "20", centro: string.Empty)]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void IniciarOperacao_SegundoItemComDepositoVazioDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.ThrowsAny<Exception>(() => orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027"), ItemSap(20, "3500028", numeroItem: "20", deposito: string.Empty)]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void IniciarOperacao_SegundoItemComUnidadeVaziaDeveSerAtomico()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();

        Assert.ThrowsAny<Exception>(() => orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027"), ItemSap(20, "3500028", numeroItem: "20", unidade: string.Empty)]));
        AssertEstadoVazioENovaTentativaValida(orquestrador);
    }

    [Fact]
    public void ConfirmarLote_DuplicidadeExataFinalizadaDeveBloquearSemReutilizarGuid()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        EstadoLoteEntradaProdutoLotes original = orquestrador.FinalizarLoteAtivo(10).Itens.Single().Lotes.Single();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30)));
        EstadoLoteEntradaProdutoLotes lote = orquestrador.ObterEstado().Itens.Single().Lotes.Single();

        Assert.Contains("já existe", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(orquestrador.ObterEstado().Itens.Single().Lotes);
        Assert.Equal(original.CodigoLocal, lote.CodigoLocal);
        Assert.Equal(original.CorrelationId, lote.CorrelationId);
        Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, lote.Estado);
    }

    [Fact]
    public void ConfirmarLote_MesmoNumeroDatasDiferentesDeveBloquearSemCriarSegundoLote()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        orquestrador.FinalizarLoteAtivo(10);

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => orquestrador.ConfirmarLote(10, "LOTE-A", Hoje.AddDays(-1), Hoje.AddDays(30)));

        Assert.Contains("datas diferentes", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(orquestrador.ObterEstado().Itens.Single().Lotes);
    }

    [Fact]
    public void RegistrarPesagem_SequenciaCanceladaNaoDeveSerReutilizada()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria p1 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        EntradaProdutoPesagemEmMemoria p2 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", Hoje);
        orquestrador.FinalizarLoteAtivo(10);
        orquestrador.ConfirmarLote(10, "LOTE-B", Hoje, Hoje.AddDays(40));
        EntradaProdutoPesagemEmMemoria p3 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 4m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "4", Hoje);

        orquestrador.CancelarPesagensDoLoteAtivo(10);
        EntradaProdutoPesagemEmMemoria p4 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 5m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "5", Hoje);

        Assert.Equal([1, 2, 3, 4], [p1.Pesagem.Sequencia, p2.Pesagem.Sequencia, p3.Pesagem.Sequencia, p4.Pesagem.Sequencia]);
    }

    [Fact]
    public void DoisItens_DeveManterSequenciaLotesPesosESnapshotsIndependentes()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();
        orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027"), ItemSap(20, "3500028", numeroItem: "20", quantidade: 22m)]);

        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria item10p1 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        EntradaProdutoPesagemEmMemoria item10p2 = orquestrador.RegistrarPesagemNoLoteAtivo(10, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", Hoje);
        orquestrador.FinalizarLoteAtivo(10);

        orquestrador.SelecionarItem(20);
        orquestrador.ConfirmarLote(20, "LOTE-B", Hoje, Hoje.AddDays(40));
        EntradaProdutoPesagemEmMemoria item20p1 = orquestrador.RegistrarPesagemNoLoteAtivo(20, 4m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "4", Hoje);
        orquestrador.FinalizarLoteAtivo(20);

        EntradaProdutoLancamentoComLotesPersistencia arvore = orquestrador.MontarLancamentoComLotesParaPersistencia();

        Assert.Equal([1, 2], [item10p1.Pesagem.Sequencia, item10p2.Pesagem.Sequencia]);
        Assert.Equal(1, item20p1.Pesagem.Sequencia);
        Assert.Equal("00020", orquestrador.ObterEstado().NumeroItemSapSelecionado);
        Assert.Equal(2, arvore.Itens.Count);
        Assert.Equal(5m, arvore.Itens.Single(i => i.NumeroItemSap == "00010").Item.QuantidadeRecebida);
        Assert.Equal(4m, arvore.Itens.Single(i => i.NumeroItemSap == "00020").Item.QuantidadeRecebida);
        Assert.Equal("3500028", arvore.Itens.Single(i => i.NumeroItemSap == "00020").Item.Material);
        Assert.Equal(22m, arvore.Itens.Single(i => i.NumeroItemSap == "00020").Item.QuantidadePrevista);
    }

    [Fact]
    public void SelecionarItemInexistente_DeveGerarErroControladoESemAlterarEstado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        EstadoOperacaoEntradaProdutoLotes antes = orquestrador.ObterEstado();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.SelecionarItem(999));

        Assert.Contains("não pertence", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(antes.NumeroItemSapSelecionado, orquestrador.ObterEstado().NumeroItemSapSelecionado);
        Assert.Empty(orquestrador.ObterEstado().Itens.Single().Lotes);
    }

    [Fact]
    public void RegistrarPesagemSemLote_DeveGerarErroControladoESemAlterarEstado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null));

        Assert.Contains("lote", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(orquestrador.ObterEstado().Itens.Single().Lotes);
    }

    [Fact]
    public void FinalizarSemLote_DeveGerarErroControladoESemAlterarEstado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.FinalizarLoteAtivo(10));

        Assert.Contains("lote ativo", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(orquestrador.ObterEstado().Itens.Single().Lotes);
    }

    [Fact]
    public void CancelarSemLoteAtivo_DeveGerarErroControladoESemAlterarEstado()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.CancelarPesagensDoLoteAtivo(10));

        Assert.Contains("lote ativo", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(orquestrador.ObterEstado().Itens.Single().Lotes);
    }

    [Fact]
    public void SnapshotSap_DeveFicarCongeladoAteMontagemDaArvore()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();
        List<PedidoCompraSapItem> itens =
        [
            ItemSap(10, "3500027", numeroItem: "10", centro: "3007", deposito: "PP01", unidade: "KG", quantidade: 10m)
        ];

        orquestrador.IniciarOperacao(Contexto(), itens);
        itens[0] = ItemSap(99, "9999999", numeroItem: "99", centro: "9999", deposito: "ZZ99", unidade: "UN", quantidade: 99m);
        itens.Clear();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        orquestrador.FinalizarLoteAtivo(10);

        EntradaProdutoItem item = orquestrador.MontarLancamentoComLotesParaPersistencia().Itens.Single().Item;

        Assert.Equal(10, item.CodigoSapPedidoCompraItem);
        Assert.Equal("00010", item.NumeroItem);
        Assert.Equal("3500027", item.Material);
        Assert.Equal("3007", item.Centro);
        Assert.Equal("PP01", item.Deposito);
        Assert.Equal("KG", item.Unidade);
        Assert.Equal(10m, item.QuantidadePrevista);
    }

    [Fact]
    public void LimparOperacao_DeveZerarEstadoEPermitirNovoPedidoSemResiduos()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.SelecionarItem(10);
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria antiga = orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        EstadoLoteEntradaProdutoLotes loteAntigo = orquestrador.ObterEstado().Itens.Single().Lotes.Single();

        orquestrador.LimparOperacao();
        AssertEstadoVazio(orquestrador);

        orquestrador.IniciarOperacao(Contexto("4500001490"), [ItemSap(20, "3500028", numeroItem: "20")]);
        orquestrador.ConfirmarLote(20, "LOTE-B", Hoje, Hoje.AddDays(40));
        EntradaProdutoPesagemEmMemoria nova = orquestrador.RegistrarPesagemNoLoteAtivo(20, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", Hoje);
        EstadoLoteEntradaProdutoLotes loteNovo = orquestrador.ObterEstado().Itens.Single().Lotes.Single();

        Assert.NotEqual(loteAntigo.CodigoLocal, loteNovo.CodigoLocal);
        Assert.NotEqual(loteAntigo.CorrelationId, loteNovo.CorrelationId);
        Assert.NotEqual(antiga.CodigoLocalPesagem, nova.CodigoLocalPesagem);
        Assert.Equal("4500001490", orquestrador.ObterEstado().NumeroPedido);
        Assert.Equal("00020", orquestrador.ObterEstado().NumeroItemSapSelecionado);
    }

    [Fact]
    public void Controller_DeveDelegarMetodosDeOperacaoComLotes()
    {
        string[] metodosEsperados =
        [
            nameof(EntradaProdutoController.IniciarOperacaoComLotes),
            nameof(EntradaProdutoController.SelecionarItemOperacaoComLotes),
            nameof(EntradaProdutoController.ConfirmarLoteOperacaoComLotes),
            nameof(EntradaProdutoController.RegistrarPesagemOperacaoComLotes),
            nameof(EntradaProdutoController.CancelarPesagensOperacaoComLotes),
            nameof(EntradaProdutoController.CancelarPesagemOperacaoComLotes),
            nameof(EntradaProdutoController.ObterPesagensItemOperacaoComLotes),
            nameof(EntradaProdutoController.ObterPesagensLoteAtivoOperacaoComLotes),
            nameof(EntradaProdutoController.FinalizarLoteOperacaoComLotes),
            nameof(EntradaProdutoController.MontarLancamentoComLotesParaPersistencia),
            nameof(EntradaProdutoController.ObterEstadoOperacaoComLotes),
            nameof(EntradaProdutoController.LimparOperacaoComLotes)
        ];

        foreach (string metodo in metodosEsperados)
        {
            Assert.Contains(typeof(EntradaProdutoController).GetMethods(), info => info.Name == metodo);
        }
    }

    [Fact]
    public void Controller_DeveExecutarOperacaoComLotesSemFluxoLegadoBancoSapOuImpressao()
    {
        FakePedidoCompraSapServico pedidoSap = new();
        FakeMaterialDocumentSapServico materialSap = new();
        EntradaProdutoController controller = NovoController(new EntradaProdutoLotesOrquestrador(new ValidadorDadosLoteEntrada(() => Hoje)), pedidoSap, materialSap);

        controller.IniciarOperacaoComLotes(Contexto(), [ItemSap(10, "3500027")]);
        controller.SelecionarItemOperacaoComLotes(10);
        controller.ConfirmarLoteOperacaoComLotes(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        controller.RegistrarPesagemOperacaoComLotes(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        controller.FinalizarLoteOperacaoComLotes(10);
        EntradaProdutoLancamentoComLotesPersistencia arvore = controller.MontarLancamentoComLotesParaPersistencia();
        EstadoOperacaoEntradaProdutoLotes estadoAntesLimpeza = controller.ObterEstadoOperacaoComLotes();
        controller.LimparOperacaoComLotes();

        Assert.Single(arvore.Itens);
        Assert.Equal(2m, arvore.Itens.Single().Item.QuantidadeRecebida);
        Assert.Equal("00010", estadoAntesLimpeza.NumeroItemSapSelecionado);
        AssertEstadoVazio(controller.ObterEstadoOperacaoComLotes());
        Assert.Equal(0, pedidoSap.Chamadas);
        Assert.Equal(0, materialSap.Chamadas);
        Assert.False(estadoAntesLimpeza.Itens is List<EstadoItemEntradaProdutoLotes>);
    }

    [Fact]
    public void ConsultarPesagensDoItem_SemLoteDeveRetornarVazioSomenteLeitura()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagens = orquestrador.ObterPesagensDoItem(10);

        Assert.Empty(pesagens);
        Assert.False(pesagens is List<EntradaProdutoPesagemEmMemoria>);
    }

    [Fact]
    public void ConsultarPesagensLoteAtivo_SemLoteAtivoDeveBloquear()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => orquestrador.ObterPesagensDoLoteAtivo(10));

        Assert.Contains("lote ativo", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsultarPesagens_DevePreservarGuidOrdemEAgregarDoisLotes()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria primeira = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        orquestrador.FinalizarLoteAtivo(10);
        orquestrador.ConfirmarLote(10, "LOTE-B", Hoje, Hoje.AddDays(40));
        EntradaProdutoPesagemEmMemoria segunda = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", Hoje);

        IReadOnlyList<EntradaProdutoPesagemEmMemoria> loteAtivo = orquestrador.ObterPesagensDoLoteAtivo(10);
        IReadOnlyList<EntradaProdutoPesagemEmMemoria> item = orquestrador.ObterPesagensDoItem(10);

        Assert.Equal([segunda.CodigoLocalPesagem], loteAtivo.Select(p => p.CodigoLocalPesagem));
        Assert.Equal([primeira.CodigoLocalPesagem, segunda.CodigoLocalPesagem], item.Select(p => p.CodigoLocalPesagem));
        Assert.Equal([1, 2], item.Select(p => p.Pesagem.Sequencia));
        Assert.False(item is List<EntradaProdutoPesagemEmMemoria>);
    }

    [Fact]
    public void CancelarPesagemPorGuid_DevePreservarDadosERecalcularTotalValido()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria primeira = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 2m, 0.5m, 4, EntradaProdutoPesagemCalculos.OrigemBalanca, 7, "BAL=2", Hoje);
        EntradaProdutoPesagemEmMemoria segunda = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 3m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", Hoje);

        EntradaProdutoPesagemEmMemoria cancelada = orquestrador.CancelarPesagemDoLoteAtivo(10, primeira.CodigoLocalPesagem);
        EntradaProdutoPesagemEmMemoria repetida = orquestrador.CancelarPesagemDoLoteAtivo(10, primeira.CodigoLocalPesagem);
        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagens = orquestrador.ObterPesagensDoLoteAtivo(10);
        EstadoLoteEntradaProdutoLotes lote = orquestrador.ObterEstado().Itens.Single().Lotes.Single();

        Assert.Equal(primeira.CodigoLocalPesagem, cancelada.CodigoLocalPesagem);
        Assert.Equal(primeira.Pesagem.Sequencia, cancelada.Pesagem.Sequencia);
        Assert.Equal(primeira.Pesagem.PesoBrutoKg, cancelada.Pesagem.PesoBrutoKg);
        Assert.Equal(primeira.Pesagem.PesoTaraKg, cancelada.Pesagem.PesoTaraKg);
        Assert.Equal(primeira.Pesagem.PesoLiquidoKg, cancelada.Pesagem.PesoLiquidoKg);
        Assert.Equal(primeira.Pesagem.CodigoTara, cancelada.Pesagem.CodigoTara);
        Assert.Equal(primeira.Pesagem.CodigoBalanca, cancelada.Pesagem.CodigoBalanca);
        Assert.Equal(primeira.Pesagem.Origem, cancelada.Pesagem.Origem);
        Assert.Equal(primeira.Pesagem.LeituraOriginal, cancelada.Pesagem.LeituraOriginal);
        Assert.Equal(primeira.Pesagem.PesadoEm, cancelada.Pesagem.PesadoEm);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusCancelada, cancelada.Pesagem.StatusPesagem);
        Assert.Equal(cancelada, repetida);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusValida, pesagens.Single(p => p.CodigoLocalPesagem == segunda.CodigoLocalPesagem).Pesagem.StatusPesagem);
        Assert.Equal(3m, lote.PesoLiquidoTotalMemoriaKg);
    }

    [Fact]
    public void CancelarPesagemPorGuid_DeveBloquearGuidInvalidoInexistenteFinalizadoELoteAnterior()
    {
        EntradaProdutoLotesOrquestrador orquestrador = OperacaoIniciada();
        orquestrador.ConfirmarLote(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria primeira = orquestrador.RegistrarPesagemNoLoteAtivo(
            10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);

        Assert.Throws<ArgumentException>(() => orquestrador.CancelarPesagemDoLoteAtivo(10, Guid.Empty));
        Assert.Throws<InvalidOperationException>(() => orquestrador.CancelarPesagemDoLoteAtivo(10, Guid.NewGuid()));

        orquestrador.FinalizarLoteAtivo(10);
        Assert.Throws<InvalidOperationException>(() => orquestrador.CancelarPesagemDoLoteAtivo(10, primeira.CodigoLocalPesagem));

        orquestrador.ConfirmarLote(10, "LOTE-B", Hoje, Hoje.AddDays(40));
        Assert.Throws<InvalidOperationException>(() => orquestrador.CancelarPesagemDoLoteAtivo(10, primeira.CodigoLocalPesagem));
    }

    [Fact]
    public void Controller_DeveDelegarConsultaECancelamentoIndividual()
    {
        FakePedidoCompraSapServico pedidoSap = new();
        FakeMaterialDocumentSapServico materialSap = new();
        EntradaProdutoController controller = NovoController(new EntradaProdutoLotesOrquestrador(new ValidadorDadosLoteEntrada(() => Hoje)), pedidoSap, materialSap);

        controller.IniciarOperacaoComLotes(Contexto(), [ItemSap(10, "3500027")]);
        controller.ConfirmarLoteOperacaoComLotes(10, "LOTE-A", Hoje, Hoje.AddDays(30));
        EntradaProdutoPesagemEmMemoria pesagem = controller.RegistrarPesagemOperacaoComLotes(
            10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);

        Assert.Single(controller.ObterPesagensItemOperacaoComLotes(10));
        Assert.Single(controller.ObterPesagensLoteAtivoOperacaoComLotes(10));
        EntradaProdutoPesagemEmMemoria cancelada = controller.CancelarPesagemOperacaoComLotes(10, pesagem.CodigoLocalPesagem);

        Assert.Equal(pesagem.CodigoLocalPesagem, cancelada.CodigoLocalPesagem);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusCancelada, cancelada.Pesagem.StatusPesagem);
        Assert.Equal(0, pedidoSap.Chamadas);
        Assert.Equal(0, materialSap.Chamadas);
    }

    [Fact]
    public void OperacaoModoQuimico_DeveUsarMesmoFluxoSemComportamentoDiferente()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();
        orquestrador.IniciarOperacao(Contexto(modo: ModoEntradaMaterial.Quimico), [ItemSap(10, "3500027")]);
        orquestrador.ConfirmarLote(10, "Q-LOTE", Hoje, Hoje.AddDays(30));
        orquestrador.RegistrarPesagemNoLoteAtivo(10, 2m, 0m, null, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", Hoje);
        EstadoOperacaoEntradaProdutoLotes estado = orquestrador.FinalizarLoteAtivo(10);

        EntradaProdutoLancamentoComLotesPersistencia arvore = orquestrador.MontarLancamentoComLotesParaPersistencia();

        Assert.Equal(ModoEntradaMaterial.Quimico, estado.ModoEntradaMaterial);
        Assert.Equal(ModoEntradaMaterial.Quimico, orquestrador.ObterEstado().ModoEntradaMaterial);
        Assert.Single(arvore.Itens);
        Assert.Equal(2m, arvore.Itens.Single().Item.QuantidadeRecebida);
        Assert.Equal("Q-LOTE", arvore.Itens.Single().Lotes.Single().Dados.NumeroLote);
    }

    private static EntradaProdutoLotesOrquestrador OperacaoIniciada()
    {
        EntradaProdutoLotesOrquestrador orquestrador = NovoOrquestrador();
        orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027")]);
        return orquestrador;
    }

    private static EntradaProdutoLotesOrquestrador NovoOrquestrador()
        => new(new ValidadorDadosLoteEntrada(() => Hoje));

    private static EntradaProdutoController NovoController(
        EntradaProdutoLotesOrquestrador orquestrador,
        FakePedidoCompraSapServico pedidoSap,
        FakeMaterialDocumentSapServico materialSap)
    {
        IFabricaConexaoBanco fabrica = new FabricaConexaoPostgreSql();
        EntradaProdutoServico entradaServico = new(
            new EntradaProdutoRepositorio(fabrica),
            new PesagemEntradaItemRepositorio(fabrica),
            new TaraRepositorio(fabrica),
            new BalancaRepositorio(fabrica),
            new AutorizacaoCentroDepositoEntrada([], []),
            null);

        return new EntradaProdutoController(
            new IntegracaoEntradaSapServico(pedidoSap, materialSap),
            entradaServico,
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            new TaraController(new TaraServico(new TaraRepositorio(fabrica), null!)),
            ehAmbienteHomologacao: () => false,
            lotesOrquestrador: orquestrador);
    }

    private static ContextoOperacaoEntradaProdutoLotes Contexto(
        string numeroPedido = "4500001489",
        ModoEntradaMaterial modo = ModoEntradaMaterial.MateriaPrima)
        => new()
        {
            NumeroPedido = $" {numeroPedido} ",
            Fornecedor = "20000244",
            CodigoSetor = 5,
            Terminal = " TERMINAL_TESTE ",
            ModoEntradaMaterial = modo
        };

    private static PedidoCompraSapItem ItemSap(
        long codigoItem,
        string? material,
        string? numeroItem = null,
        string? centro = "3007",
        string? deposito = "PP01",
        string? unidade = "KG",
        decimal? quantidade = 10m)
        => new()
        {
            CodigoItem = codigoItem,
            NumeroItem = numeroItem ?? codigoItem.ToString(),
            CodigoMaterial = material,
            Quantidade = quantidade,
            UnidadeMedida = unidade,
            Centro = centro,
            Deposito = deposito
        };

    private static void AssertEstadoVazioENovaTentativaValida(EntradaProdutoLotesOrquestrador orquestrador)
    {
        AssertEstadoVazio(orquestrador);
        EstadoOperacaoEntradaProdutoLotes estado = orquestrador.IniciarOperacao(Contexto(), [ItemSap(10, "3500027")]);
        Assert.True(estado.OperacaoIniciada);
        Assert.Single(estado.Itens);
    }

    private static void AssertEstadoVazio(EntradaProdutoLotesOrquestrador orquestrador)
        => AssertEstadoVazio(orquestrador.ObterEstado());

    private static void AssertEstadoVazio(EstadoOperacaoEntradaProdutoLotes estado)
    {
        Assert.False(estado.OperacaoIniciada);
        Assert.Null(estado.NumeroPedido);
        Assert.Null(estado.NumeroItemSapSelecionado);
        Assert.Empty(estado.Itens);
    }

    private sealed class FakePedidoCompraSapServico : IPedidoCompraSapServico
    {
        public int Chamadas { get; private set; }
        public bool EhSimulado => false;
        public bool SapConfigurado => true;
        public bool EscritaSapHabilitada => false;

        public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<ResultadoOperacao>();

        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<PedidoCompraSapAgregado?>();

        public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<PedidoCompraSap?>();

        public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
            => Falhar<IReadOnlyList<string>>();

        public Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<string>();

        public Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<DateOnly?>();

        public Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<string>();

        public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Falhar<IReadOnlyList<PedidoCompraSapItem>>();

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto, CancellationToken cancellationToken = default)
            => Falhar<ResultadoOperacao>();

        private Task<T> Falhar<T>()
        {
            Chamadas++;
            throw new InvalidOperationException("Fluxo SAP não deveria ser executado neste teste.");
        }
    }

    private sealed class FakeMaterialDocumentSapServico : IMaterialDocumentSapServico
    {
        public int Chamadas { get; private set; }
        public bool EhSimulado => false;
        public bool MaterialDocumentConfigurado => false;

        public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
            MaterialDocumentSapRequest requisicao,
            string chaveNegocio,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            throw new InvalidOperationException("Material Document não deveria ser executado neste teste.");
        }
    }
}
