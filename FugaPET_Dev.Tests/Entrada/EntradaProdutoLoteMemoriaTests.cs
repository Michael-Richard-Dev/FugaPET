using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Entrada;

public sealed class EntradaProdutoLoteMemoriaTests
{
    private static readonly DateTime Hoje = new(2026, 7, 24, 14, 30, 0);
    private readonly ValidadorDadosLoteEntrada _validador = new(() => Hoje);

    [Theory]
    [InlineData("10", "00010")]
    [InlineData("00020", "00020")]
    [InlineData(" 30 ", "00030")]
    public void NumeroItemSap_DeveNormalizarComCincoDigitos(string entrada, string esperado)
        => Assert.Equal(esperado, EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(entrada));

    [Fact]
    public void Operacao_DeveUsarChaveStringNormalizadaNoDicionario()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();

        operacao.ObterOuCriarItem("10", "3500027");

        Assert.True(operacao.ItensPorNumeroItemSap.ContainsKey("00010"));
        Assert.IsType<string>(operacao.ItensPorNumeroItemSap.Keys.Single());
    }

    [Fact]
    public void Operacao_NaoDeveExporDictionaryMutavel()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();

        Assert.False(operacao.ItensPorNumeroItemSap is Dictionary<string, EntradaProdutoItemEmMemoria>);
    }

    [Fact]
    public void Item_NaoDeveExporListaDeLotesMutavel()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");

        Assert.False(item.Lotes is List<EntradaProdutoLoteEmMemoria>);
    }

    [Fact]
    public void Lote_NaoDeveExporListaDePesagensMutavel()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");

        Assert.False(lote.Pesagens is List<EntradaProdutoPesagem>);
    }

    [Fact]
    public void Validador_DeveBloquearLoteObrigatorio()
    {
        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("   ", Hoje.AddDays(-1), Hoje.AddDays(30));

        Assert.False(resultado.Sucesso);
        Assert.Contains("número do lote", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validador_DeveBloquearLoteComMaisDeDezCaracteres()
    {
        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("12345678901", Hoje.AddDays(-1), Hoje.AddDays(30));

        Assert.False(resultado.Sucesso);
        Assert.Contains("10 caracteres", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validador_DeveBloquearFabricacaoFutura()
    {
        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("L1", Hoje.AddDays(1), Hoje.AddDays(30));

        Assert.False(resultado.Sucesso);
        Assert.Contains("futura", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validador_DeveBloquearVencimentoAnteriorAFabricacao()
    {
        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("L1", Hoje.AddDays(-1), Hoje.AddDays(-2));

        Assert.False(resultado.Sucesso);
        Assert.Contains("anterior", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validador_DeveBloquearVencimentoExpirado()
    {
        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("L1", Hoje.AddDays(-10), Hoje.AddDays(-1));

        Assert.False(resultado.Sucesso);
        Assert.Contains("vencido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DadosLoteEntrada_DeveNormalizarTrimCaixaAltaEDatasDate()
    {
        DadosLoteEntrada dados = new(" lote-a ", Hoje.AddHours(2), Hoje.AddDays(30).AddHours(5));

        Assert.Equal("LOTE-A", dados.NumeroLote);
        Assert.Equal(Hoje.Date, dados.DataFabricacao);
        Assert.Equal(Hoje.AddDays(30).Date, dados.DataVencimento);
    }

    [Fact]
    public void LoteEmMemoria_DeveManterCorrelationIdCriadoUmaVez()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");
        Guid correlation = lote.CorrelationId;

        lote.RegistrarPesagem(Pesagem(1, 2m));
        lote.FinalizarEmMemoria();

        Assert.Equal(correlation, lote.CorrelationId);
    }

    [Fact]
    public void LoteEmMemoria_DeveGerarGuidQuandoOmitido()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLote("A");

        Assert.NotEqual(Guid.Empty, lote.CodigoLocal);
        Assert.NotEqual(Guid.Empty, lote.CorrelationId);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearCodigoLocalEmptyInformadoExplicitamente()
    {
        ArgumentException erro = Assert.Throws<ArgumentException>(() => new EntradaProdutoLoteEmMemoria(Dados("A"), Guid.Empty));

        Assert.Equal("codigoLocal", erro.ParamName);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearCorrelationIdEmptyInformadoExplicitamente()
    {
        ArgumentException erro = Assert.Throws<ArgumentException>(() => new EntradaProdutoLoteEmMemoria(Dados("A"), correlationId: Guid.Empty));

        Assert.Equal("correlationId", erro.ParamName);
    }

    [Fact]
    public void Persistencia_DeveBloquearCodigoLocalEmpty()
        => Assert.Throws<ArgumentException>(() => new EntradaProdutoLoteComPesagensPersistencia { CodigoLocal = Guid.Empty, CorrelationId = Guid.NewGuid() });

    [Fact]
    public void Persistencia_DeveBloquearCorrelationIdEmpty()
        => Assert.Throws<ArgumentException>(() => new EntradaProdutoLoteComPesagensPersistencia { CodigoLocal = Guid.NewGuid(), CorrelationId = Guid.Empty });

    [Fact]
    public void Persistencia_DeveBloquearCodigoLocalPesagemEmpty()
        => Assert.Throws<ArgumentException>(() => new EntradaProdutoPesagemComCodigoLocalPersistencia { CodigoLocalPesagem = Guid.Empty });

    [Fact]
    public void LoteEmMemoria_DeveConfirmarLoteAguardando()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLote("A");

        lote.Confirmar();

        Assert.Equal(EstadoOperacionalLoteEntrada.LoteConfirmado, lote.Estado);
    }

    [Fact]
    public void LoteEmMemoria_PrimeiraPesagemDeveMudarConfirmadoParaPesando()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");

        lote.RegistrarPesagem(Pesagem(1, 2m));

        Assert.Equal(EstadoOperacionalLoteEntrada.Pesando, lote.Estado);
    }

    [Fact]
    public void LoteEmMemoria_DeveFinalizarLotePesandoComPesagem()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");

        lote.RegistrarPesagem(Pesagem(1, 2m));
        lote.FinalizarEmMemoria();

        Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, lote.Estado);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearFinalizacaoSemPesagem()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");
        lote.IniciarPesagem();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(lote.FinalizarEmMemoria);

        Assert.Contains("sem pesagem", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearRegressaoParaAguardandoDados()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => lote.AlterarEstado(EstadoOperacionalLoteEntrada.AguardandoDados));

        Assert.Contains("inválida", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearPesagemDepoisDeFinalizado()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");
        lote.RegistrarPesagem(Pesagem(1, 2m));
        lote.FinalizarEmMemoria();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => lote.RegistrarPesagem(Pesagem(2, 1m)));

        Assert.Contains("liberado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearTransicaoInvalida()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLote("A");

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => lote.AlterarEstado(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria));

        Assert.Contains("inválida", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoteEmMemoria_DeveBloquearPesagemSemConfirmarLote()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLote("A");

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => lote.RegistrarPesagem(Pesagem(1, 2m)));

        Assert.Contains("liberado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItemEmMemoria_DevePermitirVariosLotesNoMesmoItem()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");

        item.AdicionarLote(Dados("A"));
        item.AdicionarLote(Dados("B"));

        Assert.Equal(2, item.Lotes.Count);
        Assert.Contains(item.Lotes, l => l.Dados.NumeroLote == "A");
        Assert.Contains(item.Lotes, l => l.Dados.NumeroLote == "B");
    }

    [Fact]
    public void ItemEmMemoria_DeveBloquearLoteDuplicadoComMesmoNumeroEMesmasDatas()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");
        item.AdicionarLote(Dados("A"));

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => item.AdicionarLote(Dados(" A ")));

        Assert.Contains("já existe", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItemEmMemoria_DeveBloquearLoteDuplicadoComMesmoNumeroEDatasDiferentes()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");
        item.AdicionarLote(Dados("A"));

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => item.AdicionarLote(new DadosLoteEntrada("A", Hoje.AddDays(-1), Hoje.AddDays(30))));

        Assert.Contains("datas diferentes", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operacao_DeveManterLoteAtivoIndependentePorItem()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();
        EntradaProdutoItemEmMemoria item10 = operacao.SelecionarItem("10", "3500027");
        EntradaProdutoLoteEmMemoria loteA = item10.AdicionarLote(Dados("A"));

        EntradaProdutoItemEmMemoria item20 = operacao.SelecionarItem("20", "3500028");
        EntradaProdutoLoteEmMemoria loteC = item20.AdicionarLote(Dados("C"));

        Assert.Equal(loteA.CodigoLocal, operacao.ObterLoteAtivoDoItem("10")!.CodigoLocal);
        Assert.Equal(loteC.CodigoLocal, operacao.ObterLoteAtivoDoItem("20")!.CodigoLocal);
    }

    [Fact]
    public void AlternarItem_DeveRestaurarLoteAtivoCorreto()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();
        EntradaProdutoItemEmMemoria item10 = operacao.SelecionarItem("10", "3500027");
        EntradaProdutoLoteEmMemoria loteA = item10.AdicionarLote(Dados("A"));
        EntradaProdutoLoteEmMemoria loteB = item10.AdicionarLote(Dados("B"));
        item10.DefinirLoteAtivo(loteA.CodigoLocal);

        operacao.SelecionarItem("20", "3500028").AdicionarLote(Dados("C"));
        EntradaProdutoItemEmMemoria retorno = operacao.SelecionarItem("10", "3500027");

        Assert.Equal(loteA.CodigoLocal, retorno.ObterLoteAtivo()!.CodigoLocal);
        Assert.NotEqual(loteB.CodigoLocal, retorno.ObterLoteAtivo()!.CodigoLocal);
    }

    [Fact]
    public void Validador_DeveReutilizarMesmoLoteComMesmasDatas()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");
        EntradaProdutoLoteEmMemoria lote = item.AdicionarLote(Dados("a"));

        ResultadoValidacaoLoteEntrada resultado = _validador.Validar(" A ", Hoje, Hoje.AddDays(30), item);

        Assert.True(resultado.Sucesso);
        Assert.Same(lote, resultado.LoteExistente);
    }

    [Fact]
    public void Validador_DeveBloquearMesmoLoteComDatasDiferentes()
    {
        EntradaProdutoItemEmMemoria item = new("10", "3500027");
        item.AdicionarLote(Dados("A"));

        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("A", Hoje.AddDays(-1), Hoje.AddDays(30), item);

        Assert.False(resultado.Sucesso);
        Assert.Contains("datas diferentes", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Material_DeveNormalizarComTrim()
    {
        EntradaProdutoItemEmMemoria item = new("10", " 3500027 ");

        Assert.Equal("3500027", item.Material);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Material_DeveSerObrigatorioParaItemNovo(string? material)
        => Assert.Throws<ArgumentException>(() => new EntradaProdutoItemEmMemoria("10", material!));

    [Fact]
    public void Operacao_DeveReutilizarItemExistenteComMesmoMaterial()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();
        EntradaProdutoItemEmMemoria original = operacao.ObterOuCriarItem("10", "3500027");

        EntradaProdutoItemEmMemoria reutilizado = operacao.ObterOuCriarItem("00010", " 3500027 ");

        Assert.Same(original, reutilizado);
    }

    [Fact]
    public void Operacao_DeveBloquearItemExistenteComMaterialDiferente()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();
        operacao.ObterOuCriarItem("10", "3500027");

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => operacao.ObterOuCriarItem("10", "3500028"));

        Assert.Contains("outro material", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operacao_DeveBloquearSelecaoPadraoComMaterialVazio()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();

        Assert.Throws<ArgumentException>(() => operacao.SelecionarItem("10", " "));
    }

    [Fact]
    public void Operacao_DeveSelecionarSomenteItemExistenteSemSubstituirMaterial()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();
        EntradaProdutoItemEmMemoria item = operacao.ObterOuCriarItem("10", "3500027");

        EntradaProdutoItemEmMemoria selecionado = operacao.SelecionarItemExistente("10");

        Assert.Same(item, selecionado);
        Assert.Equal("3500027", selecionado.Material);
    }

    [Fact]
    public void LoteEmMemoria_DeveCalcularPesoLiquidoValido()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");

        lote.RegistrarPesagem(Pesagem(1, 2.5m));
        lote.RegistrarPesagem(Pesagem(2, 3.5m));
        lote.RegistrarPesagem(Pesagem(3, 10m) with { StatusPesagem = "CANCELADA" });

        Assert.True(lote.PossuiPesagem);
        Assert.Equal(6m, lote.PesoLiquidoTotalMemoriaKg);
    }

    [Fact]
    public void PesagemHistorica_DevePermitirSnapshotNulo()
    {
        EntradaProdutoPesagem pesagem = new() { Sequencia = 1, PesoBrutoKg = 2, PesoTaraKg = 0, PesoLiquidoKg = 2 };

        Assert.Null(pesagem.CodigoEntradaProdutoLote);
        Assert.Null(pesagem.NumeroLoteSnapshot);
        Assert.Null(pesagem.DataFabricacaoSnapshot);
        Assert.Null(pesagem.DataVencimentoSnapshot);
    }

    [Fact]
    public void ArvorePersistencia_DeveRepresentarItemLotePesagem()
    {
        Guid codigoLote = Guid.NewGuid();
        Guid codigoPesagem = Guid.NewGuid();
        EntradaProdutoLancamentoComLotesPersistencia arvore = new()
        {
            Lancamento = new EntradaProdutoLancamento { NumeroPedido = "4500001489" },
            Itens =
            [
                new EntradaProdutoItemComLotesPersistencia
                {
                    NumeroItemSap = "00010",
                    Item = new EntradaProdutoItem { NumeroItem = "10", Material = "3500027" },
                    Lotes =
                    [
                        new EntradaProdutoLoteComPesagensPersistencia
                        {
                            CodigoLocal = codigoLote,
                            Dados = Dados("A"),
                            CorrelationId = Guid.NewGuid(),
                            Pesagens =
                            [
                                new EntradaProdutoPesagemComCodigoLocalPersistencia
                                {
                                    CodigoLocalPesagem = codigoPesagem,
                                    Pesagem = Pesagem(1, 2m)
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        Assert.Equal("4500001489", arvore.Lancamento.NumeroPedido);
        Assert.Equal("00010", Assert.Single(arvore.Itens).NumeroItemSap);
        Assert.Equal(codigoLote, Assert.Single(Assert.Single(arvore.Itens).Lotes).CodigoLocal);
        Assert.Equal(codigoPesagem, Assert.Single(Assert.Single(Assert.Single(arvore.Itens).Lotes).Pesagens).CodigoLocalPesagem);
    }

    [Theory]
    [InlineData(new[] { StatusLoteEntrada.EnviandoSap, StatusLoteEntrada.FinalizadoLocal }, "ENVIADO_SAP")]
    [InlineData(new[] { StatusLoteEntrada.ErroSap, StatusLoteEntrada.ConfirmadoSap }, StatusLoteEntrada.ErroSap)]
    [InlineData(new[] { StatusLoteEntrada.FinalizadoLocal, StatusLoteEntrada.ConfirmadoSap }, StatusLoteEntrada.FinalizadoLocal)]
    [InlineData(new[] { StatusLoteEntrada.ConfirmadoSap, StatusLoteEntrada.ConfirmadoSap }, StatusLoteEntrada.ConfirmadoSap)]
    [InlineData(new[] { StatusLoteEntrada.Cancelado, StatusLoteEntrada.Cancelado }, StatusLoteEntrada.Cancelado)]
    public void Agregador_DeveAplicarPrioridadeDeStatus(string[] status, string esperado)
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar(status);

        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, resultado.StatusAgregado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Agregador_DeveRetornarInconsistenteParaStatusIsoladoNuloOuVazio(string? status)
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar([status]);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusAgregado);
        Assert.Contains("vazio ou nulo", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agregador_DeveRetornarInconsistenteParaListaVazia()
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar([]);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusAgregado);
        Assert.Contains("Nenhum status", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Agregador_DeveRetornarInconsistenteQuandoConfirmadoCombinaComNuloOuVazio(string? status)
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar([StatusLoteEntrada.ConfirmadoSap, status]);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusAgregado);
    }

    [Fact]
    public void Agregador_DeveRetornarInconsistenteParaStatusDesconhecido()
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar(["DESCONHECIDO"]);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusAgregado);
        Assert.Contains("inválido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agregador_DeveRetornarInconsistenteParaConfirmadoMaisCancelado()
    {
        ResultadoAgregacaoStatusEntrada resultado = AgregadorStatusLoteEntrada.Agregar(
            [StatusLoteEntrada.ConfirmadoSap, StatusLoteEntrada.Cancelado]);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusAgregado);
        Assert.Contains("inconsistente", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void SelecionarItemExistente_QuandoItemNaoExiste_DeveExibirMensagemCorreta()
    {
        EntradaProdutoOperacaoEmMemoria operacao = new();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => operacao.SelecionarItemExistente("10"));

        Assert.Equal("Item SAP ainda não existe na operação em memória.", erro.Message);
        Assert.DoesNotContain("\u00C3\u0192", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u00C3\u201A", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\uFFFD", erro.Message, StringComparison.Ordinal);



    }
    [Theory]
    [InlineData(ModoEntradaMaterial.MateriaPrima)]
    [InlineData(ModoEntradaMaterial.Quimico)]
    public void MateriaPrimaEQuimicos_DevemUsarMesmoValidador(ModoEntradaMaterial modo)
    {
        ContextoLoteEntrada contexto = new()
        {
            NumeroPedido = "4500001489",
            NumeroItemSap = "00010",
            Modo = modo
        };

        ResultadoValidacaoLoteEntrada resultado = _validador.Validar("L1", Hoje, Hoje.AddDays(10));

        Assert.True(resultado.Sucesso);
        Assert.Equal(modo, contexto.Modo);
        Assert.Equal("L1", resultado.DadosNormalizados!.NumeroLote);
    }

    [Fact]
    public void Persistencia_DTO_NaoDeveTerEstadoFinalizadoPadrao()
    {
        EntradaProdutoLoteComPesagensPersistencia dto = new()
        {
            CodigoLocal = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid()
        };

        Assert.NotEqual(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, dto.EstadoOperacional);
    }

    [Fact]
    public void Persistencia_FactoryRecusaLoteNaoFinalizado()
    {
        EntradaProdutoLoteEmMemoria lote = NovoLoteConfirmado("A");
        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens = [PesagemLocal(Guid.NewGuid(), 1, 2m)];

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => EntradaProdutoLoteComPesagensPersistencia.CriarDeLoteFinalizado(lote, pesagens));

        Assert.Contains("finalizado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_FactoryPreservaIdentificadoresExistentes()
    {
        Guid codigoLote = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        Guid codigoPesagem = Guid.NewGuid();
        EntradaProdutoLoteEmMemoria lote = new(Dados("A"), codigoLote, correlationId, EstadoOperacionalLoteEntrada.LoteConfirmado);
        lote.RegistrarPesagem(Pesagem(1, 2m));
        lote.FinalizarEmMemoria();

        EntradaProdutoLoteComPesagensPersistencia dto = EntradaProdutoLoteComPesagensPersistencia.CriarDeLoteFinalizado(
            lote,
            [PesagemLocal(codigoPesagem, 1, 2m)]);

        Assert.Equal(codigoLote, dto.CodigoLocal);
        Assert.Equal(correlationId, dto.CorrelationId);
        Assert.Equal(codigoPesagem, Assert.Single(dto.Pesagens).CodigoLocalPesagem);
        Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, dto.EstadoOperacional);
    }

    [Fact]
    public void ContextoAuditoria_DeveNormalizarLoginEManterValoresCapturados()
    {
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(10, " admin ", 20, Hoje);

        Assert.Equal(10, contexto.CodigoUsuario);
        Assert.Equal("admin", contexto.LoginUsuario);
        Assert.Equal(20, contexto.CodigoSetorUsuario);
        Assert.Equal(Hoje.Date, contexto.DataReferencia);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void ContextoAuditoria_SetorSessaoAusenteOuZeroDeveBloquear(long? setor)
        => Assert.Throws<InvalidOperationException>(() => ContextoAuditoriaEntradaLotes.Criar(10, "admin", setor, Hoje));

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Persistencia_SetorLancamentoAusenteOuZeroDeveBloquear(long? setor)
    {
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(10, "admin", 20, Hoje);

        Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoPersistenciaLotes.ValidarSetorObrigatorio(setor, contexto));
    }

    [Fact]
    public void Persistencia_SetorDivergenteDeveBloquear()
    {
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(10, "admin", 20, Hoje);

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarSetorObrigatorio(21, contexto));

        Assert.Contains("nao autorizado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Persistencia_CodigoSapItemAusenteOuZeroDeveBloquear(long? codigoSap)
    {
        EntradaProdutoItem item = ItemObrigatorio() with { CodigoSapPedidoCompraItem = codigoSap };

        Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoPersistenciaLotes.ValidarItemObrigatorio(item));
    }

    [Theory]
    [InlineData("Material")]
    [InlineData("Centro")]
    [InlineData("Deposito")]
    [InlineData("Unidade")]
    public void Persistencia_CamposObrigatoriosVaziosDevemBloquear(string campo)
    {
        EntradaProdutoItem item = campo switch
        {
            "Material" => ItemObrigatorio() with { Material = " " },
            "Centro" => ItemObrigatorio() with { Centro = " " },
            "Deposito" => ItemObrigatorio() with { Deposito = " " },
            _ => ItemObrigatorio() with { Unidade = " " }
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarItemObrigatorio(item));

        Assert.Contains(campo, erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pedido")]
    [InlineData("item")]
    [InlineData("material")]
    [InlineData("centro")]
    [InlineData("deposito")]
    [InlineData("unidade")]
    public void Persistencia_DivergenciaComCacheSapDeveBloquear(string campo)
    {
        EntradaProdutoItem item = ItemObrigatorio();
        ValidacaoItemPesagem cache = CacheValido();
        string pedido = "4500001489";
        (pedido, item, cache) = campo switch
        {
            "pedido" => ("4500009999", item, cache),
            "item" => (pedido, item with { NumeroItem = "20" }, cache),
            "material" => (pedido, item with { Material = "999" }, cache),
            "centro" => (pedido, item with { Centro = "3008" }, cache),
            "deposito" => (pedido, item with { Deposito = "PP02" }, cache),
            _ => (pedido, item with { Unidade = "UN" }, cache)
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarCoerenciaCacheSap(pedido, item, cache));

        Assert.Contains(campo, erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_TaraInformadaComListaVaziaDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarTaraDoSetor(5, new HashSet<long>(), "00010"));

        Assert.Contains("Tara", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_TaraDeOutroSetorDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarTaraDoSetor(5, new HashSet<long> { 6 }, "00010"));

        Assert.Contains("Tara", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_BalancaSemRepositorioDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarBalancaDoSetor(7, null, false, 20, "00010"));

        Assert.Contains("balanca", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_BalancaInativaDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarBalancaDoSetor(7, new BalancaCadastro { CodigoSetor = 20, SituacaoBalanca = false }, true, 20, "00010"));

        Assert.Contains("inativa", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Persistencia_BalancaOutroSetorDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => ValidadorEntradaProdutoPersistenciaLotes.ValidarBalancaDoSetor(7, new BalancaCadastro { CodigoSetor = 21, SituacaoBalanca = true }, true, 20, "00010"));

        Assert.Contains("setor", erro.Message, StringComparison.OrdinalIgnoreCase);
    }
    private static DadosLoteEntrada Dados(string lote)
        => new(lote, Hoje, Hoje.AddDays(30));

    private static EntradaProdutoLoteEmMemoria NovoLote(string lote)
        => new(Dados(lote));

    private static EntradaProdutoLoteEmMemoria NovoLoteConfirmado(string lote)
    {
        EntradaProdutoLoteEmMemoria loteMemoria = NovoLote(lote);
        loteMemoria.Confirmar();
        return loteMemoria;
    }

    private static EntradaProdutoItem ItemObrigatorio()
        => new()
        {
            CodigoSapPedidoCompraItem = 10,
            NumeroItem = "00010",
            Material = "3500027",
            Centro = "3007",
            Deposito = "PP01",
            Unidade = "KG"
        };

    private static ValidacaoItemPesagem CacheValido()
        => new(
            Existe: true,
            PedidoAtivo: true,
            ItemAtivo: true,
            MaterialPresente: true,
            NumeroPedido: "4500001489",
            NumeroItem: "00010",
            Material: "3500027",
            Centro: "3007",
            Deposito: "PP01",
            Unidade: "KG");

    private static EntradaProdutoPesagemComCodigoLocalPersistencia PesagemLocal(Guid codigo, int sequencia, decimal pesoLiquido)
        => new()
        {
            CodigoLocalPesagem = codigo,
            Pesagem = Pesagem(sequencia, pesoLiquido)
        };
    private static EntradaProdutoPesagem Pesagem(int sequencia, decimal pesoLiquido)
        => new()
        {
            Sequencia = sequencia,
            PesoBrutoKg = pesoLiquido,
            PesoTaraKg = 0,
            PesoLiquidoKg = pesoLiquido,
            StatusPesagem = "VALIDA"
        };
}


