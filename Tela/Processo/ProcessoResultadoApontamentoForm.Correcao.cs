namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// CORREÇÃO DO RESULTADO JÁ REGISTRADO (incremento 072, pedido do usuário em 24/11/2026).
/// <para>
/// O relato: "na tela de Tempo no consumo só está cadastrando 5, mesmo o usuário colocando outro
/// valor". A causa não estava na tela — estava no banco. A tabela tinha
/// <c>UNIQUE (codigo_apontamento)</c>, um resultado por apontamento, e a tela, ao reencontrar o
/// resultado gravado, mostrava-o e <b>travava a grade</b>. O primeiro valor registrado virava
/// definitivo.
/// </para>
/// <para>
/// <b>A correção começa pelo gesto que o operador já fazia:</b> digitar por cima do valor. Não há
/// botão novo. Ele edita a célula, a tela pergunta uma vez se é para substituir, e a partir daí o
/// F10 grava a correção. Um botão a mais na coluna de ações empurrava o painel para fora do cartão
/// lateral — o teste de layout real pegou isso —, e, mais importante, seria um segundo caminho para
/// fazer o que a própria célula já sugere.
/// </para>
/// <para>
/// A tabela continua append-only: corrigir não apaga o registro anterior, apenas deixa de ser o
/// valor corrente. Quem auditar vê a correção e o que ela substituiu.
/// </para>
/// </summary>
public partial class ProcessoResultadoApontamentoForm
{
    /// <summary>Evita repetir a pergunta a cada célula editada na mesma correção.</summary>
    private bool _correcaoConfirmada;

    /// <summary>
    /// O operador mexeu numa célula com resultado já registrado: pergunta se é correção e, sendo,
    /// devolve a tela ao estado de gravação.
    /// </summary>
    /// <returns><c>false</c> quando ele desistiu — a célula volta ao valor gravado.</returns>
    private bool ConfirmarCorrecaoDoResultadoPersistido()
    {
        if (!ResultadoPersistidoAtivo)
        {
            return true;
        }

        if (_correcaoConfirmada)
        {
            return true;
        }

        // A confirmação diz o que acontece com o valor atual. Corrigir não é inócuo: o que vale
        // depois é o novo, e quem ler o apontamento verá outro número.
        DialogResult escolha = MessageBox.Show(
            this,
            "Informar um novo resultado para esta operação?" + Environment.NewLine + Environment.NewLine
            + "O valor atual deixa de valer e passa a valer o que você gravar com F10." + Environment.NewLine
            + "O registro anterior NÃO é apagado: ele fica no histórico do apontamento.",
            "Corrigir resultado",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (escolha != DialogResult.Yes)
        {
            return false;
        }

        _correcaoConfirmada = true;
        _resultadoPersistido = null;

        gravarResultadoButton.Text = "GRAVAR RESULTADO  F10";
        gravarResultadoButton.Enabled = true;
        AtualizarVisualBotaoGravar();

        statusValueLabel.Text = "CORRIGINDO RESULTADO";
        statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
        atalhoValueLabel.Text = "F10 gravar · Esc fechar";
        instrucaoLabel.Text = "Informe o novo resultado e pressione F10. O registro anterior fica no histórico.";
        statusLabel.Text = "Correção habilitada. O valor que você gravar agora passa a valer.";
        return true;
    }

    /// <summary>
    /// Zera o estado de correção. Chamado ao recarregar as definições, para que um apontamento novo
    /// não herde a confirmação dada no anterior.
    /// </summary>
    private void ReiniciarEstadoCorrecao() => _correcaoConfirmada = false;
}
