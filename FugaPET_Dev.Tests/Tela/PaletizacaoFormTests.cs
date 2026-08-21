namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// INCREMENTAL 047: certifica a estrutura da nova PaletizacaoForm (fonte) — título/subtítulo, dois modos,
/// entradas de sequência/manual, grid com as colunas exigidas, totais, GRAVAR PALETE reutilizando os serviços
/// existentes, grid de paletes formados, e AUSÊNCIA de POST/INT012 no fluxo da tela.
/// </summary>
public sealed class PaletizacaoFormTests
{
    private static string Fonte() => LerProjeto("Tela", "Processo", "PaletizacaoForm.cs");

    [Fact]
    public void Form_TituloESubtitulo()
    {
        string f = Fonte();
        Assert.Contains("Text = \"Paletização por HU\";", f, StringComparison.Ordinal);
        Assert.Contains("Formação de paletes por HU de caixas", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-J: código de barras/HU é o elemento manual principal; Por Sequência revela os campos.
    public void Form_DoisModos_Sequencia_E_Manual()
    {
        string f = Fonte();
        Assert.Contains("Por Sequência", f, StringComparison.Ordinal);
        Assert.Contains("Seleção Manual", f, StringComparison.Ordinal);
        // sequência: material/produto + HU inicial/final + Carregar HUs
        Assert.Contains("Material/produto", f, StringComparison.Ordinal);
        Assert.Contains("HU inicial", f, StringComparison.Ordinal);
        Assert.Contains("HU final", f, StringComparison.Ordinal);
        Assert.Contains("Carregar HUs", f, StringComparison.Ordinal);
        // manual: código de barras é o elemento principal (Enter/bipagem adiciona a HU)
        Assert.Contains("Inserir por Código de Barras", f, StringComparison.Ordinal);
        Assert.Contains("Adicionar", f, StringComparison.Ordinal);
        Assert.Contains("código de barras", f, StringComparison.Ordinal);
        Assert.Contains("Keys.Enter", f, StringComparison.Ordinal); // barcode: Enter adiciona
    }

    [Fact] // GATE 047-BN: grade prioriza a HU — HU | Material/Produto | OP | Lote | Peso pesado. Sem Qtde/Peso Total legados.
    public void Form_Grid_ColunasHu_E_Acoes()
    {
        string f = Fonte();
        Assert.Contains("HUs SELECIONADAS", f, StringComparison.Ordinal);
        foreach (string coluna in new[] { "HU", "Material/Produto", "OP", "Lote", "Peso pesado" })
        {
            Assert.Contains($"\"{coluna}\"", f, StringComparison.Ordinal);
        }
        // colunas legadas removidas
        Assert.DoesNotContain("\"Qtde\"", f, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Peso Total\"", f, StringComparison.Ordinal);
        Assert.DoesNotContain("\"HU / Embalagem\"", f, StringComparison.Ordinal);
        // hidratação pela própria HU (material real + peso pesado persistido)
        Assert.Contains("c.HandlingUnitExternalId", f, StringComparison.Ordinal);
        Assert.Contains("c.Material", f, StringComparison.Ordinal);
        Assert.Contains("FormatarKg(c.PesoBrutoKg)", f, StringComparison.Ordinal);
        Assert.Contains("Excluir Item/HU", f, StringComparison.Ordinal);
        Assert.Contains("Limpar seleção", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-BN: RESUMO enxuto — Caixas selecionadas / Produtos / Peso pesado total. Sem pesos teóricos.
    public void Form_ResumoSelecao_Enxuto()
    {
        string f = Fonte();
        Assert.Contains("RESUMO DA SELEÇÃO", f, StringComparison.Ordinal);
        foreach (string rotulo in new[] { "Caixas selecionadas", "Produtos", "Peso pesado total" })
        {
            Assert.Contains($"\"{rotulo}\"", f, StringComparison.Ordinal);
        }
        Assert.Contains("_resumoPesoPesadoValor.Text", f, StringComparison.Ordinal);
        // rótulos de pesos teóricos removidos do resumo
        Assert.DoesNotContain("\"Peso bruto\"", f, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Peso líquido\"", f, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Tara\"", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-BN: blocos legados REMOVIDOS da tela; mantém Identificação (Lote principal das embalagens).
    public void Form_BlocosLegado_Removidos()
    {
        string f = Fonte();
        foreach (string removido in new[] { "QUANTIDADE PREVISTA", "QUANTIDADE REALIZADA", "CX por Pallet", "Pct. por CX", "Peso Líq. CX", "Peso Bruto CX", "Caixa / Und.", "Pacotes", "Descrição", "Estoque", "Embarcado" })
        {
            Assert.DoesNotContain($"\"{removido}\"", f, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("CardComTitulo(\"PRODUTO\"", f, StringComparison.Ordinal);
        Assert.DoesNotContain("CardComTitulo(\"STATUS\"", f, StringComparison.Ordinal);
        // GATE 047-BP: bloco IDENTIFICAÇÃO e o campo "Lote de Produção das Embalagens" REMOVIDOS (sem fonte autoritativa).
        Assert.DoesNotContain("IDENTIFICAÇÃO", f, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote de Produção das Embalagens", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-Z: GRAVAR PALETE continua SOMENTE local; o envio INT012 é o botão ENVIAR AO SAP (via Controller).
    public void Form_GravarPalete_ReutilizaServicos_SomenteLocal()
    {
        string f = Fonte();
        Assert.Contains("GRAVAR PALETE", f, StringComparison.Ordinal);
        // reutiliza o validador único + persistência local existentes
        Assert.Contains("_controller.MontarPaletePorSelecao(", f, StringComparison.Ordinal);
        Assert.Contains("_controller.CriarPaleteLocalPersistenteAsync(", f, StringComparison.Ordinal);
        // carga por HU reutiliza as leituras do controller
        Assert.Contains("_controller.ListarCaixasPorIntervaloHandlingUnitAsync(", f, StringComparison.Ordinal);
        Assert.Contains("_controller.ListarCaixasPorHandlingUnitsAsync(", f, StringComparison.Ordinal);

        // GRAVAR (GravarPaleteAsync) NÃO dispara INT012 — segue somente formação/persistência local.
        string gravar = Bloco(f, "private async Task GravarPaleteAsync()", "private async Task RecarregarPaletesFormadosAsync");
        Assert.DoesNotContain("EnviarPaleteInt012Async", gravar, StringComparison.Ordinal);

        // A Form NÃO acessa gateway/orquestrador/SAP direto nem faz POST; o envio passa SOMENTE pelo Controller.
        Assert.DoesNotContain(".PostAsync(", f, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_paleteInt012Orquestrador", f, StringComparison.Ordinal);
        Assert.DoesNotContain("IProdutoAcabadoPaleteInt012Gateway", f, StringComparison.Ordinal);
        Assert.DoesNotContain("_gateway.EnviarPaleteAsync", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-Z: botão ENVIAR AO SAP explícito, via Controller, com proteção P0 de duplo disparo e RASCUNHO-only.
    public void Form_EnviarAoSap_ExplicitoViaController_ComProtecoes()
    {
        string f = Fonte();
        Assert.Contains("ENVIAR AO SAP", f, StringComparison.Ordinal);
        Assert.Contains("_controller.EnviarPaleteInt012Async(palete)", f, StringComparison.Ordinal);
        // P0 reentrância: flag + desabilita o botão durante a operação.
        Assert.Contains("if (_enviandoPalete) { return; }", f, StringComparison.Ordinal);
        Assert.Contains("_enviandoPalete = true;", f, StringComparison.Ordinal);
        Assert.Contains("enviarSapButton.Enabled = false;", f, StringComparison.Ordinal);
        // RASCUNHO ingressa; demais estados não produzem novo POST normal.
        Assert.Contains("\"RASCUNHO\"", f, StringComparison.Ordinal);
        Assert.Contains("não elegível para novo envio normal", f, StringComparison.Ordinal);
        // Confirmado recarrega; presenter apresenta o desfecho (nunca inventar sucesso).
        Assert.Contains("EstadoPaleteInt012.Confirmado", f, StringComparison.Ordinal);
        Assert.Contains("RecarregarPaletesFormadosAsync(palete.NumeroOrdemDe())", f, StringComparison.Ordinal);
        Assert.Contains("ProdutoAcabadoPaleteEnvioPresenter.Construir(palete.CodigoPaleteLocal, resultado)", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-AB: PALETES FORMADOS é recarregado AO ABRIR (OnLoad) por terminal, via Controller; sem criar/INT012.
    public void Form_AoAbrir_RecarregaPaletesFormadosPorTerminal()
    {
        string f = Fonte();
        Assert.Contains("protected override async void OnLoad(", f, StringComparison.Ordinal);
        Assert.Contains("await CarregarPaletesFormadosDoTerminalAsync();", f, StringComparison.Ordinal);
        Assert.Contains("_controller.RecarregarPaletesLocaisPorTerminalAsync(", f, StringComparison.Ordinal);

        // o reload ao abrir NÃO cria palete nem dispara INT012 (somente leitura via Controller).
        string reload = Bloco(f, "private async Task CarregarPaletesFormadosDoTerminalAsync()", "private void AtualizarGridPaletesFormados");
        Assert.DoesNotContain("MontarPaletePorSelecao", reload, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarPaleteLocalPersistenteAsync", reload, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarPaleteInt012Async", reload, StringComparison.Ordinal);

        // APÓS ENVIO o reload continua previsto (047-Z preservado).
        Assert.Contains("RecarregarPaletesFormadosAsync(palete.NumeroOrdemDe())", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-BR: dois blocos verticais — ESQUERDO (Resumo + Paletes Formados) / DIREITO (HUs + Seleção + Material/Gravar).
    public void Form_LayoutDoisBlocos_BR()
    {
        string f = Fonte();
        // BLOCO ESQUERDO: RESUMO no topo + PALETES FORMADOS abaixo.
        string esquerdo = Bloco(f, "private Control ConstruirBlocoEsquerdo()", "private Control ConstruirBlocoResumoSelecao()");
        Assert.Contains("ConstruirBlocoResumoSelecao(), 0, 0", esquerdo, StringComparison.Ordinal);
        Assert.Contains("ConstruirPaletesFormados(), 0, 1", esquerdo, StringComparison.Ordinal);
        // BLOCO DIREITO: 3 cards alinhados (grid HUs, entrada, material/gravar).
        string direito = Bloco(f, "private Control ConstruirColunaOperacionalDireita()", "private Control ConstruirGridEmbalagem()");
        Assert.Contains("ConstruirGridEmbalagem(), 0, 0", direito, StringComparison.Ordinal);
        Assert.Contains("ConstruirEntradaOperacional(), 0, 1", direito, StringComparison.Ordinal);
        Assert.Contains("ConstruirBarraGravar(), 0, 2", direito, StringComparison.Ordinal);
        // PALETES FORMADOS deixou de ser faixa inferior full-width (sem span de coluna).
        Assert.DoesNotContain("SetColumnSpan(paletesFormados", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-CH: Material embalagem = PALLET01 automático e NÃO editável (contrato MVP congelado, centralizado).
    public void Form_MaterialEmbalagem_PALLET01_Fixo_NaoEditavel()
    {
        string f = Fonte();
        Assert.Contains("Material embalagem", f, StringComparison.Ordinal); // campo preservado
        // auto-preenchido pela regra central + read-only (sem magic string "PALLET01" na Form)
        Assert.Contains("Text = PackagingMaterialMvp.Permitido, ReadOnly = true, TabStop = false", f, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PALLET01\"", f, StringComparison.Ordinal); // regra centralizada, sem literal duplicado
    }

    private static string Bloco(string fonte, string inicio, string fim)
    {
        int i = fonte.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(i >= 0, $"Início não encontrado: {inicio}");
        int f = fonte.IndexOf(fim, i, StringComparison.Ordinal);
        Assert.True(f > i, $"Fim não encontrado: {fim}");
        return fonte[i..f];
    }

    [Fact]
    public void Form_PaletesFormados_ComColunas()
    {
        string f = Fonte();
        Assert.Contains("PALETES FORMADOS", f, StringComparison.Ordinal);
        // GATE 047-BN: grade simplificada; "Status" técnico vira "Situação SAP" (integração preservada).
        foreach (string coluna in new[] { "Palete local", "Qtd caixas", "Peso pesado", "Situação SAP", "HU SAP" })
        {
            Assert.Contains($"\"{coluna}\"", f, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("\"Peso líquido\"", f, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-V: diagnóstico sanitizado de PostgresException (SQLSTATE + MessageText), sem vazar segredo.
    public void Form_Diagnostico_PostgresException_Sanitizado()
    {
        string f = Fonte();
        Assert.Contains("DescreverFalha(\"Falha ao carregar HUs\", ex)", f, StringComparison.Ordinal);
        Assert.Contains("DescreverFalha(\"Falha ao adicionar HU\", ex)", f, StringComparison.Ordinal);
        Assert.Contains("\"PostgresException\"", f, StringComparison.Ordinal);
        Assert.Contains("\"SqlState\"", f, StringComparison.Ordinal);
        Assert.Contains("\"MessageText\"", f, StringComparison.Ordinal);
        // a mensagem exibida traz [SQLSTATE]: <texto>
        Assert.Contains("$\"{prefixo} [{sqlState}]: {mensagem}\"", f, StringComparison.Ordinal);
        // sanitização mascara segredos e dados de conexão
        foreach (string segredo in new[] { "authorization", "password", "senha", "host=", "connection string" })
        {
            Assert.Contains($"\"{segredo}\"", f, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Form_Sequencial_ExigeMaterialAntesDeBuscarIntervalo()
    {
        string f = Fonte();
        Assert.Contains("Informe o material para busca sequencial.", f, StringComparison.Ordinal);
        Assert.Contains("ListarCaixasPorIntervaloHandlingUnitAsync(huIni, huFim, material)", f, StringComparison.Ordinal);
        Assert.DoesNotContain("material.Length == 0 ? null : material", f, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelProducao_TemAcessoVisualPaletizacao()
    {
        string painel = LerProjeto("Tela", "ProcessoProducaoForm.cs");
        string shell = LerProjeto("Tela", "PainelInicialForm.cs");

        Assert.Contains("Paletização por\\r\\nHU", painel, StringComparison.Ordinal);
        Assert.Contains("Formação de paletes\\r\\npor HU de caixas", painel, StringComparison.Ordinal);
        Assert.Contains("AddPaletizacaoCard();", painel, StringComparison.Ordinal);
        Assert.True(painel.IndexOf("AddPaletizacaoCard();", StringComparison.Ordinal) < painel.IndexOf("ApplyProductionIcons();", StringComparison.Ordinal), "O card deve existir antes da aplicação dos ícones.");
        Assert.DoesNotContain("paletizacaoButton", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("AddPaletizacaoButton", painel, StringComparison.Ordinal);
        Assert.Contains("PaletizacaoRequested", painel, StringComparison.Ordinal);
        Assert.Contains("view.PaletizacaoRequested += async (_, _) => await OpenPaletizacaoAsync();", shell, StringComparison.Ordinal);
        Assert.Contains("Processo.PaletizacaoForm form = new();", shell, StringComparison.Ordinal);
    }
    [Fact]
    public void Form_AbreMaximizada_E_UsaIconePadrao()
    {
        string f = Fonte();

        Assert.Contains("WindowState = FormWindowState.Maximized;", f, StringComparison.Ordinal);
        Assert.Contains("IconeJanelaHelper.AplicarIconePadrao(this);", f, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_UsaShellPadrao_ComCabecalhoERodape()
    {        string f = Fonte();

        Assert.Contains("ShellProcessoPadraoHelper.Criar(", f, StringComparison.Ordinal);
        Assert.DoesNotContain("private Control ConstruirHeader()", f, StringComparison.Ordinal);
        Assert.DoesNotContain("private Control ConstruirRodapePadrao()", f, StringComparison.Ordinal);
        string shell = LerProjeto("Tela", "Comum", "ShellProcessoPadraoHelper.cs");
        Assert.Contains("companyLogoPictureBox", shell, StringComparison.Ordinal);
        Assert.Contains("fuga_2026_logo", shell, StringComparison.Ordinal);
        Assert.Contains("production_title_icon", shell, StringComparison.Ordinal);
        Assert.Contains("UsuarioLogadoUiHelper.ObterTextoUsuarioRodape()", shell, StringComparison.Ordinal);
        Assert.Contains("RodapeBancoHelper.ObterTextoBancoDados()", shell, StringComparison.Ordinal);
        Assert.Contains("Empresa:  FUGA COUROS S.A.", shell, StringComparison.Ordinal);
    }    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}











