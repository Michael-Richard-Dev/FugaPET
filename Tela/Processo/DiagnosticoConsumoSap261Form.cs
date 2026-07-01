using System.Text;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Diagnostico;

namespace FugaPET_Dev.Tela.Processo;

public partial class DiagnosticoConsumoSap261Form : Form
{
    private readonly DiagnosticoConsumoSap261Controller _controller = new();
    private DiagnosticoConsumoSap261Resultado? _ultimoResultado;

    public DiagnosticoConsumoSap261Form()
    {
        InitializeComponent();
        ConfigurarTela();
    }

    private void ConfigurarTela()
    {
        itensGridView.Columns.Add("Codigo", "Código");
        itensGridView.Columns.Add("Descricao", "Descrição");
        itensGridView.Columns.Add("Status", "Status");
        itensGridView.Columns.Add("Mensagem", "Mensagem");
        executarButton.Click += async (_, _) => await ExecutarDiagnosticoAsync();
        copiarButton.Click += (_, _) => CopiarResultado();
        fecharButton.Click += (_, _) => Close();
    }

    private async Task ExecutarDiagnosticoAsync()
    {
        try
        {
            mensagemLabel.Text = "Executando diagnóstico read-only...";
            executarButton.Enabled = false;
            DiagnosticoConsumoSap261Resultado resultado = await _controller.ExecutarAsync();
            _ultimoResultado = resultado;
            PreencherResultado(resultado);
            mensagemLabel.Text = "Diagnóstico concluído.";
        }
        catch (Exception ex)
        {
            mensagemLabel.Text = "Não foi possível executar o diagnóstico.";
            MessageBox.Show(
                $"Não foi possível executar o diagnóstico. Detalhe: {ex.Message}",
                "Diagnóstico Consumo 261",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            executarButton.Enabled = true;
        }
    }

    private void PreencherResultado(DiagnosticoConsumoSap261Resultado resultado)
    {
        testeLocalCard.Text = $"Teste local: {FormatarBooleano(resultado.ProntoParaTesteLocal)}";
        previewCard.Text = $"Preview 261: {FormatarBooleano(resultado.ProntoParaPreview)}";
        envioSapCard.Text = $"Envio SAP 261: {FormatarBooleano(resultado.ProntoParaEnvioSap)}";

        itensGridView.Rows.Clear();
        foreach (DiagnosticoConsumoSap261Item item in resultado.Itens)
        {
            itensGridView.Rows.Add(item.Codigo, item.Descricao, item.Status, item.Mensagem);
        }
    }

    private void CopiarResultado()
    {
        if (_ultimoResultado is null)
        {
            MessageBox.Show("Execute o diagnóstico antes de copiar.", "Diagnóstico Consumo 261",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        StringBuilder texto = new();
        texto.AppendLine($"Teste local: {_ultimoResultado.ProntoParaTesteLocal}");
        texto.AppendLine($"Preview 261: {_ultimoResultado.ProntoParaPreview}");
        texto.AppendLine($"Envio SAP 261: {_ultimoResultado.ProntoParaEnvioSap}");
        foreach (DiagnosticoConsumoSap261Item item in _ultimoResultado.Itens)
        {
            texto.AppendLine($"{item.Codigo}\t{item.Descricao}\t{item.Status}\t{item.Mensagem}");
        }

        Clipboard.SetText(texto.ToString());
        mensagemLabel.Text = "Resultado copiado para a área de transferência.";
    }

    private static string FormatarBooleano(bool valor)
        => valor ? "OK" : "Não pronto";
}
