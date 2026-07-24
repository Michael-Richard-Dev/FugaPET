namespace FugaPET_Dev.Tests.Tela;

public sealed class ZebraEntradaRobustezTests
{
    [Fact]
    public void EstadoTerminal_DevePermitirRecarregarSemReiniciar()
    {
        string conteudo = LerArquivo("Servicos", "Terminal", "EstadoTerminalLocalAtual.cs");

        Assert.Contains("public static ContextoTerminalLocal Recarregar()", conteudo, StringComparison.Ordinal);
        Assert.Contains("public static ContextoTerminalLocal ObterContextoAtualizado()", conteudo, StringComparison.Ordinal);
        Assert.Contains("lock (Sincronizacao)", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Lazy<ContextoTerminalLocal>", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void ImpressaoEntrada_DeveSerializarWarmupImpressaoEReimpressaoNoMesmoGate()
    {
        string conteudo = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");

        Assert.Contains("private static readonly SemaphoreSlim GateImpressao = new(1, 1)", conteudo, StringComparison.Ordinal);
        Assert.Contains("await GateImpressao.WaitAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("GateImpressao.Release()", conteudo, StringComparison.Ordinal);
        Assert.Contains("AquecerAsync()", conteudo, StringComparison.Ordinal);
        Assert.Contains("AquecerSeDisponivelAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("ImprimirEtiquetaMateriaPrimaAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("ReimprimirEtiquetaMateriaPrimaAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("await Task.Run(() => acao(impressora)", conteudo, StringComparison.Ordinal);
        Assert.Contains("TimeoutOperacao", conteudo, StringComparison.Ordinal);
        Assert.Contains("TentativasImpressao", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Impressao_DeveUsarContextoAtualizadoAntesDeResolverImpressora()
    {
        string conteudo = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");

        Assert.Contains("EstadoTerminalLocalAtual.ObterContextoAtualizado()", conteudo, StringComparison.Ordinal);
        Assert.Contains("DescreverImpressoraAtualAsync", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void RawPrinter_DeveEnviarZplEmBlocosEValidarEscritaParcial()
    {
        string conteudo = LerArquivo("Servicos", "ServicoImpressoraZebra.cs");

        Assert.Contains("private static extern int StartDocPrinter", conteudo, StringComparison.Ordinal);
        Assert.Contains("ResultadoEnvioZebra", conteudo, StringComparison.Ordinal);
        Assert.Contains("TryImpressoraPronta", conteudo, StringComparison.Ordinal);
        Assert.Contains("TryEnsurePrinterReady", conteudo, StringComparison.Ordinal);
        Assert.Contains("WritePrinterEmBlocos", conteudo, StringComparison.Ordinal);
        Assert.Contains("const int tamanhoBloco = 16 * 1024", conteudo, StringComparison.Ordinal);
        Assert.Contains("blocosEnviados++", conteudo, StringComparison.Ordinal);
        Assert.Contains("written != count", conteudo, StringComparison.Ordinal);
        Assert.Contains("Escrita parcial na impressora Zebra", conteudo, StringComparison.Ordinal);
        Assert.Contains("Windows", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderizacaoEtiquetaMateriaPrima_DeveNormalizarTextoGrafico()
    {
        string conteudo = LerArquivo("Servicos", "ServicoImpressoraZebra.cs");

        Assert.Contains("NormalizarBoundsTexto", conteudo, StringComparison.Ordinal);
        Assert.Contains("SanitizarTextoGrafico", conteudo, StringComparison.Ordinal);
        Assert.Contains("MathF.Max(0, bounds.X)", conteudo, StringComparison.Ordinal);
        Assert.Contains("graphics.VisibleClipBounds", conteudo, StringComparison.Ordinal);
        Assert.Contains("StringFormatFlags.LineLimit", conteudo, StringComparison.Ordinal);
        Assert.Contains("texto[..180]", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Impressao_DeveAuditarDiagnosticoFisicoSanitizadoPorTentativa()
    {
        string servico = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");
        string auditoria = LerArquivo("Servicos", "Auditoria", "AuditoriaServico.cs");

        Assert.Contains("RegistrarEventoOperacionalAsync", auditoria, StringComparison.Ordinal);
        Assert.Contains("IMPRESSAO_ZEBRA_DIAGNOSTICO", servico, StringComparison.Ordinal);
        Assert.Contains("Operacao=", servico, StringComparison.Ordinal);
        Assert.Contains("Bytes=", servico, StringComparison.Ordinal);
        Assert.Contains("Blocos=", servico, StringComparison.Ordinal);
        Assert.Contains("TempoMs=", servico, StringComparison.Ordinal);
        Assert.Contains("Tentativa=", servico, StringComparison.Ordinal);
        Assert.Contains("Win32=", servico, StringComparison.Ordinal);
        Assert.Contains("Resultado=", servico, StringComparison.Ordinal);
        Assert.Contains("SanitizarMensagemTecnica", servico, StringComparison.Ordinal);
        Assert.Contains("csrf|authorization|basic", servico, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Impressao_DeveSepararCategoriasDeFalhaDaZebra()
    {
        string zebra = LerArquivo("Servicos", "ServicoImpressoraZebra.cs");
        string operacional = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("CategoriaErroImpressaoZebra", zebra, StringComparison.Ordinal);
        Assert.Contains("ImpressoraNaoConfigurada", zebra, StringComparison.Ordinal);
        Assert.Contains("ImpressoraNaoInstalada", zebra, StringComparison.Ordinal);
        Assert.Contains("ImpressoraOfflinePausada", zebra, StringComparison.Ordinal);
        Assert.Contains("FalhaAbrirImpressora", zebra, StringComparison.Ordinal);
        Assert.Contains("FalhaIniciarDocumento", zebra, StringComparison.Ordinal);
        Assert.Contains("FalhaEnviarDados", zebra, StringComparison.Ordinal);
        Assert.Contains("EscritaParcial", zebra, StringComparison.Ordinal);
        Assert.Contains("UsuarioSemPermissao", zebra, StringComparison.Ordinal);
        Assert.Contains("ExigirPermissaoImpressaoAsync", operacional, StringComparison.Ordinal);
        Assert.Contains("ErroImpressaoZebraException", form, StringComparison.Ordinal);
    }

    [Fact]
    public void AberturaEntrada_DeveUsarWarmupSeguroSemBloquearTela()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string impressaoEntrada = LerArquivo("Servicos", "Operacao", "ImpressaoEntradaServico.cs");
        string impressora = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");

        Assert.Contains("AquecerSeImpressoraDisponivelAsync", form, StringComparison.Ordinal);
        Assert.Contains("AquecerSeImpressoraDisponivelAsync", impressaoEntrada, StringComparison.Ordinal);
        Assert.Contains("public async Task AquecerSeDisponivelAsync()", impressora, StringComparison.Ordinal);
        Assert.Contains("TryImpressoraPronta", impressora, StringComparison.Ordinal);
        Assert.Contains("return;", impressora, StringComparison.Ordinal);
        Assert.Contains("IMPRESSAO_ZEBRA_DIAGNOSTICO", impressora, StringComparison.Ordinal);
    }

    [Fact]
    public void CliqueImpressora_DeveValidarDisponibilidadeAntesDoRaw()
    {
        string impressora = LerArquivo("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");

        Assert.Contains("ValidarImpressoraProntaAntesDoRawAsync", impressora, StringComparison.Ordinal);
        Assert.Contains("_servicoImpressoraZebra.TryImpressoraPronta", impressora, StringComparison.Ordinal);
        Assert.Contains("throw new ErroOperacionalEsperadoException(erro.Message)", impressora, StringComparison.Ordinal);
        Assert.Contains("RegistrarDiagnosticoImpressaoAsync", impressora, StringComparison.Ordinal);
    }

    [Fact]
    public void FalhaAposLeitura_DeveManterPesoEExibirDiagnosticoUtil()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("Peso registrado, mas etiqueta", conteudo, StringComparison.Ordinal);
        Assert.Contains("sem permiss", conteudo, StringComparison.Ordinal);
        Assert.Contains("DescreverImpressoraAtualAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("Use a reimpress", conteudo, StringComparison.Ordinal);
        Assert.Contains("IMPRESSAO_ETIQUETA_AUTOMATICA_ERRO", conteudo, StringComparison.Ordinal);
        Assert.Contains("TentarImprimirEtiquetaAposLeituraAsync(label)", conteudo, StringComparison.Ordinal);
        Assert.Contains("=> TentarImprimirEtiquetaAutomaticaAsync(label, \"leitura de peso\")", conteudo, StringComparison.Ordinal);
        Assert.Contains("return false;", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void PesagemMultipla_ImprimePorPesagemIndividual_SemConsolidado()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        // Impressão automática genérica ainda existe (usada por pesagem individual).
        Assert.Contains("TentarImprimirEtiquetaAutomaticaAsync(", conteudo, StringComparison.Ordinal);
        // Regra definitiva: imprime por pesagem (peso líquido dela), não o total consolidado.
        Assert.Contains("ConstruirEtiquetaPorPesagem(", conteudo, StringComparison.Ordinal);
        Assert.Contains("Pesagens atualizadas. Total do item:", conteudo, StringComparison.Ordinal);
        // A expectativa antiga de etiqueta consolidada foi REMOVIDA.
        Assert.DoesNotContain("Peso bruto total {form.PesoTotalTexto} registrado no item {itemPedido}. Etiqueta enviada para", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirDadosEtiquetaMateriaPrima(linhaAlvo)", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void FluxoEntrada_NaoDeveVoltarParaPatchPurchaseOrder()
    {
        string controller = LerArquivo("Controle", "Processo", "EntradaProdutoController.cs");
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.DoesNotContain("AtualizarPesoItemSapAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", form, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
