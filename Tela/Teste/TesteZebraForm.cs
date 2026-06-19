using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Teste;

public partial class TesteZebraForm : Form
{
    private readonly ServicoImpressoraZebra _servicoImpressoraZebra = new();

    public TesteZebraForm()
    {
        InitializeComponent();
    }

    private async void printTestButton_Click(object sender, EventArgs e)
    {
        try
        {
            DadosEtiquetaProducao label = new()
            {
                OrdemProducao = "58422",
                Lote = "119 26",
                CodigoProduto = "27771",
                DescricaoProduto = "TWIST STIX CARNE 24X50PCS",
                DataSaidaEstufa = "04/05/2026",
                DataClassificacao = "04/05/2026",
                DataFabricacao = "29/04/2026",
                DataVencimento = "28/04/2029",
                CaixasPrevistas = "35",
                PacotesPrevistos = "840",
                Saldo = "5,568",
                Quantidade = "24",
                Peso = "7,664",
                CodigoProducao = "2160981"
            };

            _servicoImpressoraZebra.ImprimirEtiquetaProducao(printerNameTextBox.Text.Trim(), label);
            statusLabel.Text = "Etiqueta de teste enviada para a Zebra.";
        }
        catch (Exception ex)
        {
            string msg = await ErroUsuarioHelper.TratarAsync("IMPRESSAO_TESTE_ERRO", ex, "TesteZebraForm",
                "Não foi possível imprimir na Zebra. Verifique a impressora e acione o suporte.");
            statusLabel.Text = msg;
            MessageBox.Show(msg, "Erro ao imprimir", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void printTextButton_Click(object sender, EventArgs e)
    {
        try
        {
            _servicoImpressoraZebra.ImprimirTexto(printerNameTextBox.Text.Trim(), testTextBox.Text);
            statusLabel.Text = "Texto de teste enviado para a Zebra.";
        }
        catch (Exception ex)
        {
            string msg = await ErroUsuarioHelper.TratarAsync("IMPRESSAO_TESTE_ERRO", ex, "TesteZebraForm",
                "Não foi possível imprimir na Zebra. Verifique a impressora e acione o suporte.");
            statusLabel.Text = msg;
            MessageBox.Show(msg, "Erro ao imprimir", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}



