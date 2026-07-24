namespace FugaPET_Dev.Tests.Cadastro;

public sealed class EtiquetaFormTecnicaTests
{
    private static readonly string Raiz = LocalizarRaizProjeto();
    private static readonly string Form = File.ReadAllText(Path.Combine(Raiz, "Tela", "Cadastro", "EtiquetaForm.cs"));
    private static readonly string TaraForm = File.ReadAllText(Path.Combine(Raiz, "Tela", "Cadastro", "TaraForm.cs"));
    private static readonly string ModeloEtiquetaForm = File.ReadAllText(Path.Combine(Raiz, "Tela", "Cadastro", "ModeloEtiquetaForm.cs"));
    private static readonly string Designer = File.ReadAllText(Path.Combine(Raiz, "Tela", "Cadastro", "EtiquetaForm.Designer.cs"));
    private static readonly string Repositorio = File.ReadAllText(Path.Combine(Raiz, "AcessoDados", "Repositorio", "EtiquetaRepositorio.cs"));

    [Fact]
    public void Tela_CarregaListaRealEUsaTagComIdReal()
    {
        Assert.Contains("await _etiquetaController.ListarAsync()", Form, StringComparison.Ordinal);
        Assert.Contains("linha.Tag = etiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("_idEtiquetaAtual = etiqueta.CodigoEtiqueta", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("Etiqueta Entrada MP", Designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Etiqueta Palete Produção", Designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_SalvarEditarESituacaoUsamFluxosSeparados()
    {
        Assert.Contains("private async Task SalvarEtiquetaAsync()", Form, StringComparison.Ordinal);
        Assert.Contains("_etiquetaController.InserirAsync(etiqueta)", Form, StringComparison.Ordinal);
        Assert.Contains("private async Task EditarEtiquetaAsync()", Form, StringComparison.Ordinal);
        Assert.Contains("_etiquetaController.AtualizarAsync(etiqueta)", Form, StringComparison.Ordinal);
        Assert.Contains("private async Task AlternarSituacaoEtiquetaAsync()", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareNewEtiqueta", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_CombosSaoDropDownListELimitesEstaoAlinhados()
    {
        Assert.Contains("tipoEtiquetaComboBox.DropDownStyle = ComboBoxStyle.DropDownList", Form, StringComparison.Ordinal);
        Assert.Contains("modeloEtiquetaComboBox.DropDownStyle = ComboBoxStyle.DropDownList", Form, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList", Form, StringComparison.Ordinal);
        Assert.Contains("codigoInternoTextBox.MaxLength = EtiquetaCadastro.TamanhoMaximoCodigoInterno", Form, StringComparison.Ordinal);
        Assert.Contains("descricaoEtiquetaTextBox.MaxLength = EtiquetaCadastro.TamanhoMaximoDescricao", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ValidaAcessoDiretoEProtegeOperacoesEAtalhos()
    {
        Assert.Contains("AutorizacaoServico.PodeVisualizarRotina", Form, StringComparison.Ordinal);
        Assert.Contains("AuditarAcessoDiretoNegadoSeguroAsync", Form, StringComparison.Ordinal);
        Assert.Contains("private bool _operacaoEmAndamento", Form, StringComparison.Ordinal);
        Assert.Contains("ExecutarOperacaoProtegidaAsync", Form, StringComparison.Ordinal);
        Assert.Contains("case Keys.F5 when salvarButton.Visible && salvarButton.Enabled", Form, StringComparison.Ordinal);
        Assert.Contains("case Keys.F6 when editarButton.Visible && editarButton.Enabled", Form, StringComparison.Ordinal);
        Assert.Contains("case Keys.F8 when situacaoButton.Visible && situacaoButton.Enabled", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_FiltroNaoSelecionaTodasAsLinhasEPreservaSelecaoValida()
    {
        Assert.Contains("NomeEtiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("CodigoInterno", Form, StringComparison.Ordinal);
        Assert.Contains("TipoEtiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("if (etiqueta.CodigoEtiqueta == idSelecionado) linhaSelecionada = linha", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("SetFilteredRowsSelected", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_HeaderResumoECamposNaoContemSemanticaAntigaOuSap()
    {
        Assert.Contains("Cadastro e manutenção de etiqueta", Designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Integração SAP", Designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SAP: não configurado", Designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Setores vinculados", Designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Altura Etiqueta", Designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Largura Etiqueta", Designer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CmbSetor", Designer, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtPeso", Designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_CamposDaEtiquetaExigeRegistroSelecionadoEPermissao()
    {
        Assert.Contains("_idEtiquetaAtual > 0", Form, StringComparison.Ordinal);
        Assert.Contains("&& _situacaoSelecionadaAtiva", Form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.CampoEtiqueta", Form, StringComparison.Ordinal);
        Assert.Contains("using CamposEtiquetaForm form = new(_idEtiquetaAtual)", Form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_CamposDaEtiquetaBloqueiaEtiquetaInativaComMensagemClara()
    {
        string metodo = ExtrairEntre(Form, "private void AbrirCamposEtiqueta()", "private void PreencherCampos");

        Assert.Contains("if (!_situacaoSelecionadaAtiva)", metodo, StringComparison.Ordinal);
        Assert.Contains("Reative a etiqueta antes de configurar seus campos.", metodo, StringComparison.Ordinal);
        Assert.Contains("using CamposEtiquetaForm form = new(_idEtiquetaAtual)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NovaEtiquetaExigeModeloAtivoAntesDoModoNovo()
    {
        string metodo = ExtrairEntre(Form, "private void PrepararNovaEtiqueta()", "private async Task SalvarEtiquetaAsync()");

        Assert.Contains("_modelosCarregados.Any(modelo => modelo.SituacaoModeloEtiqueta)", metodo, StringComparison.Ordinal);
        Assert.Contains("Cadastre ou reative um Modelo de Etiqueta antes de cadastrar uma Etiqueta.", metodo, StringComparison.Ordinal);
        Assert.Contains("AplicarModoCard(ModoCard.Novo)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NovoCadastroForcaAtivoEEdicaoPreservaSituacaoSelecionada()
    {
        string metodo = ExtrairEntre(Form, "private EtiquetaCadastro? CriarEtiquetaDaTela()", "private void AbrirCamposEtiqueta()");

        Assert.Contains("_modoCard == ModoCard.Novo", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoAtiva = true", metodo, StringComparison.Ordinal);
        Assert.Contains("_modoCard == ModoCard.Edicao", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoAtiva = _situacaoSelecionadaAtiva", metodo, StringComparison.Ordinal);
        Assert.Contains("SituacaoEtiqueta = situacaoAtiva", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DesignerMantemEstruturaVisualAprovada()
    {
        Assert.Contains("profilesCard", Designer, StringComparison.Ordinal);
        Assert.Contains("detailsCard", Designer, StringComparison.Ordinal);
        Assert.Contains("summaryCard", Designer, StringComparison.Ordinal);
        Assert.Contains("footerBar", Designer, StringComparison.Ordinal);
        Assert.DoesNotContain("camposEtiquetaButton", Designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ResumoNaoSobrepoeTipTextComDivisorOuBotoes()
    {
        string metodo = ExtrairEntre(Form, "private void LayoutSummaryCard()", "private static void ApplyScaledFont");

        Assert.Contains("summaryDividerLabel5.Bounds = new Rectangle(Scale(26, scaleX), Scale(286, scaleY)", metodo, StringComparison.Ordinal);
        Assert.Contains("int tipY = Scale(314, scaleY)", metodo, StringComparison.Ordinal);
        Assert.Contains("int salvarY = Scale(402, scaleY)", metodo, StringComparison.Ordinal);
        Assert.Contains("salvarY - tipY - Scale(16, scaleY)", metodo, StringComparison.Ordinal);
        Assert.Contains("tipTextLabel.Bounds = new Rectangle(textLeft, tipY", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("tipTextLabel.Bounds = new Rectangle(Scale(32, scaleX), Scale(244, scaleY)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ResumoUsaEscalaDeFonteEquivalenteATara()
    {
        string metodo = ExtrairEntre(Form, "private void LayoutSummaryCard()", "private static void ApplyScaledFont");
        string tara = ExtrairEntre(TaraForm, "private void LayoutSummaryCard()", "private void LayoutDetailsCard()");

        string[] controles =
        [
            "summaryTitleLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 9F, 12.5F), FontStyle.Bold)",
            "summaryPerfilCaptionLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F))",
            "summaryPerfilValueLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold)",
            "summarySituacaoCaptionLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F))",
            "summarySituacaoValueLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold)",
            "summaryUsuariosCaptionLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F))",
            "summaryUsuariosValueLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold)",
            "tipTextLabel.Font = new Font(\"Segoe UI\", Math.Clamp(9F * contentScale, 8.5F, 12F))"
        ];

        foreach (string controle in controles)
        {
            Assert.Contains(controle, metodo, StringComparison.Ordinal);
            Assert.Contains(controle, tara, StringComparison.Ordinal);
        }

        Assert.Contains("salvarButton.Font = new Font(\"Segoe UI\", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold)", metodo, StringComparison.Ordinal);
        Assert.Contains("editarButton.Font = new Font(\"Segoe UI\", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold)", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoButton.Font = new Font(\"Segoe UI\", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DetalhesPadronizaFontesComTara()
    {
        string metodo = ExtrairEntre(Form, "private void LayoutDetailsCard()", "private void PosicionarControlesAuxiliares()");

        Assert.Contains("ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(label1, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(LblPeso, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(LblSetorTara, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F)", metodo, StringComparison.Ordinal);
        Assert.Contains("ApplyScaledFont(_detailsEmptyLabel, 10F, contentScale, 9.5F, 13F)", metodo, StringComparison.Ordinal);
        Assert.Contains("camposEtiquetaButton.Font = new Font(\"Segoe UI\", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DetalhesCentralizaTextBoxEComboBoxComoModeloEtiqueta()
    {
        string metodo = ExtrairEntre(Form, "private void LayoutDetailsCard()", "private void PosicionarControlesAuxiliares()");
        string modelo = ExtrairEntre(ModeloEtiquetaForm, "private void LayoutDetailsCard()", "private void PosicionarCampoExtra");

        Assert.Contains("situacaoComboBox.IntegralHeight = false", metodo, StringComparison.Ordinal);
        Assert.Contains("int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight)", metodo, StringComparison.Ordinal);
        Assert.Contains("int situacaoComboY = Math.Max(2, (situacaoInputPanel.Height - situacaoComboHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("tipoEtiquetaComboBox.IntegralHeight = false", metodo, StringComparison.Ordinal);
        Assert.Contains("int tipoComboY = Math.Max(2, (roundedPanel1.Height - tipoComboHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("modeloEtiquetaComboBox.IntegralHeight = false", metodo, StringComparison.Ordinal);
        Assert.Contains("int modeloComboY = Math.Max(2, (RdpSetor.Height - modeloComboHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("int campoTextoY = Math.Max(2, (nomePerfilInputPanel.Height - campoTextoHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("int codigoY = Math.Max(2, (roundedPanel2.Height - codigoHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.Items.Add(SituacaoCadastroHelper.Ativo)", Form, StringComparison.Ordinal);
        Assert.DoesNotContain("situacaoComboBox.Items.AddRange([SituacaoCadastroHelper.Ativo, SituacaoCadastroHelper.Inativo])", Form, StringComparison.Ordinal);
        Assert.Contains("situacaoLabel.Visible = novo", Form, StringComparison.Ordinal);
        Assert.Contains("situacaoInputPanel.Visible = novo", Form, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.Enabled = novo", Form, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.IntegralHeight = false", modelo, StringComparison.Ordinal);
        Assert.Contains("Math.Max(2, (situacaoInputPanel.Height - sitH) / 2)", modelo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ListaPadronizaFontesDeCabecalhoPesquisaELinhas()
    {
        string profiles = ExtrairEntre(Form, "private void LayoutProfilesCard()", "private void AtualizarAreaLinhasDinamicas()");
        string linhas = ExtrairEntre(Form, "private void ReposicionarLinhasDinamicas()", "private void LayoutDetailsCard()");

        Assert.Contains("profilesSearchIconLabel.Font = new Font(\"Segoe MDL2 Assets\", Math.Clamp(11F * contentScale, 11F, 14F))", profiles, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderProfileLabel.Font = new Font(\"Segoe UI\", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold)", profiles, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderUsersLabel.Font = new Font(\"Segoe UI\", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold)", profiles, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderStatusLabel.Font = new Font(\"Segoe UI\", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold)", profiles, StringComparison.Ordinal);
        Assert.Contains("Math.Max(24, Scale(24, scaleY))", profiles, StringComparison.Ordinal);
        Assert.Contains("_estadoVazioListaLabel.Font = new Font(\"Segoe UI\", Math.Clamp(9F * contentScale, 8.5F, 12F))", profiles, StringComparison.Ordinal);
        Assert.Contains("int searchIconY = Math.Max(2, searchTextY + ((searchTextHeight - searchIconHeight) / 2))", profiles, StringComparison.Ordinal);
        Assert.Contains("int linhasTop = 44", Form, StringComparison.Ordinal);
        Assert.Contains("linha.NameLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold)", linhas, StringComparison.Ordinal);
        Assert.Contains("linha.TypeLabel.Font = new Font(\"Segoe UI\", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold)", linhas, StringComparison.Ordinal);
        Assert.Contains("linha.StatusLabel.Font = new Font(\"Segoe UI\", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold)", linhas, StringComparison.Ordinal);
        Assert.Contains("int quickW = Math.Max(118, Scale(118, scaleX))", profiles, StringComparison.Ordinal);
        Assert.Contains("int rowHeight = Math.Max(40, (int)Math.Round(46 * scaleY))", linhas, StringComparison.Ordinal);
        Assert.Contains("int rowLeft = Math.Max(0, (int)Math.Round(3 * scaleX))", linhas, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderProfileLabel.Left = rowLeft + nameX", linhas, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderUsersLabel.Left = rowLeft + typeX", linhas, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderStatusLabel.Left = rowLeft + statusX", linhas, StringComparison.Ordinal);
        Assert.Contains("linha.RowPanel.Bounds = new Rectangle(0, indice * rowHeight, largura, rowHeight)", linhas, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_AtualizarNaoAlteraSituacaoEDuplicidadeEhGlobal()
    {
        string atualizar = ExtrairEntre(Repositorio, "public virtual Task<int> AtualizarAsync", "public virtual Task<int> ReativarAsync");
        string duplicidade = ExtrairEntre(Repositorio, "public virtual async Task<bool> ExisteCodigoInternoAsync", "public virtual async Task<long> InserirAsync");
        Assert.DoesNotContain("situacao_etiqueta", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_etiqueta = true", duplicidade, StringComparison.Ordinal);
        Assert.Contains("upper(trim(codigo_interno)) = upper(trim(@codigo_interno))", duplicidade, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_InativacaoProtegeProdutoAtivoEReativacaoProtegeDuplicidade()
    {
        Assert.Contains("FROM produto_etiqueta pe", Repositorio, StringComparison.Ordinal);
        Assert.Contains("pe.situacao_produto_etiqueta = true", Repositorio, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", Repositorio, StringComparison.Ordinal);
        Assert.Contains("upper(trim(outra.codigo_interno)) = upper(trim(etiqueta.codigo_interno))", Repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("campo_etiqueta", Repositorio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_PosOperacaoVoltaAoModoVazio()
    {
        Assert.Contains("await CarregarEtiquetasAsync();\r\n        VoltarAoModoVazio();", Form.Replace("\n", "\r\n"), StringComparison.Ordinal);
        Assert.Contains("AplicarModoCard(ModoCard.Vazio)", Form, StringComparison.Ordinal);
        Assert.Contains("etiquetasDataGridView.ClearSelection()", Form, StringComparison.Ordinal);
        Assert.Contains("LimparResumo()", Form, StringComparison.Ordinal);
    }

    private static string ExtrairEntre(string fonte, string inicio, string fim)
    {
        int indiceInicio = fonte.IndexOf(inicio, StringComparison.Ordinal);
        int indiceFim = fonte.IndexOf(fim, indiceInicio + inicio.Length, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0 && indiceFim > indiceInicio);
        return fonte[indiceInicio..indiceFim];
    }

    private static string LocalizarRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            string projeto = Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj");
            if (File.Exists(projeto)) return diretorio.FullName;
            diretorio = diretorio.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
