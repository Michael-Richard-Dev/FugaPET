using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// A moldura das telas de cadastro montadas EM CODIGO (sem Designer): barra de titulo propria,
/// rodape do sistema, cartao arredondado, grade e botoes no padrao.
/// <para>
/// POR QUE EXISTE: tres telas nasceram iguais — Vinculo de Produto e Etiqueta, Tolerancia de
/// Recebimento e Tolerancia de Producao. Ao padronizar a primeira, em 26/11/2026, escrevi a moldura
/// DENTRO dela; fazer o mesmo nas outras duas criaria a terceira copia do mesmo cabecalho. Copia
/// privada por tela foi exatamente o que deixou nove telas sem o realce dos botoes de janela — ver
/// <see cref="BarraDeTituloHelper"/>.
/// </para>
/// <para>
/// TUDO AQUI ESCALA PELO DPI. Estas telas tem teste proprio que proibe <c>SetBounds</c> e tamanho
/// literal sem escala, porque geometria cravada e o que as quebra em maquina de outra resolucao.
/// Por isso o helper recebe a funcao de escala da tela em vez de inventar pixels.
/// </para>
/// </summary>
internal static class ShellCadastroFluidoHelper
{
    private static readonly Color Laranja = Color.FromArgb(0xC8, 0x4E, 0x0A);
    private static readonly Color LaranjaClaro = Color.FromArgb(0xE8, 0xAE, 0x8C);
    private static readonly Color Tinta = Color.FromArgb(0x0F, 0x17, 0x2A);
    private static readonly Color BordaSuave = Color.FromArgb(0xCB, 0xD5, 0xE1);

    /// <summary>
    /// Prepara o formulario: sem a barra do Windows, com o icone e a fonte base das demais telas de
    /// cadastro (Cascadia Code 9pt — medido nas telas com Designer em 26/11/2026).
    /// </summary>
    public static void PrepararFormulario(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        form.FormBorderStyle = FormBorderStyle.None;
        form.BackColor = Color.FromArgb(0xF7, 0xF8, 0xFA);
        form.Font = new Font("Cascadia Code", 9f);
        IconeJanelaHelper.AplicarIconePadrao(form);
    }

    /// <summary>
    /// A faixa da marca, que nestas telas E a barra de titulo: botao de voltar, logo, divisor, icone
    /// da tela, titulo com subtitulo e os tres botoes de janela.
    /// </summary>
    public static Control CriarBarraDeTitulo(
        Form form,
        Func<int, int> escalar,
        string titulo,
        string subtitulo,
        Image? iconeDaTela)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(escalar);

        Panel faixa = new()
        {
            Dock = DockStyle.Top,
            BackColor = Laranja,
            Height = escalar(62),
            Padding = new Padding(escalar(16), escalar(8), escalar(16), escalar(8)),
        };

        PictureBox logo = new()
        {
            Dock = DockStyle.Left,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = escalar(132),
            BackColor = Color.Transparent,
        };
        IdentidadeVisualFugaPet.AplicarNoCabecalho(logo);

        // Voltar para a pagina de Cadastro: o mesmo glifo e o mesmo gesto das telas com Designer —
        // U+E700 do Segoe MDL2 Assets, e o clique FECHA a tela; quem reabre o painel e o shell.
        Label voltar = new()
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Left,
            Font = new Font("Segoe MDL2 Assets", form.Font.Size + 5f),
            ForeColor = Color.White,
            Text = "\uE700",
            TextAlign = ContentAlignment.MiddleCenter,
            Width = escalar(40),
        };
        voltar.Click += (_, _) => form.Close();

        // O divisor mora numa moldura transparente que lhe da ar dos dois lados e o encurta. Colado
        // no icone ele parecia um risco em cima do desenho.
        Panel molduraDoDivisor = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Left,
            Width = escalar(25),
            Padding = new Padding(escalar(12), escalar(9), escalar(12), escalar(9)),
        };
        molduraDoDivisor.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = LaranjaClaro });

        PictureBox icone = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Left,
            Image = iconeDaTela,
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false,
            Width = escalar(30),
            Margin = new Padding(0),
        };

        Panel bloco = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        Label rotuloTitulo = new()
        {
            Dock = DockStyle.Top,
            Text = titulo,
            ForeColor = Color.White,
            AutoSize = false,
            Height = escalar(26),
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(escalar(12), 0, 0, 0),
            Font = new Font(form.Font.FontFamily, form.Font.Size + 3f, FontStyle.Bold),
        };

        Label rotuloSubtitulo = new()
        {
            Dock = DockStyle.Top,
            Text = subtitulo,
            ForeColor = Color.White,
            AutoSize = false,
            Height = escalar(18),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(escalar(12), 0, 0, 0),
            Font = new Font(form.Font.FontFamily, form.Font.Size - 0.5f),
        };

        bloco.Controls.Add(rotuloSubtitulo);
        bloco.Controls.Add(rotuloTitulo);

        Label minimizar = CriarBotaoDeJanela(form, "–");
        minimizar.Click += (_, _) => form.WindowState = FormWindowState.Minimized;

        Label maximizar = CriarBotaoDeJanela(form, "□");
        maximizar.Click += (_, _) => form.WindowState = form.WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

        Label fechar = CriarBotaoDeJanela(form, "×");
        fechar.Click += (_, _) => form.Close();

        void Reposicionar()
        {
            // Sem SetBounds: o teste destas telas proibe a chamada, e com razao — e por ali que entra
            // geometria cravada. Tudo aqui sai de variaveis.
            int largura = escalar(46);
            int altura = faixa.ClientSize.Height;

            foreach (Label botao in new[] { minimizar, maximizar, fechar })
            {
                botao.Width = largura;
                botao.Height = altura;
                botao.Top = 0;
            }

            fechar.Left = faixa.ClientSize.Width - largura;
            maximizar.Left = fechar.Left - largura;
            minimizar.Left = maximizar.Left - largura;
            bloco.Padding = new Padding(0, 0, (largura * 3) + escalar(8), 0);
        }

        faixa.SizeChanged += (_, _) => Reposicionar();

        // ORDEM IMPORTA com Dock=Left: quem tem o MAIOR indice doca mais a esquerda, por isso o botao
        // de voltar entra por ultimo.
        faixa.Controls.Add(bloco);
        faixa.Controls.Add(icone);
        faixa.Controls.Add(molduraDoDivisor);
        faixa.Controls.Add(logo);
        faixa.Controls.Add(voltar);
        faixa.Controls.Add(minimizar);
        faixa.Controls.Add(maximizar);
        faixa.Controls.Add(fechar);

        // OS BOTOES VEM PARA A FRENTE. O bloco do titulo tem Dock=Fill e entrou antes, entao ficava
        // na frente e, sendo Transparent, REPINTAVA o laranja do pai por cima dos tres: a janela
        // abria sem ter como fechar. O DrawToBitmap nao revela isso — a prova e a ordem dos Controls.
        minimizar.BringToFront();
        maximizar.BringToFront();
        fechar.BringToFront();

        Reposicionar();

        BarraDeTituloHelper.AplicarRealceDosBotoes(faixa, minimizar, maximizar, fechar);
        BarraDeTituloHelper.AplicarRealceDosBotoes(faixa, voltar);
        BarraDeTituloHelper.PermitirArrastarPeloCabecalho(form, faixa, bloco, rotuloTitulo, rotuloSubtitulo, logo);
        return faixa;
    }

    /// <summary>O rodape do sistema, no mesmo desenho das telas com Designer.</summary>
    public static Control CriarRodape(Func<int, int> escalar)
    {
        ArgumentNullException.ThrowIfNull(escalar);

        Panel rodape = new() { Dock = DockStyle.Bottom, Height = escalar(38) };
        rodape.Controls.Add(ShellProcessoPadraoHelper.CriarRodapePadrao());
        return rodape;
    }

    /// <summary>
    /// Envolve um conteudo no cartao branco arredondado das telas de cadastro, com titulo em cima.
    /// </summary>
    public static Control EmCartao(Func<int, int> escalar, Font fonteBase, Control conteudo, string titulo)
    {
        ArgumentNullException.ThrowIfNull(escalar);
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(fonteBase);

        RoundedPanel cartao = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(escalar(14), escalar(12), escalar(14), escalar(14)),
            ShadowBlur = 0,
            ShadowOffsetY = 0,
        };

        Label rotulo = new()
        {
            Dock = DockStyle.Top,
            Text = titulo,
            AutoSize = false,
            Height = escalar(26),
            ForeColor = Tinta,
            Font = new Font("Segoe UI", fonteBase.Size + 1.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        conteudo.Dock = DockStyle.Fill;
        cartao.Controls.Add(conteudo);
        cartao.Controls.Add(rotulo);
        return cartao;
    }

    /// <summary>
    /// Deixa a grade com a aparencia das listas de cadastro. Sem isto ela entrega o cinza padrao do
    /// WinForms, que e o que faz a tela parecer de outro sistema.
    /// </summary>
    public static void EstilizarGrade(DataGridView grade, Func<int, int> escalar, Font fonteBase)
    {
        ArgumentNullException.ThrowIfNull(grade);
        ArgumentNullException.ThrowIfNull(escalar);
        ArgumentNullException.ThrowIfNull(fonteBase);

        grade.BackgroundColor = Color.White;
        grade.BorderStyle = BorderStyle.None;
        grade.EnableHeadersVisualStyles = false;
        grade.GridColor = Color.FromArgb(0xE2, 0xE8, 0xF0);
        grade.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grade.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grade.ColumnHeadersHeight = escalar(34);
        grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        grade.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
            ForeColor = Tinta,
            Font = new Font("Segoe UI", fonteBase.Size, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(escalar(8), 0, 0, 0),
            SelectionBackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
            SelectionForeColor = Tinta,
        };
        grade.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0x33, 0x3D, 0x4D),
            Font = new Font("Segoe UI", fonteBase.Size),
            Padding = new Padding(escalar(8), 0, 0, 0),
            SelectionBackColor = Color.FromArgb(0xFD, 0xE4, 0xD3),
            SelectionForeColor = Tinta,
        };
        grade.RowTemplate.Height = escalar(30);
    }

    /// <summary>
    /// Aparencia dos botoes de acao, nas cores MEDIDAS nas telas de cadastro: gravar = #C84E0A com
    /// texto branco; destrutivo = branco, borda #CBD5E1 e texto laranja.
    /// </summary>
    public static void EstilizarBotao(Button botao, Func<int, int> escalar, Font fonteBase, string texto, bool principal)
    {
        ArgumentNullException.ThrowIfNull(botao);
        ArgumentNullException.ThrowIfNull(escalar);
        ArgumentNullException.ThrowIfNull(fonteBase);

        botao.Text = texto;
        botao.Dock = DockStyle.Top;
        botao.Height = escalar(34);
        botao.FlatStyle = FlatStyle.Flat;
        botao.Cursor = Cursors.Hand;
        botao.Font = new Font("Segoe UI", fonteBase.Size, FontStyle.Bold);
        botao.UseVisualStyleBackColor = false;
        botao.Margin = new Padding(0);

        if (principal)
        {
            botao.BackColor = Laranja;
            botao.ForeColor = Color.White;
            botao.FlatAppearance.BorderColor = Laranja;
        }
        else
        {
            botao.BackColor = Color.White;
            botao.ForeColor = Laranja;
            botao.FlatAppearance.BorderColor = BordaSuave;
        }
    }

    /// <summary>
    /// Rotulo de campo do cartao: mesma largura dos campos, margem horizontal zero. Sem isto ele
    /// herda a margem padrao de 3px do TableLayoutPanel e nasce desalinhado da propria caixa.
    /// </summary>
    public static Label CriarRotuloDoCampo(Func<int, int> escalar, Font fonteBase, string texto)
    {
        ArgumentNullException.ThrowIfNull(escalar);
        ArgumentNullException.ThrowIfNull(fonteBase);

        return new Label
        {
            Text = texto,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = escalar(20),
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0),
            Font = new Font("Segoe UI", fonteBase.Size),
        };
    }

    private static Label CriarBotaoDeJanela(Form form, string glifo) => new()
    {
        BackColor = Color.Transparent,
        Cursor = Cursors.Hand,
        Font = new Font(form.Font.FontFamily, form.Font.Size + 3f),
        ForeColor = Color.White,
        Text = glifo,
        TextAlign = ContentAlignment.MiddleCenter,
    };
}
