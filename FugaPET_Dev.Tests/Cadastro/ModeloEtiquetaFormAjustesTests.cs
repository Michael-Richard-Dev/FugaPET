using FugaPET_Dev.Tela.Cadastro;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Modelo de Etiqueta: parse explícito da UI (versão/DPI/dimensões) + verificação por source-scan dos
/// ajustes de UX/segurança/estado alinhados a Tara/TipoTara.
/// </summary>
public sealed class ModeloEtiquetaFormAjustesTests
{
    // ---- parse explícito: nada vira valor silencioso ----

    [Theory]
    [InlineData("1", 1)]
    [InlineData("203", 203)]
    [InlineData(" 300 ", 300)]
    public void TryParseInteiroPositivo_Valido(string texto, int esperado)
    {
        Assert.True(ModeloEtiquetaForm.TryParseInteiroPositivo(texto, out int valor));
        Assert.Equal(esperado, valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void TryParseInteiroPositivo_InvalidoOuNaoPositivo(string? texto)
    {
        Assert.False(ModeloEtiquetaForm.TryParseInteiroPositivo(texto, out int valor));
        Assert.Equal(0, valor);
    }

    [Fact]
    public void TryParseDimensaoOpcional_VazioAceitaComoNulo()
    {
        Assert.True(ModeloEtiquetaForm.TryParseDimensaoOpcional("", out decimal? v1));
        Assert.Null(v1);
        Assert.True(ModeloEtiquetaForm.TryParseDimensaoOpcional("   ", out decimal? v2));
        Assert.Null(v2);
    }

    [Theory]
    [InlineData("10,5", 10.5)]
    [InlineData("10.5", 10.5)]
    [InlineData("100", 100)]
    public void TryParseDimensaoOpcional_ValidaAceitaVirgulaEPonto(string texto, double esperado)
    {
        Assert.True(ModeloEtiquetaForm.TryParseDimensaoOpcional(texto, out decimal? v));
        Assert.Equal((decimal)esperado, v);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    public void TryParseDimensaoOpcional_InvalidaOuNaoPositiva(string texto)
        => Assert.False(ModeloEtiquetaForm.TryParseDimensaoOpcional(texto, out _));

    // ---- source-scan: controle de acesso, operação protegida, atalhos, modo card ----

    [Fact]
    public void Form_UsaPodeVisualizarRotina()
    {
        string form = LerForm();
        Assert.Contains("AutorizacaoServico.PodeVisualizarRotina(", form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.ModeloEtiqueta", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoDiretoNegadoSeguroAsync", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemOperacaoProtegidaEAtalhos()
    {
        string form = LerForm();
        Assert.Contains("private bool _operacaoEmAndamento", form, StringComparison.Ordinal);
        Assert.Contains("ExecutarOperacaoProtegidaAsync(", form, StringComparison.Ordinal);
        Assert.Contains("protected override bool ProcessCmdKey", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F5 when salvarButton.Visible && salvarButton.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F6 when BtnEditar.Visible && BtnEditar.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F8 when excluirButton.Visible && excluirButton.Enabled", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemModoCardEBotaoStatusCentralizado()
    {
        string form = LerForm();
        Assert.Contains("enum ModoCard", form, StringComparison.Ordinal);
        Assert.Contains("? ExcluirModeloAsync() : ReativarModeloAsync()", form, StringComparison.Ordinal);
        Assert.Contains("\"Inativar Modelo", form, StringComparison.Ordinal);
        Assert.Contains("\"Reativar Modelo", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_SituacaoDropDownListSemConversaoSilenciosa()
    {
        string form = LerForm();
        Assert.Contains("situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList", form, StringComparison.Ordinal);
        // A situação não é interpretada de texto livre na criação: novo nasce Ativo por regra fixa.
        Assert.DoesNotContain("string.Equals(situacaoComboBox.Text, \"Ativo\"", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_ConfigurarCard_OcultaCamposNoVazioEExibeMensagem()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private void ConfigurarCard");
        Assert.Contains("nomePerfilLabel.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("descricaoInputPanel.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoLabel.Visible = novo", metodo, StringComparison.Ordinal);
        Assert.Contains("_lblEstadoVazio.Visible = modo == ModoCard.Vazio", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemMensagemEstadoVazioAmigavel()
    {
        string form = LerForm();
        Assert.Contains("Selecione um modelo cadastrado ou clique em Novo Modelo para iniciar.", form, StringComparison.Ordinal);
    }

    // ---- novo modelo nasce Ativo; situação nunca editável pelo card ----

    [Fact]
    public void Form_Situacao_LimpaEComApenasAtivo()
    {
        string form = LerForm();
        // Habilitada no Novo (aparência limpa/branca, igual às telas maduras) — não fica cinza/desabilitada.
        string configurar = ExtrairMetodo(form, "private void ConfigurarCard");
        Assert.Contains("situacaoComboBox.Enabled = novo", configurar, StringComparison.Ordinal);
        // A caixa oferece SOMENTE "Ativo" (sem opção de Inativo para o usuário selecionar).
        Assert.Contains("situacaoComboBox.Items.Clear()", form, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.Items.Add(SituacaoCadastroHelper.Ativo)", form, StringComparison.Ordinal);
        // Sem o guard antigo de snap-back (não existe mais dropdown mostrando Inativo).
        Assert.DoesNotContain("situacaoComboBox.SelectedIndexChanged", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_PrepareNew_SelecionaAtivo()
    {
        string metodo = ExtrairMetodo(LerForm(), "private void PrepareNewModelo");
        Assert.Contains("situacaoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_Salvar_MontaNovoModeloComoAtivo()
    {
        string metodo = ExtrairMetodo(LerForm(), "private async Task SalvarModeloAsync");
        Assert.Contains("modelo.SituacaoModeloEtiqueta = true", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_RotulosComAcentuacaoCorreta()
    {
        string form = LerForm();
        Assert.Contains("\"Versão *\"", form, StringComparison.Ordinal);
        Assert.Contains("\"DPI *\"", form, StringComparison.Ordinal);
        Assert.Contains("\"Conteúdo ZPL *\"", form, StringComparison.Ordinal);
        Assert.Contains("\"Observação\"", form, StringComparison.Ordinal);
    }

    // ---- após operações volta para estado vazio; Novo Modelo é o único caminho para PrepareNew ----

    [Fact]
    public void Form_FinalizarOperacao_DeixaTelaLimpa()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task FinalizarOperacaoComTelaLimpaAsync");
        Assert.Contains("_idModeloAtual = 0", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
        Assert.Contains("AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum)", metodo, StringComparison.Ordinal);
        Assert.Contains("await CarregarModelosAsync()", metodo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private async Task SalvarModeloAsync")]
    [InlineData("private async Task EditarModeloAsync")]
    [InlineData("private async Task ExcluirModeloAsync")]
    [InlineData("private async Task ReativarModeloAsync")]
    public void Form_AposOperacao_UsaTelaLimpa(string assinatura)
    {
        string metodo = ExtrairMetodo(LerForm(), assinatura);
        Assert.Contains("await FinalizarOperacaoComTelaLimpaAsync()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_BotaoNovoModelo_ContinuaUsandoPrepareNew()
    {
        string metodo = ExtrairMetodo(LerForm(), "private void AttachNovoModeloClick");
        Assert.Contains("PrepareNewModelo()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_FiltroSemSelecaoVisivel_VaiParaVazio()
    {
        string metodo = ExtrairMetodo(LerForm(), "private void RestaurarSelecaoAposFiltro");
        Assert.Contains("_idModeloAtual = 0", metodo, StringComparison.Ordinal);
        Assert.Contains("ClearRowSelection()", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_CarregarEmErro_LimpaEstadoVisual()
    {
        string metodo = ExtrairMetodo(LerForm(), "private async Task CarregarModelosAsync");
        Assert.Contains("catch", metodo, StringComparison.Ordinal);
        Assert.Contains("_modelosCarregados.Clear()", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
        Assert.Contains("MODELO_ETIQUETA_CARREGAR_ERRO", metodo, StringComparison.Ordinal);
    }

    // ---- sem SAP; coluna "Versão" no lugar de "Referência" ----

    [Fact]
    public void Designer_SemSapESubtituloCorreto()
    {
        string designer = LerDesigner();
        Assert.DoesNotContain("sapStatusPanel", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("SAP: não configurado", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Integração SAP", designer, StringComparison.Ordinal);
        Assert.Contains("headerSubtitleLabel.Text = \"Cadastro e manutenção de modelos de etiqueta (ZPL)\"", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_PlaceholderBuscaEhBuscarModelo()
    {
        string designer = LerDesigner();
        Assert.Contains("searchTextBox.PlaceholderText = \"Buscar modelo...\"", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Buscar setor...", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ColunaMostraVersaoNaoReferencia()
    {
        string designer = LerDesigner();
        string form = LerForm();
        Assert.DoesNotContain("Referência", designer, StringComparison.Ordinal);
        Assert.Contains("profilesHeaderUsersLabel.Text = \"Versão\"", form, StringComparison.Ordinal);
    }

    // ---- layout: linha separadora abaixo de todos os campos, sem sobrepor Observação, sem duplicar ----

    [Fact]
    public void Layout_PesquisaAlinhaIconeETextoComoEtiqueta()
    {
        string metodo = ExtrairMetodo(LerForm(), "private void LayoutProfilesCard");

        Assert.Contains("int searchIconWidth = Math.Max(20, Scale(22, scaleX))", metodo, StringComparison.Ordinal);
        Assert.Contains("int searchIconHeight = Math.Max(20, Scale(24, scaleY))", metodo, StringComparison.Ordinal);
        Assert.Contains("int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextHeight) / 2)", metodo, StringComparison.Ordinal);
        Assert.Contains("int searchIconY = Math.Max(2, searchTextY + ((searchTextHeight - searchIconHeight) / 2))", metodo, StringComparison.Ordinal);
        Assert.Contains("SetBounds(profilesSearchIconLabel, Scale(8, scaleX), searchIconY, searchIconWidth, searchIconHeight)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_LinhaSeparadoraAbaixoDeTodosOsCamposComMargem()
    {
        string metodo = ExtrairMetodo(LerForm(), "private void LayoutDetailsCard");
        // Divider posicionado pelo MAIOR fundo entre col1 (Observação) e col2 (Altura) + margem superior.
        Assert.Contains("_camposExtras[4].Painel.Bottom", metodo, StringComparison.Ordinal); // Observação
        Assert.Contains("_camposExtras[3].Painel.Bottom", metodo, StringComparison.Ordinal); // Altura
        Assert.Contains("Math.Max(fundoColuna1, fundoColuna2)", metodo, StringComparison.Ordinal);
        Assert.Contains("SetBounds(detailsTopDividerLabel, col1, dividerY", metodo, StringComparison.Ordinal);
        // Não usa mais o cálculo antigo que encostava na Observação.
        Assert.DoesNotContain("Math.Max(descricaoInputPanel.Bottom, linhaY + passo * 4) + Scale(40", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_SemLinhaSeparadoraDuplicada()
    {
        string form = LerForm();
        int ocorrencias = form.Split("SetBounds(detailsTopDividerLabel").Length - 1;
        Assert.Equal(1, ocorrencias); // uma única linha; sem controle runtime duplicado por cima
    }

    private static string LerForm() => LerArquivo("Tela", "Cadastro", "ModeloEtiquetaForm.cs");
    private static string LerDesigner() => LerArquivo("Tela", "Cadastro", "ModeloEtiquetaForm.Designer.cs");

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int proximo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximo < 0) proximo = fonte.Length;
        return fonte[inicio..proximo];
    }

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
