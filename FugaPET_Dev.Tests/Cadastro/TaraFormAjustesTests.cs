using FugaPET_Dev.Tela.Cadastro;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Tara: parse do peso em KG (Ajuste 9) + verificação por source-scan dos ajustes de UX/segurança/repo
/// alinhados a Setor/Cargo/TipoTara.
/// </summary>
public sealed class TaraFormAjustesTests
{
    // ---- Ajuste 9: peso em KG, vírgula/ponto, sem virar zero silenciosamente ----

    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData("2,000", 2.0)]
    [InlineData("0,4", 0.4)]
    [InlineData(" 10.250 ", 10.25)]
    public void TryParsePesoKg_Valido(string texto, double esperado)
    {
        Assert.True(TaraForm.TryParsePesoKg(texto, out decimal peso));
        Assert.Equal((decimal)esperado, peso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("0,0")]
    [InlineData("-1")]
    [InlineData("-0,5")]
    public void TryParsePesoKg_InvalidoOuNaoPositivo_NaoViraZero(string? texto)
    {
        Assert.False(TaraForm.TryParsePesoKg(texto, out decimal peso));
        Assert.Equal(0m, peso); // out fica 0, mas o retorno false impede o envio (não "vira zero" silenciosamente)
    }

    // ---- Ajuste 7/11: repositório ----

    [Fact]
    public void Repositorio_ExisteNomeNoSetorTipo_GlobalSemFiltrarSituacao()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<bool> ExisteNomeNoSetorTipoAsync");
        Assert.Contains("upper(trim(nome_tara)) = upper(trim(@nome_tara))", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_tara = true", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_AtualizarNaoAtualizaSituacao()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<int> AtualizarAsync");
        Assert.DoesNotContain("situacao_tara = @situacao_tara", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("@situacao_tara", metodo, StringComparison.Ordinal);
        Assert.Contains("nome_tara = @nome_tara", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 1/2/3/4/5/6/10: form ----

    [Fact]
    public void Form_InicializaComPermissaoEAuditaAcessoDireto()
    {
        string form = LerForm();
        Assert.Contains("private async Task InicializarTelaAsync", form, StringComparison.Ordinal);
        Assert.Contains("AutorizacaoServico.PodeVisualizarRotina(", form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.Tara", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoDiretoNegadoSeguroAsync", form, StringComparison.Ordinal);
        Assert.Contains("Você não possui permissão para acessar esta rotina.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemOperacaoProtegidaEAtalhos()
    {
        string form = LerForm();
        Assert.Contains("private bool _operacaoEmAndamento", form, StringComparison.Ordinal);
        Assert.Contains("ExecutarOperacaoProtegidaAsync(", form, StringComparison.Ordinal);
        Assert.Contains("protected override bool ProcessCmdKey", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F5 when salvarButton.Visible && salvarButton.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F6 when novoButton.Visible && novoButton.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F8 when excluirButton.Visible && excluirButton.Enabled", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemModoCardEBotaoStatusCentralizado()
    {
        string form = LerForm();
        Assert.Contains("enum ModoCard", form, StringComparison.Ordinal);
        Assert.Contains("? ExcluirTaraAsync() : ReativarTaraAsync()", form, StringComparison.Ordinal);
        string botoes = ExtrairMetodo(form, "private void AtualizarBotoesAcao");
        Assert.Contains("\"Inativar Tara", botoes, StringComparison.Ordinal);
        Assert.Contains("\"Reativar Tara", botoes, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(229, 27, 43)", botoes, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(22, 163, 74)", botoes, StringComparison.Ordinal);
        Assert.Contains("bool habilitar = !_operacaoEmAndamento", botoes, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_UsaHelperSituacaoEDropDownListSemConversaoSilenciosa()
    {
        string form = LerForm();
        Assert.Contains("SituacaoCadastroHelper.TryInterpretarSituacao(situacaoComboBox.Text", form, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList", form, StringComparison.Ordinal);
        Assert.Contains("CmbTipoTara.DropDownStyle = ComboBoxStyle.DropDownList", form, StringComparison.Ordinal);
        Assert.Contains("CmbSetor.DropDownStyle = ComboBoxStyle.DropDownList", form, StringComparison.Ordinal);
        // não converte situação por string.Equals(...,"Ativo") silenciosamente.
        Assert.DoesNotContain("string.Equals(situacaoComboBox.Text, \"Ativo\"", form, StringComparison.Ordinal);
    }

    // ---- Ajuste 1: ConfigurarCard real (habilita/desabilita + limpa no Vazio) ----

    [Fact]
    public void Form_ConfigurarCard_HabilitaCamposELimpaNoVazio()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private void ConfigurarCard");
        Assert.Contains("nomePerfilTextBox.Enabled = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("CmbTipoTara.Enabled = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("CmbSetor.Enabled = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("TxtPeso.Enabled = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.Enabled = novo", metodo, StringComparison.Ordinal);
        Assert.Contains("LimparCamposCard()", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 2: filtro que oculta a linha selecionada limpa estado (card Vazio) ----

    [Fact]
    public void Form_FiltroSemSelecaoVisivel_VaiParaVazio()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private void RestaurarSelecaoAposFiltro");
        Assert.Contains("_idTaraAtual = 0", metodo, StringComparison.Ordinal);
        Assert.Contains("ClearRowSelection()", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 4: peso >3 casas bloqueado na UI ----

    [Fact]
    public void Form_PesoUiValido_BloqueiaMaisDe3Casas()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private bool PesoUiValido");
        Assert.Contains("Math.Round(pesoKg, 3)", metodo, StringComparison.Ordinal);
        Assert.Contains("Peso da tara deve ter no máximo 3 casas decimais.", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 5: repositório ReativarAsync com NOT EXISTS de duplicidade global ----

    [Fact]
    public void Repositorio_ReativarTemNotExistsDuplicidadeGlobal()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<int> ReativarAsync");
        Assert.Contains("AND NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM tara outra", metodo, StringComparison.Ordinal);
        Assert.Contains("upper(trim(outra.nome_tara)) = upper(trim(t.nome_tara))", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 6: CarregarTarasAsync em erro limpa estado ----

    [Fact]
    public void Form_CarregarEmErro_LimpaEstadoVisual()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task CarregarTarasAsync");
        Assert.Contains("catch", metodo, StringComparison.Ordinal);
        Assert.Contains("_tarasCarregadas.Clear()", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
        Assert.Contains("AtualizarRodapePerfis(0)", metodo, StringComparison.Ordinal);
        Assert.Contains("TARA_CARREGAR_ERRO", metodo, StringComparison.Ordinal);
    }

    // ---- Refinamento UX (3): header sem SAP, estado vazio limpo, resumo lateral coerente ----

    [Fact]
    public void Designer_SubtituloSemIntegracaoSap()
    {
        string designer = LerDesigner();
        Assert.DoesNotContain("Integração SAP", designer, StringComparison.Ordinal);
        Assert.Contains("headerSubtitleLabel.Text = \"Cadastro e manutenção de tara\"", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_SemChipStatusSap()
    {
        string designer = LerDesigner();
        Assert.DoesNotContain("SAP: não configurado", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusPanel", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusLabel", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("sapStatusDotLabel", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_ResumoUsaPesoKgNaoSetoresVinculados()
    {
        string designer = LerDesigner();
        Assert.DoesNotContain("Setores vinculados", designer, StringComparison.Ordinal);
        Assert.Contains("summaryUsuariosCaptionLabel.Text = \"Peso (kg)\"", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_AbreEmModoCardVazio()
    {
        string form = LerForm();
        // Ctor aplica o estado vazio ao abrir a tela (nada de campos cinzas no carregamento).
        Assert.Contains("ConfigurarCard(ModoCard.Vazio);", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_ConfigurarCard_OcultaCamposNoVazioEExibeMensagem()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private void ConfigurarCard");
        // No modo Vazio os campos não ficam visíveis (visibilidade controlada por operacional/novo).
        Assert.Contains("nomePerfilLabel.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("nomePerfilInputPanel.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("situacaoLabel.Visible = novo", metodo, StringComparison.Ordinal);
        Assert.Contains("roundedPanel1.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("RdpTipoTara.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("RdpSetor.Visible = operacional", metodo, StringComparison.Ordinal);
        Assert.Contains("_lblEstadoVazio.Visible = modo == ModoCard.Vazio", metodo, StringComparison.Ordinal);
        Assert.Contains("LimparCamposCard()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemMensagemEstadoVazioAmigavel()
    {
        string form = LerForm();
        Assert.Contains("Selecione uma tara cadastrada ou clique em Nova Tara para iniciar.", form, StringComparison.Ordinal);
    }

    // ---- Refinamento UX (4): estado limpo padronizado pós-operação ----

    [Fact]
    public void Form_FinalizarOperacao_DeixaTelaLimpa()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task FinalizarOperacaoComTelaLimpaAsync");
        Assert.Contains("_idTaraAtual = 0", metodo, StringComparison.Ordinal);
        Assert.Contains("ClearRowSelection()", metodo, StringComparison.Ordinal);
        Assert.Contains("ClearSummarySelectionValues()", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", metodo, StringComparison.Ordinal);
        Assert.Contains("AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum)", metodo, StringComparison.Ordinal);
        Assert.Contains("await CarregarTarasAsync()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_AposSalvar_UsaTelaLimpaSemPrepareNew()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task SalvarTaraAsync");
        Assert.Contains("await FinalizarOperacaoComTelaLimpaAsync()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareNewTara()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_AposEditar_UsaTelaLimpa()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task EditarTaraAsync");
        Assert.Contains("await FinalizarOperacaoComTelaLimpaAsync()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_AposInativar_UsaTelaLimpaSemPrepareNew()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task ExcluirTaraAsync");
        Assert.Contains("await FinalizarOperacaoComTelaLimpaAsync()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareNewTara()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_AposReativar_UsaTelaLimpaSemPrepareNew()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(form, "private async Task ReativarTaraAsync");
        Assert.Contains("await FinalizarOperacaoComTelaLimpaAsync()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareNewTara()", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_BotaoNovaTara_ContinuaUsandoPrepareNew()
    {
        string form = LerForm();
        // "Nova Tara" é o único fluxo que entra em modo Novo.
        string metodo = ExtrairMetodo(form, "private void AttachNovoTaraClick");
        Assert.Contains("PrepareNewTara()", metodo, StringComparison.Ordinal);
    }

    private static string LerDesigner() => LerArquivo("Tela", "Cadastro", "TaraForm.Designer.cs");

    private static string LerForm() => LerArquivo("Tela", "Cadastro", "TaraForm.cs");

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int proximo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximo > inicio, $"Fim do método não encontrado: {assinatura}");
        return fonte[inicio..proximo];
    }

    private static string ExtrairMetodoRepo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int proximo = fonte.IndexOf("\n    public ", inicio + assinatura.Length, StringComparison.Ordinal);
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
