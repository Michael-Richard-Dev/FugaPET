namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Card no menu Processos de Produção, wiring do evento e requisitos visuais/funcionais da nova tela.
/// </summary>
public sealed class ControleApontamentosTelaTests
{
    // ---------- Card no menu ----------

    [Fact]
    public void ProcessoProducao_DeveTerCardControleApontamentosComF8()
    {
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");

        Assert.Contains("controleApontamentosCard", designer, StringComparison.Ordinal);
        Assert.Contains("apontamentosTitleLabel.Text = \"Controle de\\r\\nApontamentos\";", designer, StringComparison.Ordinal);
        Assert.Contains(
            "apontamentosDescriptionLabel.Text = \"Leitura e controle das\\r\\noperações da ordem de produção.\";",
            designer,
            StringComparison.Ordinal);
        Assert.Contains("apontamentosShortcutLabel.Text = \"F8\";", designer, StringComparison.Ordinal);
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
        Assert.Contains("if (e.KeyCode == Keys.F8 && _currentContentView == _processoProducaoForm)", painel, StringComparison.Ordinal);

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
        Assert.Contains("ObterConfiguracaoOperacaoAsync", servico, StringComparison.Ordinal);
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

    [Fact]
    public void Pacote039_DeveExistirENaoUsarNumeroJaOcupado()
    {
        string raiz = RaizProjeto();
        string pacote = Path.Combine(raiz, "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA");

        Assert.True(Directory.Exists(pacote), "Pacote 039 não encontrado.");
        foreach (string arquivo in new[]
                 {
                     "039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql",
                     "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql",
                     "039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql",
                     "039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql",
                     "README_039_CONTROLE_APONTAMENTOS_GAIA.txt"
                 })
        {
            Assert.True(File.Exists(Path.Combine(pacote, arquivo)), $"Arquivo ausente no pacote 039: {arquivo}");
        }

        string proposta = File.ReadAllText(Path.Combine(pacote, "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));
        // As três estruturas + restrições de concorrência + ausência de DELETE.
        Assert.Contains("operacao_producao_configuracao", proposta, StringComparison.Ordinal);
        Assert.Contains("operacao_producao_apontamento", proposta, StringComparison.Ordinal);
        Assert.Contains("operacao_producao_evento", proposta, StringComparison.Ordinal);
        Assert.Contains("uq_apontamento_ativo_por_operacao", proposta, StringComparison.Ordinal);
        Assert.Contains("uq_apontamento_idempotency_inicio", proposta, StringComparison.Ordinal);
        Assert.Contains("uq_apontamento_idempotency_termino", proposta, StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT DELETE", proposta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pacote039_PermissoesDevemUsarAsColunasReaisDaTabela()
    {
        string pacote = Path.Combine(
            RaizProjeto(), "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA");
        string proposta = File.ReadAllText(Path.Combine(pacote, "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));
        string rollback = File.ReadAllText(Path.Combine(pacote, "039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql"));

        // Colunas REAIS de desenvolvimento.permissao.
        foreach (string coluna in new[]
                 {
                     "modulo_permissao", "rotina_permissao", "acao_permissao",
                     "descricao_permissao", "situacao_permissao"
                 })
        {
            Assert.Contains(coluna, proposta, StringComparison.Ordinal);
        }

        // MVP: só as 3 ações com efeito funcional real (ver Pacote039_DeveCriarSomenteAsTresAcoes...).
        foreach (string acao in new[] { "VISUALIZAR", "INICIAR", "FINALIZAR" })
        {
            Assert.Contains($"('{acao}'", proposta, StringComparison.Ordinal);
        }

        Assert.Contains("desenvolvimento.perfil_permissao", proposta, StringComparison.Ordinal);
        Assert.Contains("nome_perfil_acesso = 'Administrador'", proposta, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", proposta, StringComparison.Ordinal); // idempotente

        // O rollback também precisa usar os nomes reais e limpar o vínculo antes (FK RESTRICT).
        Assert.Contains("modulo_permissao = 'PROCESSO_PRODUCAO'", rollback, StringComparison.Ordinal);
        Assert.Contains("rotina_permissao = 'CONTROLE_APONTAMENTOS'", rollback, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM desenvolvimento.perfil_permissao", rollback, StringComparison.Ordinal);
    }

    [Fact]
    public void Pacote039_DeveTerCentroTrabalhoNaChaveEOsCamposDoResultadoOperacional()
    {
        string pacote = Path.Combine(
            RaizProjeto(), "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA");
        string proposta = File.ReadAllText(Path.Combine(pacote, "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));
        string preflight = File.ReadAllText(Path.Combine(pacote, "039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql"));

        // Centro de trabalho na chave única da configuração.
        Assert.Contains(
            "(centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap, centro_trabalho)",
            proposta,
            StringComparison.Ordinal);

        // Campos do vínculo com o resultado operacional.
        foreach (string campo in new[]
                 {
                     "resultado_operacional", "codigo_registro_processo",
                     "concluido_operacional_em", "mensagem_resultado_operacional"
                 })
        {
            Assert.Contains(campo, proposta, StringComparison.Ordinal);
            Assert.Contains(campo, preflight, StringComparison.Ordinal);
        }

        // Preflight valida auditoria da configuração.
        Assert.Contains("criado_por", preflight, StringComparison.Ordinal);
        Assert.Contains("alterado_em", preflight, StringComparison.Ordinal);
        Assert.Contains("alterado_por", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void Pacote039_CheckDeStatus_DeveConterSomenteEstadosPersistidos()
    {
        string proposta = File.ReadAllText(Path.Combine(
            RaizProjeto(), "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA",
            "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));

        // Só os estados com transição real são aceitos pelo CHECK.
        Assert.Contains(
            "CHECK (status IN ('EM_ANDAMENTO','AGUARDANDO_FINALIZACAO','CONCLUIDA','CANCELADA'))",
            proposta,
            StringComparison.Ordinal);
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
