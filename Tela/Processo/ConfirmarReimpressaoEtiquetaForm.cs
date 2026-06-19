namespace FugaPET_Dev.Tela.Processo;

public sealed class ConfirmarReimpressaoEtiquetaForm : Form
{
    public ConfirmarReimpressaoEtiquetaForm(string itemPedido)
    {
        Text = "Confirmar Reimpressão";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 180);
        BackColor = Color.FromArgb(247, 248, 250);

        Label titleLabel = new()
        {
            Text = "Imprimir etiqueta novamente?",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 22),
            Size = new Size(360, 28)
        };

        Label messageLabel = new()
        {
            Text = $"Confirma a reimpressão da etiqueta do item {itemPedido}?",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(26, 60),
            Size = new Size(360, 42)
        };

        Button naoButton = CriarBotao("Não", Color.White, Color.FromArgb(45, 49, 56), new Point(178, 124));
        Button simButton = CriarBotao("Sim", Color.FromArgb(184, 18, 32), Color.White, new Point(294, 124));

        naoButton.DialogResult = DialogResult.No;
        simButton.DialogResult = DialogResult.Yes;

        Controls.Add(titleLabel);
        Controls.Add(messageLabel);
        Controls.Add(naoButton);
        Controls.Add(simButton);

        AcceptButton = naoButton;
        CancelButton = naoButton;
    }

    private static Button CriarBotao(string texto, Color fundo, Color frente, Point localizacao)
    {
        return new Button
        {
            Text = texto,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = fundo,
            ForeColor = frente,
            FlatStyle = FlatStyle.Flat,
            Location = localizacao,
            Size = new Size(92, 34)
        };
    }
}
