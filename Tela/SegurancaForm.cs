namespace FugaPET_Dev.Tela;

public partial class SegurancaForm : UserControl
{
    public event EventHandler? CadastroUsuarioRequested;
    public event EventHandler? PerfilAcessoRequested;
    public event EventHandler? PermissaoRequested;

    public SegurancaForm()
    {
        InitializeComponent();
        WireCardClickEvents();
    }

    private void WireCardClickEvents()
    {
        VincularClique(usuarioCard, OnCadastroUsuarioClick);
        VincularClique(perfilAcessoCard, OnPerfilAcessoClick);
        VincularClique(permissaoCard, OnPermissaoClick);
    }

    private static void VincularClique(Control controle, EventHandler handler)
    {
        controle.Click += handler;
        foreach (Control filho in controle.Controls)
        {
            VincularClique(filho, handler);
        }
    }

    private void OnCadastroUsuarioClick(object? sender, EventArgs e)
    {
        CadastroUsuarioRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPerfilAcessoClick(object? sender, EventArgs e)
    {
        PerfilAcessoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPermissaoClick(object? sender, EventArgs e)
    {
        PermissaoRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F3)
        {
            OnPerfilAcessoClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F4)
        {
            OnPermissaoClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F5)
        {
            OnCadastroUsuarioClick(this, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
