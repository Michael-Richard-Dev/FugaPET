using System.Globalization;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Tela.Comum;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// INCREMENTAL 047 — Paletização por HU de caixas. Tela NOVA (shell padrão FugaPET), separada do Produto Acabado.
/// GATE 047-J: a ANATOMIA INTERNA reproduz a memória espacial do SISCOMP legado — ESQUERDA larga (identificação /
/// produto / quantidades / status, horizontais e somente leitura) e DIREITA estreita (grid vertical das HUs /
/// EMBALAGEM + entrada operacional: código de barras/HU e Por Sequência). Reutiliza os serviços/orquestradores
/// existentes (ProdutoAcabadoController): monta o palete pelo validador ÚNICO e persiste via
/// CriarPaleteLocalPersistenteAsync. NÃO executa POST/SAP. "PALETES FORMADOS" é elemento FugaPET (não existe no
/// legado) preservado em faixa inferior. Campos legados sem origem no fluxo atual ficam read-only/vazios.
/// </summary>
public sealed class PaletizacaoForm : Form
{
    private static readonly Color CorFugaVermelho = Color.FromArgb(229, 27, 43);
    private static readonly Color CorHeaderEscuro = Color.FromArgb(17, 24, 39);
    private static readonly Color CorFundo = Color.FromArgb(245, 247, 250);
    private static readonly Color CorBorda = Color.FromArgb(226, 231, 238);
    private static readonly Color CorRotulo = Color.FromArgb(75, 85, 99);
    private static readonly Font FonteLabel = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteBotao = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteGridHeader = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteGridCell = new("Segoe UI", 9.5F, FontStyle.Regular);
    private static readonly Font FonteCampoLegado = new("Segoe UI", 9F, FontStyle.Regular);
    private static readonly Font FonteValorRealizada = new("Segoe UI", 10F, FontStyle.Bold);
    private static readonly Font FonteSecundaria = new("Segoe UI", 8F, FontStyle.Italic);

    private readonly ProdutoAcabadoController _controller;
    private readonly List<ProdutoAcabadoCaixa> _selecionadas = [];
    private readonly List<ProdutoAcabadoPalete> _paletesFormados = [];

    // Seletor de modo
    private RadioButton _modoSequenciaRadio = null!;
    private RadioButton _modoManualRadio = null!;
    private Panel _painelSequencia = null!;
    private Panel _painelManual = null!;

    // Sequência
    private TextBox materialSequenciaTextBox = null!;
    private TextBox huInicialTextBox = null!;
    private TextBox huFinalTextBox = null!;
    private Button carregarHusButton = null!;

    // Manual (código de barras / HU)
    private TextBox huManualTextBox = null!;
    private Button adicionarHuButton = null!;

    // Grid + ações
    private DataGridView caixasDataGridView = null!;
    private Button excluirHuButton = null!;
    private Button limparSelecaoButton = null!;

    // RESUMO DA SELEÇÃO (FugaPET — dados REAIS da seleção)
    private TextBox _resumoCaixasValor = null!;
    private TextBox _resumoProdutosValor = null!;
    private TextBox _resumoPesoPesadoValor = null!;

    // Gravar + paletes formados
    private TextBox materialEmbalagemTextBox = null!;
    private Button gravarPaleteButton = null!;
    private DataGridView paletesFormadosDataGridView = null!;
    private Button enviarSapButton = null!;
    private Label statusLabel = null!;

    // P0 (GATE 047-Z): reentrância — um clique lógico em ENVIAR AO SAP ⇒ no máximo UMA execução do orquestrador.
    private bool _enviandoPalete;

    public PaletizacaoForm()
        : this(new ProdutoAcabadoController())
    {
    }

    internal PaletizacaoForm(ProdutoAcabadoController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        ConstruirLayout();
        AtualizarVisibilidadeModo();
        AtualizarResumo();
        KeyPreview = true;
    }

    // GATE 047-AB: AO ABRIR a tela, recarrega PALETES FORMADOS persistidos do terminal (banco = fonte autoritativa;
    // inclui RASCUNHO). Não cria palete, não chama INT012/SAP. Preserva o reload após GRAVAR/ENVIAR (047-Z).
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await CarregarPaletesFormadosDoTerminalAsync();
    }

    private void ConstruirLayout()
    {
        Text = "Paletização por HU";
        BackColor = CorFundo;
        MinimumSize = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 9F);
        IconeJanelaHelper.AplicarIconePadrao(this);

        // GATE 047-BR: dois grandes blocos verticais equilibrados (divisor apenas conceitual, sem barra vermelha).
        //   ESQUERDA (~42%): RESUMO DA SELEÇÃO (topo) + PALETES FORMADOS (abaixo; grade estreita, sem invadir o centro).
        //   DIREITA  (~58%): HUs SELECIONADAS + SELEÇÃO (Manual/Sequência) + MATERIAL EMBALAGEM/GRAVAR — 3 cards alinhados.
        TableLayoutPanel conteudo = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16),
            BackColor = CorFundo
        };
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F)); // BLOCO ESQUERDO (Resumo + Paletes Formados)
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F)); // BLOCO DIREITO (HUs + Seleção + Material/Gravar)
        conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        conteudo.Controls.Add(ConstruirBlocoEsquerdo(), 0, 0);
        conteudo.Controls.Add(ConstruirColunaOperacionalDireita(), 1, 0);

        Controls.Add(ShellProcessoPadraoHelper.Criar(
            this,
            "Paletização por HU",
            "Formação de paletes por HU de caixas",
            conteudo));
    }

    // ---------------- BLOCO ESQUERDO: Resumo da Seleção (topo) + Paletes Formados (abaixo) ----------------

    private Control ConstruirBlocoEsquerdo()
    {
        TableLayoutPanel coluna = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = CorFundo,
            Padding = new Padding(0, 0, 12, 0)
        };
        coluna.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        coluna.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // RESUMO DA SELEÇÃO (topo)
        coluna.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // PALETES FORMADOS (abaixo, ocupa o restante)

        coluna.Controls.Add(ConstruirBlocoResumoSelecao(), 0, 0);
        coluna.Controls.Add(ConstruirPaletesFormados(), 0, 1);
        return coluna;
    }

    // §4 (047-BN): RESUMO DA SELEÇÃO enxuto — dados REAIS. "Peso pesado total" = soma do peso PESADO/persistido das HUs.
    private Control ConstruirBlocoResumoSelecao()
    {
        _resumoCaixasValor = CriarCampoValorRealizada();
        _resumoProdutosValor = CriarCampoValorRealizada();
        _resumoPesoPesadoValor = CriarCampoValorRealizada();
        return CardComTitulo("RESUMO DA SELEÇÃO", LinhaCampos(
            ("Caixas selecionadas", 34F, _resumoCaixasValor),
            ("Produtos", 22F, _resumoProdutosValor),
            ("Peso pesado total", 44F, _resumoPesoPesadoValor)));
    }

    // ---------------- lado direito: grid vertical + entrada operacional ----------------

    private Control ConstruirColunaOperacionalDireita()
    {
        TableLayoutPanel coluna = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = CorFundo
        };
        coluna.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        coluna.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // §8: grid vertical ocupa a maior parte
        coluna.RowStyles.Add(new RowStyle(SizeType.Absolute, 214F));  // §9/§10/§11: entrada operacional junto ao grid
        coluna.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));   // GRAVAR PALETE

        coluna.Controls.Add(ConstruirGridEmbalagem(), 0, 0);
        coluna.Controls.Add(ConstruirEntradaOperacional(), 0, 1);
        coluna.Controls.Add(ConstruirBarraGravar(), 0, 2);
        return coluna;
    }

    // §3 (047-BN): grade prioriza a informação da PRÓPRIA HU — HU | Material/Produto | OP | Lote | Peso pesado.
    // Sem coluna Qtde legada, sem Peso Total calculado. Lotes/materiais/OPs podem diferir (sem guard de unicidade).
    private Control ConstruirGridEmbalagem()
    {
        RoundedPanel card = new() { Dock = DockStyle.Fill, FillColor = Color.White, BorderColor = CorBorda, BorderRadius = 8, Padding = new Padding(10) };
        Label titulo = new() { Text = "HUs SELECIONADAS", Font = FonteLabel, ForeColor = CorFugaVermelho, Dock = DockStyle.Top, Height = 22 };

        caixasDataGridView = CriarGrid();
        foreach ((string nome, string tituloCol, int largura) in new[]
        {
            ("colHu", "HU", 150), ("colMaterial", "Material/Produto", 150),
            ("colOp", "OP", 100), ("colLote", "Lote", 110), ("colPesoPesado", "Peso pesado", 120)
        })
        {
            caixasDataGridView.Columns.Add(nome, tituloCol);
            caixasDataGridView.Columns[nome].Width = largura;
        }

        card.Controls.Add(caixasDataGridView);
        card.Controls.Add(titulo);
        return card;
    }

    // §9/§10/§11: entrada junto ao grid. Manual = "Inserir por Código de Barras" (elemento principal);
    // "Por Sequência" revela Material/produto + HU inicial/final + Carregar HUs. Ações Excluir Item/HU + Limpar.
    private Control ConstruirEntradaOperacional()
    {
        RoundedPanel card = new() { Dock = DockStyle.Fill, FillColor = Color.White, BorderColor = CorBorda, BorderRadius = 8, Padding = new Padding(12, 8, 12, 8) };

        TableLayoutPanel t = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White };
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));  // modos
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // manual XOR sequência
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));  // ações

        // Modos (compactos): Seleção Manual (código de barras) e Por Sequência.
        FlowLayoutPanel modos = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.White };
        _modoManualRadio = new RadioButton { Text = "Seleção Manual", Font = FonteLabel, AutoSize = true, Checked = true, Margin = new Padding(0, 2, 18, 0) };
        _modoSequenciaRadio = new RadioButton { Text = "Por Sequência", Font = FonteLabel, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        _modoManualRadio.CheckedChanged += (_, _) => AtualizarVisibilidadeModo();
        _modoSequenciaRadio.CheckedChanged += (_, _) => AtualizarVisibilidadeModo();
        modos.Controls.Add(_modoManualRadio);
        modos.Controls.Add(_modoSequenciaRadio);

        // Container onde manual e sequência se alternam (ocupam o mesmo espaço).
        Panel container = new() { Dock = DockStyle.Fill, BackColor = Color.White };
        container.Controls.Add(ConstruirPainelSequencia());
        container.Controls.Add(ConstruirPainelManual());

        // Ações próximas ao grid.
        FlowLayoutPanel acoes = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.White };
        excluirHuButton = CriarBotao("Excluir Item/HU", 0, 130, CorHeaderEscuro);
        excluirHuButton.Margin = new Padding(0, 4, 8, 0);
        excluirHuButton.Click += (_, _) => ExcluirHuSelecionada();
        limparSelecaoButton = CriarBotao("Limpar seleção", 0, 130, Color.FromArgb(107, 114, 128));
        limparSelecaoButton.Margin = new Padding(0, 4, 0, 0);
        limparSelecaoButton.Click += (_, _) => LimparSelecao();
        acoes.Controls.Add(excluirHuButton);
        acoes.Controls.Add(limparSelecaoButton);

        t.Controls.Add(modos, 0, 0);
        t.Controls.Add(container, 0, 1);
        t.Controls.Add(acoes, 0, 2);
        card.Controls.Add(t);
        return card;
    }

    private Panel ConstruirPainelManual()
    {
        _painelManual = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        Label cap = new() { Text = "Inserir por Código de Barras", Font = FonteCampoLegado, ForeColor = CorRotulo, Dock = DockStyle.Top, Height = 18 };

        TableLayoutPanel linha = new() { Dock = DockStyle.Top, Height = 34, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
        huManualTextBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11F), Margin = new Padding(0, 0, 8, 0) };
        huManualTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AdicionarManualAsync(); }
        };
        adicionarHuButton = CriarBotao("Adicionar", 0, 120, CorHeaderEscuro);
        adicionarHuButton.Dock = DockStyle.Fill;
        adicionarHuButton.Margin = new Padding(0);
        adicionarHuButton.Click += async (_, _) => await AdicionarManualAsync();
        linha.Controls.Add(huManualTextBox, 0, 0);
        linha.Controls.Add(adicionarHuButton, 1, 0);

        Label dica = new()
        {
            Text = "Bipagem/Enter: consulta, adiciona a HU e limpa o campo.",
            Font = FonteSecundaria,
            ForeColor = Color.FromArgb(107, 114, 128),
            Dock = DockStyle.Top,
            Height = 18
        };

        _painelManual.Controls.Add(dica);
        _painelManual.Controls.Add(linha);
        _painelManual.Controls.Add(cap);
        return _painelManual;
    }

    private Panel ConstruirPainelSequencia()
    {
        _painelSequencia = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };

        materialSequenciaTextBox = new TextBox { Font = FonteCampoLegado };
        huInicialTextBox = new TextBox { Font = FonteCampoLegado };
        huFinalTextBox = new TextBox { Font = FonteCampoLegado };

        Control campos = LinhaCampos(
            ("Material/produto", 40F, materialSequenciaTextBox),
            ("HU inicial", 30F, huInicialTextBox),
            ("HU final", 30F, huFinalTextBox));
        campos.Dock = DockStyle.Fill;

        FlowLayoutPanel acao = new() { Dock = DockStyle.Bottom, Height = 34, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.White };
        carregarHusButton = CriarBotao("Carregar HUs", 0, 140, CorFugaVermelho);
        carregarHusButton.Margin = new Padding(0, 2, 0, 0);
        carregarHusButton.Click += async (_, _) => await CarregarPorSequenciaAsync();
        acao.Controls.Add(carregarHusButton);

        _painelSequencia.Controls.Add(campos);
        _painelSequencia.Controls.Add(acao);
        return _painelSequencia;
    }

    // GRAVAR PALETE (destacado) + Material embalagem + status; sem quebrar a anatomia.
    private Control ConstruirBarraGravar()
    {
        RoundedPanel card = new() { Dock = DockStyle.Fill, FillColor = Color.White, BorderColor = CorBorda, BorderRadius = 8, Padding = new Padding(10, 6, 10, 6) };

        TableLayoutPanel linha = new() { Dock = DockStyle.Top, Height = 34, ColumnCount = 3, RowCount = 1, BackColor = Color.White };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        linha.Controls.Add(new Label { Text = "Material embalagem", Font = FonteLabel, ForeColor = CorHeaderEscuro, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        // GATE 047-CH: contrato MVP CONGELADO — material fixo (PackagingMaterialMvp.Permitido = PALLET01), auto-preenchido
        // e NÃO editável pelo operador (ReadOnly). Campo preservado no layout aprovado.
        materialEmbalagemTextBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 4, 8, 4), Text = PackagingMaterialMvp.Permitido, ReadOnly = true, TabStop = false, BackColor = Color.FromArgb(245, 247, 250) };
        linha.Controls.Add(materialEmbalagemTextBox, 1, 0);
        gravarPaleteButton = CriarBotao("GRAVAR PALETE", 0, 170, CorFugaVermelho);
        gravarPaleteButton.Dock = DockStyle.Fill;
        gravarPaleteButton.Margin = new Padding(0, 2, 0, 2);
        gravarPaleteButton.Click += async (_, _) => await GravarPaleteAsync();
        linha.Controls.Add(gravarPaleteButton, 2, 0);

        statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(55, 65, 81), Font = new Font("Segoe UI", 8.5F) };

        card.Controls.Add(statusLabel);
        card.Controls.Add(linha);
        return card;
    }

    // §12: PALETES FORMADOS — elemento FugaPET (não existe no legado). Preservado em faixa inferior full-width.
    private Control ConstruirPaletesFormados()
    {
        RoundedPanel card = new() { Dock = DockStyle.Fill, FillColor = Color.White, BorderColor = CorBorda, BorderRadius = 8, Padding = new Padding(10), Margin = new Padding(3, 8, 3, 3) };
        Label titulo = new() { Text = "PALETES FORMADOS", Font = FonteLabel, ForeColor = CorHeaderEscuro, Dock = DockStyle.Top, Height = 22 };
        paletesFormadosDataGridView = CriarGrid();
        // GATE 047-BR: grade estreita — só o necessário para as colunas (não atravessa o centro da tela).
        foreach ((string nome, string tituloCol, int largura) in new[]
        {
            ("pfLocal", "Palete local", 170), ("pfQtd", "Qtd caixas", 80), ("pfPesoPesado", "Peso pesado", 110),
            ("pfSituacao", "Situação SAP", 120), ("pfHuSap", "HU SAP", 130)
        })
        {
            paletesFormadosDataGridView.Columns.Add(nome, tituloCol);
            paletesFormadosDataGridView.Columns[nome].Width = largura;
        }

        // GATE 047-Z: ação EXPLÍCITA "ENVIAR AO SAP" para o palete selecionado (o operador depende de UI visível;
        // nada de comportamento oculto). GRAVAR PALETE continua SOMENTE local; o envio INT012 é este botão.
        Panel acoes = new() { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.White };
        enviarSapButton = CriarBotao("ENVIAR AO SAP", 0, 190, CorFugaVermelho);
        enviarSapButton.Location = new Point(0, 8);
        enviarSapButton.Height = 32;
        enviarSapButton.Click += async (_, _) => await EnviarPaleteSelecionadoAsync();
        acoes.Controls.Add(enviarSapButton);

        card.Controls.Add(paletesFormadosDataGridView);
        card.Controls.Add(acoes);
        card.Controls.Add(titulo);
        return card;
    }

    // ---------------- comportamento ----------------

    private void AtualizarVisibilidadeModo()
    {
        _painelSequencia.Visible = _modoSequenciaRadio.Checked;
        _painelManual.Visible = _modoManualRadio.Checked;
    }

    private async Task CarregarPorSequenciaAsync()
    {
        string material = materialSequenciaTextBox.Text.Trim();
        string huIni = huInicialTextBox.Text.Trim();
        string huFim = huFinalTextBox.Text.Trim();
        if (material.Length == 0)
        {
            statusLabel.Text = "Informe o material para busca sequencial.";
            materialSequenciaTextBox.Focus();
            return;
        }
        if (huIni.Length == 0 || huFim.Length == 0)
        {
            statusLabel.Text = "Informe HU inicial e HU final.";
            return;
        }

        try
        {
            IReadOnlyList<ProdutoAcabadoCaixa> caixas =
                await _controller.ListarCaixasPorIntervaloHandlingUnitAsync(huIni, huFim, material);
            int adicionadas = MesclarCaixas(caixas);
            statusLabel.Text = caixas.Count == 0
                ? "Nenhuma caixa encontrada no intervalo de HU informado."
                : $"{adicionadas} caixa(s) adicionada(s) ({caixas.Count} no intervalo).";
        }
        catch (Exception ex)
        {
            statusLabel.Text = DescreverFalha("Falha ao carregar HUs", ex);
        }
    }

    private async Task AdicionarManualAsync()
    {
        string hu = huManualTextBox.Text.Trim();
        if (hu.Length == 0)
        {
            statusLabel.Text = "Informe a HU da caixa.";
            return;
        }

        try
        {
            IReadOnlyList<ProdutoAcabadoCaixa> caixas = await _controller.ListarCaixasPorHandlingUnitsAsync([hu]);
            int adicionadas = MesclarCaixas(caixas);
            statusLabel.Text = caixas.Count == 0
                ? $"HU {hu} não encontrada."
                : adicionadas == 0 ? $"HU {hu} já estava na seleção." : $"HU {hu} adicionada.";
            huManualTextBox.Clear();
            huManualTextBox.Focus();
        }
        catch (Exception ex)
        {
            statusLabel.Text = DescreverFalha("Falha ao adicionar HU", ex);
        }
    }

    // GATE 047-V: diagnóstico sanitizado. Para PostgresException expõe SQLSTATE + MessageText (ex.: "[42703]: coluna
    // não encontrada"); nunca host/connection string/senha/credencial/Authorization. Sem acoplar a Tela ao Npgsql.
    private static string DescreverFalha(string prefixo, Exception ex)
    {
        Type tipo = ex.GetType();
        if (tipo.Name == "PostgresException")
        {
            string sqlState = tipo.GetProperty("SqlState")?.GetValue(ex) as string ?? "-";
            string mensagem = SanitizarMensagemErro(tipo.GetProperty("MessageText")?.GetValue(ex) as string);
            return $"{prefixo} [{sqlState}]: {mensagem}";
        }

        return $"{prefixo}: {tipo.Name}.";
    }

    private static string SanitizarMensagemErro(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return "erro não detalhado"; }
        string s = texto.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        foreach (string segredo in new[] { "authorization", "password", "senha", "credential", "cookie", "token", "basic ", "host=", "server=", "connection string", "user id", "pwd=" })
        {
            int idx = s.IndexOf(segredo, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) { s = s[..idx] + "***"; break; }
        }

        return s.Length <= 200 ? s : s[..200];
    }

    private int MesclarCaixas(IReadOnlyList<ProdutoAcabadoCaixa> caixas)
    {
        int adicionadas = 0;
        foreach (ProdutoAcabadoCaixa caixa in caixas)
        {
            bool jaExiste = _selecionadas.Any(c =>
                string.Equals(c.HandlingUnitExternalId, caixa.HandlingUnitExternalId, StringComparison.OrdinalIgnoreCase)
                && c.CodigoProdutoAcabadoCaixa == caixa.CodigoProdutoAcabadoCaixa);
            if (jaExiste) { continue; }
            _selecionadas.Add(caixa);
            adicionadas++;
        }
        AtualizarGridCaixas();
        AtualizarResumo();
        return adicionadas;
    }

    private void ExcluirHuSelecionada()
    {
        if (caixasDataGridView.CurrentRow is not { Index: >= 0 } row || row.Index >= _selecionadas.Count)
        {
            statusLabel.Text = "Selecione uma linha para excluir.";
            return;
        }
        _selecionadas.RemoveAt(row.Index);
        AtualizarGridCaixas();
        AtualizarResumo();
    }

    private void LimparSelecao()
    {
        _selecionadas.Clear();
        AtualizarGridCaixas();
        AtualizarResumo();
        statusLabel.Text = "Seleção limpa.";
    }

    private void AtualizarGridCaixas()
    {
        caixasDataGridView.Rows.Clear();
        foreach (ProdutoAcabadoCaixa c in _selecionadas)
        {
            // GATE 047-BN: informação da PRÓPRIA HU. Peso pesado = peso persistido da HU (PesoBrutoKg) — sem cálculo novo.
            caixasDataGridView.Rows.Add(
                c.HandlingUnitExternalId ?? "-",
                c.Material,
                c.NumeroOrdemProducao,
                c.Lote,
                FormatarKg(c.PesoBrutoKg));
        }
    }

    private void AtualizarResumo()
    {
        // GATE 047-BN: resumo enxuto — caixas, produtos distintos e o PESO PESADO total (soma do peso persistido das HUs).
        _resumoCaixasValor.Text = _selecionadas.Count.ToString(CultureInfo.InvariantCulture);
        _resumoProdutosValor.Text = _selecionadas.Select(c => (c.Material ?? string.Empty).Trim())
            .Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(CultureInfo.InvariantCulture);
        _resumoPesoPesadoValor.Text = FormatarKg(_selecionadas.Sum(c => c.PesoBrutoKg));
    }

    private async Task GravarPaleteAsync()
    {
        if (_selecionadas.Count == 0)
        {
            statusLabel.Text = "Adicione caixas antes de gravar o palete.";
            return;
        }
        string materialEmbalagem = materialEmbalagemTextBox.Text.Trim();
        if (materialEmbalagem.Length == 0)
        {
            statusLabel.Text = "Informe o material de embalagem do palete.";
            return;
        }

        try
        {
            // Reutiliza o validador ÚNICO + a persistência local existentes (sem duplicar regra na Form, sem POST).
            ProdutoAcabadoPalete palete = _controller.MontarPaletePorSelecao(_selecionadas, _paletesFormados, materialEmbalagem);
            long? usuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
            string terminal = EstadoTerminalLocalAtual.ObterContextoAtualizado()?.NomeTerminal ?? string.Empty;

            ResultadoPaletePersistenciaLocal persistencia =
                await _controller.CriarPaleteLocalPersistenteAsync(palete, usuario, terminal);
            if (!persistencia.Sucesso)
            {
                statusLabel.Text = persistencia.Mensagem;
                return;
            }

            statusLabel.Text = $"Palete {palete.CodigoPaleteLocal} gravado localmente em RASCUNHO.";
            LimparSelecao();
            await RecarregarPaletesFormadosAsync(palete.NumeroOrdemDe());
        }
        catch (Exception ex)
        {
            statusLabel.Text = ex.Message;
        }
    }

    private async Task RecarregarPaletesFormadosAsync(string numeroOrdem)
    {
        try
        {
            string terminal = EstadoTerminalLocalAtual.ObterContextoAtualizado()?.NomeTerminal ?? string.Empty;
            IReadOnlyList<ProdutoAcabadoPalete> paletes = await _controller.RecarregarPaletesLocaisAsync(numeroOrdem, terminal);
            _paletesFormados.Clear();
            _paletesFormados.AddRange(paletes);
            AtualizarGridPaletesFormados();
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Palete gravado, mas a releitura da grade falhou ({ex.GetType().Name}).";
        }
    }

    // GATE 047-Z: ENVIAR AO SAP do palete selecionado. Fluxo OBRIGATÓRIO Form → Controller → orquestrador INT012
    // (a Form NÃO acessa banco/gateway/SAP/CPI/orquestrador diretamente). Reutiliza a infra INT012 existente.
    private async Task EnviarPaleteSelecionadoAsync()
    {
        // P0: proteção de duplo disparo (duplo clique / Enter repetido / reentrada async) — no máximo 1 execução.
        if (_enviandoPalete) { return; }

        if (paletesFormadosDataGridView.CurrentRow is not { Index: >= 0 } row || row.Index >= _paletesFormados.Count)
        {
            statusLabel.Text = "Selecione um palete formado para enviar ao SAP.";
            return;
        }
        ProdutoAcabadoPalete palete = _paletesFormados[row.Index];

        // MVP: RASCUNHO ingressa no fluxo normal. ENVIADO_SAP / INDETERMINADO / CONFIRMADO_SAP NÃO produzem segundo
        // POST normal (contrato de recovery/reconciliação preservado; nenhuma chamada ao Controller/orquestrador).
        if (!string.Equals((palete.StatusSap ?? string.Empty).Trim(), "RASCUNHO", StringComparison.OrdinalIgnoreCase))
        {
            statusLabel.Text = $"Palete {palete.CodigoPaleteLocal} em '{palete.StatusSap}': não elegível para novo envio normal (recovery/reconciliação preserva o contrato).";
            return;
        }

        _enviandoPalete = true;
        enviarSapButton.Enabled = false;
        gravarPaleteButton.Enabled = false;
        try
        {
            // Reutiliza o caminho existente: Controller.EnviarPaleteInt012Async → orquestrador → claim → gateway → fechamento.
            ResultadoPaleteInt012 resultado = await _controller.EnviarPaleteInt012Async(palete);
            ApresentacaoEnvioPaleteInt012 apresentacao =
                ProdutoAcabadoPaleteEnvioPresenter.Construir(palete.CodigoPaleteLocal, resultado);
            statusLabel.Text = apresentacao.Mensagem;

            // Confirmado ⇒ recarrega PALETES FORMADOS (status/HU SAP atualizados). Indeterminado/Erro ⇒ sem reload,
            // sem reenvio (mensagem fail-closed já apresentada; UC preservada pelo presenter). Nunca inventar sucesso.
            if (resultado.Estado == EstadoPaleteInt012.Confirmado)
            {
                await RecarregarPaletesFormadosAsync(palete.NumeroOrdemDe());
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"{ProdutoAcabadoPaleteEnvioPresenter.ParaExcecao(palete.CodigoPaleteLocal).Mensagem} ({ex.GetType().Name})";
        }
        finally
        {
            _enviandoPalete = false;
            enviarSapButton.Enabled = true;
            gravarPaleteButton.Enabled = true;
        }
    }

    // GATE 047-AB: reload por TERMINAL ao abrir. SOMENTE leitura via Controller (Form não acessa banco/SAP direto);
    // não cria palete nem dispara INT012. Reutilizado o mesmo grid/lista de PALETES FORMADOS.
    private async Task CarregarPaletesFormadosDoTerminalAsync()
    {
        try
        {
            string terminal = EstadoTerminalLocalAtual.ObterContextoAtualizado()?.NomeTerminal ?? string.Empty;
            IReadOnlyList<ProdutoAcabadoPalete> paletes = await _controller.RecarregarPaletesLocaisPorTerminalAsync(terminal);
            _paletesFormados.Clear();
            _paletesFormados.AddRange(paletes);
            AtualizarGridPaletesFormados();
        }
        catch (Exception ex)
        {
            statusLabel.Text = DescreverFalha("Falha ao carregar paletes formados", ex);
        }
    }

    private void AtualizarGridPaletesFormados()
    {
        paletesFormadosDataGridView.Rows.Clear();
        foreach (ProdutoAcabadoPalete p in _paletesFormados)
        {
            // GATE 047-BN: Situação SAP = status de integração técnico preservado (StatusSap). Peso pesado = PesoBrutoKg.
            paletesFormadosDataGridView.Rows.Add(
                p.CodigoPaleteLocal,
                p.Caixas.Count.ToString(CultureInfo.InvariantCulture),
                FormatarKg(p.PesoBrutoKg),
                p.StatusSap,
                string.IsNullOrWhiteSpace(p.HandlingUnitPalete) ? "-" : p.HandlingUnitPalete);
        }
    }

    // ---------------- helpers visuais ----------------

    private static string FormatarKg(decimal valor) => valor.ToString("N3", CultureInfo.GetCultureInfo("pt-BR")) + " kg";

    /// <summary>Card branco arredondado com título vermelho no topo e o conteúdo (Dock=Fill) abaixo.</summary>
    private static Control CardComTitulo(string titulo, Control conteudo)
    {
        RoundedPanel card = new() { Dock = DockStyle.Fill, FillColor = Color.White, BorderColor = CorBorda, BorderRadius = 8, Padding = new Padding(12, 6, 12, 8) };
        Label tituloLabel = new() { Text = titulo, Font = FonteLabel, ForeColor = CorFugaVermelho, Dock = DockStyle.Top, Height = 20 };
        conteudo.Dock = DockStyle.Fill;
        card.Controls.Add(conteudo);
        card.Controls.Add(tituloLabel);
        return card;
    }

    /// <summary>Linha horizontal de campos rotulados (rótulo em cima, campo embaixo), com pesos percentuais.</summary>
    private static Control LinhaCampos(params (string rotulo, float pct, Control campo)[] celulas)
    {
        TableLayoutPanel t = new() { Dock = DockStyle.Fill, ColumnCount = celulas.Length, RowCount = 2, BackColor = Color.White, Margin = Padding.Empty };
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        for (int i = 0; i < celulas.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, celulas[i].pct));
            Label cap = new() { Text = celulas[i].rotulo, Font = FonteCampoLegado, ForeColor = CorRotulo, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2, 0, 2, 0) };
            celulas[i].campo.Dock = DockStyle.Fill;
            celulas[i].campo.Margin = new Padding(2, 1, 2, 2);
            t.Controls.Add(cap, i, 0);
            t.Controls.Add(celulas[i].campo, i, 1);
        }
        return t;
    }

    private static TextBox CriarCampoValorRealizada() => new()
    {
        // Campo da Quantidade Realizada: read-only (preenchido por código com dados reais da seleção).
        ReadOnly = true,
        TabStop = false,
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Font = FonteValorRealizada,
        ForeColor = CorHeaderEscuro
    };

    private static Button CriarBotao(string texto, int x, int largura, Color cor) => new()
    {
        Text = texto,
        Font = FonteBotao,
        Location = new Point(x, 20),
        Size = new Size(largura, 30),
        FlatStyle = FlatStyle.Flat,
        BackColor = cor,
        ForeColor = Color.White,
        Cursor = Cursors.Hand
    };

    private static DataGridView CriarGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            RowTemplate = { Height = 26 }
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = CorHeaderEscuro;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = FonteGridHeader;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 30;
        grid.DefaultCellStyle.Font = FonteGridCell;
        return grid;
    }
}

internal static class PaleteOrdemExtensoes
{
    /// <summary>OP do palete (a partir da primeira caixa reconstruída/selecionada), para recarregar a grade.</summary>
    public static string NumeroOrdemDe(this ProdutoAcabadoPalete palete)
        => palete.Caixas.Count > 0 ? (palete.Caixas[0].NumeroOrdemProducao ?? string.Empty).Trim() : string.Empty;
}
