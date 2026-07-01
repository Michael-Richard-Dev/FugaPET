using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoConsumoMaterialModoTests
{
    [Fact]
    public void Configuracao_MateriaPrima_DeveAplicarTextosEsperados()
    {
        ConfiguracaoTelaConsumoMaterial configuracao = ConfiguracaoTelaConsumoMaterialFactory.Criar(ModoConsumoMaterial.MateriaPrima);

        Assert.Equal(ModoConsumoMaterial.MateriaPrima, configuracao.Modo);
        Assert.Equal("Consumo de Matéria-Prima", configuracao.TituloTela);
        Assert.Equal("Pesagem e consumo de componentes da ordem de produção", configuracao.SubtituloTela);
        Assert.Equal("CONSUMO_MATERIA_PRIMA", configuracao.TipoBalancaPreferencial);
        Assert.False(configuracao.UsarFiltroQuimicos);
    }

    [Fact]
    public void Configuracao_Quimico_DeveAplicarTextosEsperados()
    {
        ConfiguracaoTelaConsumoMaterial configuracao = ConfiguracaoTelaConsumoMaterialFactory.Criar(ModoConsumoMaterial.Quimico);

        Assert.Equal(ModoConsumoMaterial.Quimico, configuracao.Modo);
        Assert.Equal("Consumo de Químicos", configuracao.TituloTela);
        Assert.Equal("Pesagem e consumo de químicos da ordem de produção", configuracao.SubtituloTela);
        Assert.Equal("SAIDA_QUIMICOS", configuracao.TipoBalancaPreferencial);
        Assert.Equal("Balança de saída de químicos não configurada para esta operação.", configuracao.TextoSemBalancaConfigurada);
        Assert.True(configuracao.UsarFiltroQuimicos);
    }

    [Fact]
    public void Form_DeveManterConstrutorPadraoComoMateriaPrima()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("public ProcessoConsumoMaterialForm()", form, StringComparison.Ordinal);
        Assert.Contains(": this(ModoConsumoMaterial.MateriaPrima)", form, StringComparison.Ordinal);
        Assert.Contains("public ProcessoConsumoMaterialForm(ModoConsumoMaterial modo)", form, StringComparison.Ordinal);
        Assert.Contains("ConfiguracaoTelaConsumoMaterialFactory.Criar(modo)", form, StringComparison.Ordinal);
        Assert.Contains("AplicarConfiguracaoModoConsumo();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_DeveAplicarTituloESubtituloPorModo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AplicarConfiguracaoModoConsumo");

        Assert.Contains("Text = _configuracaoConsumo.TituloTela;", metodo, StringComparison.Ordinal);
        Assert.Contains("headerTitleLabel.Text = _configuracaoConsumo.TituloTela;", metodo, StringComparison.Ordinal);
        Assert.Contains("headerSubtitleLabel.Text = _configuracaoConsumo.SubtituloTela;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessoProducao_DeveExibirModulosMateriaPrimaEQuimicos()
    {
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "ProcessoProducaoForm.cs");

        Assert.Contains("processoConsumoMaterialCard", designer, StringComparison.Ordinal);
        Assert.Contains("Consumo de\\r\\nMatéria-Prima", designer, StringComparison.Ordinal);
        Assert.Contains("processoConsumoQuimicosCard", designer, StringComparison.Ordinal);
        Assert.Contains("Consumo de\\r\\nQuímicos", designer, StringComparison.Ordinal);
        Assert.Contains("ProcessoConsumoQuimicosRequested", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelInicial_DeveAbrirConsumoNoModoCorreto()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");

        Assert.Contains("ProcessoConsumoMaterialRequested += async (_, _) => await OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.MateriaPrima)", painel, StringComparison.Ordinal);
        Assert.Contains("ProcessoConsumoQuimicosRequested += async (_, _) => await OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico)", painel, StringComparison.Ordinal);
        Assert.Contains("Processo.ProcessoConsumoMaterialForm form = new(modo);", painel, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_NaoDeveTerComboParaTrocarTipoDeConsumo()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.DoesNotContain("modoConsumoComboBox", designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tipoConsumoComboBox", designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modoConsumoComboBox", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tipoConsumoComboBox", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Form_DeveManterBotaoUnicoEControlesTecnicosOcultos()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string atualizar = ExtrairMetodo(form, "private void AtualizarBotaoConfirmar");

        Assert.Contains("Text = \"Confirmar Consumo\"", form, StringComparison.Ordinal);
        Assert.Contains("_previewSap261Button.Visible = false", atualizar, StringComparison.Ordinal);
        Assert.Contains("_enviarSap261Button.Visible = false", atualizar, StringComparison.Ordinal);
        Assert.Contains("_previewConfirmacaoButton.Visible = false", atualizar, StringComparison.Ordinal);
        Assert.Contains("_enviarConfirmacaoButton.Visible = false", atualizar, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_Quimicos_DevePrepararBalancaSaidaQuimicosEFiltroSemDuplicarTela()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("ResolverBalancaSaidaQuimicosAsync", form, StringComparison.Ordinal);
        Assert.Contains("TipoBalancaPreferencial", form, StringComparison.Ordinal);
        Assert.Contains("FiltrarComponentesPorModo", form, StringComparison.Ordinal);
        Assert.Contains("ComponenteEhQuimico", form, StringComparison.Ordinal);
        Assert.Contains("MaterialGroup = QUIMICO/LQ/L003", form, StringComparison.Ordinal);
        Assert.DoesNotContain("class ProcessoConsumoQuimicosForm", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_Quimicos_DeveUsar261DiretoSemPreviewOuConfirmacaoBackflush()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string orquestrar = ExtrairMetodo(form, "private async Task<(string mensagem, MessageBoxIcon icone)> OrquestrarEnvioAposConfirmarAsync");

        int quimico = orquestrar.IndexOf("_modoConsumo == ModoConsumoMaterial.Quimico", StringComparison.Ordinal);
        int envio261 = orquestrar.IndexOf("ExecutarEnvioSap261AposConfirmarAsync", quimico, StringComparison.Ordinal);
        int backflush = orquestrar.IndexOf("RotaEnvioConsumo.BackflushConfirmacao", StringComparison.Ordinal);
        Assert.True(quimico >= 0 && envio261 > quimico);
        Assert.True(backflush > envio261);
        Assert.DoesNotContain("VisualizarPreviewConfirmacao", orquestrar, StringComparison.Ordinal);
    }

    [Fact]
    public void Ajuste_NaoDeveMexerEntrada101PatchOuSql()
    {
        string entrada = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string itemSap = LerArquivoProjeto("Modelo", "IntegracaoSap", "MaterialDocumentSapItemRequest.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("GoodsMovementRefDocType", itemSap, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", entrada, StringComparison.Ordinal);
        Assert.DoesNotContain("API_PURCHASEORDER_2", form, StringComparison.Ordinal);
        Assert.DoesNotContain(" PATCH ", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", form, StringComparison.OrdinalIgnoreCase);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do método não encontrado: {assinatura}");

        return fonte[inicio..proximoMetodo];
    }

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
