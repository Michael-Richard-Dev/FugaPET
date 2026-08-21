using System.Globalization;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Comum;

internal static class ShellProcessoPadraoHelper
{
    private static readonly Color CorHeaderEscuro = Color.FromArgb(24, 31, 43);
    private static readonly Color CorFooter = Color.FromArgb(248, 250, 253);
    private static readonly Color CorBordaFooter = Color.FromArgb(229, 231, 235);
    private static readonly Color CorFugaVermelho = Color.FromArgb(229, 27, 43);

    public static TableLayoutPanel Criar(Form form, string titulo, string subtitulo, Control conteudo)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(conteudo);

        form.BackColor = Color.FromArgb(247, 248, 250);
        form.ClientSize = new Size(1366, 720);
        form.Font = new Font("Cascadia Code", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        form.FormBorderStyle = FormBorderStyle.None;
        form.MinimumSize = new Size(1180, 648);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.WindowState = FormWindowState.Maximized;
        IconeJanelaHelper.AplicarIconePadrao(form);

        TableLayoutPanel shell = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = form.BackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        conteudo.Dock = DockStyle.Fill;
        shell.Controls.Add(CriarCabecalho(form, titulo, subtitulo), 0, 0);
        shell.Controls.Add(conteudo, 0, 1);
        shell.Controls.Add(CriarRodape(), 0, 2);
        return shell;
    }

    private static Control CriarCabecalho(Form form, string titulo, string subtitulo)
    {
        Panel header = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CorHeaderEscuro,
            Margin = Padding.Empty
        };

        Label menuHeaderLabel = new()
        {
            Cursor = Cursors.Hand,
            Font = new Font("Segoe MDL2 Assets", 15F),
            ForeColor = Color.White,
            Location = new Point(18, 5),
            Size = new Size(36, 40),
            Text = "",
            TextAlign = ContentAlignment.MiddleCenter
        };
        menuHeaderLabel.Click += (_, _) => form.Close();

        PictureBox companyLogoPictureBox = new()
        {
            BackColor = Color.Transparent,
            Image = FugaPET_Dev.Properties.Resources.fuga_2026_logo,
            Location = new Point(60, 5),
            Size = new Size(128, 43),
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        };

        Label logoSaLabel = new()
        {
            BackColor = Color.Transparent,
            Font = new Font("Cascadia Code", 3F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.White,
            Location = new Point(171, 12),
            Size = new Size(21, 10),
            Text = "S/A",
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label headerDividerLabel = new()
        {
            BackColor = Color.FromArgb(100, 116, 139),
            Location = new Point(210, 8),
            Size = new Size(1, 36)
        };

        RoundedPanel headerTitleIconPanel = new()
        {
            BackColor = Color.Transparent,
            FillColor = Color.Transparent,
            Location = new Point(239, 9),
            Size = new Size(29, 29),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };
        PictureBox headerTitleIconPictureBox = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Image = FugaPET_Dev.Properties.Resources.production_title_icon,
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        };
        headerTitleIconPanel.Controls.Add(headerTitleIconPictureBox);

        Label headerTitleLabel = new()
        {
            AutoSize = true,
            Font = new Font("Cascadia Code", 13F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.White,
            Location = new Point(281, 7),
            Text = titulo
        };

        Label headerSubtitleLabel = new()
        {
            AutoSize = true,
            Font = new Font("Cascadia Code", 7F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.White,
            Location = new Point(281, 33),
            Text = subtitulo
        };

        RoundedPanel statusPanel = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            BorderColor = Color.FromArgb(58, 68, 83),
            BorderRadius = 12,
            FillColor = CorHeaderEscuro,
            Location = new Point(910, 10),
            Size = new Size(190, 27),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };
        Label statusDotLabel = new()
        {
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(250, 204, 21),
            Location = new Point(12, 2),
            Size = new Size(20, 22),
            Text = "●",
            TextAlign = ContentAlignment.MiddleCenter
        };
        Label statusLabel = new()
        {
            BackColor = Color.Transparent,
            Font = new Font("Cascadia Code", 7F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(34, 4),
            Size = new Size(145, 18),
            Text = "PALETIZAÇÃO",
            TextAlign = ContentAlignment.MiddleLeft
        };
        statusPanel.Controls.Add(statusDotLabel);
        statusPanel.Controls.Add(statusLabel);

        Label minimizar = CriarBotaoJanela("-", new Point(1218, 0), new Font("Cascadia Code", 9.75F));
        minimizar.Click += (_, _) => form.WindowState = FormWindowState.Minimized;
        Label maximizar = CriarBotaoJanela("□", new Point(1266, 0), new Font("Cascadia Code", 9.75F));
        maximizar.Click += (_, _) => form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        Label fechar = CriarBotaoJanela("×", new Point(1314, 0), new Font("Cascadia Code ExtraLight", 12F));
        fechar.Click += (_, _) => form.Close();

        header.SizeChanged += (_, _) =>
        {
            statusPanel.Left = Math.Max(560, header.Width - 456);
            minimizar.Left = header.Width - 148;
            maximizar.Left = header.Width - 100;
            fechar.Left = header.Width - 52;
        };

        header.Controls.Add(logoSaLabel);
        header.Controls.Add(menuHeaderLabel);
        header.Controls.Add(companyLogoPictureBox);
        header.Controls.Add(headerDividerLabel);
        header.Controls.Add(headerTitleIconPanel);
        header.Controls.Add(headerTitleLabel);
        header.Controls.Add(headerSubtitleLabel);
        header.Controls.Add(statusPanel);
        header.Controls.Add(minimizar);
        header.Controls.Add(maximizar);
        header.Controls.Add(fechar);
        return header;
    }

    private static Label CriarBotaoJanela(string texto, Point localizacao, Font fonte) => new()
    {
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
        BackColor = Color.Transparent,
        Font = fonte,
        ForeColor = Color.White,
        Location = localizacao,
        Size = new Size(48, 52),
        Text = texto,
        TextAlign = ContentAlignment.MiddleCenter
    };

    private static Control CriarRodape()
    {
        Panel footerBar = new()
        {
            BackColor = CorFooter,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        TableLayoutPanel footerBarLayout = new()
        {
            BackColor = Color.Transparent,
            ColumnCount = 6,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 1
        };
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        footerBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        string terminal = EstadoTerminalLocalAtual.ObterContextoAtualizado()?.NomeTerminal ?? "TERMINAL";
        footerBarLayout.Controls.Add(CriarCelulaRodape("", UsuarioLogadoUiHelper.ObterTextoUsuarioRodape()), 0, 0);
        footerBarLayout.Controls.Add(CriarCelulaRodape("", $"Terminal:  {terminal}"), 1, 0);
        footerBarLayout.Controls.Add(CriarCelulaRodape("", "Empresa:  FUGA COUROS S.A."), 2, 0);
        footerBarLayout.Controls.Add(CriarCelulaRodape("", RodapeBancoHelper.ObterTextoBancoDados()), 3, 0);
        footerBarLayout.Controls.Add(CriarCelulaRodape("", DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"))), 4, 0);
        footerBarLayout.Controls.Add(CriarCelulaRodape("", DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))), 5, 0);
        footerBar.Controls.Add(footerBarLayout);
        return footerBar;
    }

    private static Control CriarCelulaRodape(string icone, string texto)
    {
        Panel cell = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        Label iconLabel = new()
        {
            Dock = DockStyle.Left,
            Font = new Font("Segoe MDL2 Assets", 9F),
            ForeColor = CorFugaVermelho,
            Size = new Size(28, 38),
            Text = icone,
            TextAlign = ContentAlignment.MiddleCenter
        };
        Label textLabel = new()
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(98, 108, 124),
            Padding = new Padding(2, 0, 0, 0),
            Text = texto,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Panel divider = new()
        {
            BackColor = CorBordaFooter,
            Dock = DockStyle.Right,
            Width = 1
        };
        cell.Controls.Add(textLabel);
        cell.Controls.Add(iconLabel);
        cell.Controls.Add(divider);
        return cell;
    }
}
