using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class CadastroUsuarioForm : Form
{
    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private System.Windows.Forms.Timer? _footerClockTimer;
    private readonly List<UserRowSelection> _userRowSelections = new();
    private readonly Dictionary<Panel, Panel> _rowSelectionMarkers = new();
    private readonly List<ProfileSearchRow> _profileSearchRows = new();
    private readonly Dictionary<Panel, UserSummaryData> _userSummaryByRow = new();
    private readonly Dictionary<Panel, long> _idUsuarioPorLinha = new();
    private readonly Dictionary<Panel, UsuarioCadastro> _usuarioPorLinha = new();
    private readonly UsuarioController _usuarioController;
    private readonly CargoController _cargoController;
    private readonly SetorController _setorController;
    private readonly PerfilAcessoController _perfilAcessoController;
    private long _idUsuarioAtual;
    private Button? _alterarSenhaButton;

    public CadastroUsuarioForm(UsuarioController? usuarioController = null)
    {
        _usuarioController = usuarioController ?? FabricaControladoresCadastro.CriarUsuarioController();
        _cargoController = FabricaControladoresCadastro.CriarCargoController();
        _setorController = FabricaControladoresCadastro.CriarSetorController();
        _perfilAcessoController = FabricaControladoresCadastro.CriarPerfilAcessoController();
        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        LoadWindowIcon();
        ApplyDesignTimeSampleData();
        ConfigureCustomTitleBar();
        ConfigureFooterDate();
        ConfigurePhotoPanelResponsiveLayout();
        ConfigureUploadImagePicker();
        EnsureFormCardTextBoxesEditable();
        ConfigureProfilesSearchFilter();
        InitializeProfilesTableSelection();
        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        formCard.Resize += (_, _) => LayoutFormCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();
        Shown += (_, _) => FocusProfilesSearch();
        LayoutProfilesCard();
        LayoutFormCard();
        LayoutSummaryCard();
        ApplyProfilesFilter();
        ConectarAcoesCadastro();
        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) =>
            {
                await CarregarCombosCadastroAsync();
                await CarregarUsuariosAsync();
            };
        }

        if (IsDesignerHost())
        {
            ApplyDesignerFormGridSamples();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void LoadWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconPath);

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private void ApplyDesignTimeSampleData()
    {
        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
        {
            return;
        }

        nomeTextBox.Text = "João da Silva";
        loginTextBox.Text = "jsilva";
        emailTextBox.Text = "joao@empresa.com.br";
        confirmarSenhaTextBox.Text = "••••••••";
        senhaTextBox.Text = "••••••••";
        perfilComboBox.Text = "Operador";
        cargoComboBox.Text = "Analista";
        setorComboBox.Text = "Produção";
        situacaoComboBox.Text = "Ativo";
    }

    private static bool IsDesignerHost()
    {
        if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
        {
            return true;
        }

        string processName = Process.GetCurrentProcess().ProcessName;
        return processName.Equals("devenv", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyDesignerFormGridSamples()
    {
        nomeTextBox.Text = "João da Silva";
        loginTextBox.Text = "jsilva";
        emailTextBox.Text = "joao@empresa.com.br";
        confirmarSenhaTextBox.Text = "********";
        senhaTextBox.Text = "********";

        perfilComboBox.SelectedIndex = 0;
        cargoComboBox.SelectedIndex = 1;
        setorComboBox.SelectedIndex = 0;
        situacaoComboBox.SelectedIndex = 0;
    }

    private void ConfigureCustomTitleBar()
    {
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        menuHeaderLabel.Click += (_, _) => ReturnToCadastroModules();
        customTitleBarPanel.Resize += (_, _) => AlignHeaderRightControls();

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));

        voltarButton.Click += (_, _) => ReturnToCadastroModules();
        limparButton.Click += (_, _) => ClearFields();

        AlignHeaderRightControls();
    }

    private void ReturnToCadastroModules()
    {
        Close();
    }

    private void CustomTitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void AlignHeaderRightControls()
    {
        int right = customTitleBarPanel.ClientSize.Width;

        closeWindowLabel.Location = new Point(right - 52, 0);
        maximizeWindowLabel.Location = new Point(right - 100, 0);
        minimizeWindowLabel.Location = new Point(right - 148, 0);
        sapStatusPanel.Location = new Point(right - 456, 12);

        sapStatusPanel.BringToFront();
        minimizeWindowLabel.BringToFront();
        maximizeWindowLabel.BringToFront();
        closeWindowLabel.BringToFront();
    }

    private static void ConfigureTitleButtonHover(Label button, Color hoverColor)
    {
        Color normalColor = button.BackColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = normalColor;
    }

    private void ConfigureFooterDate()
    {
        UpdateFooterDateTime();

        _footerClockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _footerClockTimer.Tick += (_, _) => UpdateFooterDateTime();
        _footerClockTimer.Start();
    }

    private void ConfigurePhotoPanelResponsiveLayout()
    {
        photoPanel.Resize += (_, _) => AlignPhotoPanelContent();
        AlignPhotoPanelContent();
    }

    private void AlignPhotoPanelContent()
    {
        int panelWidth = photoPanel.ClientSize.Width;

        avatarIconLabel.Left = (panelWidth - avatarIconLabel.Width) / 2;
        avatarTitleLabel.Left = (panelWidth - avatarTitleLabel.Width) / 2;

        uploadButtonPanel.Left = (panelWidth - uploadButtonPanel.Width) / 2;

        uploadIconLabel.Left = 10;

        uploadTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        uploadTextLabel.Left = 42;
        uploadTextLabel.Width = Math.Max(0, uploadButtonPanel.Width - 48);

        uploadHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        uploadHintLabel.Left = 42;
        uploadHintLabel.Width = Math.Max(0, uploadButtonPanel.Width - 48);
    }

    private void ConfigureUploadImagePicker()
    {
        SetHandCursor(uploadButtonPanel);
        AttachUploadImageClick(uploadButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;
        foreach (Control child in control.Controls)
        {
            SetHandCursor(child);
        }
    }

    private void AttachUploadImageClick(Control control)
    {
        control.Click += (_, _) => OpenImagePicker();

        foreach (Control child in control.Controls)
        {
            AttachUploadImageClick(child);
        }
    }

    private void OpenImagePicker()
    {
        string userDownloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        string fallbackDownloads = @"C:\Downloads";
        string initialDir = Directory.Exists(userDownloads)
            ? userDownloads
            : (Directory.Exists(fallbackDownloads) ? fallbackDownloads : @"C:\");

        using OpenFileDialog dialog = new()
        {
            Title = "Selecionar imagem de usuário",
            InitialDirectory = initialDir,
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos os arquivos|*.*",
            FilterIndex = 1,
            RestoreDirectory = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        uploadHintLabel.Text = Path.GetFileName(dialog.FileName);
    }

    private void UpdateFooterDateTime()
    {
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        DateTime now = DateTime.Now;
        cellDataText.Text = now.ToString("dd/MM/yyyy", ptBr);
        cellHoraText.Text = now.ToString("HH:mm", ptBr);
    }

    private void ClearFields()
    {
        nomeTextBox.Clear();
        loginTextBox.Clear();
        emailTextBox.Clear();
        senhaTextBox.Clear();
        confirmarSenhaTextBox.Clear();
        cargoComboBox.SelectedIndex = -1;
        setorComboBox.SelectedIndex = -1;
        perfilComboBox.SelectedIndex = -1;
    }

    private async Task CarregarCombosCadastroAsync()
    {
        IReadOnlyList<CargoCadastro> cargos = (await _cargoController.ListarAsync())
            .Where(c => c.SituacaoCargo)
            .OrderBy(c => c.NomeCargo)
            .ToList();

        IReadOnlyList<SetorCadastro> setores = (await _setorController.ListarAsync())
            .Where(s => s.SituacaoSetor)
            .OrderBy(s => s.NomeSetor)
            .ToList();

        IReadOnlyList<PerfilAcessoCadastro> perfis = (await _perfilAcessoController.ListarAsync())
            .Where(p => p.SituacaoPerfilAcesso)
            .OrderBy(p => p.NomePerfilAcesso)
            .ToList();

        ConfigurarCombo(cargoComboBox, cargos, "NomeCargo", "IdCargo");
        ConfigurarCombo(setorComboBox, setores, "NomeSetor", "CodigoSetor");
        ConfigurarCombo(perfilComboBox, perfis, "NomePerfilAcesso", "IdPerfilAcesso");
    }

    private static void ConfigurarCombo<T>(ComboBox comboBox, IReadOnlyList<T> dados, string displayMember, string valueMember)
    {
        comboBox.DataSource = null;
        comboBox.DisplayMember = displayMember;
        comboBox.ValueMember = valueMember;
        comboBox.DataSource = dados;
        comboBox.SelectedIndex = dados.Count > 0 ? 0 : -1;
    }

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarUsuarioAsync();
        novoButton.Click += (_, _) =>
        {
            _idUsuarioAtual = 0;
            ClearFields();
            AtualizarRotuloSalvar();
        };
        excluirButton.Click += async (_, _) => await ExcluirUsuarioAsync();

        // Botao "Alterar Senha" criado em runtime: fluxo proprio, separado da edicao de dados.
        _alterarSenhaButton = new Button
        {
            Name = "alterarSenhaButton",
            Text = "Alterar Senha",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(31, 41, 55),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _alterarSenhaButton.Click += async (_, _) => await AlterarSenhaUsuarioAsync();
        summaryCard.Controls.Add(_alterarSenhaButton);
        _alterarSenhaButton.BringToFront();

        AtualizarRotuloSalvar();
    }

    // Rotulo do botao salvar reflete o modo (novo x edicao) e mostra/esconde "Alterar Senha".
    private void AtualizarRotuloSalvar()
    {
        bool edicao = _idUsuarioAtual > 0;
        salvarButton.Text = edicao ? "Salvar Alterações" : "Salvar Usuário";
        if (_alterarSenhaButton is not null)
        {
            _alterarSenhaButton.Visible = edicao;
        }
    }

    private async Task SalvarUsuarioAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        long? idCargo = ObterValorLongoSelecionado(cargoComboBox);
        long? idSetor = ObterValorLongoSelecionado(setorComboBox);
        long? idPerfil = ObterValorLongoSelecionado(perfilComboBox);

        if (string.IsNullOrWhiteSpace(nomeTextBox.Text))
        {
            MessageBox.Show("Informe o nome do usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(loginTextBox.Text))
        {
            MessageBox.Show("Informe o login do usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (idCargo is null or <= 0)
        {
            MessageBox.Show("Selecione um cargo para o usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (idSetor is null or <= 0)
        {
            MessageBox.Show("Selecione um setor padrao para o usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (idPerfil is null or <= 0)
        {
            MessageBox.Show("Selecione um perfil de acesso para o usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_idUsuarioAtual <= 0)
        {
            await InserirUsuarioAsync(idCargo, idSetor.Value, idPerfil.Value);
        }
        else
        {
            await AtualizarUsuarioAsync(idCargo, idSetor.Value, idPerfil.Value);
        }
    }

    // FLUXO 1a: NOVO usuario -> exige senha (a tela nunca monta hash; SenhaServico gera).
    private async Task InserirUsuarioAsync(long? idCargo, long idSetor, long idPerfil)
    {
        string senha = senhaTextBox.Text.Trim();
        string confirmarSenha = confirmarSenhaTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(senha))
        {
            MessageBox.Show("Informe a senha para o novo usuario.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.Equals(senha, confirmarSenha, StringComparison.Ordinal))
        {
            MessageBox.Show("Senha e confirmacao de senha precisam ser iguais.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UsuarioCadastro usuario = new()
        {
            IdCargo = idCargo,
            IdSetorPadrao = idSetor,
            NomeUsuario = nomeTextBox.Text.Trim(),
            LoginUsuario = loginTextBox.Text.Trim(),
            EmailUsuario = emailTextBox.Text.Trim(),
            TelefoneUsuario = string.Empty,
            SituacaoUsuario = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase),
            DeveTrocarSenha = true,
            BloqueadoUsuario = false
        };

        var resultado = await _usuarioController.InserirComVinculosAsync(usuario, senha, idPerfil, idSetor);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Usuario", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idUsuarioAtual = 0;
            ClearFields();
            AtualizarRotuloSalvar();
            await CarregarUsuariosAsync();
        }
    }

    // FLUXO 1b: EDITAR dados basicos -> NAO exige senha. Sincroniza perfil e setor padrao.
    private async Task AtualizarUsuarioAsync(long? idCargo, long idSetor, long idPerfil)
    {
        // Preserva campos que a tela nao edita (telefone, deve_trocar_senha, bloqueado).
        UsuarioCadastro? original = await _usuarioController.ObterPorIdAsync(_idUsuarioAtual);
        if (original is null)
        {
            MessageBox.Show("Usuario nao encontrado para edicao.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UsuarioCadastro usuario = new()
        {
            IdUsuario = _idUsuarioAtual,
            IdCargo = idCargo,
            IdSetorPadrao = idSetor,
            NomeUsuario = nomeTextBox.Text.Trim(),
            LoginUsuario = loginTextBox.Text.Trim(),
            EmailUsuario = emailTextBox.Text.Trim(),
            TelefoneUsuario = original.TelefoneUsuario,
            SituacaoUsuario = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase),
            DeveTrocarSenha = original.DeveTrocarSenha,
            BloqueadoUsuario = original.BloqueadoUsuario
        };

        var resultado = await _usuarioController.AtualizarComVinculosAsync(usuario, idPerfil, idSetor);
        if (!resultado.Sucesso)
        {
            MessageBox.Show(resultado.Mensagem, "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(resultado.Mensagem, "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await CarregarUsuariosAsync();
    }

    // FLUXO 3: alterar senha em fluxo proprio (so em edicao). Usa os campos de senha da tela.
    private async Task AlterarSenhaUsuarioAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idUsuarioAtual <= 0)
        {
            MessageBox.Show("Selecione um usuario para alterar a senha.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string senha = senhaTextBox.Text.Trim();
        string confirmarSenha = confirmarSenhaTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(senha))
        {
            MessageBox.Show("Informe a nova senha nos campos de senha.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.Equals(senha, confirmarSenha, StringComparison.Ordinal))
        {
            MessageBox.Show("Senha e confirmacao de senha precisam ser iguais.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var resultado = await _usuarioController.AlterarSenhaAsync(
            _idUsuarioAtual,
            senha,
            exigirTrocaNoProximoLogin: true);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Usuario", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            senhaTextBox.Clear();
            confirmarSenhaTextBox.Clear();
        }
    }

    private async Task ExcluirUsuarioAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idUsuarioAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione um usuario para inativar.", "Cadastro de Usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a inativacao do usuario atual?\n\nO usuario ficara inativo e nao podera mais fazer login, mas pode ser reativado depois.",
            "Cadastro de Usuario",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        var resultado = await _usuarioController.ExcluirAsync(_idUsuarioAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Usuario", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idUsuarioAtual = 0;
            ClearFields();
            await CarregarUsuariosAsync();
        }
    }

    private static long? ObterValorLongoSelecionado(ComboBox comboBox)
    {
        return comboBox.SelectedValue switch
        {
            long valor => valor,
            int valor => valor,
            short valor => valor,
            string texto when long.TryParse(texto, out long valor) => valor,
            _ => null
        };
    }

    private async Task CarregarUsuariosAsync()
    {
        IReadOnlyList<UsuarioCadastro> usuarios = await _usuarioController.ListarAsync();
        (Panel RowPanel, Label LoginLabel, Label CargoLabel, Label StatusLabel)[] linhas =
        [
            (profileRow1Panel, profileRow1LoginLabel, profileRow1CargoLabel, profileRow1StatusLabel),
            (profileRow2Panel, profileRow2LoginLabel, profileRow2CargoLabel, profileRow2StatusLabel),
            (profileRow3Panel, profileRow3LoginLabel, profileRow3CargoLabel, profileRow3StatusLabel),
            (profileRow4Panel, profileRow4LoginLabel, profileRow4CargoLabel, profileRow4StatusLabel)
        ];

        _idUsuarioPorLinha.Clear();
        _usuarioPorLinha.Clear();
        _userSummaryByRow.Clear();

        for (int i = 0; i < linhas.Length; i++)
        {
            if (i < usuarios.Count)
            {
                UsuarioCadastro usuario = usuarios[i];
                linhas[i].LoginLabel.Text = usuario.LoginUsuario;
                linhas[i].CargoLabel.Text = "-";
                linhas[i].StatusLabel.Text = usuario.SituacaoUsuario ? "Ativo" : "Inativo";
                _idUsuarioPorLinha[linhas[i].RowPanel] = usuario.IdUsuario;
                _usuarioPorLinha[linhas[i].RowPanel] = usuario;
                _userSummaryByRow[linhas[i].RowPanel] = new UserSummaryData(
                    usuario.NomeUsuario,
                    usuario.LoginUsuario,
                    usuario.SituacaoUsuario ? "Ativo" : "Inativo",
                    "-",
                    "-",
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                continue;
            }

            linhas[i].LoginLabel.Text = "-";
            linhas[i].CargoLabel.Text = "-";
            linhas[i].StatusLabel.Text = "-";
        }
    }

    private void EnsureFormCardTextBoxesEditable()
    {
        TextBox[] fields =
        [
            nomeTextBox,
            loginTextBox,
            emailTextBox,
            senhaTextBox,
            confirmarSenhaTextBox
        ];

        foreach (TextBox field in fields)
        {
            field.Enabled = true;
            field.ReadOnly = false;
            field.ShortcutsEnabled = true;
            field.TabStop = true;
        }
    }

    private void ConfigureProfilesSearchFilter()
    {
        _profileSearchRows.Clear();
        _profileSearchRows.Add(new ProfileSearchRow(profileRow1Panel, profileRow1LoginLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow2Panel, profileRow2LoginLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow3Panel, profileRow3LoginLabel));
        _profileSearchRows.Add(new ProfileSearchRow(profileRow4Panel, profileRow4LoginLabel));

        profilesSearchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void FocusProfilesSearch()
    {
        profilesSearchTextBox.Focus();
        profilesSearchTextBox.SelectAll();
    }

    private void ApplyProfilesFilter()
    {
        string query = NormalizeForSearch(profilesSearchTextBox.Text.Trim());
        int visibleIndex = 0;

        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            string rowName = NormalizeForSearch(row.NameLabel.Text);
            bool match = string.IsNullOrWhiteSpace(query) || rowName.Contains(query, StringComparison.Ordinal);
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

    private void InitializeProfilesTableSelection()
    {
        _userRowSelections.Clear();
        _userRowSelections.Add(new UserRowSelection(profileRow1Panel, profileRow1Panel.BackColor));
        _userRowSelections.Add(new UserRowSelection(profileRow2Panel, profileRow2Panel.BackColor));
        _userRowSelections.Add(new UserRowSelection(profileRow3Panel, profileRow3Panel.BackColor));
        _userRowSelections.Add(new UserRowSelection(profileRow4Panel, profileRow4Panel.BackColor));

        foreach (UserRowSelection row in _userRowSelections)
        {
            AttachRowSelectionHandlers(row.RowPanel, row.RowPanel);
        }

        _userSummaryByRow.Clear();
        _userSummaryByRow[profileRow1Panel] = new UserSummaryData("Jéssica Silva", "jessica.silva", "Ativo", "Operador", "Produção", "13/05/2026 13:37");
        _userSummaryByRow[profileRow2Panel] = new UserSummaryData("Marceli Oliveira", "marceli.oliveira", "Ativo", "Analista", "PCP", "12/05/2026 09:10");
        _userSummaryByRow[profileRow3Panel] = new UserSummaryData("Cristina Santos", "cristina.santos", "Inativo", "Supervisor", "Qualidade", "08/05/2026 16:44");
        _userSummaryByRow[profileRow4Panel] = new UserSummaryData("Michael Bezerra", "michael.bezerra", "Ativo", "Administrador", "TI", "10/05/2026 11:25");

        EnsureRowSelectionMarkers();
        ClearRowSelection();
    }

    private void AttachRowSelectionHandlers(Control control, Panel rowPanel)
    {
        control.Click += (_, _) => SetSelectedProfileRow(rowPanel);

        foreach (Control child in control.Controls)
        {
            AttachRowSelectionHandlers(child, rowPanel);
        }
    }

    private void SetSelectedProfileRow(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (UserRowSelection row in _userRowSelections)
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

        _idUsuarioAtual = _idUsuarioPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposUsuarioPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
    }

    private void PreencherCamposUsuarioPorLinha(Panel rowPanel)
    {
        if (!_usuarioPorLinha.TryGetValue(rowPanel, out UsuarioCadastro? usuario))
        {
            return;
        }

        nomeTextBox.Text = usuario.NomeUsuario;
        loginTextBox.Text = usuario.LoginUsuario;
        emailTextBox.Text = usuario.EmailUsuario;
        situacaoComboBox.Text = usuario.SituacaoUsuario ? "Ativo" : "Inativo";

        // Senha NUNCA e exibida; campos ficam vazios e so sao usados no fluxo "Alterar Senha".
        senhaTextBox.Text = string.Empty;
        confirmarSenhaTextBox.Text = string.Empty;

        SelecionarValorCombo(cargoComboBox, usuario.IdCargo);
        SelecionarValorCombo(setorComboBox, usuario.IdSetorPadrao);

        // Perfil ativo do usuario (carregado de forma assincrona).
        _ = CarregarPerfilAtivoDoUsuarioAsync(usuario.IdUsuario);

        AtualizarRotuloSalvar();
    }

    private async Task CarregarPerfilAtivoDoUsuarioAsync(long idUsuario)
    {
        try
        {
            IReadOnlyList<UsuarioPerfilCadastro> perfis = await _usuarioController.ListarPerfisAsync(idUsuario);
            UsuarioPerfilCadastro? ativo = perfis.FirstOrDefault(p => p.Ativo);
            if (ativo is not null)
            {
                SelecionarValorCombo(perfilComboBox, ativo.IdPerfilAcesso);
            }
            else
            {
                perfilComboBox.SelectedIndex = -1;
            }
        }
        catch
        {
            // Falha ao carregar perfil nao deve travar a selecao.
        }
    }

    private static void SelecionarValorCombo(ComboBox comboBox, long? valor)
    {
        if (valor is null or <= 0)
        {
            comboBox.SelectedIndex = -1;
            return;
        }

        comboBox.SelectedValue = valor.Value;
    }

    private void LayoutFormCard()
    {
        const int baseCardWidth = 559;
        const int baseCardHeight = 602;

        if (formCard.Width <= 0 || formCard.Height <= 0)
        {
            return;
        }

        float scaleX = formCard.Width / (float)baseCardWidth;
        float scaleY = formCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int side = Scale(24, scaleX);
        int contentW = Math.Max(320, formCard.Width - side - side);

        formTitleLabel.Font = new Font("Segoe UI", Math.Clamp(12F * contentScale, 11F, 15F), FontStyle.Bold);
        formTitleIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(16F * contentScale, 14F, 19F));
        permissionsTitleLabel.Font = new Font("Segoe UI", Math.Clamp(9.5F * contentScale, 9F, 12F), FontStyle.Bold);
        permissionsTitleIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(13F * contentScale, 12F, 16F));
        enviarEmailLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 11F));
        limparButton.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8F, 10.5F), FontStyle.Bold);
        voltarButton.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8F, 10.5F), FontStyle.Bold);

        SetBounds(formTitleIconLabel, side, Scale(18, scaleY), Scale(28, scaleX), Scale(28, scaleY));
        SetBounds(formTitleLabel, side + Scale(34, scaleX), Scale(19, scaleY), Math.Max(220, Scale(260, scaleX)), Scale(26, scaleY));
        SetBounds(formTitleDivider, side, Scale(55, scaleY), contentW, 1);

        int gridTop = Scale(68, scaleY);
        int gridHeight = Math.Max(280, Math.Min(360, Scale(300, scaleY)));
        SetBounds(formGrid, side, gridTop, contentW, gridHeight);

        // Let fields naturally expand with the grid width.
        foreach (Control control in new Control[]
                 { nomeInputPanel, loginInputPanel, emailInputPanel, confirmarSenhaInputPanel, senhaInputPanel, perfilInputPanel, cargoInputPanel, situacaoInputPanel, photoPanel })
        {
            control.Dock = DockStyle.Fill;
        }

        // Keep setor aligned with the same visual structure of sibling inputs.
        setorInputPanel.Dock = DockStyle.None;
        setorInputPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        setorInputPanel.Width = nomeInputPanel.Width;
        setorInputPanel.Height = nomeInputPanel.Height;

        int afterGridY = formGrid.Bottom + Scale(6, scaleY);
        SetBounds(permissionsDivider, side, afterGridY, contentW, 1);
        SetBounds(permissionsTitleIconLabel, side + Scale(4, scaleX), afterGridY + Scale(8, scaleY), Scale(24, scaleX), Scale(24, scaleY));
        SetBounds(permissionsTitleLabel, side + Scale(34, scaleX), afterGridY + Scale(8, scaleY), Math.Max(180, Scale(240, scaleX)), Scale(24, scaleY));

        int actionsTop = formCard.Height - Scale(108, scaleY);
        int actionsHeight = Math.Max(84, formCard.Height - actionsTop - Scale(14, scaleY));
        SetBounds(actionsPanel, side, actionsTop, contentW, actionsHeight);

        LayoutCargoInputContent(scaleX, scaleY);
        LayoutSetorInputContent(scaleX, scaleY);
        LayoutPerfilInputContent(scaleX, scaleY);
        LayoutSituacaoInputContent(scaleX, scaleY);
        LayoutNomeInputContent(scaleX, scaleY);
        LayoutLoginInputContent(scaleX, scaleY);
        LayoutEmailInputContent(scaleX, scaleY);
        LayoutConfirmarSenhaInputContent(scaleX, scaleY);
        LayoutSenhaInputContent(scaleX, scaleY);
    }

    private static void LayoutTextInputContent(RoundedPanel panel, Label iconLabel, TextBox textBox, float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int iconLeft = Scale(8, scaleX);
        int iconSize = Math.Max(16, Scale(20, Math.Min(scaleX, scaleY)));
        int iconTop = Math.Max(2, (panel.Height - iconSize) / 2);

        int textLeft = Scale(38, scaleX);
        int textHeight = Math.Max(16, textBox.PreferredHeight);
        int textTop = Math.Max(2, (panel.Height - textHeight) / 2);
        int textWidth = Math.Max(60, panel.Width - textLeft - Scale(8, scaleX));

        SetBounds(iconLabel, iconLeft, iconTop, iconSize, iconSize);
        SetBounds(textBox, textLeft, textTop, textWidth, textHeight);
    }

    private void LayoutNomeInputContent(float scaleX, float scaleY) =>
        LayoutTextInputContent(nomeInputPanel, nomeIconLabel, nomeTextBox, scaleX, scaleY);

    private void LayoutLoginInputContent(float scaleX, float scaleY) =>
        LayoutTextInputContent(loginInputPanel, loginIconLabel, loginTextBox, scaleX, scaleY);

    private void LayoutEmailInputContent(float scaleX, float scaleY) =>
        LayoutTextInputContent(emailInputPanel, emailIconLabel, emailTextBox, scaleX, scaleY);

    private void LayoutConfirmarSenhaInputContent(float scaleX, float scaleY) =>
        LayoutTextInputContent(confirmarSenhaInputPanel, confirmarSenhaIconLabel, confirmarSenhaTextBox, scaleX, scaleY);

    private void LayoutSenhaInputContent(float scaleX, float scaleY) =>
        LayoutTextInputContent(senhaInputPanel, senhaIconLabel, senhaTextBox, scaleX, scaleY);

    private void LayoutCargoInputContent(float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int iconLeft = Scale(8, scaleX);
        int iconSize = Math.Max(16, Scale(20, Math.Min(scaleX, scaleY)));
        int iconTop = Math.Max(2, (cargoInputPanel.Height - iconSize) / 2);

        int comboLeft = Scale(38, scaleX);
        int comboHeight = Math.Max(23, cargoComboBox.PreferredHeight);
        int comboTop = Math.Max(2, (cargoInputPanel.Height - comboHeight) / 2);
        int comboWidth = Math.Max(60, cargoInputPanel.Width - comboLeft - Scale(8, scaleX));

        SetBounds(cargoIconLabel, iconLeft, iconTop, iconSize, iconSize);
        SetBounds(cargoComboBox, comboLeft, comboTop, comboWidth, comboHeight);
    }

    private void LayoutSetorInputContent(float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int iconLeft = Scale(8, scaleX);
        int iconSize = Math.Max(16, Scale(20, Math.Min(scaleX, scaleY)));
        int iconTop = Math.Max(2, (setorInputPanel.Height - iconSize) / 2);

        int comboLeft = Scale(38, scaleX);
        int comboHeight = Math.Max(23, setorComboBox.PreferredHeight);
        int comboTop = Math.Max(2, (setorInputPanel.Height - comboHeight) / 2);
        int comboWidth = Math.Max(60, setorInputPanel.Width - comboLeft - Scale(8, scaleX));

        SetBounds(setorIconLabel, iconLeft, iconTop, iconSize, iconSize);
        SetBounds(setorComboBox, comboLeft, comboTop, comboWidth, comboHeight);
    }

    private void LayoutPerfilInputContent(float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int iconLeft = Scale(8, scaleX);
        int iconSize = Math.Max(16, Scale(20, Math.Min(scaleX, scaleY)));
        int iconTop = Math.Max(2, (perfilInputPanel.Height - iconSize) / 2);

        int comboLeft = Scale(38, scaleX);
        int comboHeight = Math.Max(23, perfilComboBox.PreferredHeight);
        int comboTop = Math.Max(2, (perfilInputPanel.Height - comboHeight) / 2);
        int comboWidth = Math.Max(60, perfilInputPanel.Width - comboLeft - Scale(8, scaleX));

        SetBounds(perfilIconLabel, iconLeft, iconTop, iconSize, iconSize);
        SetBounds(perfilComboBox, comboLeft, comboTop, comboWidth, comboHeight);
    }

    private void LayoutSituacaoInputContent(float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int comboLeft = Scale(8, scaleX);
        int comboHeight = Math.Max(23, situacaoComboBox.PreferredHeight);
        int comboTop = Math.Max(2, (situacaoInputPanel.Height - comboHeight) / 2);
        int comboWidth = Math.Max(60, situacaoInputPanel.Width - comboLeft - Scale(8, scaleX));

        SetBounds(situacaoComboBox, comboLeft, comboTop, comboWidth, comboHeight);
    }

    private void LayoutSummaryCard()
    {
        const int baseCardWidth = 334;
        const int baseCardHeight = 602;

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

        int side = Scale(24, scaleX);
        int dividerW = Math.Max(180, summaryCard.Width - Scale(58, scaleX));
        int labelW = Math.Max(160, summaryCard.Width - Scale(98, scaleX));
        int buttonW = Math.Max(220, summaryCard.Width - Scale(48, scaleX));
        int buttonH = Math.Max(28, Scale(28, scaleY));

        summaryTitleLabel.Font = new Font("Segoe UI", Math.Clamp(11F * contentScale, 10F, 14F), FontStyle.Bold);
        summaryTitleIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(16F * contentScale, 14F, 19F));
        profileCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        profileValueLabel.Font = new Font("Segoe UI", Math.Clamp(10F * contentScale, 9F, 13F), FontStyle.Bold);
        situationCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        situationValueLabel.Font = new Font("Segoe UI", Math.Clamp(10F * contentScale, 9F, 13F), FontStyle.Bold);
        lastCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        lastValueLabel.Font = new Font("Segoe UI", Math.Clamp(10F * contentScale, 9F, 13F), FontStyle.Bold);
        tipTextLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 12F));
        salvarButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        novoButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        excluirButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);

        SetBounds(summaryTitleIconLabel, Scale(28, scaleX), Scale(24, scaleY), Scale(28, scaleX), Scale(28, scaleY));
        SetBounds(summaryTitleLabel, Scale(64, scaleX), Scale(25, scaleY), Math.Max(180, Scale(220, scaleX)), Scale(26, scaleY));

        SetBounds(summaryDivider1, Scale(28, scaleX), Scale(66, scaleY), dividerW, 1);
        SetBounds(summaryDivider2, Scale(28, scaleX), Scale(148, scaleY), dividerW, 1);
        SetBounds(summaryDivider3, Scale(28, scaleX), Scale(230, scaleY), dividerW, 1);
        SetBounds(summaryDivider4, Scale(28, scaleX), Scale(312, scaleY), dividerW, 1);

        SetBounds(profileIconLabel, Scale(32, scaleX), Scale(86, scaleY), Scale(28, scaleX), Scale(28, scaleY));
        SetBounds(profileCaptionLabel, Scale(76, scaleX), Scale(88, scaleY), labelW, Scale(18, scaleY));
        SetBounds(profileValueLabel, Scale(76, scaleX), Scale(109, scaleY), labelW, Scale(24, scaleY));

        SetBounds(situationIconLabel, Scale(32, scaleX), Scale(168, scaleY), Scale(28, scaleX), Scale(28, scaleY));
        SetBounds(situationCaptionLabel, Scale(76, scaleX), Scale(170, scaleY), labelW, Scale(18, scaleY));
        SetBounds(situationValueLabel, Scale(76, scaleX), Scale(191, scaleY), labelW, Scale(24, scaleY));

        SetBounds(lastIconLabel, Scale(32, scaleX), Scale(250, scaleY), Scale(28, scaleX), Scale(28, scaleY));
        SetBounds(lastCaptionLabel, Scale(76, scaleX), Scale(252, scaleY), labelW, Scale(18, scaleY));
        SetBounds(lastValueLabel, Scale(76, scaleX), Scale(273, scaleY), labelW, Scale(24, scaleY));

        SetBounds(tipTextLabel, Scale(72, scaleX), Scale(366, scaleY), Math.Max(170, summaryCard.Width - Scale(96, scaleX)), Math.Max(60, Scale(70, scaleY)));

        SetBounds(salvarButton, side, summaryCard.Height - Scale(160, scaleY), buttonW, buttonH);
        if (_alterarSenhaButton is not null)
        {
            _alterarSenhaButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
            SetBounds(_alterarSenhaButton, side, summaryCard.Height - Scale(128, scaleY), buttonW, buttonH);
        }
        SetBounds(novoButton, side, summaryCard.Height - Scale(96, scaleY), buttonW, buttonH);
        SetBounds(excluirButton, side, summaryCard.Height - Scale(64, scaleY), buttonW, buttonH);
    }

    private void ClearRowSelection()
    {
        foreach (UserRowSelection row in _userRowSelections)
        {
            row.RowPanel.BackColor = row.NormalBackColor;
        }

        HideAllMarkers();
    }

    private void SetFilteredRowsSelected()
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (UserRowSelection row in _userRowSelections)
        {
            bool isVisible = row.RowPanel.Visible;
            row.RowPanel.BackColor = isVisible ? selectedBackColor : row.NormalBackColor;
            if (isVisible)
            {
                ShowMarkerForRow(row.RowPanel);
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }
    }

    private void EnsureRowSelectionMarkers()
    {
        if (_rowSelectionMarkers.Count > 0)
        {
            return;
        }

        foreach (UserRowSelection row in _userRowSelections)
        {
            Panel marker = new()
            {
                Name = $"{row.RowPanel.Name}MarkerPanel",
                BackColor = Color.FromArgb(239, 68, 68),
                Size = new Size(3, row.RowPanel.Height),
                Visible = false
            };
            row.RowPanel.Controls.Add(marker);
            _rowSelectionMarkers[row.RowPanel] = marker;
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

    private void SyncSummaryFromRow(Panel rowPanel)
    {
        if (!_userSummaryByRow.TryGetValue(rowPanel, out UserSummaryData? data))
        {
            return;
        }

        profileValueLabel.Text = data.FullName;
        situationValueLabel.Text = data.Login;
        lastValueLabel.Text = data.Status;
        lastValueLabel.ForeColor = data.Status.Equals("Ativo", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(22, 163, 74)
            : Color.FromArgb(234, 88, 12);

        tipTextLabel.Text = $"Cargo: {data.Cargo}\r\nSetor: {data.Setor}\r\nÚltimo cadastro: {data.LastRegistration}";
    }

    private void LayoutProfilesCard()
    {
        const int baseCardWidth = 413;
        const int baseCardHeight = 602;

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
        int rowX = Scale(3, scaleX);
        int rowWidth = Math.Max(200, cardWidth - Scale(6, scaleX));
        int rowHeight = Math.Max(40, Scale(46, scaleY));

        profilesTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        profilesFooterLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11F));
        novoPerfilTextLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        label1.Font = new Font("Segoe MDL2 Assets", Math.Clamp(9F * contentScale, 9F, 12F));
        profilesSearchTextBox.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 9F, 12F));
        profilesSearchIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(11F * contentScale, 11F, 14F));
        profilesHeaderLoginLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderCargoLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderStatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);

        SetBounds(profilesTitleIconLabel, Scale(16, scaleX), Scale(19, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(profilesTitleLabel, Scale(43, scaleX), Scale(21, scaleY), Scale(190, scaleX), Scale(24, scaleY));

        int quickW = Math.Max(137, Scale(137, scaleX));
        int quickH = Math.Max(27, Scale(27, scaleY));
        SetBounds(novoPerfilButtonPanel, Math.Max(side, profilesCard.Width - side - quickW), Scale(18, scaleY), quickW, quickH);
        int buttonContentH = Math.Max(18, Scale(21, scaleY));
        int iconW = Math.Max(16, Scale(18, scaleX));
        int gap = Math.Max(4, Scale(5, scaleX));
        int textW = Math.Max(70, quickW - Scale(34, scaleX));
        Size textMeasured = TextRenderer.MeasureText(novoPerfilTextLabel.Text, novoPerfilTextLabel.Font);
        int textDrawW = Math.Min(textW, Math.Max(70, textMeasured.Width + Scale(8, scaleX)));
        int blockW = iconW + gap + textDrawW;
        int blockLeft = Math.Max(6, (quickW - blockW) / 2);
        int contentY = Math.Max(2, (quickH - buttonContentH) / 2);
        SetBounds(label1, blockLeft, contentY, iconW, buttonContentH);
        SetBounds(novoPerfilTextLabel, blockLeft + iconW + gap, contentY, textDrawW, buttonContentH);

        int searchY = Scale(60, scaleY);
        int searchH = Math.Max(34, Scale(34, scaleY));
        SetBounds(profilesSearchPanel, side, searchY, cardWidth, searchH);
        int searchIconW = Math.Max(20, Scale(22, scaleX));
        int searchIconH = Math.Max(20, Scale(24, scaleY));
        int searchTextH = Math.Max(16, profilesSearchTextBox.PreferredHeight);
        int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextH) / 2);
        int searchIconY = Math.Max(2, searchTextY + ((searchTextH - searchIconH) / 2));
        SetBounds(profilesSearchIconLabel, Scale(8, scaleX), searchIconY, searchIconW, searchIconH);
        SetBounds(profilesSearchTextBox, Scale(34, scaleX), searchTextY, Math.Max(120, cardWidth - Scale(44, scaleX)), searchTextH);

        int tableY = Scale(108, scaleY);
        int footerY = Math.Max(tableY + 180, profilesCard.Height - Scale(28, scaleY));
        int tableH = Math.Max(220, footerY - tableY - Scale(10, scaleY));
        SetBounds(profilesTablePanel, side, tableY, cardWidth, tableH);

        int usersX = Math.Max(Scale(183, scaleX), cardWidth - Scale(196, scaleX));
        int statusX = Math.Max(Scale(311, scaleX), cardWidth - Scale(68, scaleX));
        SetBounds(profilesHeaderLoginLabel, Scale(16, scaleX), Scale(9, scaleY), Math.Max(110, usersX - Scale(24, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderCargoLabel, usersX, Scale(9, scaleY), Math.Max(80, statusX - usersX - Scale(6, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderStatusLabel, statusX, Scale(9, scaleY), Math.Max(50, Scale(52, scaleX)), Scale(20, scaleY));

        SetBounds(profileRow1Panel, rowX, Scale(34, scaleY), rowWidth, rowHeight);
        SetBounds(profileRow2Panel, rowX, Scale(80, scaleY), rowWidth, rowHeight);
        SetBounds(profileRow3Panel, rowX, Scale(126, scaleY), rowWidth, rowHeight);
        SetBounds(profileRow4Panel, rowX, Scale(172, scaleY), rowWidth, rowHeight);

        Label[] logins = [profileRow1LoginLabel, profileRow2LoginLabel, profileRow3LoginLabel, profileRow4LoginLabel];
        Label[] cargos = [profileRow1CargoLabel, profileRow2CargoLabel, profileRow3CargoLabel, profileRow4CargoLabel];
        RoundedPanel[] statusPanels = [profileRow1StatusPanel, profileRow2StatusPanel, profileRow3StatusPanel, profileRow4StatusPanel];
        Label[] statusLabels = [profileRow1StatusLabel, profileRow2StatusLabel, profileRow3StatusLabel, profileRow4StatusLabel];

        int nameX = Scale(12, scaleX);
        int panelStatusW = Math.Max(50, Scale(50, scaleX));
        int panelStatusX = Math.Max(Scale(310, scaleX), rowWidth - panelStatusW - Scale(12, scaleX));
        int cargoW = Math.Max(60, panelStatusX - usersX - Scale(8, scaleX));

        for (int i = 0; i < logins.Length; i++)
        {
            logins[i].Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            cargos[i].Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            statusLabels[i].Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold);

            SetBounds(logins[i], nameX, Scale(11, scaleY), Math.Max(90, usersX - nameX - Scale(8, scaleX)), Scale(24, scaleY));
            SetBounds(cargos[i], usersX, Scale(11, scaleY), cargoW, Scale(24, scaleY));
            SetBounds(statusPanels[i], panelStatusX, Scale(11, scaleY), panelStatusW, Scale(24, scaleY));
            statusLabels[i].Dock = DockStyle.Fill;
        }

        SetBounds(profilesFooterLabel, Scale(20, scaleX), footerY, Math.Max(180, Scale(240, scaleX)), Scale(22, scaleY));
    }

    private sealed class UserRowSelection
    {
        public UserRowSelection(Panel rowPanel, Color normalBackColor)
        {
            RowPanel = rowPanel;
            NormalBackColor = normalBackColor;
        }

        public Panel RowPanel { get; }

        public Color NormalBackColor { get; }
    }

    private sealed class ProfileSearchRow
    {
        public ProfileSearchRow(Panel rowPanel, Label nameLabel)
        {
            RowPanel = rowPanel;
            NameLabel = nameLabel;
        }

        public Panel RowPanel { get; }
        public Label NameLabel { get; }
    }

    private sealed class UserSummaryData
    {
        public UserSummaryData(string fullName, string login, string status, string cargo, string setor, string lastRegistration)
        {
            FullName = fullName;
            Login = login;
            Status = status;
            Cargo = cargo;
            Setor = setor;
            LastRegistration = lastRegistration;
        }

        public string FullName { get; }
        public string Login { get; }
        public string Status { get; }
        public string Cargo { get; }
        public string Setor { get; }
        public string LastRegistration { get; }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _footerClockTimer?.Stop();
        _footerClockTimer?.Dispose();
        base.OnFormClosed(e);
    }
}







