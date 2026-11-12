using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 081/082-FIX1 — legibilidade "Recebimento de Mercadoria": prova que a escala é de FONTE apenas (2.0x),
/// NUNCA de geometria (que quebrava o viewport 1920x1080/100% do 081), e verifica a fiação na tela (branding
/// FugaPET, ícone laranja/traços brancos, status SAP destacado, sem AutoScroll, janela Maximizada, estrutura
/// responsiva TableLayoutPanel/Anchor preservada, escopo restrito ao modo, sem tocar regra funcional).
/// </summary>
public sealed class LegibilidadeRecebimentoMercadoria081Tests
{
    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try { acao(); }
            catch (Exception ex) { erro = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (erro is not null) throw erro;
    }

    private static string Ler(string arquivo)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 9; i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine(raiz, "Tela", "Processo", arquivo),
                Path.Combine(raiz, "FugaPet_HML", "Tela", "Processo", arquivo)
            })
            {
                if (File.Exists(cand)) return File.ReadAllText(cand);
            }
            raiz = Directory.GetParent(raiz)?.FullName ?? raiz;
        }
        throw new FileNotFoundException(arquivo);
    }

    private static string FonteTela() => Ler("ProcessoEntradaProdutoForm.cs");
    private static string FonteDesigner() => Ler("ProcessoEntradaProdutoForm.Designer.cs");
    private static string FonteHelper() => Ler("LegibilidadeRecebimentoMercadoria.cs");

    // ---------------- escalador puro (FONTE apenas) ----------------

    // TEST_12: fonte 2.0x preservada. §6/§7: geometria NÃO é escalada (adapta-se pelo layout responsivo).
    [Fact]
    public void Escala_DobraFonte_PreservaGeometriaEIcone()
        => ExecutarEmSta(() =>
        {
            using Panel raiz = new() { Size = new Size(400, 300) };
            Label titulo = new() { Location = new Point(10, 10), Size = new Size(200, 24), Font = new Font("Segoe UI", 12F, FontStyle.Bold), AutoSize = false };
            Button botao = new() { Location = new Point(10, 50), Size = new Size(120, 30), Font = new Font("Segoe UI", 9F), AutoSize = false };
            TextBox campo = new() { Location = new Point(10, 90), Size = new Size(150, 24), Font = new Font("Segoe UI", 10F) };
            PictureBox icone = new() { Location = new Point(300, 10), Size = new Size(29, 29) };
            raiz.Controls.AddRange([titulo, botao, campo, icone]);

            LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(raiz, 2.0f);

            // Fonte dobrou...
            Assert.Equal(24F, titulo.Font.Size, 3);
            Assert.Equal(18F, botao.Font.Size, 3);
            Assert.Equal(20F, campo.Font.Size, 3);
            // ...mas geometria NÃO mudou (nem posição, nem tamanho).
            Assert.Equal(new Point(10, 10), titulo.Location);
            Assert.Equal(new Size(200, 24), titulo.Size);
            Assert.Equal(new Point(10, 50), botao.Location);
            Assert.Equal(new Point(300, 10), icone.Location);
            Assert.Equal(new Size(29, 29), icone.Size);
        });

    // TEST_12 (grid): fonte 2.0x + altura de linha/header; largura de coluna PRESERVADA (evita overflow horizontal).
    [Fact]
    public void Escala_DataGridView_FonteEAlturasDobram_LarguraColunaPreservada()
        => ExecutarEmSta(() =>
        {
            using Panel raiz = new() { Size = new Size(400, 300) };
            DataGridView grid = new()
            {
                Location = new Point(5, 5),
                Size = new Size(300, 200),
                Font = new Font("Segoe UI", 8F),
                ColumnHeadersHeight = 30
            };
            grid.RowTemplate.Height = 22;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c1", Width = 100 });
            raiz.Controls.Add(grid);

            LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(raiz, 2.0f);

            Assert.Equal(16F, grid.Font.Size, 3);
            Assert.Equal(16F, grid.DefaultCellStyle.Font!.Size, 3);
            Assert.Equal(16F, grid.ColumnHeadersDefaultCellStyle.Font!.Size, 3);
            Assert.Equal(60, grid.ColumnHeadersHeight);   // 30 -> 60
            Assert.Equal(44, grid.RowTemplate.Height);    // 22 -> 44
            Assert.Equal(100, grid.Columns[0].Width);     // largura preservada
            Assert.Equal(new Size(300, 200), grid.Size);  // geometria do grid preservada (elástica no runtime)
        });

    [Fact]
    public void Escala_NaoCompoePorHeranca()
        => ExecutarEmSta(() =>
        {
            using Panel raiz = new() { Font = new Font("Segoe UI", 10F) };
            Label herdeiro = new();
            raiz.Controls.Add(herdeiro);

            LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(raiz, 2.0f);

            Assert.Equal(20F, herdeiro.Font.Size, 3); // 10 -> 20, não 40
        });

    [Fact] // TEST_12: alvo de escala é 2.0x.
    public void Escala_ConstanteEhTwoX()
        => Assert.Equal(2.0f, LegibilidadeRecebimentoMercadoria.Escala);

    // ---------------- fiação na tela ----------------

    [Fact] // FIX4: escala 2.0x SOMENTE na área interna (rootTableLayoutPanel + sidePanel); cabeçalho/rodapé padrão.
    public void Tela_EscalaApenasAreaInterna_SemAutoScroll_Maximizada()
    {
        string src = FonteTela();
        Assert.Contains("AplicarEscalaFonte(rootTableLayoutPanel, LegibilidadeRecebimentoMercadoria.Escala);", src, StringComparison.Ordinal);
        Assert.Contains("AplicarEscalaFonte(sidePanel, LegibilidadeRecebimentoMercadoria.Escala);", src, StringComparison.Ordinal);
        Assert.Contains("AutoScroll = false;", src, StringComparison.Ordinal);
        Assert.Contains("WindowState = FormWindowState.Maximized;", src, StringComparison.Ordinal);
        // cabeçalho e rodapé NÃO são escalados como a tela inteira (não escala o Form todo).
        Assert.DoesNotContain("AplicarEscalaFonte(this,", src, StringComparison.Ordinal);
        // regressão do defeito 081: nada de escala de geometria nem AutoScroll ligado.
        Assert.DoesNotContain("AplicarEscala(this", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoScroll = true;", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AutoScrollMinSize", src, StringComparison.Ordinal);
    }

    [Fact] // helper não mexe em Location/Size (só fonte + alturas de grid).
    public void Helper_NaoAlteraLocationNemSizeDeControles()
    {
        string h = FonteHelper();
        Assert.DoesNotContain(".Location = new Point", h, StringComparison.Ordinal);
        Assert.DoesNotContain(".Size = new Size", h, StringComparison.Ordinal);
        Assert.Contains("public const float Escala = 2.0f;", h, StringComparison.Ordinal);
    }

    [Fact] // TEST_01/03..11: a estrutura responsiva que garante o fit está preservada (Maximized + TLP + grid elástica).
    public void Designer_EstruturaResponsivaPreservada()
    {
        string d = FonteDesigner();
        Assert.Contains("AutoScaleMode = AutoScaleMode.Font;", d, StringComparison.Ordinal);
        Assert.Contains("WindowState = FormWindowState.Maximized;", d, StringComparison.Ordinal);
        Assert.Contains("rootTableLayoutPanel.Dock = DockStyle.Fill;", d, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;", d, StringComparison.Ordinal);
        // MinimumSize não excede o viewport alvo (1920x1080).
        Assert.Contains("MinimumSize = new Size(1180, 648);", d, StringComparison.Ordinal);
    }

    [Fact] // TEST_16/branding, TEST_19/20 escopo: só RecebimentoMercadoria; regra funcional intacta.
    public void Tela_Branding_Escopo_FuncionalIntacto()
    {
        string src = FonteTela();
        Assert.Contains("if (_modoEntrada != global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.RecebimentoMercadoria)", src, StringComparison.Ordinal);
        Assert.Contains("CarregarLogoFugaPetBranco()", src, StringComparison.Ordinal);
        Assert.Contains("Servicos\\\\image\\\\FugaPet branco sem fundo.png", src, StringComparison.Ordinal);
        Assert.Contains("headerDividerLabel.BackColor = Color.White;", src, StringComparison.Ordinal);
        Assert.Contains("_controller.ConsultarPedidoAsync(numeroPedido, _modoEntrada", src, StringComparison.Ordinal);
    }

    [Fact] // Ícone do recebimento mantém somente os traços brancos, sem fundo colorido.
    public void Tela_IconeRecebimentoSomenteBranco_SemFundoColorido()
    {
        string src = FonteTela();
        int ini = src.IndexOf("private void AplicarLegibilidadeRecebimentoMercadoria()", StringComparison.Ordinal);
        int fim = src.IndexOf("PerformLayout();", ini, StringComparison.Ordinal);
        string corpo = src[ini..fim];
        Assert.Contains("headerTitleIconPanel.FillColor = Color.Transparent;", corpo, StringComparison.Ordinal);
        Assert.Contains("headerTitleIconPictureBox.Image = ExtrairTracosBrancos(global::FugaPET_Dev.Properties.Resources.production_title_icon);", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Red", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Pink", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtrairTracosBrancos_RemoveFundoVermelhoDoIcone()
    {
        MethodInfo? metodo = typeof(ProcessoEntradaProdutoForm).GetMethod(
            "ExtrairTracosBrancos",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);
        using Bitmap resultado = Assert.IsType<Bitmap>(metodo.Invoke(
            null,
            [global::FugaPET_Dev.Properties.Resources.production_title_icon]));

        Color[] pixels = Enumerable.Range(0, resultado.Width)
            .SelectMany(x => Enumerable.Range(0, resultado.Height).Select(y => resultado.GetPixel(x, y)))
            .ToArray();

        Assert.Contains(pixels, pixel => pixel.A == 0);
        Assert.Contains(pixels, pixel => pixel.A > 0);
        Assert.All(pixels.Where(pixel => pixel.A > 0), pixel =>
        {
            Assert.Equal(255, pixel.R);
            Assert.Equal(255, pixel.G);
            Assert.Equal(255, pixel.B);
        });
    }

    [Fact]
    public void Tela_BotoesJanelaUsamMesmoHoverLaranjaEscuro()
    {
        string src = FonteTela();
        const string hover = "Color.FromArgb(160, 55, 5)";

        Assert.Contains($"ConfigureTitleButtonHover(minimizeWindowLabel, {hover});", src, StringComparison.Ordinal);
        Assert.Contains($"ConfigureTitleButtonHover(maximizeWindowLabel, {hover});", src, StringComparison.Ordinal);
        Assert.Contains($"ConfigureTitleButtonHover(closeWindowLabel, {hover});", src, StringComparison.Ordinal);
    }

    [Fact] // TEST_18: status SAP destacado (badge preenchido + texto branco).
    public void Tela_StatusSap_Destacado()
    {
        string src = FonteTela();
        Assert.Contains("sapStatusPanel.FillColor = LegibilidadeRecebimentoMercadoria.LaranjaFugaPetEscuro;", src, StringComparison.Ordinal);
        Assert.Contains("sapStatusLabel.ForeColor = Color.White;", src, StringComparison.Ordinal);
    }

    // ---------------- FIX2: adaptação de geometria ao conteúdo (alturas) ----------------

    // Linha ABSOLUTA de um TableLayoutPanel cresce para caber o texto 2x; linha da grade (elástica) intacta;
    // altura de controle de texto fixo cresce até a altura preferida da fonte. (CONTROL_HEIGHTS_ADAPTED)
    [Fact]
    public void Adaptar_CresceLinhaAbsolutaEAlturaDeTexto_GridElasticaIntacta()
        => ExecutarEmSta(() =>
        {
            using TableLayoutPanel tlp = new() { ColumnCount = 1, RowCount = 2, Size = new Size(300, 200) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));   // linha de texto (curta p/ fonte 2x)
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // linha elástica (grade)

            Label valor = new() { AutoSize = false, Size = new Size(120, 14), Font = new Font("Segoe UI", 18F, FontStyle.Bold), Text = "4500000005" };
            DataGridView grid = new() { Font = new Font("Segoe UI", 16F) };
            tlp.Controls.Add(valor, 0, 0);
            tlp.Controls.Add(grid, 0, 1);

            int alturaPreferidaTexto = valor.PreferredSize.Height;

            LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(tlp);

            Assert.True(tlp.RowStyles[0].Height >= alturaPreferidaTexto,
                $"linha absoluta deveria crescer para >= {alturaPreferidaTexto}, ficou {tlp.RowStyles[0].Height}");
            Assert.Equal(SizeType.Percent, tlp.RowStyles[1].SizeType);   // grade continua elástica
            Assert.Equal(100f, tlp.RowStyles[1].Height);                 // não fixada
            Assert.True(valor.Height >= alturaPreferidaTexto);           // texto cabe verticalmente
        });

    // A adaptação só CRESCE: um controle/linha já suficiente não é reduzido. (monotônico)
    [Fact]
    public void Adaptar_NaoReduz_QuandoJaCabe()
        => ExecutarEmSta(() =>
        {
            using TableLayoutPanel tlp = new() { ColumnCount = 1, RowCount = 1, Size = new Size(200, 200) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 400f));   // já bem maior que o necessário
            Label pequeno = new() { AutoSize = false, Size = new Size(80, 60), Font = new Font("Segoe UI", 9F), Text = "x" };
            tlp.Controls.Add(pequeno, 0, 0);

            LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(tlp);

            Assert.Equal(400f, tlp.RowStyles[0].Height);   // não reduziu
            Assert.Equal(60, pequeno.Height);              // não reduziu
        });

    // ComboBox recebe altura compatível com a fonte (COMBOBOX_ADAPTED).
    [Fact]
    public void Adaptar_ComboBox_AlturaCompativelComFonte()
        => ExecutarEmSta(() =>
        {
            using Panel raiz = new() { Size = new Size(300, 100) };
            ComboBox combo = new() { Font = new Font("Segoe UI", 18F), Location = new Point(5, 5), Width = 200 };
            raiz.Controls.Add(combo);
            int preferida = combo.PreferredSize.Height;

            LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(raiz);

            Assert.False(combo.IntegralHeight);
            Assert.True(combo.Height >= preferida, $"ComboBox {combo.Height} < preferida {preferida}");
        });

    [Fact] // a adaptação de alturas da área interna vem após a escala de fonte da área interna (FIX4).
    public void Tela_AdaptaAlturasAposEscalaFonte()
    {
        string src = FonteTela();
        int iEscala = src.IndexOf("AplicarEscalaFonte(rootTableLayoutPanel,", StringComparison.Ordinal);
        int iAdaptar = src.IndexOf("AdaptarAlturasParaFonte(rootTableLayoutPanel);", StringComparison.Ordinal);
        Assert.True(iEscala >= 0 && iAdaptar > iEscala, "AdaptarAlturasParaFonte deve ser chamado após AplicarEscalaFonte");
        // cabeçalho/rodapé não passam pela adaptação de alturas (permanecem padrão).
        Assert.DoesNotContain("AdaptarAlturasParaFonte(this);", src, StringComparison.Ordinal);
    }

    // ---------------- FIX3: orçamento de altura (regiões de conteúdo + grade elástica) ----------------

    // TOTAL_HEIGHT_BUDGET_VALID: com regiões de conteúdo 2x típicas, sobra para a grade > mínimo operacional.
    [Fact]
    public void Orcamento_GradeRecebeRestante_AcimaDoMinimo()
    {
        int alturaCliente = 1040;   // WorkingArea típica em 1920x1080/100%
        int header = 76, cards = 140, toolbar = 64, footer = 48; // conteúdo 2x
        int restante = LegibilidadeRecebimentoMercadoria.AlturaRestanteParaGrade(alturaCliente, header, cards, toolbar, footer);
        Assert.Equal(1040 - 76 - 140 - 64 - 48, restante);
        Assert.True(restante > LegibilidadeRecebimentoMercadoria.AlturaMinimaGradeOperacional,
            $"grade restante {restante} <= mínimo {LegibilidadeRecebimentoMercadoria.AlturaMinimaGradeOperacional}");
    }

    // CrescerTetoDeConteudo: linha ABSOLUTA de cards cresce até o necessário; só cresce; grade (Percent) intacta.
    [Fact]
    public void Orcamento_CresceTetoAbsolutoDosCards_SoCresce()
        => ExecutarEmSta(() =>
        {
            using TableLayoutPanel raiz = new() { ColumnCount = 1, RowCount = 2 };
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));  // faixa de cards (subdimensionada p/ 2x)
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // grade elástica

            float nova = LegibilidadeRecebimentoMercadoria.CrescerTetoDeConteudo(raiz, 0, 140);
            Assert.Equal(140f, nova);                        // cresceu para o necessário
            Assert.Equal(140f, raiz.RowStyles[0].Height);
            Assert.Equal(SizeType.Percent, raiz.RowStyles[1].SizeType); // grade continua elástica

            // não reduz quando o necessário é menor que o atual
            float mantida = LegibilidadeRecebimentoMercadoria.CrescerTetoDeConteudo(raiz, 0, 100);
            Assert.Equal(140f, mantida);
        });

    [Fact] // FIX4: o orçamento cresce SOMENTE o teto interno dos cards; cabeçalho/rodapé ficam no padrão.
    public void Tela_OrcamentoCresceApenasCardsInternos()
    {
        string src = FonteTela();
        int iAdaptar = src.IndexOf("AdaptarAlturasParaFonte(rootTableLayoutPanel);", StringComparison.Ordinal);
        int iOrcamento = src.IndexOf("AjustarOrcamentoAlturaRecebimento();", StringComparison.Ordinal);
        Assert.True(iAdaptar >= 0 && iOrcamento > iAdaptar, "AjustarOrcamentoAlturaRecebimento deve vir após AdaptarAlturasParaFonte");
        // cresce só a faixa interna dos cards (rootTableLayoutPanel linha 0).
        Assert.Contains("CrescerTetoDeConteudo(rootTableLayoutPanel, 0,", src, StringComparison.Ordinal);
        // cabeçalho/rodapé (tableLayoutPanel2) NÃO são crescidos — permanecem no tamanho padrão.
        Assert.DoesNotContain("CrescerTetoDeConteudo(tableLayoutPanel2", src, StringComparison.Ordinal);
        Assert.DoesNotContain("headerSubtitleLabel.Top = headerTitleLabel.Bottom", src, StringComparison.Ordinal);
    }
}
