using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class EtiquetaForm : Form
{
    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private static readonly Color MarkerColor = Color.FromArgb(239, 68, 68);
    private readonly List<RowSelection> _rowSelections = new();
    private readonly List<ProfileSearchRow> _profileSearchRows = new();
    private readonly Dictionary<Panel, Panel> _rowSelectionMarkers = new();
    private long _idEtiquetaAtual;
    private readonly EtiquetaController _etiquetaController;
    private Button? _btnCampos;

    public EtiquetaForm(EtiquetaController? etiquetaController = null)
    {
        _etiquetaController = etiquetaController ?? FabricaControladoresCadastro.CriarEtiquetaController();
        InitializeComponent();
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        ConfigureWindowButtons();
        ConfigureDragOnTitleBar();
        ConfigureNovoEtiquetaAction();
        ConfigureProfilesSearchFilter();
        CriarBotaoCampos();
        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        detailsCard.Resize += (_, _) => PosicionarBotaoCampos();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();
        InitializeProfilesTableSelection();
        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();
        PosicionarBotaoCampos();
        ApplyProfilesFilter();
        ConectarAcoesCadastro();
    }

    // Botao runtime que abre o dialogo de campos+mapeamento da etiqueta.
    private void CriarBotaoCampos()
    {
        _btnCampos = new Button
        {
            Name = "btnCamposEtiquetaRuntime",
            Text = "Campos da Etiqueta...",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(31, 41, 55),
            Cursor = Cursors.Hand,
            Width = 180,
            Height = 30
        };
        _btnCampos.Click += (_, _) => AbrirCamposEtiqueta();
        detailsCard.Controls.Add(_btnCampos);
        _btnCampos.BringToFront();
    }

    private void PosicionarBotaoCampos()
    {
        if (_btnCampos is null || detailsCard.Width <= 0) return;
        _btnCampos.Location = new Point(Math.Max(12, detailsCard.Width - _btnCampos.Width - 24), 18);
    }

    private void AbrirCamposEtiqueta()
    {
        // Abre o dialogo; pre-seleciona a etiqueta atual se houver. O dialogo tem combo proprio.
        using CamposEtiquetaForm form = new(_idEtiquetaAtual);
        form.ShowDialog(this);
    }

    private void ConfigureProfilesSearchFilter()
    {
        _profileSearchRows.Clear();
        _profileSearchRows.Add(new ProfileSearchRow(profileRow1Panel, profileRow1NameLabel, profileRow1UsersLabel, profileRow1StatusLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow2Panel, profileRow2NameLabel, profileRow2UsersLabel, profileRow2StatusLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow3Panel, profileRow3NameLabel, profileRow3UsersLabel, profileRow3StatusLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow4Panel, profileRow4NameLabel, profileRow4UsersLabel, profileRow4StatusLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow5Panel, profileRow5NameLabel, profileRow5UsersLabel, profileRow5StatusLabel));

        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void ApplyProfilesFilter()
    {
        string query = NormalizeForSearch(searchTextBox.Text.Trim());
        int visibleIndex = 0;

        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            string rowName = NormalizeForSearch(row.NameLabel.Text);

            bool match = string.IsNullOrWhiteSpace(query)
                || rowName.Contains(query, StringComparison.Ordinal);

            row.RowPanel.Visible = match;

            if (!match)
            {
                continue;
            }

            int top = profileRow1Panel.Top + (profileRow1Panel.Height * visibleIndex);
            row.RowPanel.Location = new Point(profileRow1Panel.Left, top);
            visibleIndex++;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            ClearRowSelection();
            return;
        }

        if (visibleIndex > 0)
        {
            SetFilteredRowsSelected();
            return;
        }

        HideAllMarkers();
    }

    private void ClearRowSelection()
    {
        foreach (RowSelection row in _rowSelections)
        {
            row.RowPanel.BackColor = row.NormalBackColor;
        }

        HideAllMarkers();
        ClearSummarySelectionValues();
    }

    private void SetFilteredRowsSelected()
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);
        Panel? firstVisibleRow = null;

        foreach (RowSelection row in _rowSelections)
        {
            bool isVisible = row.RowPanel.Visible;
            row.RowPanel.BackColor = isVisible ? selectedBackColor : row.NormalBackColor;
            if (isVisible)
            {
                ShowMarkerForRow(row.RowPanel);
                firstVisibleRow ??= row.RowPanel;
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }

        if (firstVisibleRow is null)
        {
            HideAllMarkers();
            ClearSummarySelectionValues();
            return;
        }
        ClearSummarySelectionValues();
    }

    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);

        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void ConfigureNovoEtiquetaAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoEtiquetaClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;

        foreach (Control child in control.Controls)
        {
            SetHandCursor(child);
        }
    }

    private void AttachNovoEtiquetaClick(Control control)
    {
        control.Click += (_, _) => PrepareNewEtiqueta();

        foreach (Control child in control.Controls)
        {
            AttachNovoEtiquetaClick(child);
        }
    }

    private void PrepareNewEtiqueta()
    {
        _idEtiquetaAtual = 0;
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        situacaoComboBox.SelectedIndex = -1;
        situacaoComboBox.Text = string.Empty;

        nomePerfilTextBox.Focus();
        nomePerfilTextBox.SelectionStart = 0;
        nomePerfilTextBox.SelectionLength = 0;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ConfigureWindowButtons()
    {
        menuHeaderLabel.Click += (_, _) => Close();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        };
        closeWindowLabel.Click += (_, _) => Close();
    }

    private void ConfigureDragOnTitleBar()
    {
        customTitleBarPanel.MouseDown += HandleTitleBarMouseDown;
        headerTitleLabel.MouseDown += HandleTitleBarMouseDown;
        headerSubtitleLabel.MouseDown += HandleTitleBarMouseDown;
        companyLogoPictureBox.MouseDown += HandleTitleBarMouseDown;
    }

    private void HandleTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void LayoutSummaryCard()
    {
        const int baseCardWidth = 326;
        const int baseCardHeight = 590;

        if (summaryCard.Width <= 0 || summaryCard.Height <= 0)
        {
            return;
        }

        float scaleX = summaryCard.Width / (float)baseCardWidth;
        float scaleY = summaryCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int innerLeft = Scale(32, scaleX);
        int textLeft = Scale(74, scaleX);
        int labelWidth = Math.Max(160, summaryCard.Width - Scale(98, scaleX));
        int dividerWidth = Math.Max(120, summaryCard.Width - Scale(52, scaleX));
        int buttonX = Scale(24, scaleX);
        int buttonWidth = Math.Max(220, summaryCard.Width - Scale(48, scaleX));
        int buttonHeight = Math.Max(28, Scale(28, scaleY));
        int iconSize = Math.Max(32, Scale(32, scaleX));
        int titleHeight = Math.Max(24, Scale(24, scaleY));

        summaryTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 9F, 12.5F), FontStyle.Bold);
        summaryPerfilCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryPerfilValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summarySituacaoCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summarySituacaoValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summaryUsuariosCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryUsuariosValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        tipTextLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 12F));
        salvarButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        novoButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        excluirButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);

        SetBounds(summaryTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), iconSize, iconSize);
        SetBounds(summaryTitleLabel, Scale(52, scaleX), Scale(20, scaleY), Scale(200, scaleX), titleHeight);

        SetBounds(summaryDividerLabel, Scale(26, scaleX), Scale(56, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel2, Scale(26, scaleX), Scale(124, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel3, Scale(26, scaleX), Scale(178, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel4, Scale(26, scaleX), Scale(232, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel5, Scale(26, scaleX), Scale(286, scaleY), dividerWidth, 1);

        SetBounds(summaryPerfilIconLabel, innerLeft, Scale(78, scaleY), iconSize, iconSize);
        SetBounds(summaryPerfilCaptionLabel, textLeft, Scale(78, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryPerfilValueLabel, textLeft, Scale(98, scaleY), labelWidth, titleHeight);

        SetBounds(summarySituacaoIconLabel, innerLeft, Scale(132, scaleY), iconSize, iconSize);
        SetBounds(summarySituacaoCaptionLabel, textLeft, Scale(132, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summarySituacaoValueLabel, textLeft, Scale(152, scaleY), labelWidth, titleHeight);

        SetBounds(summaryUsuariosIconLabel, innerLeft, Scale(186, scaleY), iconSize, iconSize);
        SetBounds(summaryUsuariosCaptionLabel, textLeft, Scale(186, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryUsuariosValueLabel, textLeft, Scale(206, scaleY), labelWidth, titleHeight);

        SetBounds(tipTextLabel, textLeft, Scale(314, scaleY), Math.Max(180, summaryCard.Width - Scale(98, scaleX)), Math.Max(56, Scale(66, scaleY)));

        SetBounds(salvarButton, buttonX, Scale(402, scaleY), buttonWidth, buttonHeight);
        SetBounds(novoButton, buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        SetBounds(excluirButton, buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);
    }

    private void LayoutDetailsCard()
    {
        const int baseCardWidth = 557;
        const int baseCardHeight = 596;

        if (detailsCard.Width <= 0 || detailsCard.Height <= 0)
        {
            return;
        }

        float scaleX = detailsCard.Width / (float)baseCardWidth;
        float scaleY = detailsCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int leftPadding = Scale(24, scaleX);
        int rightPadding = Scale(24, scaleX);
        int cardInnerWidth = Math.Max(260, detailsCard.Width - leftPadding - rightPadding);

        int gap = Scale(30, scaleX);
        int fieldWidth = Math.Max(170, Math.Min(Scale(300, scaleX), (cardInnerWidth - gap) / 2));

        int layoutWidth = fieldWidth * 2 + gap;
        int col1 = leftPadding + Math.Max(0, (cardInnerWidth - layoutWidth) / 2);
        int col2 = col1 + fieldWidth + gap;

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblPeso, 7.75F, contentScale, 8.5F, 11F);
        //ApplyScaledFont(LblTipoTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblSetorTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(TxtPeso, 9F, contentScale, 9F, 12F);
        //ApplyScaledFont(CmbTipoTara, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(CmbSetor, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(label1, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(label2, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(textBox1, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(textBox2, 9F, contentScale, 9F, 12F);
        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        //CmbTipoTara.IntegralHeight = false;
        //CmbTipoTara.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        CmbSetor.IntegralHeight = false;
        CmbSetor.DropDownHeight = Math.Max(96, Scale(120, scaleY));

        SetBounds(detailsTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(detailsTitleLabel, Scale(52, scaleX), Scale(22, scaleY), Math.Max(190, Scale(220, scaleX)), Scale(24, scaleY));

        SetBounds(nomePerfilLabel, col1, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(nomePerfilInputPanel, col1, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(nomePerfilTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(situacaoLabel, col2, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(situacaoInputPanel, col2, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int situacaoComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight);
        int situacaoComboY = Math.Max(2, (situacaoInputPanel.Height - situacaoComboHeight) / 2);
        SetBounds(situacaoComboBox, Scale(12, scaleX), situacaoComboY, situacaoComboWidth, situacaoComboHeight);

        // Linha 2: Altura | Impressora
        SetBounds(label1, col1, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(roundedPanel2, col1, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(textBox1, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(LblPeso, col2, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(roundedPanel1, col2, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(TxtPeso, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        // Linha 3: Largura | Setor
        SetBounds(label2, col1, Scale(182, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(roundedPanel3, col1, Scale(204, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(textBox2, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(LblSetorTara, col2, Scale(182, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(RdpSetor, col2, Scale(204, scaleY), fieldWidth, Scale(33, scaleY));
        int setorComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int setorComboHeight = Math.Max(22, CmbSetor.PreferredHeight);
        int setorComboY = Math.Max(2, (RdpSetor.Height - setorComboHeight) / 2);
        SetBounds(CmbSetor, Scale(12, scaleX), setorComboY, setorComboWidth, setorComboHeight);

        // Linha 4: Descrição ocupando as duas colunas
        SetBounds(descricaoLabel, col1, Scale(246, scaleY), layoutWidth, Scale(19, scaleY));
        SetBounds(descricaoInputPanel, col1, Scale(269, scaleY), layoutWidth, Math.Max(95, Scale(108, scaleY)));
        SetBounds(descricaoTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(140, layoutWidth - Scale(24, scaleX)), Math.Max(60, descricaoInputPanel.Height - Scale(16, scaleY)));

        int dividerBottom = descricaoInputPanel.Bottom;
        SetBounds(detailsTopDividerLabel, col1, dividerBottom + Scale(12, scaleY), layoutWidth, 1);
    }

    private void LayoutProfilesCard()
    {
        const int baseCardWidth = 411;
        const int baseCardHeight = 596;

        if (profilesCard.Width <= 0 || profilesCard.Height <= 0)
        {
            return;
        }

        float scaleX = profilesCard.Width / (float)baseCardWidth;
        float scaleY = profilesCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int side = Scale(16, scaleX);
        int cardWidth = Math.Max(230, profilesCard.Width - side - side);
        int titleY = Scale(21, scaleY);

        profilesTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        profilesFooterLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11F));
        novoPerfilTextLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        searchTextBox.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 9F, 12F));
        profilesSearchIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(11F * contentScale, 11F, 14F));
        profilesHeaderProfileLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderUsersLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderStatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        searchTextBox.Multiline = false;

        SetBounds(profilesCheckMarkLabel, Scale(16, scaleX), Scale(19, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(profilesTitleLabel, Scale(43, scaleX), titleY, Scale(190, scaleX), Scale(24, scaleY));

        int quickH = Math.Max(31, Scale(31, scaleY));
        int iconW = Math.Max(20, Scale(20, scaleX));
        int iconLeft = Math.Max(10, Scale(10, scaleX));
        int textLeft = iconLeft + iconW + Math.Max(4, Scale(4, scaleX));
        int horizontalPadding = Math.Max(8, Scale(8, scaleX));
        int minQuickW = Math.Max(142, Scale(142, scaleX));
        int textWidth = TextRenderer.MeasureText(novoPerfilTextLabel.Text, novoPerfilTextLabel.Font).Width;
        int quickW = Math.Max(minQuickW, textLeft + textWidth + horizontalPadding);

        SetBounds(novoPerfilButtonPanel, Math.Max(side, profilesCard.Width - side - quickW), Scale(16, scaleY), quickW, quickH);
        SetBounds(novoPerfilIconLabel, iconLeft, Math.Max(3, (quickH - Math.Max(18, Scale(21, scaleY))) / 2), iconW, Math.Max(18, Scale(21, scaleY)));
        SetBounds(novoPerfilTextLabel, textLeft, Math.Max(3, (quickH - Math.Max(18, Scale(21, scaleY))) / 2), Math.Max(72, quickW - textLeft - horizontalPadding), Math.Max(18, Scale(21, scaleY)));

        int searchY = Scale(60, scaleY);
        int searchH = Math.Max(34, Scale(34, scaleY));
        SetBounds(profilesSearchPanel, side, searchY, cardWidth, searchH);
        int searchIconWidth = Math.Max(20, Scale(22, scaleX));
        int searchIconHeight = Math.Max(20, Scale(24, scaleY));
        int searchTextHeight = Math.Max(16, searchTextBox.PreferredHeight);
        int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextHeight) / 2);
        int searchIconY = Math.Max(2, searchTextY + ((searchTextHeight - searchIconHeight) / 2));
        SetBounds(profilesSearchIconLabel, Scale(8, scaleX), searchIconY, searchIconWidth, searchIconHeight);
        SetBounds(searchTextBox, Scale(36, scaleX), searchTextY, Math.Max(120, cardWidth - Scale(44, scaleX)), searchTextHeight);

        int tableY = Scale(108, scaleY);
        int footerY = Math.Max(tableY + 180, profilesCard.Height - Scale(28, scaleY));
        int tableH = Math.Max(220, footerY - tableY - Scale(10, scaleY));
        SetBounds(profilesTablePanel, side, tableY, cardWidth, tableH);

        int nameX = Scale(12, scaleX);
        int usersX = Math.Max(Scale(150, scaleX), cardWidth - Scale(185, scaleX));
        int statusPanelW = Math.Max(50, Scale(58, scaleX));
        int statusPanelX = Math.Max(Scale(250, scaleX), cardWidth - Scale(3, scaleX) - statusPanelW - Scale(10, scaleX));

        SetBounds(profilesHeaderProfileLabel, Scale(16, scaleX), Scale(9, scaleY), Math.Max(120, usersX - Scale(26, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderUsersLabel, usersX, Scale(9, scaleY), Math.Max(90, statusPanelX - usersX - Scale(8, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderStatusLabel, statusPanelX, Scale(9, scaleY), statusPanelW, Scale(20, scaleY));

        int rowWidth = Math.Max(200, cardWidth - Scale(6, scaleX));
        int rowHeight = Math.Max(40, Scale(46, scaleY));
        int baseRowY = Scale(34, scaleY);

        SetBounds(profileRow1Panel, Scale(3, scaleX), baseRowY + rowHeight * 0, rowWidth, rowHeight);
        SetBounds(profileRow2Panel, Scale(3, scaleX), baseRowY + rowHeight * 1, rowWidth, rowHeight);
        SetBounds(profileRow3Panel, Scale(3, scaleX), baseRowY + rowHeight * 2, rowWidth, rowHeight);
        SetBounds(profileRow4Panel, Scale(3, scaleX), baseRowY + rowHeight * 3, rowWidth, rowHeight);
        SetBounds(profileRow5Panel, Scale(3, scaleX), baseRowY + rowHeight * 4, rowWidth, rowHeight);
        UpdateMarkerSizes(Math.Max(3, Scale(3, scaleX)));

        Label[] names = [profileRow1NameLabel, profileRow2NameLabel, profileRow3NameLabel, profileRow4NameLabel, profileRow5NameLabel];
        Label[] users = [profileRow1UsersLabel, profileRow2UsersLabel, profileRow3UsersLabel, profileRow4UsersLabel, profileRow5UsersLabel];
        RoundedPanel[] statusPanels = [profileRow1StatusPanel, profileRow2StatusPanel, profileRow3StatusPanel, profileRow4StatusPanel, profileRow5StatusPanel];
        Label[] statusLabels = [profileRow1StatusLabel, profileRow2StatusLabel, profileRow3StatusLabel, profileRow4StatusLabel, profileRow5StatusLabel];

        for (int i = 0; i < names.Length; i++)
        {
            names[i].Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            users[i].Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            statusLabels[i].Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold);

            SetBounds(names[i], nameX, Scale(11, scaleY), Math.Max(90, usersX - nameX - Scale(8, scaleX)), Scale(24, scaleY));
            SetBounds(users[i], usersX, Scale(11, scaleY), Math.Max(60, statusPanelX - usersX - Scale(8, scaleX)), Scale(24, scaleY));
            SetBounds(statusPanels[i], statusPanelX, Scale(11, scaleY), statusPanelW, Scale(24, scaleY));
            statusLabels[i].Dock = DockStyle.Fill;
        }

        SetBounds(profilesFooterLabel, Scale(20, scaleX), footerY, Math.Max(180, Scale(220, scaleX)), Scale(22, scaleY));
        ApplyProfilesFilter();
    }

    private static void ApplyScaledFont(Control control, float baseSize, float scale, float minSize, float maxSize)
    {
        float size = Math.Clamp(baseSize * scale, minSize, maxSize);
        if (Math.Abs(control.Font.Size - size) < 0.01f)
        {
            return;
        }

        control.Font = new Font(control.Font.FontFamily, size, control.Font.Style);
    }

    private void InitializeProfilesTableSelection()
    {
        _rowSelections.Clear();
        _rowSelections.Add(new RowSelection(profileRow1Panel, Color.White));
        _rowSelections.Add(new RowSelection(profileRow2Panel, Color.White));
        _rowSelections.Add(new RowSelection(profileRow3Panel, Color.White));
        _rowSelections.Add(new RowSelection(profileRow4Panel, Color.White));
        _rowSelections.Add(new RowSelection(profileRow5Panel, Color.White));

        foreach (RowSelection row in _rowSelections)
        {
            row.RowPanel.BackColor = row.NormalBackColor;
            SetHandCursor(row.RowPanel);
            AttachRowSelectionHandlers(row.RowPanel, row.RowPanel);
        }

        EnsureRowSelectionMarkers();
        HideAllMarkers();
    }

    private void AttachRowSelectionHandlers(Control control, Panel rowPanel)
    {
        control.Click += (_, _) => SetSelectedRow(rowPanel);

        foreach (Control child in control.Controls)
        {
            AttachRowSelectionHandlers(child, rowPanel);
        }
    }

    private void SetSelectedRow(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (RowSelection row in _rowSelections)
        {
            bool isSelected = row.RowPanel == selectedRowPanel;
            row.RowPanel.BackColor = isSelected ? selectedBackColor : row.NormalBackColor;
            if (isSelected)
            {
                ShowMarkerForRow(row.RowPanel);
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }

        SyncSummaryPerfilFromRow(selectedRowPanel);
    }

    private void SyncSummaryPerfilFromRow(Panel rowPanel)
    {
        ProfileSearchRow? row = _profileSearchRows.FirstOrDefault(x => x.RowPanel == rowPanel);
        if (row is null)
        {
            ClearSummarySelectionValues();
            return;
        }

        summaryPerfilValueLabel.Text = row.NameLabel.Text;
        summarySituacaoValueLabel.Text = row.StatusLabel.Text;
        summaryUsuariosValueLabel.Text = row.UsersLabel.Text;
    }

    private void ClearSummarySelectionValues()
    {
        summaryPerfilValueLabel.Text = "-";
        summarySituacaoValueLabel.Text = "-";
        summaryUsuariosValueLabel.Text = "-";
    }

    private void EnsureRowSelectionMarkers()
    {
        if (_rowSelectionMarkers.Count > 0)
        {
            return;
        }

        foreach (RowSelection row in _rowSelections)
        {
            Panel marker = new()
            {
                Name = $"{row.RowPanel.Name}MarkerPanel",
                BackColor = MarkerColor,
                Size = new Size(3, row.RowPanel.Height),
                Visible = false
            };
            row.RowPanel.Controls.Add(marker);

            _rowSelectionMarkers[row.RowPanel] = marker;
        }
    }

    private void UpdateMarkerSizes(int markerWidth)
    {
        foreach (RowSelection row in _rowSelections)
        {
            if (_rowSelectionMarkers.TryGetValue(row.RowPanel, out Panel? marker))
            {
                marker.Size = new Size(markerWidth, row.RowPanel.Height);
                if (marker.Visible)
                {
                    marker.Bounds = new Rectangle(0, 0, markerWidth, row.RowPanel.Height);
                    marker.BringToFront();
                }
            }
        }
    }

    private void ShowMarkerForRow(Panel rowPanel)
    {
        if (!_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            return;
        }

        marker.Parent = rowPanel;
        marker.Bounds = new Rectangle(0, 0, marker.Width, rowPanel.Height);
        marker.Visible = true;
        marker.BringToFront();
    }

    private void HideMarkerForRow(Panel rowPanel)
    {
        if (_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            marker.Visible = false;
        }
    }

    private void HideAllMarkers()
    {
        foreach (Panel marker in _rowSelectionMarkers.Values)
        {
            marker.Visible = false;
        }
    }


    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarEtiquetaAsync();
        novoButton.Click += (_, _) => PrepareNewEtiqueta();
        excluirButton.Click += async (_, _) => await ExcluirEtiquetaAsync();
    }

    private async Task SalvarEtiquetaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integracao com banco desabilitada no momento.", "Cadastro de Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        EtiquetaCadastro etiqueta = new()
        {
            IdModeloEtiqueta = TryParseLong(CmbSetor.SelectedValue),
            Codigo = textBox1.Text.Trim(),
            Nome = nomePerfilTextBox.Text.Trim(),
            TipoEtiqueta = textBox2.Text.Trim(),
            Descricao = descricaoTextBox.Text.Trim(),
            Ativo = situacaoComboBox.Text.Equals("Ativo", StringComparison.OrdinalIgnoreCase)
        };

        ResultadoOperacao resultado = await _etiquetaController.InserirAsync(etiqueta);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            PrepareNewEtiqueta();
        }
    }

    private async Task ExcluirEtiquetaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integracao com banco desabilitada no momento.", "Cadastro de Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idEtiquetaAtual <= 0)
        {
            MessageBox.Show("Selecione uma etiqueta para excluir.", "Cadastro de Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ResultadoOperacao resultado = await _etiquetaController.ExcluirAsync(_idEtiquetaAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            PrepareNewEtiqueta();
        }
    }

    private static long TryParseLong(object? value)
    {
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is string s && long.TryParse(s, out long p)) return p;
        return 0;
    }    private sealed class RowSelection
    {
        public RowSelection(Panel rowPanel, Color normalBackColor)
        {
            RowPanel = rowPanel;
            NormalBackColor = normalBackColor;
        }

        public Panel RowPanel { get; }

        public Color NormalBackColor { get; }
    }

    private sealed class ProfileSearchRow
    {
        public ProfileSearchRow(Panel rowPanel, Label nameLabel, Label usersLabel, Label statusLabel)
        {
            RowPanel = rowPanel;
            NameLabel = nameLabel;
            UsersLabel = usersLabel;
            StatusLabel = statusLabel;
        }

        public Panel RowPanel { get; }
        public Label NameLabel { get; }
        public Label UsersLabel { get; }
        public Label StatusLabel { get; }
    }
}















