using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Tests.Entrada;

public sealed class ValidadorRecuperacaoPersistenciaEntradaLotesTests
{
    [Fact]
    public void ArvoreExatamenteIgual_DeveRecuperarCodigosEDicionariosSomenteLeitura()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.True(validacao.Sucesso);
        Assert.NotNull(validacao.ResultadoRecuperado);
        Assert.True(validacao.ResultadoRecuperado.PersistenciaRecuperada);
        Assert.Equal(9001, validacao.ResultadoRecuperado.CodigoLancamento);
        Assert.Equal(9101, validacao.ResultadoRecuperado.CodigosItensPorNumeroItemSap["00010"]);
        Assert.Equal(9201, validacao.ResultadoRecuperado.CodigosLotesPorCodigoLocal[esperado.Itens[0].Lotes[0].CodigoLocal]);
        Assert.Equal(9301, validacao.ResultadoRecuperado.CodigosPesagensPorCodigoLocal[esperado.Itens[0].Lotes[0].Pesagens[0].CodigoLocalPesagem]);
        Assert.Equal(3.000m, validacao.ResultadoRecuperado.PesosConsolidadosPorLoteLocal[esperado.Itens[0].Lotes[0].CodigoLocal]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<Guid, long>)validacao.ResultadoRecuperado.CodigosLotesPorCodigoLocal).Add(Guid.NewGuid(), 1));
    }

    public static IEnumerable<object[]> Divergencias()
    {
        yield return [AlterarLancamento(static e => e with { Lancamento = e.Lancamento with { NumeroPedido = "4500009999" } }), "pedido"];
        yield return [AlterarLancamento(static e => e with { Lancamento = e.Lancamento with { Fornecedor = "FORN-X" } }), "fornecedor"];
        yield return [AlterarLancamento(static e => e with { Lancamento = e.Lancamento with { CodigoSetor = 999 } }), "setor"];
        yield return [AlterarLancamento(static e => e with { Lancamento = e.Lancamento with { Terminal = "TERM-X" } }), "terminal"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { CodigoSapPedidoCompraItem = 999 } }), "codigo SAP"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { Material = "MAT-X" } }), "dados SAP"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { Centro = "3999" } }), "dados SAP"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { Deposito = "PX99" } }), "dados SAP"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { Unidade = "UN" } }), "dados SAP"];
        yield return [AlterarItem(static i => i with { Item = i.Item with { QuantidadePrevista = 99m } }), "quantidade"];
        yield return [AlterarItem(static i => i with { NumeroItemSap = "20", Item = i.Item with { NumeroItem = "20" } }), "item"];
        yield return [AlterarLote(static l => l with { CorrelationId = Guid.NewGuid() }), "lote/correlation"];
        yield return [AlterarLote(static l => l with { Dados = new DadosLoteEntrada("LOTE-X", DataBase.AddDays(-1), DataBase.AddDays(30)) }), "dados do lote"];
        yield return [AlterarLote(static l => l with { Dados = new DadosLoteEntrada("LOTE-A", DataBase.AddDays(-2), DataBase.AddDays(30)) }), "dados do lote"];
        yield return [AlterarLote(static l => l with { Dados = new DadosLoteEntrada("LOTE-A", DataBase.AddDays(-1), DataBase.AddDays(31)) }), "dados do lote"];
        yield return [AlterarLote(static l => l with { Pesagens = [CriarPesagem(GuidA, 1, 4m, DataHoraBase)] }), "peso consolidado"];
        yield return [AlterarPesagem(static p => p with { Sequencia = 2 }), "pesagem/sequencia"];
        yield return [AlterarPesagem(static p => p with { PesoBrutoKg = 4m, PesoLiquidoKg = 3m }), "pesos"];
        yield return [AlterarPesagem(static p => p with { PesoTaraKg = 1m, PesoLiquidoKg = 2m }), "peso"];
        yield return [AlterarPesagem(static p => p with { PesoLiquidoKg = 2.5m }), "peso"];
        yield return [AlterarPesagem(static p => p with { CodigoTara = 55 }), "operacionais"];
        yield return [AlterarPesagem(static p => p with { CodigoBalanca = 77 }), "operacionais"];
        yield return [AlterarPesagem(static p => p with { Origem = "BALANCA" }), "operacionais"];
        yield return [AlterarPesagem(static p => p with { StatusPesagem = "CANCELADA" }), "peso"];
        yield return [AlterarPesagem(static p => p with { LeituraOriginal = "LEITURA-X" }), "operacionais"];
        yield return [AlterarPesagem(static p => p with { PayloadBalanca = "{\"peso\":3}" }), "operacionais"];
        yield return [AlterarPesagem(static p => p with { PesadoEm = DataHoraBase.AddSeconds(5) }), "pesado_em"];
    }

    [Theory]
    [MemberData(nameof(Divergencias))]
    public void ArvoreDivergente_DeveRejeitar(Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia> alterar, string trechoEsperado)
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        EntradaProdutoLancamentoComLotesPersistencia divergente = alterar(esperado);

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(divergente, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains(trechoEsperado, validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(validacao.ResultadoRecuperado);
    }

    [Fact]
    public void ItemAdicionalPersistido_DeveRejeitarConjuntoNaoExato()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        persistido = persistido with
        {
            Itens = persistido.Itens.Concat([persistido.Itens[0] with { CodigoItem = 9999, NumeroItemSap = "00020" }]).ToList()
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("item adicional", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoteAdicionalPersistido_DeveRejeitarConjuntoNaoExato()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        persistido = persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes = persistido.Itens[0].Lotes.Concat([persistido.Itens[0].Lotes[0] with { CodigoLote = 9999, CorrelationId = Guid.NewGuid() }]).ToList()
                }
            ]
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("lote/correlation", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PesagemAdicionalPersistida_DeveRejeitarConjuntoNaoExato()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        EntradaProdutoPesagemPersistidaLote adicional = persistido.Itens[0].Lotes[0].Pesagens[0] with
        {
            CodigoPesagem = 9999,
            Sequencia = 2
        };
        persistido = persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes =
                    [
                        persistido.Itens[0].Lotes[0] with
                        {
                            Pesagens = persistido.Itens[0].Lotes[0].Pesagens.Concat([adicional]).ToList()
                        }
                    ]
                }
            ]
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("pesagem/sequencia", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PesadoEmEquivalenteAposNormalizacaoPostgreSql_DeveAprovar()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        persistido = persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes =
                    [
                        persistido.Itens[0].Lotes[0] with
                        {
                            Pesagens =
                            [
                                persistido.Itens[0].Lotes[0].Pesagens[0] with { PesadoEm = DataHoraBase.AddTicks(3000) }
                            ]
                        }
                    ]
                }
            ]
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.True(validacao.Sucesso);
    }

    // ---------------------------------------------------------------------------------------------------
    // §7 — Comparação SEMÂNTICA do JSONB (payload_balanca).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Jsonb_MesmoTexto_DeveSerEquivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente("{\"peso\":4.000}", "{\"peso\":4.000}"));

    [Fact]
    public void Jsonb_EspacosDiferentes_DeveSerEquivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4.000,\"origem\":\"TESTE\"}",
            "{  \"peso\" : 4.000 ,  \"origem\" : \"TESTE\"  }"));

    [Fact]
    public void Jsonb_OrdemDePropriedadesDiferente_DeveSerEquivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4.000,\"origem\":\"TESTE\"}",
            "{\"origem\":\"TESTE\",\"peso\":4.000}"));

    [Fact]
    public void Jsonb_ObjetoAninhadoOrdemDiferente_DeveSerEquivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"balanca\":{\"id\":1,\"protocolo\":\"P03\"},\"peso\":4.000}",
            "{\"peso\":4.000,\"balanca\":{\"protocolo\":\"P03\",\"id\":1}}"));

    [Fact]
    public void Jsonb_ArraysIguais_DeveSerEquivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"leituras\":[1,2,3]}",
            "{\"leituras\":[1,2,3]}"));

    [Fact]
    public void Jsonb_OrdemDeArrayDiferente_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"leituras\":[1,2,3]}",
            "{\"leituras\":[3,2,1]}"));

    [Fact]
    public void Jsonb_NumeroDiferente_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4.000}",
            "{\"peso\":5.000}"));

    [Fact]
    public void Jsonb_StringDiferente_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"origem\":\"TESTE\"}",
            "{\"origem\":\"OUTRO\"}"));

    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    public void Jsonb_AmbosAusentes_DeveSerEquivalente(string? esperado, string? persistido)
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(esperado, persistido));

    [Theory]
    [InlineData(null, "{\"peso\":4.000}")]
    [InlineData("{\"peso\":4.000}", "")]
    public void Jsonb_UmAusenteEOutroPresente_DeveSerDivergente(string? esperado, string? persistido)
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(esperado, persistido));

    // §2 — propriedade duplicada: a última ocorrência prevalece (semântica jsonb do PostgreSQL).
    [Fact]
    public void Jsonb_DuplicadaNoEsperado_UltimaPrevalece_Equivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":1,\"peso\":4}",
            "{\"peso\":4}"));

    [Fact]
    public void Jsonb_DuplicadaNoPersistido_UltimaPrevalece_Equivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4}",
            "{\"peso\":1,\"peso\":4}"));

    [Fact]
    public void Jsonb_DuplicadaComUltimoValorDiferente_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4,\"peso\":1}",
            "{\"peso\":4}"));

    [Fact]
    public void Jsonb_DuplicadaEmObjetoAninhado_UltimaPrevalece_Equivalente()
        => Assert.True(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"balanca\":{\"id\":1,\"id\":9},\"peso\":4.000}",
            "{\"peso\":4.000,\"balanca\":{\"id\":9}}"));

    [Fact]
    public void Jsonb_DuplicadaEmObjetoAninhadoComValorFinalDiferente_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"balanca\":{\"id\":1,\"id\":9}}",
            "{\"balanca\":{\"id\":1}}"));

    [Fact]
    public void Jsonb_ArrayPermaneceSensivelAOrdemMesmoComObjetosDuplicados_DeveSerDivergente()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"itens\":[{\"v\":1},{\"v\":2}]}",
            "{\"itens\":[{\"v\":2},{\"v\":1}]}"));

    [Fact]
    public void Jsonb_EsperadoInvalido_DeveSerDivergenciaControlada()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{peso:4}",
            "{\"peso\":4.000}"));

    [Fact]
    public void Jsonb_PersistidoInvalido_DeveSerDivergenciaControlada()
        => Assert.False(ValidadorRecuperacaoPersistenciaEntradaLotes.JsonEquivalente(
            "{\"peso\":4.000}",
            "{peso:4}"));

    // ---------------------------------------------------------------------------------------------------
    // §6 — o JSONB equivalente com ordem diferente RECUPERA através do comparador completo.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void PayloadBalancaEquivalenteComOrdemDiferente_DeveRecuperar()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();

        esperado = AlterarPesagem(p => p with { PayloadBalanca = "{\"peso\":3.000,\"origem\":\"TESTE\"}" })(esperado);
        persistido = SubstituirPayloadPersistido(persistido, "{\"origem\":\"TESTE\",\"peso\":3.000}");

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.True(validacao.Sucesso);
    }

    [Fact]
    public void PayloadBalancaComValorDiferente_DeveRejeitarComoArvoreDiferente()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();

        esperado = AlterarPesagem(p => p with { PayloadBalanca = "{\"peso\":3.000,\"origem\":\"TESTE\"}" })(esperado);
        persistido = SubstituirPayloadPersistido(persistido, "{\"origem\":\"TESTE\",\"peso\":9.999}");

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("operacionais", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------------
    // §8 — diagnóstico de conjuntos com tipos-valor: nunca reportar Guid.Empty/zero indevidamente.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ItemAdicionalPersistido_DeveApontarAdicionalSemGuidVazio()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        persistido = persistido with
        {
            Itens = persistido.Itens.Concat([persistido.Itens[0] with { CodigoItem = 9999, NumeroItemSap = "00020" }]).ToList()
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("adicional", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("00020", validacao.Divergencia!, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrelationIdAdicionalPersistida_DeveApontarAdicionalSemGuidVazio()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        Guid adicional = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        persistido = persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes = persistido.Itens[0].Lotes
                        .Concat([persistido.Itens[0].Lotes[0] with { CodigoLote = 9999, CorrelationId = adicional }])
                        .ToList()
                }
            ]
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("adicional", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", validacao.Divergencia!, StringComparison.Ordinal);
    }

    [Fact]
    public void SequenciaAdicionalPersistida_DeveApontarAdicionalSemZero()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        EntradaProdutoPesagemPersistidaLote adicional = persistido.Itens[0].Lotes[0].Pesagens[0] with
        {
            CodigoPesagem = 9999,
            Sequencia = 7
        };
        persistido = persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes =
                    [
                        persistido.Itens[0].Lotes[0] with
                        {
                            Pesagens = persistido.Itens[0].Lotes[0].Pesagens.Concat([adicional]).ToList()
                        }
                    ]
                }
            ]
        };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("adicional (7)", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
    }

    // §12 (inverso) — esperado 00010+00020, persistido só 00010 ⇒ falta o item 00020.
    [Fact]
    public void ItemAusenteNoPersistido_DeveApontarItemAusente()
    {
        (EntradaProdutoLancamentoComLotesPersistencia esperado, EntradaProdutoLancamentoPersistidoComLotes persistido) =
            CriarArvoresIguais();
        EntradaProdutoItemComLotesPersistencia segundoItem = esperado.Itens[0] with
        {
            NumeroItemSap = "20",
            Item = esperado.Itens[0].Item with { NumeroItem = "20" },
            Lotes =
            [
                esperado.Itens[0].Lotes[0] with { CorrelationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") }
            ]
        };
        esperado = esperado with { Itens = esperado.Itens.Concat([segundoItem]).ToList() };

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(esperado, persistido);

        Assert.False(validacao.Sucesso);
        Assert.Contains("ausente", validacao.Divergencia!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("00020", validacao.Divergencia!, StringComparison.Ordinal);
    }

    private static EntradaProdutoLancamentoPersistidoComLotes SubstituirPayloadPersistido(
        EntradaProdutoLancamentoPersistidoComLotes persistido,
        string payload)
        => persistido with
        {
            Itens =
            [
                persistido.Itens[0] with
                {
                    Lotes =
                    [
                        persistido.Itens[0].Lotes[0] with
                        {
                            Pesagens =
                            [
                                persistido.Itens[0].Lotes[0].Pesagens[0] with { PayloadBalanca = payload }
                            ]
                        }
                    ]
                }
            ]
        };

    private static readonly DateTime DataBase = new(2026, 7, 27);
    private static readonly DateTimeOffset DataHoraBase = new(2026, 7, 27, 15, 30, 10, 123, TimeSpan.Zero);
    private static readonly Guid GuidA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid GuidB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (EntradaProdutoLancamentoComLotesPersistencia Esperado, EntradaProdutoLancamentoPersistidoComLotes Persistido)
        CriarArvoresIguais()
    {
        EntradaProdutoPesagemComCodigoLocalPersistencia pesagem = CriarPesagem(GuidB, 1, 3m, DataHoraBase);
        EntradaProdutoLancamentoComLotesPersistencia esperado = new()
        {
            Lancamento = new EntradaProdutoLancamento
            {
                NumeroPedido = "4500000001",
                Fornecedor = "FORN-1",
                CodigoSetor = 10,
                Terminal = "TERM-1"
            },
            Itens =
            [
                new EntradaProdutoItemComLotesPersistencia
                {
                    NumeroItemSap = "10",
                    Item = new EntradaProdutoItem
                    {
                        CodigoSapPedidoCompraItem = 1001,
                        NumeroItem = "10",
                        Material = "MAT-1",
                        Centro = "3007",
                        Deposito = "PP01",
                        Unidade = "KG",
                        QuantidadePrevista = 10m
                    },
                    Lotes =
                    [
                        new EntradaProdutoLoteComPesagensPersistencia
                        {
                            CodigoLocal = GuidA,
                            CorrelationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                            Dados = new DadosLoteEntrada("LOTE-A", DataBase.AddDays(-1), DataBase.AddDays(30)),
                            EstadoOperacional = EstadoOperacionalLoteEntrada.FinalizadoEmMemoria,
                            Pesagens = [pesagem]
                        }
                    ]
                }
            ]
        };

        EntradaProdutoLancamentoPersistidoComLotes persistido = new()
        {
            CodigoLancamento = 9001,
            NumeroPedido = "4500000001",
            Fornecedor = "FORN-1",
            CodigoSetor = 10,
            Terminal = "TERM-1",
            StatusLancamento = StatusLoteEntrada.FinalizadoLocal,
            Itens =
            [
                new EntradaProdutoItemPersistidoComLotes
                {
                    CodigoItem = 9101,
                    CodigoSapPedidoCompraItem = 1001,
                    NumeroItemSap = "00010",
                    Material = "MAT-1",
                    Centro = "3007",
                    Deposito = "PP01",
                    Unidade = "KG",
                    QuantidadePrevista = 10m,
                    Lotes =
                    [
                        new EntradaProdutoLotePersistidoComPesagens
                        {
                            CodigoLote = 9201,
                            CorrelationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                            NumeroLote = "LOTE-A",
                            DataFabricacao = DataBase.AddDays(-1),
                            DataVencimento = DataBase.AddDays(30),
                            StatusLote = StatusLoteEntrada.FinalizadoLocal,
                            PesoLiquidoTotalKg = 3m,
                            Pesagens =
                            [
                                new EntradaProdutoPesagemPersistidaLote
                                {
                                    CodigoPesagem = 9301,
                                    Sequencia = 1,
                                    PesoBrutoKg = 3m,
                                    PesoTaraKg = 0m,
                                    PesoLiquidoKg = 3m,
                                    Origem = "MANUAL",
                                    StatusPesagem = EntradaProdutoPesagemCalculos.StatusValida,
                                    LeituraOriginal = "3",
                                    PayloadBalanca = "{}",
                                    PesadoEm = DataHoraBase,
                                    NumeroLoteSnapshot = "LOTE-A",
                                    DataFabricacaoSnapshot = DataBase.AddDays(-1),
                                    DataVencimentoSnapshot = DataBase.AddDays(30)
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        return (esperado, persistido);
    }

    private static EntradaProdutoPesagemComCodigoLocalPersistencia CriarPesagem(
        Guid codigoLocal,
        int sequencia,
        decimal peso,
        DateTimeOffset pesadoEm)
        => new()
        {
            CodigoLocalPesagem = codigoLocal,
            Pesagem = new EntradaProdutoPesagem
            {
                Sequencia = sequencia,
                PesoBrutoKg = peso,
                PesoTaraKg = 0m,
                PesoLiquidoKg = peso,
                Origem = "MANUAL",
                StatusPesagem = EntradaProdutoPesagemCalculos.StatusValida,
                LeituraOriginal = peso.ToString("0.###"),
                PayloadBalanca = "{}",
                PesadoEm = pesadoEm
            }
        };

    private static Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia>
        AlterarLancamento(Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia> alterar)
        => alterar;

    private static Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia>
        AlterarItem(Func<EntradaProdutoItemComLotesPersistencia, EntradaProdutoItemComLotesPersistencia> alterar)
        => entrada => entrada with
        {
            Itens = [alterar(entrada.Itens[0])]
        };

    private static Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia>
        AlterarLote(Func<EntradaProdutoLoteComPesagensPersistencia, EntradaProdutoLoteComPesagensPersistencia> alterar)
        => entrada => entrada with
        {
            Itens =
            [
                entrada.Itens[0] with
                {
                    Lotes = [alterar(entrada.Itens[0].Lotes[0])]
                }
            ]
        };

    private static Func<EntradaProdutoLancamentoComLotesPersistencia, EntradaProdutoLancamentoComLotesPersistencia>
        AlterarPesagem(Func<EntradaProdutoPesagem, EntradaProdutoPesagem> alterar)
        => entrada => entrada with
        {
            Itens =
            [
                entrada.Itens[0] with
                {
                    Lotes =
                    [
                        entrada.Itens[0].Lotes[0] with
                        {
                            Pesagens =
                            [
                                entrada.Itens[0].Lotes[0].Pesagens[0] with
                                {
                                    Pesagem = alterar(entrada.Itens[0].Lotes[0].Pesagens[0].Pesagem)
                                }
                            ]
                        }
                    ]
                }
            ]
        };
}
