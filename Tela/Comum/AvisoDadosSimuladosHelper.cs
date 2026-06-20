using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// Sinaliza visualmente que uma tela ainda usa DADOS SIMULADOS (nao integrada ao banco/SAP).
/// Use enquanto a tela nao le dados reais; quando integrar, ponha UsaDadosSimulados = false
/// na tela e o aviso/faixa some.
///
/// Regra de ambiente: telas simuladas so sao permitidas em MODO DEMONSTRACAO (com faixa).
/// Em operacao real (homologacao/producao, fora do modo demonstracao) a tela e BLOQUEADA.
/// </summary>
internal static class AvisoDadosSimuladosHelper
{
    public const string Texto = "DADOS SIMULADOS — TELA AINDA NÃO INTEGRADA AO BANCO/SAP";

    private const string NomeFaixa = "faixaDadosSimulados";
    private static readonly Color FundoFaixa = Color.FromArgb(180, 20, 30);
    private static readonly Color TextoFaixa = Color.White;
    private static readonly Color TextoSubtitulo = Color.FromArgb(251, 191, 36);

    /// <summary>
    /// Aviso textual no subtitulo do cabecalho (discreto, complementar a faixa).
    /// </summary>
    public static void Aplicar(Label subtituloLabel)
    {
        if (subtituloLabel.Text.Contains("DADOS SIMULADOS", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        subtituloLabel.Text = string.IsNullOrWhiteSpace(subtituloLabel.Text)
            ? Texto
            : $"{subtituloLabel.Text}  |  {Texto}";

        subtituloLabel.ForeColor = TextoSubtitulo;
    }

    /// <summary>
    /// Faixa fixa, vermelha, no topo do formulario. Idempotente.
    /// </summary>
    public static void AplicarFaixa(Form form)
    {
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
        // SendToBack faz a faixa docar no topo absoluto (acima dos demais Dock=Top),
        // empurrando o conteudo (root Fill) para baixo sem sobrepor.
        faixa.SendToBack();
    }

    /// <summary>
    /// True somente com banco desabilitado, modo demonstracao ativo e ambiente
    /// explicitamente demonstrativo.
    /// </summary>
    public static bool PodeUsarDadosSimulados()
        => EstadoIntegracaoBanco.PodeUsarDadosSimulados;

    /// <summary>
    /// Bloqueia uma tela simulada em operacao real: avisa o usuario e agenda o fechamento
    /// do form (no Load, antes de virar utilizavel). Chame no construtor e de "return" em seguida.
    /// </summary>
    public static void BloquearTelaSimulada(Form form)
    {
        MessageBox.Show(
            "Esta tela utiliza dados simulados e só pode abrir com banco desabilitado, modo demonstração ativo e ambiente demonstrativo.",
            "Tela não disponível",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        form.Load += (_, _) => form.Close();
    }
}
