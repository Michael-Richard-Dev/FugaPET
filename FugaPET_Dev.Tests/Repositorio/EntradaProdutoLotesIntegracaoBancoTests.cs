using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Operacao;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.Tests.Repositorio;

public sealed class EntradaProdutoLotesIntegracaoBancoTests : IAsyncLifetime
{
    private const string Schema = "desenvolvimento";
    private const string DatabaseEsperado = "fuga_jales_local_desenvolvimento";

    private readonly string _prefixo = "IT_LOTE_" + Guid.NewGuid().ToString("N")[..12];
    private FabricaConexaoEntradaLotesDev? _fabrica;
    private string? _motivoIgnorado;
    private string? _hostSanitizado;
    private int _portaSanitizada;
    private readonly List<long> _lancamentosCriados = [];
    private readonly List<string> _terminaisCriados = [];

    public async Task InitializeAsync()
    {
        if (!BancoTesteEntradaLotesDev.TentarCriar(out FabricaConexaoEntradaLotesDev fabrica, out string motivo, out string host, out int porta))
        {
            _motivoIgnorado = motivo;
            return;
        }

        _fabrica = fabrica;
        _hostSanitizado = host;
        _portaSanitizada = porta;

        try
        {
            await BancoTesteEntradaLotesDev.ValidarEstruturaObrigatoriaAsync(fabrica);
        }
        catch (Exception ex)
        {
            _motivoIgnorado = ex.Message;
            _fabrica = null;
        }
    }

    public async Task DisposeAsync()
    {
        EstadoSessaoUsuarioAtual.Limpar();

        if (_fabrica is null)
        {
            return;
        }

        foreach (long codigoLancamento in _lancamentosCriados.Distinct())
        {
            await LimparLancamentoAsync(codigoLancamento);
        }

        foreach (string terminal in _terminaisCriados.Distinct(StringComparer.Ordinal))
        {
            await LimparPorTerminalAsync(terminal);
            Assert.Equal(0, await ContarLancamentosPorTerminalAsync(terminal));
        }
    }

    [EntradaLotesIntegrationFact]
    [Trait("Categoria", "IntegracaoEntradaLotes")]
    public async Task AppUsuarioId_OverloadExplicitoDeveUsarUsuarioInformadoMesmoComSessaoDivergente()
    {
        BancoConfigurado();
        UsuarioTeste usuario = await SelecionarUsuarioAtivoAsync();

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = usuario.CodigoUsuario + 999999,
            Login = _prefixo + "_sessao_divergente",
            Nome = "Sessao Divergente",
            IntegracaoBancoHabilitada = true
        });

        RepositorioAppUsuarioTeste repositorio = new(_fabrica!);
        string? appUsuarioId = await repositorio.ObterAppUsuarioIdNaTransacaoAsync(usuario.CodigoUsuario);

        Assert.Equal(usuario.CodigoUsuario.ToString(), appUsuarioId);
        Console.WriteLine($"[Entrada Lotes DEV] app.usuario_id validado. Host={_hostSanitizado}; Porta={_portaSanitizada}; Database={DatabaseEsperado}; Schema={Schema}; Usuario={usuario.CodigoUsuario}.");
    }

    [EntradaLotesIntegrationFact]
    [Trait("Categoria", "IntegracaoEntradaLotes")]
    public async Task RegistrarLancamentoComLotesAsync_DevePersistirArvoreRealEConsolidarPesosComRollbackIntegral()
    {
        BancoConfigurado();

        UsuarioTeste usuario = await SelecionarUsuarioAtivoAsync();
        SetorTeste setor = await SelecionarSetorAtivoAsync();
        ItemSapTeste itemSap = await SelecionarItemSapAtivoAsync();
        DateTime dataReferencia = DateTime.Today;
        string terminalSucesso = _prefixo + "_OK";
        string terminalFalha = _prefixo + "_FAIL";
        _terminaisCriados.Add(terminalSucesso);
        _terminaisCriados.Add(terminalFalha);

        Guid codigoLocalLoteA = Guid.NewGuid();
        Guid codigoLocalLoteB = Guid.NewGuid();
        Guid correlationLoteA = Guid.NewGuid();
        Guid correlationLoteB = Guid.NewGuid();
        Guid pesagemA1 = Guid.NewGuid();
        Guid pesagemA2 = Guid.NewGuid();
        Guid pesagemB1 = Guid.NewGuid();
        string numeroLoteA = CriarNumeroLote("ITA");
        string numeroLoteB = CriarNumeroLote("ITB");
        string numeroLoteC = CriarNumeroLote("ITC");

        Assert.True(numeroLoteA.Length <= 10);
        Assert.True(numeroLoteB.Length <= 10);
        Assert.True(numeroLoteC.Length <= 10);
        Assert.NotEqual(numeroLoteA, numeroLoteB);

        EntradaProdutoLancamentoComLotesPersistencia entrada = CriarEntrada(itemSap, setor, terminalSucesso,
        [
            CriarLoteFinalizado(numeroLoteA, dataReferencia, codigoLocalLoteA, correlationLoteA,
            [
                CriarPesagem(pesagemA1, 1, 10.000m, terminalSucesso),
                CriarPesagem(pesagemA2, 2, 5.250m, terminalSucesso)
            ]),
            CriarLoteFinalizado(numeroLoteB, dataReferencia, codigoLocalLoteB, correlationLoteB,
            [
                CriarPesagem(pesagemB1, 3, 2.750m, terminalSucesso)
            ])
        ]);
        Assert.Equal(numeroLoteA, entrada.Itens[0].Lotes[0].Dados.NumeroLote);
        Assert.Equal(numeroLoteB, entrada.Itens[0].Lotes[1].Dados.NumeroLote);
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(usuario.CodigoUsuario, usuario.LoginUsuario, setor.CodigoSetor, dataReferencia);

        EntradaProdutoRepositorio repositorio = new(_fabrica!);
        ResultadoPersistenciaEntradaComLotes resultado = await repositorio.RegistrarLancamentoComLotesAsync(entrada, contexto);
        _lancamentosCriados.Add(resultado.CodigoLancamento);

        Assert.True(resultado.CodigoLancamento > 0);
        Assert.True(resultado.CodigosItensPorNumeroItemSap.TryGetValue(itemSap.NumeroItemNormalizado, out long codigoItemPersistido));
        Assert.True(codigoItemPersistido > 0);
        Assert.Equal(2, resultado.CodigosLotesPorCodigoLocal.Count);
        Assert.Equal(3, resultado.CodigosPesagensPorCodigoLocal.Count);
        Assert.All(resultado.CodigosLotesPorCodigoLocal, par => Assert.True(par.Key != Guid.Empty && par.Value > 0));
        Assert.All(resultado.CodigosPesagensPorCodigoLocal, par => Assert.True(par.Key != Guid.Empty && par.Value > 0));
        Assert.Equal(15.250m, resultado.PesosConsolidadosPorLoteLocal[codigoLocalLoteA]);
        Assert.Equal(2.750m, resultado.PesosConsolidadosPorLoteLocal[codigoLocalLoteB]);

        await ValidarLancamentoAsync(resultado.CodigoLancamento, itemSap, usuario, setor, terminalSucesso);
        await ValidarItemAsync(codigoItemPersistido, itemSap, usuario);
        await ValidarLotesAsync(resultado, usuario, dataReferencia, codigoLocalLoteA, codigoLocalLoteB, correlationLoteA, correlationLoteB, numeroLoteA, numeroLoteB);
        await ValidarPesagensAsync(resultado, usuario, dataReferencia, codigoLocalLoteA, codigoLocalLoteB, numeroLoteA, numeroLoteB);

        EntradaProdutoLancamentoComLotesPersistencia entradaComFalha = CriarEntrada(itemSap, setor, terminalFalha,
        [
            CriarLoteFinalizado(numeroLoteC, dataReferencia, Guid.NewGuid(), correlationLoteA,
            [
                CriarPesagem(Guid.NewGuid(), 1, 1.000m, terminalFalha)
            ])
        ]);

        PostgresException excecao = await Assert.ThrowsAsync<PostgresException>(() => repositorio.RegistrarLancamentoComLotesAsync(entradaComFalha, contexto));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, excecao.SqlState);
        Assert.Equal("uq_entrada_lote_correlation_id", excecao.ConstraintName);
        (int lancamentosFalha, int itensFalha, int lotesFalha, int pesagensFalha) = await ContarArvorePorTerminalAsync(terminalFalha);
        Assert.Equal(0, lancamentosFalha);
        Assert.Equal(0, itensFalha);
        Assert.Equal(0, lotesFalha);
        Assert.Equal(0, pesagensFalha);

        Console.WriteLine($"[Entrada Lotes DEV] Persistencia OK. Host={_hostSanitizado}; Porta={_portaSanitizada}; Database={DatabaseEsperado}; Schema={Schema}; Lancamento={resultado.CodigoLancamento}; Item={codigoItemPersistido}; Lotes={resultado.CodigosLotesPorCodigoLocal.Count}; Pesagens={resultado.CodigosPesagensPorCodigoLocal.Count}; TotalItem=18.000.");
    }

    [EntradaLotesIntegrationFact]
    [Trait("Categoria", "IntegracaoEntradaLotes")]
    public async Task RegistrarOuRecuperarLancamentoComLotesAsync_DeveSerIdempotentePorCorrelationId()
    {
        BancoConfigurado();

        UsuarioTeste usuario = await SelecionarUsuarioAtivoAsync();
        SetorTeste setor = await SelecionarSetorAtivoAsync();
        ItemSapTeste itemSap = await SelecionarItemSapAtivoAsync();
        DateTime dataReferencia = DateTime.Today;
        string terminal = _prefixo + "_IDEMP";
        string terminalParcial = _prefixo + "_PARC";
        _terminaisCriados.Add(terminal);
        _terminaisCriados.Add(terminalParcial);

        Guid codigoLocalLoteA = Guid.NewGuid();
        Guid codigoLocalLoteB = Guid.NewGuid();
        Guid correlationLoteA = Guid.NewGuid();
        Guid correlationLoteB = Guid.NewGuid();
        Guid pesagemA1 = Guid.NewGuid();
        Guid pesagemB1 = Guid.NewGuid();
        string numeroLoteA = CriarNumeroLote("IDA");
        string numeroLoteB = CriarNumeroLote("IDB");

        // §15 — payload JSONB real; a recuperação usa um payload semanticamente equivalente com ORDEM
        // diferente de propriedades; a rejeição usa o mesmo payload com um VALOR alterado.
        const string payloadOriginalA = "{\"peso\":4.000,\"origem\":\"TESTE\"}";
        const string payloadReordenadoA = "{\"origem\":\"TESTE\",\"peso\":4.000}";
        const string payloadValorAlteradoA = "{\"origem\":\"TESTE\",\"peso\":9.999}";

        EntradaProdutoLancamentoComLotesPersistencia entrada = CriarEntrada(itemSap, setor, terminal,
        [
            CriarLoteFinalizado(numeroLoteA, dataReferencia, codigoLocalLoteA, correlationLoteA,
            [
                CriarPesagem(pesagemA1, 1, 4.000m, terminal, payloadOriginalA)
            ]),
            CriarLoteFinalizado(numeroLoteB, dataReferencia, codigoLocalLoteB, correlationLoteB,
            [
                CriarPesagem(pesagemB1, 2, 6.000m, terminal)
            ])
        ]);
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(usuario.CodigoUsuario, usuario.LoginUsuario, setor.CodigoSetor, dataReferencia);
        EntradaProdutoRepositorio repositorio = new(_fabrica!);

        ResultadoPersistenciaEntradaComLotes primeira =
            await repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(entrada, contexto);
        _lancamentosCriados.Add(primeira.CodigoLancamento);

        // Segunda chamada: MESMA árvore, porém com o JSONB reordenado (equivalente) → deve RECUPERAR.
        EntradaProdutoLancamentoComLotesPersistencia recuperacaoEquivalente = CriarEntrada(itemSap, setor, terminal,
        [
            CriarLoteFinalizado(numeroLoteA, dataReferencia, codigoLocalLoteA, correlationLoteA,
            [
                CriarPesagem(pesagemA1, 1, 4.000m, terminal, payloadReordenadoA)
            ]),
            CriarLoteFinalizado(numeroLoteB, dataReferencia, codigoLocalLoteB, correlationLoteB,
            [
                CriarPesagem(pesagemB1, 2, 6.000m, terminal)
            ])
        ]);
        ResultadoPersistenciaEntradaComLotes segunda =
            await repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(recuperacaoEquivalente, contexto);

        Assert.False(primeira.PersistenciaRecuperada);
        Assert.True(segunda.PersistenciaRecuperada);
        Assert.Equal(primeira.CodigoLancamento, segunda.CodigoLancamento);
        Assert.Equal(primeira.CodigosItensPorNumeroItemSap, segunda.CodigosItensPorNumeroItemSap);
        Assert.Equal(primeira.CodigosLotesPorCodigoLocal, segunda.CodigosLotesPorCodigoLocal);
        Assert.Equal(primeira.CodigosPesagensPorCodigoLocal, segunda.CodigosPesagensPorCodigoLocal);
        Assert.Equal(primeira.PesosConsolidadosPorLoteLocal, segunda.PesosConsolidadosPorLoteLocal);
        Assert.Equal(1, await ContarLancamentosPorTerminalAsync(terminal));

        // §15 — JSONB com VALOR diferente ⇒ árvore diferente. Repository lança a exceção de domínio.
        EntradaProdutoLancamentoComLotesPersistencia divergentePayload = CriarEntrada(itemSap, setor, terminal,
        [
            CriarLoteFinalizado(numeroLoteA, dataReferencia, codigoLocalLoteA, correlationLoteA,
            [
                CriarPesagem(pesagemA1, 1, 4.000m, terminal, payloadValorAlteradoA)
            ]),
            CriarLoteFinalizado(numeroLoteB, dataReferencia, codigoLocalLoteB, correlationLoteB,
            [
                CriarPesagem(pesagemB1, 2, 6.000m, terminal)
            ])
        ]);

        ConflitoPersistenciaEntradaLotesException erroDivergente =
            await Assert.ThrowsAsync<ConflitoPersistenciaEntradaLotesException>(() =>
                repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(divergentePayload, contexto));
        Assert.Contains("correlation_id", erroDivergente.MensagemUsuario, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ContarLancamentosPorTerminalAsync(terminal));

        // Árvore PARCIAL: correlationLoteA já existe, a segunda correlação é nova.
        EntradaProdutoLancamentoComLotesPersistencia parcial = CriarEntrada(itemSap, setor, terminalParcial,
        [
            CriarLoteFinalizado(CriarNumeroLote("IDC"), dataReferencia, Guid.NewGuid(), correlationLoteA,
            [
                CriarPesagem(Guid.NewGuid(), 1, 1.000m, terminalParcial)
            ]),
            CriarLoteFinalizado(CriarNumeroLote("IDD"), dataReferencia, Guid.NewGuid(), Guid.NewGuid(),
            [
                CriarPesagem(Guid.NewGuid(), 2, 1.000m, terminalParcial)
            ])
        ]);

        ConflitoPersistenciaEntradaLotesException erroParcial =
            await Assert.ThrowsAsync<ConflitoPersistenciaEntradaLotesException>(() =>
                repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(parcial, contexto));
        Assert.Contains("parte das correlações", erroParcial.MensagemUsuario, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await ContarLancamentosPorTerminalAsync(terminalParcial));

        Console.WriteLine($"[Entrada Lotes DEV] Idempotencia por correlation_id + JSONB semantico OK. Host={_hostSanitizado}; Porta={_portaSanitizada}; Database={DatabaseEsperado}; Schema={Schema}; Lancamento={primeira.CodigoLancamento}.");
    }
    // Runtime skip REAL: o xUnit v2 (core 2.9.3) não expõe Assert.Skip (SkipException.ForSkip só funciona em
    // v3). O mecanismo equivalente aqui é o skip em tempo de DESCOBERTA: EntradaLotesDoisItensIntegrationFact
    // consulta a existência de um pedido com dois itens elegíveis e marca Skip quando não há dados — o teste
    // fica IGNORADO com motivo objetivo, nunca APROVADO sem executar o cenário.
    [EntradaLotesDoisItensIntegrationFact]
    [Trait("Categoria", "IntegracaoEntradaLotes")]
    public async Task RegistrarOuRecuperar_RepeticaoOmitindoItem_DeveRejeitarComoArvoreDiferente()
    {
        BancoConfigurado();

        UsuarioTeste usuario = await SelecionarUsuarioAtivoAsync();
        SetorTeste setor = await SelecionarSetorAtivoAsync();
        (ItemSapTeste ItemA, ItemSapTeste ItemB)? dois = await SelecionarDoisItensMesmoPedidoAsync();
        // A disponibilidade já foi confirmada no atributo (discovery). Se, por corrida, não houver dado agora,
        // FALHA explícita — nunca um sucesso silencioso.
        Assert.False(dois is null, "Pedido com dois itens desapareceu entre a descoberta e a execução do teste.");

        (ItemSapTeste itemA, ItemSapTeste itemB) = dois.Value;
        DateTime dataReferencia = DateTime.Today;
        string terminal = _prefixo + "_OMIT";
        _terminaisCriados.Add(terminal);

        Guid correlationA = Guid.NewGuid();
        Guid correlationB = Guid.NewGuid();

        EntradaProdutoLancamentoComLotesPersistencia completa = CriarEntradaDoisItens(itemA, itemB, setor, terminal, dataReferencia, correlationA, correlationB);
        ContextoAuditoriaEntradaLotes contexto = ContextoAuditoriaEntradaLotes.Criar(usuario.CodigoUsuario, usuario.LoginUsuario, setor.CodigoSetor, dataReferencia);
        EntradaProdutoRepositorio repositorio = new(_fabrica!);

        ResultadoPersistenciaEntradaComLotes primeira =
            await repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(completa, contexto);
        _lancamentosCriados.Add(primeira.CodigoLancamento);
        Assert.False(primeira.PersistenciaRecuperada);
        Assert.Equal(2, primeira.CodigosItensPorNumeroItemSap.Count);

        // Repetição íntegra recupera a árvore completa.
        ResultadoPersistenciaEntradaComLotes segunda =
            await repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(completa, contexto);
        Assert.True(segunda.PersistenciaRecuperada);
        Assert.Equal(primeira.CodigoLancamento, segunda.CodigoLancamento);

        // Repetição OMITINDO o item B (envia só o item A, cuja correlação já existe) ⇒ árvore diferente.
        EntradaProdutoLancamentoComLotesPersistencia somenteItemA = CriarEntrada(itemA, setor, terminal,
        [
            CriarLoteFinalizado(CriarNumeroLote("OMA"), dataReferencia, Guid.NewGuid(), correlationA,
            [
                CriarPesagem(Guid.NewGuid(), 1, 4.000m, terminal)
            ])
        ]);

        ConflitoPersistenciaEntradaLotesException erro =
            await Assert.ThrowsAsync<ConflitoPersistenciaEntradaLotesException>(() =>
                repositorio.RegistrarOuRecuperarLancamentoComLotesAsync(somenteItemA, contexto));
        Assert.Contains("correlation_id", erro.MensagemUsuario, StringComparison.OrdinalIgnoreCase);
        // O diagnóstico técnico deve apontar o item adicional persistido (item B).
        Assert.Contains("adicional", erro.DivergenciaTecnica, StringComparison.OrdinalIgnoreCase);

        // Sem segundo lançamento; a árvore original permanece intacta (um único lançamento, dois itens).
        Assert.Equal(1, await ContarLancamentosPorTerminalAsync(terminal));
        (_, int itensTerminal, int lotesTerminal, _) = await ContarArvorePorTerminalAsync(terminal);
        Assert.Equal(2, itensTerminal);
        Assert.Equal(2, lotesTerminal);

        Console.WriteLine($"[Entrada Lotes DEV] Repeticao omitindo item rejeitada. Host={_hostSanitizado}; Porta={_portaSanitizada}; Database={DatabaseEsperado}; Schema={Schema}; Lancamento={primeira.CodigoLancamento}.");
    }

    private void BancoConfigurado()
    {
        if (_fabrica is null)
        {
            throw new InvalidOperationException($"Teste de integracao Entrada Lotes indisponivel: {_motivoIgnorado ?? "banco DEV nao configurado."}");
        }
    }

    private static EntradaProdutoLancamentoComLotesPersistencia CriarEntrada(
        ItemSapTeste itemSap,
        SetorTeste setor,
        string terminal,
        IReadOnlyList<EntradaProdutoLoteComPesagensPersistencia> lotes)
        => new()
        {
            Lancamento = new EntradaProdutoLancamento
            {
                NumeroPedido = itemSap.NumeroPedido,
                Fornecedor = terminal,
                CodigoSetor = setor.CodigoSetor,
                Terminal = terminal
            },
            Itens =
            [
                new EntradaProdutoItemComLotesPersistencia
                {
                    NumeroItemSap = itemSap.NumeroItem,
                    Item = new EntradaProdutoItem
                    {
                        CodigoSapPedidoCompraItem = itemSap.CodigoSapPedidoCompraItem,
                        NumeroItem = itemSap.NumeroItem,
                        Material = itemSap.Material,
                        Centro = itemSap.Centro,
                        Deposito = itemSap.Deposito,
                        Unidade = itemSap.Unidade,
                        QuantidadePrevista = itemSap.QuantidadePrevista
                    },
                    Lotes = lotes
                }
            ]
        };

    private EntradaProdutoLancamentoComLotesPersistencia CriarEntradaDoisItens(
        ItemSapTeste itemA,
        ItemSapTeste itemB,
        SetorTeste setor,
        string terminal,
        DateTime dataReferencia,
        Guid correlationA,
        Guid correlationB)
        => new()
        {
            Lancamento = new EntradaProdutoLancamento
            {
                NumeroPedido = itemA.NumeroPedido,
                Fornecedor = terminal,
                CodigoSetor = setor.CodigoSetor,
                Terminal = terminal
            },
            Itens =
            [
                CriarItemComLote(itemA, dataReferencia, correlationA, CriarNumeroLote("DIA"), 4.000m, terminal),
                CriarItemComLote(itemB, dataReferencia, correlationB, CriarNumeroLote("DIB"), 6.000m, terminal)
            ]
        };

    private static EntradaProdutoItemComLotesPersistencia CriarItemComLote(
        ItemSapTeste itemSap,
        DateTime dataReferencia,
        Guid correlationId,
        string numeroLote,
        decimal peso,
        string terminal)
        => new()
        {
            NumeroItemSap = itemSap.NumeroItem,
            Item = new EntradaProdutoItem
            {
                CodigoSapPedidoCompraItem = itemSap.CodigoSapPedidoCompraItem,
                NumeroItem = itemSap.NumeroItem,
                Material = itemSap.Material,
                Centro = itemSap.Centro,
                Deposito = itemSap.Deposito,
                Unidade = itemSap.Unidade,
                QuantidadePrevista = itemSap.QuantidadePrevista
            },
            Lotes =
            [
                CriarLoteFinalizado(numeroLote, dataReferencia, Guid.NewGuid(), correlationId,
                [
                    CriarPesagem(Guid.NewGuid(), 1, peso, terminal)
                ])
            ]
        };

    private static EntradaProdutoLoteComPesagensPersistencia CriarLoteFinalizado(
        string numeroLote,
        DateTime dataReferencia,
        Guid codigoLocal,
        Guid correlationId,
        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens)
    {
        EntradaProdutoLoteEmMemoria lote = new(
            new DadosLoteEntrada(numeroLote, dataReferencia.AddDays(-1), dataReferencia.AddDays(365)),
            codigoLocal,
            correlationId);
        lote.Confirmar();
        foreach (EntradaProdutoPesagemComCodigoLocalPersistencia pesagem in pesagens)
        {
            lote.RegistrarPesagem(pesagem.Pesagem);
        }

        lote.FinalizarEmMemoria();
        return EntradaProdutoLoteComPesagensPersistencia.CriarDeLoteFinalizado(lote, pesagens);
    }

    private static string CriarNumeroLote(string prefixo)
    {
        string numero = DadosLoteEntrada.NormalizarNumeroLote(prefixo + Guid.NewGuid().ToString("N")[..7]);
        Assert.Equal(10, numero.Length);
        Assert.Equal(numero, numero.ToUpperInvariant());
        return numero;
    }

    // PesadoEm determinístico: permite reconstruir a MESMA árvore (mesma pesagem) numa segunda chamada de
    // recuperação sem divergir por pesado_em. payload permite exercitar o §15 (JSONB semântico) com um
    // payload real e uma reordenação equivalente.
    private static readonly DateTimeOffset PesadoEmBase = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static EntradaProdutoPesagemComCodigoLocalPersistencia CriarPesagem(Guid codigoLocalPesagem, int sequencia, decimal peso, string prefixo, string payload = "{}")
        => new()
        {
            CodigoLocalPesagem = codigoLocalPesagem,
            Pesagem = new EntradaProdutoPesagem
            {
                Sequencia = sequencia,
                PesoBrutoKg = peso,
                PesoTaraKg = 0m,
                PesoLiquidoKg = peso,
                Origem = "MANUAL",
                StatusPesagem = "VALIDA",
                LeituraOriginal = prefixo + "_SEQ_" + sequencia,
                PayloadBalanca = payload,
                PesadoEm = PesadoEmBase.AddSeconds(sequencia)
            }
        };

    private async Task<UsuarioTeste> SelecionarUsuarioAtivoAsync()
    {
        const string sql = """
            SELECT codigo_usuario, login_usuario
              FROM usuario
             WHERE codigo_usuario > 0
               AND situacao_usuario = true
               AND nullif(trim(login_usuario), '') IS NOT NULL
             ORDER BY codigo_usuario
             LIMIT 1;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        if (!await leitor.ReadAsync())
        {
            throw new InvalidOperationException("Nenhum usuario ativo encontrado para o teste de integracao Entrada Lotes.");
        }

        return new UsuarioTeste(leitor.GetInt64(0), leitor.GetString(1));
    }

    private async Task<SetorTeste> SelecionarSetorAtivoAsync()
    {
        const string sql = """
            SELECT codigo_setor
              FROM setor
             WHERE codigo_setor > 0
               AND situacao_setor = true
             ORDER BY codigo_setor
             LIMIT 1;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is long codigo
            ? new SetorTeste(codigo)
            : throw new InvalidOperationException("Nenhum setor ativo encontrado para o teste de integracao Entrada Lotes.");
    }

    private async Task<ItemSapTeste> SelecionarItemSapAtivoAsync()
    {
        const string sql = """
            SELECT i.codigo_sap_pedido_compra_item,
                   p.numero_pedido,
                   i.numero_item,
                   i.codigo_produto,
                   i.centro,
                   i.deposito,
                   i.unidade_medida,
                   COALESCE(i.quantidade_pedida, 18.000)
              FROM sap_pedido_compra_item i
              JOIN sap_pedido_compra p
                ON p.codigo_sap_pedido_compra = i.codigo_sap_pedido_compra
             WHERE p.situacao_sap_pedido_compra = true
               AND p.ativo_sap = true
               AND i.situacao_sap_pedido_compra_item = true
               AND i.ativo_sap = true
               AND nullif(trim(p.numero_pedido), '') IS NOT NULL
               AND nullif(trim(i.numero_item), '') IS NOT NULL
               AND nullif(trim(i.codigo_produto), '') IS NOT NULL
               AND nullif(trim(i.centro), '') IS NOT NULL
               AND nullif(trim(i.deposito), '') IS NOT NULL
               AND nullif(trim(i.unidade_medida), '') IS NOT NULL
             ORDER BY p.numero_pedido, i.numero_item
             LIMIT 1;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        if (!await leitor.ReadAsync())
        {
            throw new InvalidOperationException("Nenhum item SAP ativo elegivel encontrado no cache local para o teste de integracao Entrada Lotes.");
        }

        string numeroItem = leitor.GetString(2);
        return new ItemSapTeste(
            leitor.GetInt64(0),
            leitor.GetString(1),
            numeroItem,
            EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItem),
            leitor.GetString(3),
            leitor.GetString(4),
            leitor.GetString(5),
            leitor.GetString(6),
            leitor.GetDecimal(7));
    }
    private async Task<(ItemSapTeste ItemA, ItemSapTeste ItemB)?> SelecionarDoisItensMesmoPedidoAsync()
    {
        // Seleciona os dois primeiros itens elegíveis de um MESMO pedido (o primeiro pedido que tiver >= 2).
        const string sql = """
            SELECT i.codigo_sap_pedido_compra_item,
                   p.numero_pedido,
                   i.numero_item,
                   i.codigo_produto,
                   i.centro,
                   i.deposito,
                   i.unidade_medida,
                   COALESCE(i.quantidade_pedida, 18.000)
              FROM sap_pedido_compra_item i
              JOIN sap_pedido_compra p
                ON p.codigo_sap_pedido_compra = i.codigo_sap_pedido_compra
             WHERE p.situacao_sap_pedido_compra = true
               AND p.ativo_sap = true
               AND i.situacao_sap_pedido_compra_item = true
               AND i.ativo_sap = true
               AND nullif(trim(p.numero_pedido), '') IS NOT NULL
               AND nullif(trim(i.numero_item), '') IS NOT NULL
               AND nullif(trim(i.codigo_produto), '') IS NOT NULL
               AND nullif(trim(i.centro), '') IS NOT NULL
               AND nullif(trim(i.deposito), '') IS NOT NULL
               AND nullif(trim(i.unidade_medida), '') IS NOT NULL
               AND i.codigo_sap_pedido_compra = (
                   SELECT i2.codigo_sap_pedido_compra
                     FROM sap_pedido_compra_item i2
                     JOIN sap_pedido_compra p2
                       ON p2.codigo_sap_pedido_compra = i2.codigo_sap_pedido_compra
                    WHERE p2.situacao_sap_pedido_compra = true
                      AND p2.ativo_sap = true
                      AND i2.situacao_sap_pedido_compra_item = true
                      AND i2.ativo_sap = true
                      AND nullif(trim(i2.numero_item), '') IS NOT NULL
                      AND nullif(trim(i2.codigo_produto), '') IS NOT NULL
                      AND nullif(trim(i2.centro), '') IS NOT NULL
                      AND nullif(trim(i2.deposito), '') IS NOT NULL
                      AND nullif(trim(i2.unidade_medida), '') IS NOT NULL
                    GROUP BY i2.codigo_sap_pedido_compra
                   HAVING count(*) >= 2
                    ORDER BY i2.codigo_sap_pedido_compra
                    LIMIT 1)
             ORDER BY i.numero_item
             LIMIT 2;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();

        List<ItemSapTeste> itens = [];
        while (await leitor.ReadAsync())
        {
            string numeroItem = leitor.GetString(2);
            itens.Add(new ItemSapTeste(
                leitor.GetInt64(0),
                leitor.GetString(1),
                numeroItem,
                EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItem),
                leitor.GetString(3),
                leitor.GetString(4),
                leitor.GetString(5),
                leitor.GetString(6),
                leitor.GetDecimal(7)));
        }

        return itens.Count >= 2 ? (itens[0], itens[1]) : null;
    }

    private async Task ValidarLancamentoAsync(long codigoLancamento, ItemSapTeste itemSap, UsuarioTeste usuario, SetorTeste setor, string terminal)
    {
        const string sql = """
            SELECT numero_pedido, codigo_usuario, codigo_setor, status_lancamento, finalizado_em IS NOT NULL,
                   entrada_produto_lancamento_criado_por, terminal
              FROM entrada_produto_lancamento
             WHERE codigo_entrada_produto_lancamento = @codigo;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo", codigoLancamento);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Assert.True(await leitor.ReadAsync());
        Assert.Equal(itemSap.NumeroPedido, leitor.GetString(0));
        Assert.Equal(usuario.CodigoUsuario, leitor.GetInt64(1));
        Assert.Equal(setor.CodigoSetor, leitor.GetInt64(2));
        Assert.Equal("FINALIZADO_LOCAL", leitor.GetString(3));
        Assert.True(leitor.GetBoolean(4));
        Assert.Equal(usuario.CodigoUsuario, leitor.GetInt64(5));
        Assert.Equal(terminal, leitor.GetString(6));
    }

    private async Task ValidarItemAsync(long codigoItem, ItemSapTeste itemSap, UsuarioTeste usuario)
    {
        const string sql = """
            SELECT codigo_sap_pedido_compra_item, numero_item, material, centro, deposito, unidade,
                   status_item, quantidade_recebida, entrada_produto_item_criado_por
              FROM entrada_produto_item
             WHERE codigo_entrada_produto_item = @codigo;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo", codigoItem);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Assert.True(await leitor.ReadAsync());
        Assert.Equal(itemSap.CodigoSapPedidoCompraItem, leitor.GetInt64(0));
        Assert.Equal(itemSap.NumeroItemNormalizado, leitor.GetString(1));
        Assert.Equal(itemSap.Material, leitor.GetString(2));
        Assert.Equal(itemSap.Centro, leitor.GetString(3));
        Assert.Equal(itemSap.Deposito, leitor.GetString(4));
        Assert.Equal(itemSap.Unidade, leitor.GetString(5));
        Assert.Equal("FINALIZADO_LOCAL", leitor.GetString(6));
        Assert.Equal(18.000m, leitor.GetDecimal(7));
        Assert.Equal(usuario.CodigoUsuario, leitor.GetInt64(8));
    }

    private async Task ValidarLotesAsync(ResultadoPersistenciaEntradaComLotes resultado, UsuarioTeste usuario, DateTime dataReferencia, Guid loteA, Guid loteB, Guid correlationA, Guid correlationB, string numeroLoteA, string numeroLoteB)
    {
        const string sql = """
            SELECT numero_lote, data_fabricacao, data_vencimento, status_lote,
                   correlation_id, criado_por, confirmado_em IS NOT NULL, peso_liquido_total_kg,
                   documento_material_sap, exercicio_documento_material_sap, documento_material_item,
                   batch_retornado_sap, quantidade_retorno_sap, payload_enviado_json, resposta_sap_json,
                   erro_sanitizado, enviado_sap_em, confirmado_sap_em
              FROM entrada_produto_lote
             WHERE codigo_entrada_produto_lote = ANY(@codigos)
             ORDER BY codigo_entrada_produto_lote;
            """;
        long[] codigos = resultado.CodigosLotesPorCodigoLocal.Values.ToArray();
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(new NpgsqlParameter("@codigos", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = codigos });
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Dictionary<Guid, (string NumeroLote, decimal Peso)> esperado = new()
        {
            [correlationA] = (numeroLoteA, 15.250m),
            [correlationB] = (numeroLoteB, 2.750m)
        };
        int registros = 0;
        while (await leitor.ReadAsync())
        {
            registros++;
            Guid correlation = leitor.GetGuid(4);
            Assert.Contains(correlation, esperado.Keys);
            Assert.Equal(esperado[correlation].NumeroLote, leitor.GetString(0));
            Assert.Equal(dataReferencia.AddDays(-1), leitor.GetDateTime(1));
            Assert.Equal(dataReferencia.AddDays(365), leitor.GetDateTime(2));
            Assert.Equal("FINALIZADO_LOCAL", leitor.GetString(3));
            Assert.Equal(usuario.LoginUsuario, leitor.GetString(5));
            Assert.True(leitor.GetBoolean(6));
            Assert.Equal(esperado[correlation].Peso, leitor.GetDecimal(7));
            for (int ordinal = 8; ordinal <= 17; ordinal++)
            {
                Assert.True(leitor.IsDBNull(ordinal), $"Campo SAP ordinal {ordinal} deveria permanecer NULL apos persistencia local.");
            }
        }

        Assert.Equal(2, registros);
        Assert.True(resultado.CodigosLotesPorCodigoLocal.ContainsKey(loteA));
        Assert.True(resultado.CodigosLotesPorCodigoLocal.ContainsKey(loteB));
    }
    private async Task ValidarPesagensAsync(ResultadoPersistenciaEntradaComLotes resultado, UsuarioTeste usuario, DateTime dataReferencia, Guid loteA, Guid loteB, string numeroLoteA, string numeroLoteB)
    {
        const string sql = """
            SELECT sequencia, peso_bruto_kg, peso_tara_kg, peso_liquido_kg, status_pesagem, origem,
                   codigo_entrada_produto_lote, numero_lote_snapshot, data_fabricacao_snapshot,
                   data_vencimento_snapshot, codigo_usuario, entrada_produto_pesagem_criado_por
              FROM entrada_produto_pesagem
             WHERE codigo_entrada_produto_pesagem = ANY(@codigos)
             ORDER BY sequencia;
            """;
        long codigoLoteA = resultado.CodigosLotesPorCodigoLocal[loteA];
        long codigoLoteB = resultado.CodigosLotesPorCodigoLocal[loteB];
        long[] codigosPesagens = resultado.CodigosPesagensPorCodigoLocal.Values.ToArray();
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(new NpgsqlParameter("@codigos", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = codigosPesagens });
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        int registros = 0;
        while (await leitor.ReadAsync())
        {
            registros++;
            int sequencia = leitor.GetInt32(0);
            decimal esperado = sequencia switch { 1 => 10.000m, 2 => 5.250m, 3 => 2.750m, _ => throw new InvalidOperationException("Sequencia inesperada.") };
            long codigoLoteEsperado = sequencia is 1 or 2 ? codigoLoteA : codigoLoteB;
            string numeroLoteEsperado = sequencia is 1 or 2 ? numeroLoteA : numeroLoteB;
            Assert.Equal(esperado, leitor.GetDecimal(1));
            Assert.Equal(0m, leitor.GetDecimal(2));
            Assert.Equal(esperado, leitor.GetDecimal(3));
            Assert.Equal("VALIDA", leitor.GetString(4));
            Assert.Equal("MANUAL", leitor.GetString(5));
            Assert.Equal(codigoLoteEsperado, leitor.GetInt64(6));
            Assert.Equal(numeroLoteEsperado, leitor.GetString(7));
            Assert.Equal(dataReferencia.AddDays(-1), leitor.GetDateTime(8));
            Assert.Equal(dataReferencia.AddDays(365), leitor.GetDateTime(9));
            Assert.Equal(usuario.CodigoUsuario, leitor.GetInt64(10));
            Assert.Equal(usuario.CodigoUsuario, leitor.GetInt64(11));
        }

        Assert.Equal(3, registros);
    }
    private async Task<int> ContarLancamentosPorTerminalAsync(string terminal)
    {
        const string sql = "SELECT count(*)::int FROM entrada_produto_lancamento WHERE terminal = @terminal;";
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@terminal", terminal);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is int total ? total : 0;
    }


    private async Task<(int Lancamentos, int Itens, int Lotes, int Pesagens)> ContarArvorePorTerminalAsync(string terminal)
    {
        const string sql = """
            SELECT
                (SELECT count(*)::int FROM entrada_produto_lancamento WHERE terminal = @terminal),
                (SELECT count(*)::int
                   FROM entrada_produto_item item
                   JOIN entrada_produto_lancamento lancamento
                     ON lancamento.codigo_entrada_produto_lancamento = item.codigo_entrada_produto_lancamento
                  WHERE lancamento.terminal = @terminal),
                (SELECT count(*)::int
                   FROM entrada_produto_lote lote
                   JOIN entrada_produto_item item
                     ON item.codigo_entrada_produto_item = lote.codigo_entrada_produto_item
                   JOIN entrada_produto_lancamento lancamento
                     ON lancamento.codigo_entrada_produto_lancamento = item.codigo_entrada_produto_lancamento
                  WHERE lancamento.terminal = @terminal),
                (SELECT count(*)::int
                   FROM entrada_produto_pesagem pesagem
                   JOIN entrada_produto_item item
                     ON item.codigo_entrada_produto_item = pesagem.codigo_entrada_produto_item
                   JOIN entrada_produto_lancamento lancamento
                     ON lancamento.codigo_entrada_produto_lancamento = item.codigo_entrada_produto_lancamento
                  WHERE lancamento.terminal = @terminal);
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@terminal", terminal);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Assert.True(await leitor.ReadAsync());
        return (leitor.GetInt32(0), leitor.GetInt32(1), leitor.GetInt32(2), leitor.GetInt32(3));
    }
    private async Task LimparLancamentoAsync(long codigoLancamento)
    {
        const string sql = """
            DELETE FROM entrada_produto_pesagem
             WHERE codigo_entrada_produto_item IN (
                   SELECT codigo_entrada_produto_item FROM entrada_produto_item WHERE codigo_entrada_produto_lancamento = @codigo_lancamento);
            DELETE FROM entrada_produto_lote
             WHERE codigo_entrada_produto_item IN (
                   SELECT codigo_entrada_produto_item FROM entrada_produto_item WHERE codigo_entrada_produto_lancamento = @codigo_lancamento);
            DELETE FROM entrada_produto_item WHERE codigo_entrada_produto_lancamento = @codigo_lancamento;
            DELETE FROM entrada_produto_lancamento WHERE codigo_entrada_produto_lancamento = @codigo_lancamento;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo_lancamento", codigoLancamento);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task LimparPorTerminalAsync(string terminal)
    {
        const string sql = """
            DELETE FROM entrada_produto_pesagem
             WHERE codigo_entrada_produto_item IN (
                   SELECT item.codigo_entrada_produto_item
                     FROM entrada_produto_item item
                     JOIN entrada_produto_lancamento lancamento
                       ON lancamento.codigo_entrada_produto_lancamento = item.codigo_entrada_produto_lancamento
                    WHERE lancamento.terminal = @terminal);
            DELETE FROM entrada_produto_lote
             WHERE codigo_entrada_produto_item IN (
                   SELECT item.codigo_entrada_produto_item
                     FROM entrada_produto_item item
                     JOIN entrada_produto_lancamento lancamento
                       ON lancamento.codigo_entrada_produto_lancamento = item.codigo_entrada_produto_lancamento
                    WHERE lancamento.terminal = @terminal);
            DELETE FROM entrada_produto_item
             WHERE codigo_entrada_produto_lancamento IN (
                   SELECT codigo_entrada_produto_lancamento FROM entrada_produto_lancamento WHERE terminal = @terminal);
            DELETE FROM entrada_produto_lancamento WHERE terminal = @terminal;
            """;
        await using NpgsqlConnection conexao = await _fabrica!.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@terminal", terminal);
        await comando.ExecuteNonQueryAsync();
    }

    private sealed record UsuarioTeste(long CodigoUsuario, string LoginUsuario);
    private sealed record SetorTeste(long CodigoSetor);
    private sealed record ItemSapTeste(
        long CodigoSapPedidoCompraItem,
        string NumeroPedido,
        string NumeroItem,
        string NumeroItemNormalizado,
        string Material,
        string Centro,
        string Deposito,
        string Unidade,
        decimal QuantidadePrevista);

    private sealed class RepositorioAppUsuarioTeste(IFabricaConexaoBanco fabricaConexaoBanco) : RepositorioBase(fabricaConexaoBanco)
    {
        public Task<string?> ObterAppUsuarioIdNaTransacaoAsync(long codigoUsuario)
            => ExecutarEmTransacaoAuditavelAsync(codigoUsuario, async (conexao, transacao) =>
            {
                const string sql = "SELECT current_setting('app.usuario_id', true);";
                await using NpgsqlCommand comando = new(sql, conexao, transacao);
                object? retorno = await comando.ExecuteScalarAsync();
                return retorno?.ToString();
            });
    }
}

internal class EntradaLotesIntegrationFactAttribute : FactAttribute
{
    public EntradaLotesIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BancoTesteEntradaLotesDev.VariavelConnectionString)))
        {
            Skip =
                $"Teste de integracao Entrada Lotes ignorado: variavel {BancoTesteEntradaLotesDev.VariavelConnectionString} nao configurada.";
        }
        else if (!string.Equals(Environment.GetEnvironmentVariable(BancoTesteEntradaLotesDev.VariavelPermitirEntradaLotesReal)?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip =
                $"Teste de integracao Entrada Lotes ignorado: defina {BancoTesteEntradaLotesDev.VariavelPermitirEntradaLotesReal}=true para autorizar.";
        }
        else if (!string.Equals(Environment.GetEnvironmentVariable(BancoTesteEntradaLotesDev.VariavelConfirmarDatabase)?.Trim(), "fuga_jales_local_desenvolvimento", StringComparison.Ordinal))
        {
            Skip =
                $"Teste de integracao Entrada Lotes ignorado: confirme {BancoTesteEntradaLotesDev.VariavelConfirmarDatabase}=fuga_jales_local_desenvolvimento.";
        }
    }
}
/// <summary>
/// §1 — Testes de unidade da regra de skip do atributo de dois itens (SEM banco). Confirmam: opt-in ausente
/// preserva o skip da base sem tocar no banco; ausência de dados vira skip; falha técnica vira exceção
/// sanitizada de descoberta (nunca skip).
/// </summary>
public sealed class EntradaLotesDoisItensSkipTests
{
    [Fact]
    public void OptInAusente_PreservaSkipDaBaseSemAcessarBanco()
    {
        bool verificadorChamado = false;
        string? motivo = EntradaLotesDoisItensIntegrationFactAttribute.ResolverMotivoSkip(
            "skip da base por opt-in ausente",
            () => { verificadorChamado = true; return true; });

        Assert.Equal("skip da base por opt-in ausente", motivo);
        Assert.False(verificadorChamado);
    }

    [Fact]
    public void ComDados_NaoGeraSkip()
        => Assert.Null(EntradaLotesDoisItensIntegrationFactAttribute.ResolverMotivoSkip(null, () => true));

    [Fact]
    public void SemDados_GeraSkipObjetivo()
        => Assert.Equal(
            EntradaLotesDoisItensIntegrationFactAttribute.MotivoSemDados,
            EntradaLotesDoisItensIntegrationFactAttribute.ResolverMotivoSkip(null, () => false));

    [Fact]
    public void FalhaTecnica_LancaExcecaoSanitizadaEnaoSkip()
    {
        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(() =>
            EntradaLotesDoisItensIntegrationFactAttribute.ResolverMotivoSkip(
                null,
                () => throw new InvalidOperationException("Host=192.168.4.160;Password=segredo;SELECT * FROM x")));

        Assert.Equal(EntradaLotesDoisItensIntegrationFactAttribute.MensagemFalhaTecnica, erro.Message);
        Assert.DoesNotContain("192.168.4.160", erro.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AtributoRealSemOptIn_DefineSkipDaBaseSemLancar()
    {
        // Sem as três variáveis de opt-in, a base define Skip e a derivada NÃO acessa o banco (não lança).
        EntradaLotesDoisItensIntegrationFactAttribute atributo = new();
        Assert.False(string.IsNullOrEmpty(atributo.Skip));
    }
}

/// <summary>
/// Herda todo o gate de opt-in de <see cref="EntradaLotesIntegrationFactAttribute"/> e, quando o opt-in está
/// autorizado, faz um skip REAL em tempo de descoberta se o banco DEV não tiver um pedido com dois itens SAP
/// elegíveis — condição indispensável ao cenário de repetição omitindo item. Sem dados ⇒ teste IGNORADO com
/// motivo objetivo (nunca aprovado sem executar o cenário).
/// </summary>
internal sealed class EntradaLotesDoisItensIntegrationFactAttribute : EntradaLotesIntegrationFactAttribute
{
    public const string MotivoSemDados =
        "Nenhum pedido com dois itens SAP elegiveis foi encontrado no cache DEV.";

    public const string MensagemFalhaTecnica =
        "Falha técnica ao verificar a disponibilidade de pedido DEV com dois itens.";

    public EntradaLotesDoisItensIntegrationFactAttribute()
    {
        // Skip herdado (opt-in ausente/recusado) é preservado; caso contrário, decide entre skip por AUSÊNCIA
        // de dados ou EXCEÇÃO de descoberta em falha técnica.
        Skip = ResolverMotivoSkip(Skip, VerificarExistenciaPedidoDoisItens);
    }

    /// <summary>
    /// Regra PURA e testável: mantém o skip herdado (opt-in ausente ⇒ nenhum acesso ao banco); só a ausência
    /// de dados (verificador == false) vira Skip; qualquer falha técnica no verificador vira
    /// <see cref="InvalidOperationException"/> SANITIZADA (nunca skip silencioso, nunca connection string,
    /// usuário, senha, SQL ou Authorization na mensagem).
    /// </summary>
    internal static string? ResolverMotivoSkip(string? skipHerdado, Func<bool> verificarExistenciaDoisItens)
    {
        if (!string.IsNullOrEmpty(skipHerdado))
        {
            return skipHerdado; // Opt-in ausente: não toca no banco.
        }

        bool existe;
        try
        {
            existe = verificarExistenciaDoisItens();
        }
        catch (Exception)
        {
            // Conexão/timeout/schema/tabela/SQL/permissão/NpgsqlException/qualquer inesperada:
            // erro de DESCOBERTA, jamais teste ignorado.
            throw new InvalidOperationException(MensagemFalhaTecnica);
        }

        return existe ? null : MotivoSemDados;
    }

    private static bool VerificarExistenciaPedidoDoisItens()
    {
        if (!BancoTesteEntradaLotesDev.TentarCriar(out FabricaConexaoEntradaLotesDev fabrica, out _, out _, out _))
        {
            // Opt-in passou na base mas a fábrica recusou (ex.: Database divergente): falha de configuração.
            throw new InvalidOperationException("Banco DEV indisponível para verificar pedido com dois itens.");
        }

        return BancoTesteEntradaLotesDev.ExistePedidoComDoisItensAsync(fabrica).GetAwaiter().GetResult();
    }
}

internal static class BancoTesteEntradaLotesDev
{
    public const string VariavelConnectionString = "FUGAPET_DEV_TESTE_CONNECTION_STRING";
    public const string VariavelPermitirEntradaLotesReal = "FUGAPET_DEV_PERMITIR_ENTRADA_LOTES_REAL";
    public const string VariavelConfirmarDatabase = "FUGAPET_DEV_CONFIRMAR_DATABASE";
    private const string DatabaseEsperado = "fuga_jales_local_desenvolvimento";
    private const string Schema = "desenvolvimento";

    public static bool OptInConfigurado()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariavelConnectionString))
           && string.Equals(Environment.GetEnvironmentVariable(VariavelPermitirEntradaLotesReal)?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
           && string.Equals(Environment.GetEnvironmentVariable(VariavelConfirmarDatabase)?.Trim(), DatabaseEsperado, StringComparison.Ordinal);

    public static bool TentarCriar(out FabricaConexaoEntradaLotesDev fabrica, out string motivo, out string host, out int porta)
    {
        fabrica = null!;
        host = string.Empty;
        porta = 0;
        string? connectionString = Environment.GetEnvironmentVariable(VariavelConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            motivo = $"Variavel {VariavelConnectionString} nao configurada.";
            return false;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable(VariavelPermitirEntradaLotesReal)?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            motivo = $"Defina {VariavelPermitirEntradaLotesReal}=true para autorizar o teste real de Entrada Lotes DEV.";
            return false;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable(VariavelConfirmarDatabase)?.Trim(), DatabaseEsperado, StringComparison.Ordinal))
        {
            motivo = $"Defina {VariavelConfirmarDatabase}={DatabaseEsperado} para confirmar o banco DEV real.";
            return false;
        }

        NpgsqlConnectionStringBuilder builder = new(connectionString);
        if (!string.Equals(builder.Database, DatabaseEsperado, StringComparison.Ordinal))
        {
            motivo = $"Database recusado: '{builder.Database}' diferente de '{DatabaseEsperado}'.";
            return false;
        }

        builder.SearchPath = Schema;
        host = builder.Host ?? string.Empty;
        porta = builder.Port;
        fabrica = new FabricaConexaoEntradaLotesDev(builder.ConnectionString);
        Console.WriteLine($"[Entrada Lotes DEV] Opt-in autorizado. Host={host}; Porta={porta}; Database={builder.Database}; Schema={Schema}.");
        motivo = string.Empty;
        return true;
    }
    public static async Task<bool> ExistePedidoComDoisItensAsync(FabricaConexaoEntradaLotesDev fabrica)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                  FROM sap_pedido_compra_item i
                  JOIN sap_pedido_compra p
                    ON p.codigo_sap_pedido_compra = i.codigo_sap_pedido_compra
                 WHERE p.situacao_sap_pedido_compra = true
                   AND p.ativo_sap = true
                   AND i.situacao_sap_pedido_compra_item = true
                   AND i.ativo_sap = true
                   AND nullif(trim(i.numero_item), '') IS NOT NULL
                   AND nullif(trim(i.codigo_produto), '') IS NOT NULL
                   AND nullif(trim(i.centro), '') IS NOT NULL
                   AND nullif(trim(i.deposito), '') IS NOT NULL
                   AND nullif(trim(i.unidade_medida), '') IS NOT NULL
                 GROUP BY i.codigo_sap_pedido_compra
                HAVING count(*) >= 2);
            """;
        await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is bool existe && existe;
    }

    public static async Task ValidarEstruturaObrigatoriaAsync(FabricaConexaoEntradaLotesDev fabrica)
    {
        await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync();
        if (!await ExisteSchemaAsync(conexao, Schema))
        {
            throw new InvalidOperationException($"Schema {Schema} nao encontrado no banco DEV confirmado.");
        }

        string[] tabelas =
        [
            "usuario",
            "setor",
            "sap_pedido_compra",
            "sap_pedido_compra_item",
            "entrada_produto_lancamento",
            "entrada_produto_item",
            "entrada_produto_lote",
            "entrada_produto_pesagem"
        ];
        foreach (string tabela in tabelas)
        {
            if (!await ExisteTabelaAsync(conexao, tabela))
            {
                throw new InvalidOperationException($"Tabela obrigatoria {Schema}.{tabela} nao encontrada.");
            }
        }

        string[] colunasPesagem043 =
        [
            "codigo_entrada_produto_lote",
            "numero_lote_snapshot",
            "data_fabricacao_snapshot",
            "data_vencimento_snapshot"
        ];
        foreach (string coluna in colunasPesagem043)
        {
            if (!await ExisteColunaAsync(conexao, "entrada_produto_pesagem", coluna))
            {
                throw new InvalidOperationException($"Coluna 043 obrigatoria entrada_produto_pesagem.{coluna} nao encontrada.");
            }
        }
    }

    private static async Task<bool> ExisteSchemaAsync(NpgsqlConnection conexao, string schema)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema);";
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", schema);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is bool existe && existe;
    }

    private static async Task<bool> ExisteTabelaAsync(NpgsqlConnection conexao, string tabela)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                 WHERE table_schema = @schema AND table_name = @tabela);
            """;
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        comando.Parameters.AddWithValue("@tabela", tabela);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is bool existe && existe;
    }

    private static async Task<bool> ExisteColunaAsync(NpgsqlConnection conexao, string tabela, string coluna)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                 WHERE table_schema = @schema AND table_name = @tabela AND column_name = @coluna);
            """;
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@schema", Schema);
        comando.Parameters.AddWithValue("@tabela", tabela);
        comando.Parameters.AddWithValue("@coluna", coluna);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is bool existe && existe;
    }
}

internal sealed class FabricaConexaoEntradaLotesDev : IFabricaConexaoBanco
{
    private readonly string _connectionString;

    public FabricaConexaoEntradaLotesDev(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection CriarConexao()
        => new(_connectionString);

    public async Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection conexao = CriarConexao();
        await conexao.OpenAsync(cancellationToken);
        return conexao;
    }

    public string ObterConnectionString()
        => _connectionString;
}
