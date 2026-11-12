namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Card no menu Processos de Produção, wiring do evento e requisitos visuais/funcionais da nova tela.
/// </summary>
public sealed class ControleApontamentosTelaTests
{
    // ---------- Card no menu ----------

    [Fact]
    public void ProcessoProducao_DeveTerCardControleApontamentosComF7()
    {
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");

        Assert.Contains("controleApontamentosCard", designer, StringComparison.Ordinal);
        Assert.Contains("apontamentosTitleLabel.Text = \"Controle de\\r\\nApontamentos\";", designer, StringComparison.Ordinal);
        Assert.Contains(
            "apontamentosDescriptionLabel.Text = \"Leitura e controle das\\r\\noperações da ordem de produção.\";",
            designer,
            StringComparison.Ordinal);
        Assert.Contains("apontamentosShortcutLabel.Text = \"F7\";", designer, StringComparison.Ordinal);
        // Card colocado no slot livre da grade de cards (mesmo tamanho dos demais).
        Assert.Contains("controleApontamentosCard.Location = new Point(796, 336);", designer, StringComparison.Ordinal);
        Assert.Contains("controleApontamentosCard.Size = new Size(240, 250);", designer, StringComparison.Ordinal);
        Assert.Contains("contentPanel.Controls.Add(controleApontamentosCard);", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessoProducao_DeveExporEventoEConectarTodosOsControlesDoCard()
    {
        string form = LerArquivoProjeto("Tela", "ProcessoProducaoForm.cs");

        Assert.Contains("public event EventHandler? ControleApontamentosRequested;", form, StringComparison.Ordinal);
        Assert.Contains("ControleApontamentosRequested?.Invoke(this, EventArgs.Empty);", form, StringComparison.Ordinal);

        // Todos os controles do card acionam o evento (mesmo padrão dos demais cards).
        foreach (string controle in new[]
                 {
                     "controleApontamentosCard", "apontamentosIconPanel", "apontamentosIconLabel",
                     "apontamentosTitleLabel", "apontamentosDescriptionLabel", "apontamentosStatusLabel",
                     "apontamentosShortcutLabel", "apontamentosArrowLabel"
                 })
        {
            Assert.Contains($"{controle}.Click += OnControleApontamentosClick;", form, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PainelInicial_DeveAssinarEventoAbrirTelaEValidarPermissao()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");

        Assert.Contains(
            "view.ControleApontamentosRequested += async (_, _) => await OpenControleApontamentosAsync();",
            painel,
            StringComparison.Ordinal);
        Assert.Contains("private async Task OpenControleApontamentosAsync()", painel, StringComparison.Ordinal);
        Assert.Contains("using Processo.ProcessoControleApontamentosForm form = new();", painel, StringComparison.Ordinal);
        // Permissão + padrão de esconder/restaurar painel.
        Assert.Contains("PermiteAbrirTelaAsync(", painel, StringComparison.Ordinal);
        Assert.Contains("if (e.KeyCode == Keys.F7 && _currentContentView == _processoProducaoForm)", painel, StringComparison.Ordinal);

        string abertura = ExtrairMetodo(painel, "private async Task OpenControleApontamentosAsync()");
        Assert.Contains("Hide();", abertura, StringComparison.Ordinal);
        Assert.Contains("form.ShowDialog(this);", abertura, StringComparison.Ordinal);
        Assert.Contains("Show();", abertura, StringComparison.Ordinal);
        Assert.Contains("NavigateToProcessoProducao();", abertura, StringComparison.Ordinal);
    }

    // ---------- Nova tela ----------

    [Fact]
    public void Tela_DeveExistirComTituloSubtituloEPadraoVisualFugaPet()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.Designer.cs");

        Assert.Contains("headerTitleLabel.Text = \"Controle de Apontamentos\";", designer, StringComparison.Ordinal);
        Assert.Contains(
            "headerSubtitleLabel.Text = \"Leitura, início e término das operações da ordem de produção\";",
            designer,
            StringComparison.Ordinal);

        // Padrão visual atual: cabeçalho escuro, cards claros arredondados, Segoe UI, status SAP, painel lateral.
        Assert.Contains("CorCabecalho = Color.FromArgb(17, 24, 39)", designer, StringComparison.Ordinal);
        Assert.Contains("new RoundedPanel()", designer, StringComparison.Ordinal);
        Assert.Contains("\"Segoe UI\"", designer, StringComparison.Ordinal);
        Assert.Contains("sapStatusPanel", designer, StringComparison.Ordinal);
        // Cabeçalho padrão das telas de Processo: barra de título custom (logo, ícone, min/max/fechar).
        Assert.Contains("customTitleBarPanel", designer, StringComparison.Ordinal);
        Assert.Contains("minimizeWindowLabel", designer, StringComparison.Ordinal);
        Assert.Contains("maximizeWindowLabel", designer, StringComparison.Ordinal);
        // Abre maximizada (tela cheia).
        Assert.Contains("WindowState = FormWindowState.Maximized;", designer, StringComparison.Ordinal);
        // Layout enxuto: sem o painel lateral pesado e sem a caixa "Situação da leitura"; faixa de destaque
        // da operação/processo atual mantida; a grade é o centro da tela.
        Assert.Contains("destaqueCard", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("lateralCard", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("statusCard = new RoundedPanel", designer, StringComparison.Ordinal);
        Assert.Contains("operacoesGridView", designer, StringComparison.Ordinal);

        // Não reproduzir o laranja do SISCOMP: nenhuma cor laranja nomeada é usada.
        Assert.DoesNotContain("Color.Orange", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.DarkOrange", designer, StringComparison.Ordinal);
        // A paleta é a do FugaPET (acento vermelho institucional).
        Assert.Contains("CorAcento = Color.FromArgb(229, 27, 43)", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveTerColunasDeOperacaoExigidas()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.Designer.cs");

        // Grade enxuta: apenas Seleção, Apontamento/Batida, Data início e Hora início.
        foreach (string cabecalho in new[]
                 {
                     "\"Seleção\"", "\"Apontamento / Batida\"", "\"Data início\"", "\"Hora início\""
                 })
        {
            Assert.Contains($"HeaderText = {cabecalho};", designer, StringComparison.Ordinal);
        }

        // Colunas antigas removidas.
        Assert.DoesNotContain("HeaderText = \"Tipo de processo\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("HeaderText = \"Tela de destino\";", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_CampoDeLeitura_DeveFocarProcessarNoEnterEBloquearLeituraConcorrente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");

        // Foco ao abrir e após cada processamento.
        Assert.Contains("Load += (_, _) => DevolverFocoParaLeitor();", form, StringComparison.Ordinal);
        Assert.Contains("codigoLeituraTextBox.Focus();", form, StringComparison.Ordinal);

        // Enter processa (leitor configurado como teclado).
        string keyDown = ExtrairMetodo(form, "private async void CodigoLeituraTextBox_KeyDown");
        Assert.Contains("Keys.Enter", keyDown, StringComparison.Ordinal);
        Assert.Contains("await ProcessarLeituraAsync(codigoLeituraTextBox.Text);", keyDown, StringComparison.Ordinal);

        // Bloqueia nova leitura enquanto processa e devolve o foco no finally.
        string processar = ExtrairMetodo(form, "private async Task ProcessarLeituraAsync(string codigoLido)");
        Assert.Contains("if (_processandoLeitura)", processar, StringComparison.Ordinal);
        Assert.Contains("_processandoLeitura = true;", processar, StringComparison.Ordinal);
        Assert.Contains("finally", processar, StringComparison.Ordinal);
        Assert.Contains("_processandoLeitura = false;", processar, StringComparison.Ordinal);
        Assert.Contains("DevolverFocoParaLeitor();", processar, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_UsuarioEEstacao_DevemSerAutomaticos()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string resolver = ExtrairMetodo(form, "private void ResolverUsuarioEEstacao()");

        // Usuário vem da sessão autenticada.
        Assert.Contains("EstadoSessaoUsuarioAtual.SessaoAtual", resolver, StringComparison.Ordinal);
        Assert.Contains("sessao.IdUsuario", resolver, StringComparison.Ordinal);
        Assert.Contains("sessao.Login", resolver, StringComparison.Ordinal);
        Assert.Contains("sessao.IdSetorPadrao", resolver, StringComparison.Ordinal);

        // Estação vem do terminal; MachineName é só fallback.
        Assert.Contains("EstadoTerminalLocalAtual.ObterContextoAtualizado()", resolver, StringComparison.Ordinal);
        Assert.Contains("Environment.MachineName", resolver, StringComparison.Ordinal);

        // Sem sessão: bloqueia a operação.
        Assert.Contains("codigoLeituraTextBox.Enabled = false;", resolver, StringComparison.Ordinal);
        Assert.Contains("MensagemSemSessao", resolver, StringComparison.Ordinal);

        // Nada de digitação manual de funcionário/máquina.
        Assert.DoesNotContain("matrícula", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Informe o funcionário", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_NaoDeveConsultarSapOuBancoDiretamente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");

        // A View passa SEMPRE pelo controller.
        Assert.Contains("_controller.ProcessarLeituraAsync(", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", form, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductionOrderSapApiClient", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Repositorio(", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DestinoNaoPodeSerDecididoPelaDescricaoDaOperacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        // Roteamento por TipoProcesso (dado da configuração), nunca por texto da operação.
        Assert.Contains("TipoProcessoOperacao.ConsumoMateriaPrima =>", form, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.ConsumoQuimicos =>", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Descricao.Contains", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Descricao.Contains", servico, StringComparison.Ordinal);
        // GATE 093D: a rota funcional é resolvida por Plant + WorkCenter (dado da configuração), nunca por
        // texto/descrição da operação nem pela operação como chave primária.
        Assert.Contains("ObterConfiguracaoRotaPorWorkCenterAsync", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Consumo_DevePreservarConstrutoresEAceitarContextoOpcional()
    {
        string consumo = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Construtores atuais preservados.
        Assert.Contains("public ProcessoConsumoMaterialForm()", consumo, StringComparison.Ordinal);
        Assert.Contains("public ProcessoConsumoMaterialForm(ModoConsumoMaterial modo)", consumo, StringComparison.Ordinal);
        // Sobrecarga opcional com contexto de apontamento.
        Assert.Contains(
            "public ProcessoConsumoMaterialForm(ModoConsumoMaterial modo, ContextoApontamentoProcesso contextoApontamento)",
            consumo,
            StringComparison.Ordinal);

        // Com contexto: OP travada e carregada automaticamente.
        string aplicar = ExtrairMetodo(consumo, "private void AplicarContextoApontamento()");
        Assert.Contains("if (_contextoApontamento is null)", aplicar, StringComparison.Ordinal);
        Assert.Contains("productionOrderComboBox.Enabled = false;", aplicar, StringComparison.Ordinal);
        Assert.Contains("ConsultarOrdemProducaoAsync(exibirAvisoOrdemObrigatoria: false)", aplicar, StringComparison.Ordinal);

        // Fechar não conclui: o padrão do resultado é NaoConcluido.
        Assert.Contains("ResultadoExecucaoProcesso ResultadoExecucaoApontamento", consumo, StringComparison.Ordinal);
        Assert.Contains("= ResultadoExecucaoProcesso.NaoConcluido;", consumo, StringComparison.Ordinal);
        // O vínculo com o lançamento do Consumo é o codigo_lancamento devolvido pela persistência.
        Assert.Contains("resultado.CodigoLancamento,", consumo, StringComparison.Ordinal);
        // Sem contexto, nada é registrado (fluxo manual intacto).
        string registrar = ExtrairMetodo(consumo, "private void RegistrarResultadoApontamento(");
        Assert.Contains("if (_contextoApontamento is not null)", registrar, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximoMetodo < 0)
        {
            proximoMetodo = fonte.Length;
        }

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
