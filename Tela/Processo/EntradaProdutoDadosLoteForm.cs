using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tela.Processo;

public partial class EntradaProdutoDadosLoteForm : Form
{
    private readonly ValidadorDadosLoteEntrada _validadorDadosLote;

    public EntradaProdutoDadosLoteForm(ModoEntradaMaterial modoEntrada)
        : this(modoEntrada, new ValidadorDadosLoteEntrada())
    {
    }

    internal EntradaProdutoDadosLoteForm(
        ModoEntradaMaterial modoEntrada,
        ValidadorDadosLoteEntrada validadorDadosLote)
    {
        if (!Enum.IsDefined(modoEntrada))
        {
            throw new ArgumentOutOfRangeException(nameof(modoEntrada), "Modo de entrada inválido.");
        }

        _validadorDadosLote = validadorDadosLote ?? throw new ArgumentNullException(nameof(validadorDadosLote));
        InitializeComponent();
        DadosConfirmados = null;
        ConfigurarModo(modoEntrada);
        AtualizarContadorLote();
    }

    public DadosLoteEntrada? DadosConfirmados { get; private set; }

    protected override void OnShown(EventArgs e)
    {
        LimparResultadoParaNovaExibicao();

        base.OnShown(e);
        numeroLoteTextBox.Focus();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (Visible)
        {
            LimparResultadoParaNovaExibicao();
        }

        base.OnVisibleChanged(e);

        if (Visible)
        {
            numeroLoteTextBox.Focus();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
        {
            DadosConfirmados = null;
        }

        base.OnFormClosing(e);
    }

    private void LimparResultadoParaNovaExibicao()
    {
        DadosConfirmados = null;
        DialogResult = DialogResult.None;
        statusLabel.Text = string.Empty;
        statusLabel.Visible = false;
    }

    private void ConfigurarModo(ModoEntradaMaterial modoEntrada)
    {
        modoLabel.Text = modoEntrada == ModoEntradaMaterial.Quimico
            ? "Informe o lote do produto químico antes da pesagem."
            : "Informe o lote da matéria-prima antes da pesagem.";
    }

    private void NumeroLoteTextBox_TextChanged(object? sender, EventArgs e)
        => AtualizarContadorLote();

    private void ConfirmarButton_Click(object? sender, EventArgs e)
        => Confirmar();

    private void CancelarButton_Click(object? sender, EventArgs e)
    {
        DadosConfirmados = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void Confirmar()
    {
        DadosConfirmados = null;
        string numeroLote = numeroLoteTextBox.Text.Trim();
        DateTime? fabricacao = dataFabricacaoDateTimePicker.Checked
            ? dataFabricacaoDateTimePicker.Value
            : null;
        DateTime? vencimento = dataVencimentoDateTimePicker.Checked
            ? dataVencimentoDateTimePicker.Value
            : null;

        ResultadoValidacaoLoteEntrada resultado = _validadorDadosLote.Validar(numeroLote, fabricacao, vencimento);
        if (!resultado.Sucesso)
        {
            statusLabel.Text = resultado.Mensagem;
            statusLabel.Visible = true;
            FocarPrimeiroCampoInvalido(numeroLote, fabricacao, vencimento);
            return;
        }

        DadosConfirmados = resultado.DadosNormalizados;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void FocarPrimeiroCampoInvalido(string numeroLote, DateTime? fabricacao, DateTime? vencimento)
    {
        if (string.IsNullOrWhiteSpace(numeroLote))
        {
            numeroLoteTextBox.Focus();
            return;
        }

        if (!fabricacao.HasValue)
        {
            dataFabricacaoDateTimePicker.Focus();
            return;
        }

        if (!vencimento.HasValue)
        {
            dataVencimentoDateTimePicker.Focus();
            return;
        }

        numeroLoteTextBox.Focus();
    }

    private void AtualizarContadorLote()
        => contadorLoteLabel.Text = $"{numeroLoteTextBox.Text.Trim().Length}/10";
}
