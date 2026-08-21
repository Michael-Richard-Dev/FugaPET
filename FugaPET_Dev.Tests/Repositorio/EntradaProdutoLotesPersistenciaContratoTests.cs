namespace FugaPET_Dev.Tests.Repositorio;

public sealed class EntradaProdutoLotesPersistenciaContratoTests
{
    [Fact]
    public void TesteEstatico_Repositorio_DevePreservarMetodoLegadoECriarNovoFluxoSeparado()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");

        Assert.Contains("Task<long> SalvarLancamentoAsync(EntradaProdutoLancamento lancamento", fonte);
        Assert.Contains("Task<ResultadoPersistenciaEntradaComLotes> RegistrarLancamentoComLotesAsync", fonte);
        Assert.Contains("EntradaProdutoArvoreLotesSnapshot.Criar(entrada)", fonte);
        Assert.Contains("ValidadorEntradaProdutoArvoreLotes.Validar(snapshot, contexto.DataReferencia);", fonte);

        string legado = ExtrairMetodo(fonte, "SalvarLancamentoAsync");
        Assert.DoesNotContain("RegistrarLancamentoComLotesAsync", legado, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Repositorio_NovoFluxoDeveUsarUmaTransacaoNaOrdemCorreta()
    {
        string metodo = ExtrairMetodo(LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"), "RegistrarLancamentoComLotesAsync");

        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", metodo);
        int lancamento = metodo.IndexOf("InserirLancamentoAsync", StringComparison.Ordinal);
        int item = metodo.IndexOf("InserirItemAsync", StringComparison.Ordinal);
        int lote = metodo.IndexOf("InserirLoteAsync", StringComparison.Ordinal);
        int pesagem = metodo.IndexOf("InserirPesagemComLoteAsync", StringComparison.Ordinal);
        int peso = metodo.IndexOf("ConsultarPesoConsolidadoLoteAsync", StringComparison.Ordinal);
        int total = metodo.IndexOf("AtualizarTotalRecebidoAsync", StringComparison.Ordinal);
        int resultado = metodo.IndexOf("ResultadoPersistenciaEntradaComLotes.Criar", StringComparison.Ordinal);

        Assert.True(lancamento >= 0 && item > lancamento && lote > item && pesagem > lote && peso > pesagem && total > peso && resultado > total);
    }

    [Fact]
    public void TesteEstatico_Repositorio_InserirLoteDeveUsarFinalizadoConfirmadoEmELoginTextualSemAtualizarPeso()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        string metodo = ExtrairMetodo(fonte, "private async Task<long> InserirLoteAsync");

        Assert.Contains("INSERT INTO entrada_produto_lote", metodo);
        Assert.Contains("status_lote", metodo);
        Assert.Contains("'FINALIZADO_LOCAL'", metodo);
        Assert.Contains("confirmado_em", metodo);
        Assert.Contains("now()", metodo);
        Assert.Contains("criado_por", metodo);
        Assert.Contains("@criado_por", metodo);
        Assert.Contains("RETURNING codigo_entrada_produto_lote", metodo);
        Assert.DoesNotContain("peso_liquido_total_kg", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE entrada_produto_lote", fonte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET peso_liquido_total_kg", fonte, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TesteEstatico_Repositorio_InserirPesagemComLoteDeveDerivarSnapshotsDoLote()
    {
        string metodo = ExtrairMetodo(LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"), "private async Task<long> InserirPesagemComLoteAsync");

        Assert.Contains("codigo_entrada_produto_lote", metodo);
        Assert.Contains("numero_lote_snapshot", metodo);
        Assert.Contains("data_fabricacao_snapshot", metodo);
        Assert.Contains("data_vencimento_snapshot", metodo);
        Assert.Contains("RETURNING codigo_entrada_produto_pesagem", metodo);
        Assert.Contains("lote.Dados.NumeroLote", metodo);
        Assert.Contains("lote.Dados.DataFabricacao.Date", metodo);
        Assert.Contains("lote.Dados.DataVencimento.Date", metodo);
        Assert.DoesNotContain("pesagem.NumeroLoteSnapshot", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("pesagem.DataFabricacaoSnapshot", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("pesagem.DataVencimentoSnapshot", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Servico_DeveValidarSessaoPermissaoArvoreENaoChamarSapOuImpressao()
    {
        string fonteServico = LerFonte("Servicos", "Operacao", "EntradaProdutoServico.cs");
        string metodo = ExtrairMetodo(fonteServico, "RegistrarLancamentoComLotesAsync");
        string preparacao = ExtrairMetodo(fonteServico, "private async Task<(EntradaProdutoLancamentoComLotesPersistencia Snapshot, ContextoAuditoriaEntradaLotes Contexto)>");

        Assert.Contains("PrepararLancamentoComLotesAsync", metodo);
        Assert.Contains("CapturarContextoAuditoriaEntradaLotes", preparacao);
        Assert.Contains("AutorizacaoEntradaProdutoServico.PossuiPermissao", preparacao);
        Assert.Contains("ContextoAuditoriaEntradaLotes", preparacao);
        Assert.Contains("EntradaProdutoArvoreLotesSnapshot.Criar(entrada)", preparacao);
        Assert.Contains("ValidadorEntradaProdutoArvoreLotes.Validar(snapshot, contexto.DataReferencia)", preparacao);
        Assert.Contains("PermissoesSistema.Acoes.PesoManual", preparacao);
        Assert.Contains("CarregarTarasDoSetorAsync", preparacao);
        Assert.Contains("ValidarItemComLotesAsync", preparacao);
        Assert.Contains("SelectMany", preparacao);
        Assert.Contains("_repositorio.RegistrarLancamentoComLotesAsync(snapshot, contexto", metodo);
        Assert.DoesNotContain("Sap", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Impress", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zebra", metodo, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void TesteEstatico_Repositorio_FalhaDuranteLoteFicaDentroDaTransacaoAntesDoResultado()
    {
        string metodo = ExtrairMetodo(LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"), "RegistrarLancamentoComLotesAsync");

        int inicioTransacao = metodo.IndexOf("ExecutarEmTransacaoAuditavelAsync", StringComparison.Ordinal);
        int inserirLote = metodo.IndexOf("InserirLoteAsync", StringComparison.Ordinal);
        int resultado = metodo.IndexOf("ResultadoPersistenciaEntradaComLotes.Criar", StringComparison.Ordinal);

        Assert.True(inicioTransacao >= 0);
        Assert.True(inserirLote > inicioTransacao);
        Assert.True(resultado > inserirLote);
    }


    [Fact]
    public void TesteEstatico_Repositorio_NaoDeveMascararOrigemOuStatusInvalidosNoFluxoComLotes()
    {
        string metodo = ExtrairMetodo(LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"), "private async Task<long> InserirPesagemComLoteAsync");

        Assert.Contains("ParametroTexto(\"@origem\", pesagem.Origem)", metodo);
        Assert.Contains("ParametroTexto(\"@status\", pesagem.StatusPesagem)", metodo);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(pesagem.Origem) ? \"BALANCA\"", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(pesagem.StatusPesagem) ? \"VALIDA\"", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Servico_NovoFluxoReusaProtecoesLegadasAntesDaPersistencia()
    {
        string fonteServico = LerFonte("Servicos", "Operacao", "EntradaProdutoServico.cs");
        string metodo = ExtrairMetodo(fonteServico, "RegistrarLancamentoComLotesAsync");
        string preparacao = ExtrairMetodo(fonteServico, "private async Task<(EntradaProdutoLancamentoComLotesPersistencia Snapshot, ContextoAuditoriaEntradaLotes Contexto)>");
        int contexto = preparacao.IndexOf("CapturarContextoAuditoriaEntradaLotes", StringComparison.Ordinal);
        int snapshot = preparacao.IndexOf("EntradaProdutoArvoreLotesSnapshot.Criar", StringComparison.Ordinal);
        int primeiroAwait = preparacao.IndexOf("await", StringComparison.Ordinal);
        int validarItem = preparacao.IndexOf("ValidarItemComLotesAsync", StringComparison.Ordinal);
        int persistir = metodo.IndexOf("_repositorio.RegistrarLancamentoComLotesAsync", StringComparison.Ordinal);

        Assert.True(contexto >= 0 && snapshot > contexto && primeiroAwait > snapshot);
        Assert.True(validarItem >= 0);
        Assert.True(persistir >= 0);
        Assert.Contains("PermissoesSistema.Acoes.PesoManual", preparacao);
        Assert.Contains("ValidarSetorObrigatorio", preparacao);
        Assert.Contains("CarregarTarasDoSetorAsync", preparacao);
        Assert.Contains("SelectMany(lote => lote.Pesagens)", preparacao);
    }
    [Fact]
    public void TesteEstatico_CacheSap_ValidarItemAsyncDeveProjetarNoveCamposLidos()
    {
        string metodo = ExtrairMetodo(LerFonte("AcessoDados", "Repositorio", "PesagemEntradaItemRepositorio.cs"), "ValidarItemAsync");

        Assert.Contains("p.numero_pedido", metodo);
        Assert.Contains("i.numero_item", metodo);
        Assert.Contains("i.codigo_produto", metodo);
        Assert.Contains("i.centro", metodo);
        Assert.Contains("i.deposito", metodo);
        Assert.Contains("i.unidade_medida", metodo);
        Assert.Contains("leitor.GetString(3)", metodo);
        Assert.Contains("leitor.GetString(4)", metodo);
        Assert.Contains("leitor.GetString(5)", metodo);
        Assert.Contains("leitor.GetString(6)", metodo);
        Assert.Contains("leitor.GetString(7)", metodo);
        Assert.Contains("leitor.GetString(8)", metodo);

        int materialPresente = metodo.IndexOf("material_presente,", StringComparison.Ordinal);
        int numeroPedido = metodo.IndexOf("p.numero_pedido", StringComparison.Ordinal);
        int unidade = metodo.IndexOf("i.unidade_medida", StringComparison.Ordinal);
        int from = metodo.IndexOf("FROM sap_pedido_compra_item", StringComparison.Ordinal);

        Assert.True(materialPresente >= 0 && numeroPedido > materialPresente && unidade > numeroPedido && from > unidade);
    }

    [Fact]
    public void TesteEstatico_Servico_NovoFluxoDeveConectarAutorizacaoCentroDeposito()
    {
        string metodo = ExtrairMetodo(LerFonte("Servicos", "Operacao", "EntradaProdutoServico.cs"), "private async Task ValidarItemComLotesAsync");

        Assert.Contains("_autorizacaoCentroDeposito.ItemAutorizado", metodo);
        Assert.Contains("fora do centro/deposito autorizado", metodo);
        Assert.Contains("_itemRepositorio.ValidarItemAsync", metodo);
        Assert.True(
            metodo.IndexOf("_autorizacaoCentroDeposito.ItemAutorizado", StringComparison.Ordinal)
            < metodo.IndexOf("_itemRepositorio.ValidarItemAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void TesteEstatico_Servico_ValidarPesagemDeveConectarTaraEBalancaEstritas()
    {
        string metodo = ExtrairMetodo(LerFonte("Servicos", "Operacao", "EntradaProdutoServico.cs"), "private async Task ValidarPesagemAsync");

        Assert.Contains("ValidadorEntradaProdutoPersistenciaLotes.ValidarTaraDoSetor", metodo);
        Assert.Contains("ValidadorEntradaProdutoPersistenciaLotes.ValidarBalancaDoSetor", metodo);
        Assert.DoesNotContain("tarasDoSetor.Count > 0", metodo, StringComparison.Ordinal);
        Assert.Contains("_balancaRepositorio is not null", metodo);
        Assert.Contains("await NegarAsync(usuario, ex.Message", metodo);
    }

    [Fact]
    public void TesteEstatico_Arquitetura_ModeloEntradaNaoDeveDependerDeAcessoDados()
    {
        string raiz = LocalizarRaizProjeto();
        string modeloEntrada = Path.Combine(raiz, "Modelo", "Entrada");
        foreach (string arquivo in Directory.GetFiles(modeloEntrada, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string fonte = File.ReadAllText(arquivo);
            Assert.DoesNotContain("FugaPET_Dev.AcessoDados", fonte, StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Path.Combine(modeloEntrada, "ValidacaoItemPesagem.cs")));
        string repositorio = LerFonte("AcessoDados", "Repositorio", "PesagemEntradaItemRepositorio.cs");
        Assert.Contains("using FugaPET_Dev.Modelo.Entrada;", repositorio);
    }

    [Fact]
    public void TesteEstatico_RepositorioBase_DevePossuirOverloadAuditavelComUsuarioExplicito()
    {
        string fonte = LerFonte("AcessoDados", "Comum", "RepositorioBase.cs");

        Assert.Contains("protected async Task<T> ExecutarEmTransacaoAuditavelAsync<T>(", fonte);
        Assert.Contains("long codigoUsuario,", fonte);
        Assert.Contains("Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operacao", fonte);
    }

    [Fact]
    public void TesteEstatico_RepositorioBase_OverloadExplicitoDeveValidarUsuarioEDefinirAppUsuarioSemSessaoGlobal()
    {
        string metodo = ExtrairMetodo(
            LerFonte("AcessoDados", "Comum", "RepositorioBase.cs"),
            "long codigoUsuario,");

        Assert.Contains("if (codigoUsuario <= 0)", metodo);
        Assert.Contains("throw new ArgumentOutOfRangeException(nameof(codigoUsuario)", metodo);
        Assert.Contains("DefinirUsuarioAppAsync(conexao, transacao, codigoUsuario, cancellationToken)", metodo);
        Assert.Contains("CommitAsync(cancellationToken)", metodo);
        Assert.Contains("RollbackAsync(cancellationToken)", metodo);
        Assert.Contains("ExceptionDispatchInfo.Capture(ex)", metodo);
        Assert.DoesNotContain("EstadoSessaoUsuarioAtual", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("ParametroUsuarioApp()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Repositorio_NovoFluxoComLotesDeveUsarUsuarioDoContextoNaTransacaoAuditavel()
    {
        string metodo = ExtrairMetodo(
            LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"),
            "RegistrarLancamentoComLotesAsync");

        Assert.Contains("long? usuario = contexto.CodigoUsuario;", metodo);
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync(contexto.CodigoUsuario", metodo);
        Assert.DoesNotContain("ExecutarEmTransacaoAuditavelAsync(async", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Repositorio_SalvarLancamentoLegadoDeveContinuarUsandoTransacaoAuditavelLegada()
    {
        string metodo = ExtrairMetodo(
            LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"),
            "SalvarLancamentoAsync");

        Assert.Contains("long? usuario = ObterCodigoUsuarioSessao();", metodo);
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync(async", metodo);
        Assert.DoesNotContain("contexto.CodigoUsuario", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_Repositorio_NovoFluxoDeveUsarMesmoUsuarioNasColunasENoAppUsuarioId()
    {
        string fonte = LerFonte("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        string metodo = ExtrairMetodo(fonte, "RegistrarLancamentoComLotesAsync");

        Assert.Contains("long? usuario = contexto.CodigoUsuario;", metodo);
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync(contexto.CodigoUsuario", metodo);
        Assert.Contains("codigo_usuario", fonte);
        Assert.Contains("entrada_produto_lancamento_criado_por", fonte);
        Assert.Contains("entrada_produto_item_criado_por", fonte);
        Assert.Contains("entrada_produto_pesagem_criado_por", fonte);
        Assert.Contains("criado_por", fonte);
        Assert.Contains("@usuario", fonte);
    }

    [Fact]
    public void TesteEstatico_GuardIntegracaoGeral_NaoDeveReferenciarEntradaLotes()
    {
        string fonte = LerFonte("FugaPET_Dev.Tests", "Repositorio", "RepositorioIntegracaoBancoTests.cs");
        string atributo = ExtrairMetodo(fonte, "IntegrationFactAttribute()");

        Assert.Contains("BancoTesteIntegracao.DestrutivoAutorizado()", atributo);
        Assert.Contains("BancoTesteIntegracao.VariavelPermitirDestrutivo", atributo);
        Assert.DoesNotContain("BancoTesteEntradaLotesDev", atributo, StringComparison.Ordinal);
        Assert.DoesNotContain("OptInConfigurado", atributo, StringComparison.Ordinal);
    }

    [Fact]
    public void TesteEstatico_GuardEntradaLotes_DeveUsarAtributoExclusivoSomenteNosDoisTestesNovos()
    {
        string fonte = LerFonte("FugaPET_Dev.Tests", "Repositorio", "EntradaProdutoLotesIntegracaoBancoTests.cs");
        string atributo = ExtrairMetodo(fonte, "EntradaLotesIntegrationFactAttribute()");

        Assert.Contains("BancoTesteEntradaLotesDev.VariavelConnectionString", atributo);
        Assert.Contains("BancoTesteEntradaLotesDev.VariavelPermitirEntradaLotesReal", atributo);
        Assert.Contains("BancoTesteEntradaLotesDev.VariavelConfirmarDatabase", atributo);
        Assert.DoesNotContain("BancoTesteIntegracao", atributo, StringComparison.Ordinal);
        // 3 usam o atributo de opt-in puro; 1 (repeticao omitindo item) usa a variante que também exige um
        // pedido com dois itens no DEV, com skip REAL em tempo de descoberta.
        Assert.Equal(3, ContarOcorrencias(fonte, "[EntradaLotesIntegrationFact]"));
        Assert.Equal(1, ContarOcorrencias(fonte, "[EntradaLotesDoisItensIntegrationFact]"));
        Assert.DoesNotContain("[IntegrationFact]", fonte, StringComparison.Ordinal);
    }
    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { LocalizarRaizProjeto() }.Concat(partes).ToArray()));

    private static int ContarOcorrencias(string texto, string trecho)
    {
        int contador = 0;
        int indice = 0;
        while ((indice = texto.IndexOf(trecho, indice, StringComparison.Ordinal)) >= 0)
        {
            contador++;
            indice += trecho.Length;
        }

        return contador;
    }

    private static string LocalizarRaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            string candidatoProjeto = Path.Combine(diretorio, "FugaPET_Dev.csproj");
            if (File.Exists(candidatoProjeto))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada para teste de contrato.");
    }

    private static string ExtrairMetodo(string fonte, string nomeMetodo)
    {
        int inicio = fonte.IndexOf(nomeMetodo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Metodo {nomeMetodo} nao encontrado.");

        int abreChave = fonte.IndexOf('{', inicio);
        Assert.True(abreChave >= 0, $"Metodo {nomeMetodo} sem corpo localizado.");

        int profundidade = 0;
        for (int i = abreChave; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            if (fonte[i] == '}') profundidade--;
            if (profundidade == 0) return fonte[inicio..(i + 1)];
        }

        throw new InvalidOperationException($"Nao foi possivel extrair o corpo do metodo {nomeMetodo}.");
    }
}





