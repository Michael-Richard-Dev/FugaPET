using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Entrada;

public sealed class EntradaProdutoArvoreLotesPersistenciaTests
{
    [Fact]
    public void TesteUnitario_Estatica_ArvoreValidaDevePassar()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida();

        ValidadorEntradaProdutoArvoreLotes.Validar(entrada);
    }

    [Fact]
    public void TesteUnitario_Estatica_LancamentoSemItemDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with { Itens = [] };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("pelo menos um item", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_ItemSemLoteDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("não possui lote", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_ItemSapDuplicadoDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens =
            [
                Item("10", "3500027", [Lote("A", sequencia: 1)]),
                Item("00010", "3500027", [Lote("B", sequencia: 2)])
            ]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("duplicado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoteLocalDuplicadoDeveBloquear()
    {
        Guid codigoLocal = Guid.NewGuid();
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens =
            [
                Item("10", "3500027", [Lote("A", codigoLocal: codigoLocal, sequencia: 1)]),
                Item("20", "3500028", [Lote("B", codigoLocal: codigoLocal, sequencia: 1)])
            ]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("Lote local duplicado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_CorrelationIdDuplicadoDeveBloquear()
    {
        Guid correlationId = Guid.NewGuid();
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens =
            [
                Item("10", "3500027", [Lote("A", correlationId: correlationId, sequencia: 1)]),
                Item("20", "3500028", [Lote("B", correlationId: correlationId, sequencia: 1)])
            ]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("CorrelationId duplicado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_PesagemLocalDuplicadaDeveBloquear()
    {
        Guid codigoPesagem = Guid.NewGuid();
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens =
            [
                Item("10", "3500027", [
                    Lote("A", pesagens: [PesagemLocal(codigoPesagem, 1, 1m)]),
                    Lote("B", pesagens: [PesagemLocal(codigoPesagem, 2, 1m)])
                ])
            ]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("Pesagem local duplicada", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoteSemPesagemValidaDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", pesagens: [PesagemLocal(Guid.NewGuid(), 1, 1m, "CANCELADA")])])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("soma válida maior que zero", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_SomaValidaZeroDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", pesagens: [PesagemLocal(Guid.NewGuid(), 1, 0m)])])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("maior que zero", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_ItemSapInconsistenteComNumeroItemDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A")]) with { Item = new EntradaProdutoItem { NumeroItem = "20", Material = "3500027" } }]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("inconsistente", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_SequenciaDuplicadaEntreLotesDoMesmoItemDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", sequencia: 1), Lote("B", sequencia: 1)])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("Sequência duplicada", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TesteUnitario_Estatica_LoginAusenteDeveBloquear(string? login)
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.ValidarLoginAuditoriaLote(login));

        Assert.Contains("Login", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoginMaiorQueOitentaDeveBloquear()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.ValidarLoginAuditoriaLote(new string('A', 81)));

        Assert.Contains("80", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoginDeveAplicarTrim()
    {
        string login = ValidadorEntradaProdutoArvoreLotes.ValidarLoginAuditoriaLote(" admin ");

        Assert.Equal("admin", login);
    }

    [Fact]
    public void TesteUnitario_Estatica_ConferenciaPesoToleraTresCasas()
    {
        ValidadorEntradaProdutoArvoreLotes.ConferirPesoConsolidado(Guid.NewGuid(), 1.0004m, 1.000m);
    }

    [Fact]
    public void TesteUnitario_Estatica_ConferenciaPesoDivergenteDeveBloquear()
    {
        Guid lote = Guid.NewGuid();

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.ConferirPesoConsolidado(lote, 1m, 1.010m));

        Assert.Contains(lote.ToString(), erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_ResultadoDeveConterMapasSomenteLeitura()
    {
        Guid lote = Guid.NewGuid();
        Guid pesagem = Guid.NewGuid();
        ResultadoPersistenciaEntradaComLotes resultado = ResultadoPersistenciaEntradaComLotes.Criar(
            1,
            new Dictionary<string, long>(StringComparer.Ordinal) { ["00010"] = 2 },
            new Dictionary<Guid, long> { [lote] = 3 },
            new Dictionary<Guid, long> { [pesagem] = 4 },
            new Dictionary<Guid, decimal> { [lote] = 5m });

        Assert.Equal(1, resultado.CodigoLancamento);
        Assert.Equal(2, resultado.CodigosItensPorNumeroItemSap["00010"]);
        Assert.Equal(3, resultado.CodigosLotesPorCodigoLocal[lote]);
        Assert.Equal(4, resultado.CodigosPesagensPorCodigoLocal[pesagem]);
        Assert.Equal(5m, resultado.PesosConsolidadosPorLoteLocal[lote]);
        Assert.False(resultado.CodigosItensPorNumeroItemSap is Dictionary<string, long>);
    }

    [Theory]
    [InlineData(ModoEntradaMaterial.MateriaPrima)]
    [InlineData(ModoEntradaMaterial.Quimico)]
    public void TesteUnitario_Estatica_MateriaPrimaEQuimicosUsamMesmaPersistencia(ModoEntradaMaterial modo)
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida();
        ContextoLoteEntrada contexto = new() { Modo = modo, NumeroPedido = entrada.Lancamento.NumeroPedido, NumeroItemSap = "00010" };

        ValidadorEntradaProdutoArvoreLotes.Validar(entrada);

        Assert.Equal(modo, contexto.Modo);
    }

    [Fact]
    public void TesteUnitario_Estatica_EstadoDiferenteDeFinalizadoDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A") with { EstadoOperacional = EstadoOperacionalLoteEntrada.Pesando }])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("finalizado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_EstadoIndefinidoDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A") with { EstadoOperacional = (EstadoOperacionalLoteEntrada)999 }])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("finalizado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_FabricacaoFuturaDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A") with { Dados = new DadosLoteEntrada("A", new DateTime(2026, 7, 28), new DateTime(2026, 8, 20)) }])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada, new DateTime(2026, 7, 27)));

        Assert.Contains("futura", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoteVencidoDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A") with { Dados = new DadosLoteEntrada("A", new DateTime(2026, 7, 20), new DateTime(2026, 7, 26)) }])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada, new DateTime(2026, 7, 27)));

        Assert.Contains("vencido", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoteFuncionalDuplicadoMesmasDatasDeveBloquear()
    {
        DadosLoteEntrada dados = new("LOTE1", new DateTime(2026, 7, 20), new DateTime(2026, 8, 20));
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", sequencia: 1) with { Dados = dados }, Lote("A", sequencia: 2) with { Dados = dados }])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("duplicado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_LoteMesmoNumeroDatasDiferentesDeveBloquear()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [
                Lote("A", sequencia: 1) with { Dados = new DadosLoteEntrada("LOTE1", new DateTime(2026, 7, 20), new DateTime(2026, 8, 20)) },
                Lote("B", sequencia: 2) with { Dados = new DadosLoteEntrada("LOTE1", new DateTime(2026, 7, 21), new DateTime(2026, 8, 20)) }
            ])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains("datas diferentes", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "VALIDA", "Origem")]
    [InlineData("SAP", "VALIDA", "Origem")]
    [InlineData("MANUAL", "", "Status")]
    [InlineData("MANUAL", "PENDENTE", "Status")]
    public void TesteUnitario_Estatica_OrigemOuStatusInvalidosDevemBloquear(string origem, string status, string esperado)
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", pesagens: [PesagemLocal(Guid.NewGuid(), 1, 1m, status) with { Pesagem = PesagemLocal(Guid.NewGuid(), 1, 1m, status).Pesagem with { Origem = origem } }])])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains(esperado, erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 0, 0, "bruto")]
    [InlineData(1, -0.1, 1.1, "tara")]
    [InlineData(1, 1, 0, "maior que a tara")]
    [InlineData(1, 0, 0, "líquido")]
    [InlineData(2, 0.5, 1.4, "bruto menos a tara")]
    public void TesteUnitario_Estatica_PesosInvalidosDevemBloquear(double bruto, double tara, double liquido, string esperado)
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", pesagens: [PesagemLocal(Guid.NewGuid(), 1, (decimal)liquido) with { Pesagem = new EntradaProdutoPesagem { Sequencia = 1, PesoBrutoKg = (decimal)bruto, PesoTaraKg = (decimal)tara, PesoLiquidoKg = (decimal)liquido, Origem = "MANUAL", StatusPesagem = "VALIDA" } }])])]
        };

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() => ValidadorEntradaProdutoArvoreLotes.Validar(entrada));

        Assert.Contains(esperado, erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteUnitario_Estatica_SnapshotDeveSerImutavelContraMutacaoDaListaOriginal()
    {
        List<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens = [PesagemLocal(Guid.NewGuid(), 1, 1m)];
        List<EntradaProdutoLoteComPesagensPersistencia> lotes = [Lote("A", pesagens: pesagens)];
        List<EntradaProdutoItemComLotesPersistencia> itens = [Item("10", "3500027", lotes)];
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with { Itens = itens };

        EntradaProdutoLancamentoComLotesPersistencia snapshot = EntradaProdutoArvoreLotesSnapshot.Criar(entrada);
        pesagens.Add(PesagemLocal(Guid.NewGuid(), 2, 1m));
        lotes.Add(Lote("B", sequencia: 3));
        itens.Add(Item("20", "3500028", [Lote("C", sequencia: 1)]));

        Assert.Single(snapshot.Itens);
        Assert.Single(snapshot.Itens[0].Lotes);
        Assert.Single(snapshot.Itens[0].Lotes[0].Pesagens);
        Assert.Single(snapshot.Lancamento.Itens);
    }

    [Fact]
    public void TesteUnitario_Estatica_SnapshotNormalizaOrigemEStatusParaPersistenciaCanonica()
    {
        EntradaProdutoLancamentoComLotesPersistencia entrada = ArvoreValida() with
        {
            Itens = [Item("10", "3500027", [Lote("A", pesagens: [PesagemLocal(Guid.NewGuid(), 1, 1m, " valida ") with { Pesagem = PesagemLocal(Guid.NewGuid(), 1, 1m, " valida ").Pesagem with { Origem = " manual " } }])])]
        };

        EntradaProdutoLancamentoComLotesPersistencia snapshot = EntradaProdutoArvoreLotesSnapshot.Criar(entrada);

        Assert.Equal("MANUAL", snapshot.Itens[0].Lotes[0].Pesagens[0].Pesagem.Origem);
        Assert.Equal("VALIDA", snapshot.Itens[0].Lotes[0].Pesagens[0].Pesagem.StatusPesagem);
        ValidadorEntradaProdutoArvoreLotes.Validar(snapshot);
    }
    private static EntradaProdutoLancamentoComLotesPersistencia ArvoreValida()
        => new()
        {
            Lancamento = new EntradaProdutoLancamento
            {
                NumeroPedido = "4500001424",
                Fornecedor = "20000244",
                Terminal = "TERMINAL_TESTE"
            },
            Itens = [Item("10", "3500027", [Lote("A")])]
        };

    private static EntradaProdutoItemComLotesPersistencia Item(
        string numeroItemSap,
        string material,
        IReadOnlyList<EntradaProdutoLoteComPesagensPersistencia> lotes)
        => new()
        {
            NumeroItemSap = numeroItemSap,
            Item = new EntradaProdutoItem
            {
                CodigoSapPedidoCompraItem = 10,
                NumeroItem = numeroItemSap,
                Material = material,
                Centro = "3007",
                Deposito = "PP01",
                Unidade = "KG",
                QuantidadePrevista = 10m
            },
            Lotes = lotes
        };

    private static EntradaProdutoLoteComPesagensPersistencia Lote(
        string numero,
        Guid? codigoLocal = null,
        Guid? correlationId = null,
        int sequencia = 1,
        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia>? pesagens = null)
        => new()
        {
            CodigoLocal = codigoLocal ?? Guid.NewGuid(),
            Dados = new DadosLoteEntrada(numero, new DateTime(2026, 7, 20), new DateTime(2026, 8, 20)),
            CorrelationId = correlationId ?? Guid.NewGuid(),
            EstadoOperacional = EstadoOperacionalLoteEntrada.FinalizadoEmMemoria,
            Pesagens = pesagens ?? [PesagemLocal(Guid.NewGuid(), sequencia, 1m)]
        };

    private static EntradaProdutoPesagemComCodigoLocalPersistencia PesagemLocal(
        Guid codigoLocalPesagem,
        int sequencia,
        decimal pesoLiquido,
        string status = "VALIDA")
        => new()
        {
            CodigoLocalPesagem = codigoLocalPesagem,
            Pesagem = new EntradaProdutoPesagem
            {
                Sequencia = sequencia,
                PesoBrutoKg = pesoLiquido,
                PesoTaraKg = 0,
                PesoLiquidoKg = pesoLiquido,
                Origem = "MANUAL",
                StatusPesagem = status,
                NumeroLoteSnapshot = "NAO_USAR",
                DataFabricacaoSnapshot = new DateTime(2000, 1, 1),
                DataVencimentoSnapshot = new DateTime(2000, 1, 2)
            }
        };
}



