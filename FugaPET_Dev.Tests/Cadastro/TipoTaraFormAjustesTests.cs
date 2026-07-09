using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Tipo de Tara: helper puro de situação/referência (Ajuste 1/4) + verificação por source-scan
/// dos ajustes de UX/consistência na TipoTaraForm (combo DropDownList, validação de situação, filtro e nome).
/// </summary>
public sealed class TipoTaraFormAjustesTests
{
    // ---- Ajuste 1: interpretação explícita da situação ----

    [Theory]
    [InlineData("Ativo", true, true)]
    [InlineData("  ativo ", true, true)]
    [InlineData("Inativo", true, false)]
    [InlineData("INATIVO", true, false)]
    public void TryInterpretarSituacao_Valida(string texto, bool esperaValido, bool esperaAtivo)
    {
        bool valido = SituacaoCadastroHelper.TryInterpretarSituacao(texto, out bool ativo);
        Assert.Equal(esperaValido, valido);
        Assert.Equal(esperaAtivo, ativo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("qualquer")]
    public void TryInterpretarSituacao_InvalidaNaoViraInativoSilenciosamente(string? texto)
    {
        // Nunca converte texto vazio/inválido silenciosamente para Inativo: retorna false (tela bloqueia).
        Assert.False(SituacaoCadastroHelper.TryInterpretarSituacao(texto, out bool ativo));
        Assert.False(ativo);
    }

    // ---- Ajuste 4: rótulo de referência ----

    [Theory]
    [InlineData(0, "0 taras")]
    [InlineData(1, "1 tara")]
    [InlineData(5, "5 taras")]
    public void FormatarReferenciaTaras(int quantidade, string esperado)
        => Assert.Equal(esperado, SituacaoCadastroHelper.FormatarReferenciaTaras(quantidade));

    // ---- Ajuste 1: combo DropDownList (Designer) ----

    [Fact]
    public void Designer_ComboSituacao_DeveSerDropDownList()
    {
        string designer = LerArquivo("Tela", "Cadastro", "TipoTaraForm.Designer.cs");
        Assert.Contains("situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList", designer, StringComparison.Ordinal);
        Assert.Contains("new object[] { \"Ativo\", \"Inativo\" }", designer, StringComparison.Ordinal);
    }

    // ---- Ajuste 1/2/6: form ----

    [Fact]
    public void Form_ValidaSituacaoEUsaHelperSemConverterInvalidoParaInativo()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("SituacaoCadastroHelper.TryInterpretarSituacao(situacaoComboBox.Text", form, StringComparison.Ordinal);
        Assert.Contains("Selecione a situação (Ativo ou Inativo).", form, StringComparison.Ordinal);
        // não deve mais existir a conversão silenciosa antiga.
        Assert.DoesNotContain("string.Equals(situacaoComboBox.Text, \"Ativo\"", form, StringComparison.Ordinal);
        // novo cadastro inicia Ativo.
        Assert.Contains("situacaoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_CarregarComTratamentoDeErro()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("TIPO_TARA_CARREGAR_ERRO", form, StringComparison.Ordinal);
        Assert.Contains("ErroUsuarioHelper.TratarAsync", form, StringComparison.Ordinal);
        // Salvar/Editar/Excluir protegidos.
        Assert.Contains("TIPO_TARA_SALVAR_ERRO", form, StringComparison.Ordinal);
        Assert.Contains("TIPO_TARA_EDITAR_ERRO", form, StringComparison.Ordinal);
        Assert.Contains("TIPO_TARA_EXCLUIR_ERRO", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_FiltroNaoSelecionaTodasAsLinhas()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        // método bugado removido; filtro apenas restaura a seleção real.
        Assert.DoesNotContain("SetFilteredRowsSelected", form, StringComparison.Ordinal);
        Assert.Contains("RestaurarSelecaoAposFiltro()", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_ReferenciaUsaContagemDeTaras()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("ContarTarasAtivasPorTipoAsync", form, StringComparison.Ordinal);
        Assert.Contains("SituacaoCadastroHelper.FormatarReferenciaTaras", form, StringComparison.Ordinal);
        Assert.DoesNotContain("linha.UsersLabel.Text = \"-\"", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_NomeEditavelAposCadastro()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        string metodo = ExtrairMetodo(form, "private void PreencherCamposTipoTaraPorLinha");
        Assert.Contains("nomePerfilTextBox.ReadOnly = false", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("nomePerfilTextBox.ReadOnly = true", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 1: duplicidade GLOBAL no repositório (não filtra situação) ----

    [Fact]
    public void Repositorio_ExisteNome_DeveSerGlobalPorUpperTrim()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TipoTaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<bool> ExisteNomeAsync");

        Assert.Contains("upper(trim(nome_tipo_tara)) = upper(trim(@nome_tipo_tara))", metodo, StringComparison.Ordinal);
        // NÃO pode filtrar por situação (senão volta a permitir ativo + inativo com o mesmo nome).
        Assert.DoesNotContain("situacao_tipo_tara = true", metodo, StringComparison.Ordinal);
    }

    // ---- Ajuste 6: consulta de diagnóstico de duplicados ----

    [Fact]
    public void Repositorio_TemDiagnosticoDeDuplicados()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TipoTaraRepositorio.cs");
        Assert.Contains("ListarNomesDuplicadosAsync", repo, StringComparison.Ordinal);
        Assert.Contains("GROUP BY upper(trim(nome_tipo_tara))", repo, StringComparison.Ordinal);
        Assert.Contains("HAVING count(*) > 1", repo, StringComparison.Ordinal);
    }

    // ---- Ajuste 3/4: UI ----

    [Fact]
    public void Designer_TextBoxes_TemMaxLength()
    {
        string designer = LerArquivo("Tela", "Cadastro", "TipoTaraForm.Designer.cs");
        Assert.Contains("nomePerfilTextBox.MaxLength = 80", designer, StringComparison.Ordinal);
        Assert.Contains("descricaoTextBox.MaxLength = 255", designer, StringComparison.Ordinal);
    }

    // ---- Ajuste 2/3: botão alterna Inativar/Reativar; edição não altera situação ----

    [Fact]
    public void Form_BotaoStatusAlternaInativarReativar()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("AlternarSituacaoTipoTaraAsync", form, StringComparison.Ordinal);
        Assert.Contains("? ExcluirTipoTaraAsync() : ReativarTipoTaraAsync()", form, StringComparison.Ordinal);
        Assert.Contains("\"Reativar Tipo", form, StringComparison.Ordinal);
        Assert.Contains("\"Inativar Tipo", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_SituacaoNaoEditavelNoRegistroSelecionado()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        // A edição usa ConfigurarCard(Edicao), que oculta a situação e desabilita o combo (situação só via toggle).
        string preencher = ExtrairMetodo(form, "private void PreencherCamposTipoTaraPorLinha");
        Assert.Contains("ConfigurarCard(ModoCard.Edicao)", preencher, StringComparison.Ordinal);

        string configurar = ExtrairMetodo(form, "private void ConfigurarCard");
        Assert.Contains("situacaoInputPanel.Visible = novo", configurar, StringComparison.Ordinal);
        Assert.Contains("situacaoComboBox.Enabled = novo", configurar, StringComparison.Ordinal);
    }

    // ---- Alinhamento Setor/Cargo (Ajustes 2/3/4/5/6) ----

    [Fact]
    public void Form_InicializacaoValidaPermissaoEAuditaAcessoDireto()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("private async Task InicializarTelaAsync", form, StringComparison.Ordinal);
        Assert.Contains("AutorizacaoServico.PodeVisualizarRotina(", form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.TipoTara", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoDiretoNegadoSeguroAsync", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoNegadoAsync", form, StringComparison.Ordinal);
        Assert.Contains("Você não possui permissão para acessar esta rotina.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemProtecaoContraOperacaoDuplicada()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("private bool _operacaoEmAndamento", form, StringComparison.Ordinal);
        Assert.Contains("ExecutarOperacaoProtegidaAsync(", form, StringComparison.Ordinal);
        Assert.Contains("if (_operacaoEmAndamento)", form, StringComparison.Ordinal);
        // botões desabilitados durante a operação.
        string botoes = ExtrairMetodo(form, "private void AtualizarBotoesAcao");
        Assert.Contains("bool habilitar = !_operacaoEmAndamento", botoes, StringComparison.Ordinal);
        Assert.Contains("salvarButton.Enabled = habilitar", botoes, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemAtalhosF5F6F8SemBurlarPermissao()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("protected override bool ProcessCmdKey", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F5 when salvarButton.Visible && salvarButton.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F6 when BtnEditar.Visible && BtnEditar.Enabled", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F8 when excluirButton.Visible && excluirButton.Enabled", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_TemModoCardVazioNovoEdicao()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        Assert.Contains("enum ModoCard", form, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Vazio)", form, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Novo)", form, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCard(ModoCard.Edicao)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_CentralizaTextoECorDoBotaoStatusEmAtualizarBotoesAcao()
    {
        string form = LerArquivo("Tela", "Cadastro", "TipoTaraForm.cs");
        string botoes = ExtrairMetodo(form, "private void AtualizarBotoesAcao");
        Assert.Contains("\"Inativar Tipo", botoes, StringComparison.Ordinal);
        Assert.Contains("\"Reativar Tipo", botoes, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(229, 27, 43)", botoes, StringComparison.Ordinal); // vermelho inativar
        Assert.Contains("Color.FromArgb(22, 163, 74)", botoes, StringComparison.Ordinal); // verde reativar
        // não depende de texto setado em PreencherCamposTipoTaraPorLinha.
        string preencher = ExtrairMetodo(form, "private void PreencherCamposTipoTaraPorLinha");
        Assert.DoesNotContain("excluirButton.Text", preencher, StringComparison.Ordinal);
    }

    // ---- Ajuste 8/9/10: repositório ----

    [Fact]
    public void Repositorio_AtualizarNaoAtualizaSituacao()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TipoTaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<int> AtualizarAsync");
        Assert.DoesNotContain("situacao_tipo_tara = @situacao_tipo_tara", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("@situacao_tipo_tara", metodo, StringComparison.Ordinal);
        Assert.Contains("nome_tipo_tara = @nome_tipo_tara", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_ExcluirTemNotExistsContraTaraAtiva()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "TipoTaraRepositorio.cs");
        string metodo = ExtrairMetodoRepo(repo, "public virtual async Task<int> ExcluirAsync");
        Assert.Contains("AND NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM tara", metodo, StringComparison.Ordinal);
        Assert.Contains("situacao_tara = true", metodo, StringComparison.Ordinal);
    }

    private static string ExtrairMetodoRepo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int proximo = fonte.IndexOf("\n    public ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximo < 0)
        {
            proximo = fonte.Length;
        }

        return fonte[inicio..proximo];
    }

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
