using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// Sinaliza, com faixa fixa no topo, que a aplicacao esta em MODO DEMONSTRACAO
/// (banco desabilitado + modo_demonstracao = true): a autorizacao libera tudo e o
/// login local offline e permitido. Fora do modo demonstracao, o banco desabilitado
/// BLOQUEIA login/operacao — entao a faixa nunca aparece "liberando" sem seguranca.
/// </summary>
internal static class ModoDemonstracaoHelper
{
    public const string Texto =
        "MODO DEMONSTRAÇÃO — BANCO DESABILITADO, SEGURANÇA LIBERADA. NÃO USE EM PRODUÇÃO.";

    private const string NomeFaixa = "faixaModoDemonstracao";
    private static readonly Color FundoFaixa = Color.FromArgb(180, 20, 30);
    private static readonly Color TextoFaixa = Color.White;

    /// <summary>
    /// Aplica a faixa fixa apenas quando em modo demonstracao. Idempotente.
    /// </summary>
    public static void AplicarFaixaSeModoDemonstracao(Form form)
    {
        if (!EstadoIntegracaoBanco.PodeUsarDadosSimulados)
        {
            return;
        }

        foreach (Control existente in form.Controls)
        {
            if (existente.Name == NomeFaixa)
            {
                return;
            }
        }

        Label faixa = new()
        {
            Name = NomeFaixa,
            Text = Texto,
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = FundoFaixa,
            ForeColor = TextoFaixa,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        form.Controls.Add(faixa);
        // SendToBack faz a faixa docar no topo absoluto (acima dos demais Dock=Top).
        faixa.SendToBack();
    }
}
