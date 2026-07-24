namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CamposEtiquetaFormTecnicaTests
{
    private static readonly string Form = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");
    private static readonly string Designer = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.Designer.cs");

    [Fact]
    public void Tela_ExigeEtiquetaPreSelecionadaEValidaAcessoDireto()
    {
        Assert.Contains("_etiquetaPreSelecionada <= 0", Form, StringComparison.Ordinal);
        Assert.Contains("Abra Campos da Etiqueta a partir de uma Etiqueta ativa selecionada.", Form, StringComparison.Ordinal);
        Assert.Contains("ValidarAcessoDiretoAsync", Form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.CampoEtiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoNegadoAsync", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NaoTrocaEtiquetaSilenciosamenteEBloqueiaInativa()
    {
        Assert.Contains("e.CodigoEtiqueta == _etiquetaPreSelecionada", Form, StringComparison.Ordinal);
        Assert.Contains("!etiqueta.SituacaoEtiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("A etiqueta selecionada não está mais ativa. Recarregue o Cadastro de Etiqueta.", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("_cmbEtiqueta", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("EtiquetaSelecionada", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PossuiModoOperacaoProtegidaEAtalhosCampo()
    {
        Assert.Contains("private enum ModoCampo", Form, StringComparison.Ordinal);
        Assert.Contains("private bool _operacaoEmAndamento;", Form, StringComparison.Ordinal);
        Assert.Contains("ExecutarOperacaoProtegidaAsync", Form, StringComparison.Ordinal);
        Assert.Contains("Keys.F5 && salvarCampoButton.Visible && salvarCampoButton.Enabled", Form, StringComparison.Ordinal);
        Assert.Contains("Keys.F6 && editarCampoButton.Visible && editarCampoButton.Enabled", Form, StringComparison.Ordinal);
        Assert.Contains("Keys.F8 && situacaoCampoButton.Visible && situacaoCampoButton.Enabled", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_BloqueiaSituacaoLivreEValidaEntradaNumerica()
    {
        Assert.Contains("situacaoCampoComboBox.Enabled = false", Form, StringComparison.Ordinal);
        Assert.Contains("SituacaoCampoEtiqueta = _modoCampo == ModoCampo.Novo || _campoAtualAtivo", Form, StringComparison.Ordinal);
        Assert.Contains("Informe uma ordem válida maior que zero.", Form, StringComparison.Ordinal);
        Assert.Contains("Informe um tamanho máximo válido maior que zero", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_UsaDesignerOficialFugaPetComTresCardsResponsivos()
    {
        Assert.Contains("public sealed partial class CamposEtiquetaForm", Form, StringComparison.Ordinal);
        Assert.Contains("InitializeComponent();", Form, StringComparison.Ordinal);
        Assert.Contains("private TableLayoutPanel rootLayout", Designer, StringComparison.Ordinal);
        Assert.Contains("private Panel headerBar", Designer, StringComparison.Ordinal);
        Assert.Contains("private TableLayoutPanel bodyLayout", Designer, StringComparison.Ordinal);
        Assert.Contains("private Panel footerBar", Designer, StringComparison.Ordinal);
        Assert.Contains("private RoundedPanel camposCard", Designer, StringComparison.Ordinal);
        Assert.Contains("private RoundedPanel dadosCampoCard", Designer, StringComparison.Ordinal);
        Assert.Contains("private RoundedPanel mapeamentoCard", Designer, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RaizProjeto(), "Tela", "Cadastro", "CamposEtiquetaForm.resx")));
        Assert.Contains("FormBorderStyle = FormBorderStyle.None", Designer, StringComparison.Ordinal);
        Assert.Contains("WindowState = FormWindowState.Maximized", Designer, StringComparison.Ordinal);
        Assert.Contains("bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F))", Designer, StringComparison.Ordinal);
        Assert.Contains("bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F))", Designer, StringComparison.Ordinal);
        Assert.Contains("card.Dock = DockStyle.Fill", Designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Fechar", Designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ListaCamposNaoUsaDataGridViewEFiltraSemAutoSelecionarPrimeiro()
    {
        Assert.DoesNotContain("DataGridView", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGridView", Designer, StringComparison.Ordinal);
        Assert.Contains("FlowLayoutPanel camposRowsPanel", Designer, StringComparison.Ordinal);
        Assert.Contains("RenderizarLinhasCampos", Form, StringComparison.Ordinal);
        Assert.Contains("CriarLinhaCampo", Form, StringComparison.Ordinal);
        Assert.Contains("CriarStatusBadge", Form, StringComparison.Ordinal);
        Assert.Contains("LimparSelecaoVisualCampos", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("Rows[0]", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_EstadosVisuaisOcultamEditorEMapeamentoQuandoNecessario()
    {
        Assert.Contains("dadosEditorPanel.Visible = editavel", Form, StringComparison.Ordinal);
        Assert.Contains("dadosVazioPanel.Visible = modo == ModoCampo.Vazio", Form, StringComparison.Ordinal);
        Assert.Contains("mapeamentoEditorPanel.Visible", Form, StringComparison.Ordinal);
        Assert.Contains("mapeamentoVazioPanel.Visible", Form, StringComparison.Ordinal);
        Assert.Contains("Reative o campo antes de alterar seu mapeamento.", Form, StringComparison.Ordinal);
        Assert.Contains("Inativar Mapeamento", Designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NaoMantemVisualProvisorioNemCoresSolidasDeAcao()
    {
        Assert.DoesNotContain("ConstruirUi", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderStyle.FixedSingle", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel leftCard", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb(37, 99, 235)", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("BackColor = Color.FromArgb(220, 38, 38)", Form, StringComparison.Ordinal);
        Assert.Contains("ConfigureActionButton", Designer, StringComparison.Ordinal);
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([RaizProjeto(), .. partes]));

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj"))) return diretorio;
            diretorio = Directory.GetParent(diretorio)?.FullName;
        }
        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}


