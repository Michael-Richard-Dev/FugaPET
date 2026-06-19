using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Teste;

public partial class BalanceTestForm : Form
{
    // Usa a camada operacional: le a config da balanca padrao do terminal (cadastro),
    // em vez do leitor de baixo nivel com porta/baud fixos.
    private readonly BalancaLeituraServico _balancaLeituraServico = new();

    public BalanceTestForm()
    {
        InitializeComponent();
    }

    private async void readWeightButton_Click(object sender, EventArgs e)
    {
        readWeightButton.Enabled = false;
        weightValueLabel.Text = "Lendo...";
        statusLabel.Text = "Aguardando dados da balanca...";

        try
        {
            ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
            if (leitura.Sucesso)
            {
                weightValueLabel.Text = leitura.Peso;
                statusLabel.Text = "Peso capturado com sucesso.";
            }
            else
            {
                weightValueLabel.Text = "--";
                statusLabel.Text = leitura.Mensagem;
            }
        }
        catch (Exception ex)
        {
            weightValueLabel.Text = "--";
            statusLabel.Text = await ErroUsuarioHelper.TratarAsync("LEITURA_BALANCA_TESTE_ERRO", ex, "BalanceTestForm",
                "Não foi possível ler a balança. Verifique a conexão e acione o suporte.");
        }
        finally
        {
            readWeightButton.Enabled = true;
        }
    }
}



