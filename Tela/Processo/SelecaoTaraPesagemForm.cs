using FugaPET_Dev.Modelo.Cadastro;
using System.Globalization;

namespace FugaPET_Dev.Tela.Processo;

public sealed class SelecaoTaraPesagemForm : Form
{
    private readonly DataGridView _tarasGrid = new();
    private readonly Button _confirmarButton = new();
    private readonly Button _cancelarButton = new();
    private readonly Label _statusLabel = new();
    private readonly IReadOnlyList<TaraCadastro> _taras;

    public TaraCadastro? TaraSelecionada { get; private set; }

    public SelecaoTaraPesagemForm(IReadOnlyList<TaraCadastro> taras, string itemPedido)
    {
        _taras = taras;
        Text = "Selecionar Tara";
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 460);
        BackColor = Color.FromArgb(247, 248, 250);

        Label tituloLabel = new()
        {
            Text = "Escolha a tara utilizada na pesagem",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 18),
            Size = new Size(520, 30)
        };

        Label itemLabel = new()
        {
            Text = $"Item do pedido: {itemPedido}",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(26, 50),
            Size = new Size(520, 22)
        };

        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _statusLabel.ForeColor = Color.FromArgb(184, 18, 32);
        _statusLabel.Location = new Point(26, 78);
        _statusLabel.Size = new Size(650, 22);

        ConfigurarGrid();
        ConfigurarBotoes();

        Controls.Add(tituloLabel);
        Controls.Add(itemLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_tarasGrid);
        Controls.Add(_confirmarButton);
        Controls.Add(_cancelarButton);

        CarregarTaras();
    }

    private void ConfigurarGrid()
    {
        _tarasGrid.Location = new Point(24, 106);
        _tarasGrid.Size = new Size(672, 280);
        _tarasGrid.AllowUserToAddRows = false;
        _tarasGrid.AllowUserToDeleteRows = false;
        _tarasGrid.AllowUserToResizeRows = false;
        _tarasGrid.BackgroundColor = Color.White;
        _tarasGrid.BorderStyle = BorderStyle.FixedSingle;
        _tarasGrid.ColumnHeadersHeight = 30;
        _tarasGrid.EnableHeadersVisualStyles = false;
        _tarasGrid.MultiSelect = false;
        _tarasGrid.ReadOnly = true;
        _tarasGrid.RowHeadersVisible = false;
        _tarasGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _tarasGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _tarasGrid.CellDoubleClick += (_, _) => ConfirmarSelecao();

        _tarasGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "codigoColumn", HeaderText = "Código", FillWeight = 18 });
        _tarasGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "nomeColumn", HeaderText = "Tara", FillWeight = 42 });
        _tarasGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pesoColumn", HeaderText = "Peso (kg)", FillWeight = 20 });
        _tarasGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "situacaoColumn", HeaderText = "Situação", FillWeight = 20 });
    }

    private void ConfigurarBotoes()
    {
        _confirmarButton.Text = "Confirmar";
        _confirmarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _confirmarButton.BackColor = Color.FromArgb(34, 166, 82);
        _confirmarButton.ForeColor = Color.White;
        _confirmarButton.FlatStyle = FlatStyle.Flat;
        _confirmarButton.Location = new Point(456, 406);
        _confirmarButton.Size = new Size(112, 34);
        _confirmarButton.Click += (_, _) => ConfirmarSelecao();

        _cancelarButton.Text = "Cancelar";
        _cancelarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _cancelarButton.BackColor = Color.White;
        _cancelarButton.ForeColor = Color.FromArgb(45, 49, 56);
        _cancelarButton.FlatStyle = FlatStyle.Flat;
        _cancelarButton.Location = new Point(584, 406);
        _cancelarButton.Size = new Size(112, 34);
        _cancelarButton.DialogResult = DialogResult.Cancel;

        AcceptButton = _confirmarButton;
        CancelButton = _cancelarButton;
    }

    private void CarregarTaras()
    {
        _tarasGrid.Rows.Clear();

        foreach (TaraCadastro tara in _taras.OrderBy(tara => tara.NomeTara))
        {
            int rowIndex = _tarasGrid.Rows.Add(
                tara.CodigoTara,
                tara.NomeTara,
                tara.PesoKg.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR")),
                tara.SituacaoTara ? "Ativa" : "Inativa");

            DataGridViewRow row = _tarasGrid.Rows[rowIndex];
            row.Tag = tara;
            if (!tara.SituacaoTara)
            {
                row.DefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            }
        }

        _statusLabel.Text = _taras.Count == 0
            ? "Nenhuma tara cadastrada no banco."
            : "Selecione uma tara ativa para continuar.";
    }

    private void ConfirmarSelecao()
    {
        DataGridViewRow? row = _tarasGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .FirstOrDefault();

        if (row?.Tag is not TaraCadastro tara)
        {
            _statusLabel.Text = "Selecione uma tara para continuar.";
            return;
        }

        if (!tara.SituacaoTara)
        {
            _statusLabel.Text = "A tara selecionada está inativa.";
            return;
        }

        TaraSelecionada = tara;
        DialogResult = DialogResult.OK;
        Close();
    }
}
