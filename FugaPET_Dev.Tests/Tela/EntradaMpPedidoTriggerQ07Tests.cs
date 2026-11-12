namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 07 — contrato determinístico de disparo do pedidoComboBox na Entrada de Matéria-Prima:
/// Enter (KeyDown) e saída do campo (Leave) carregam o pedido; seleção continua carregando; nenhuma
/// combinação provoca duas sincronizações concorrentes do mesmo pedido. Endpoint/regra de negócio preservados.
/// Provas por source-scan (os handlers WinForms não são instanciáveis offline).
/// </summary>
public sealed class EntradaMpPedidoTriggerQ07Tests
{
    private static string Form() => LerFonte("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

    [Fact] // A: Enter dispara a carga (KeyDown → AtualizarDadosPedidoSelecionadoAsync), com SuppressKeyPress.
    public void A_Enter_DisparaCarga()
    {
        string metodo = ExtrairMetodo(Form(), "private async void PedidoComboBox_KeyDown");
        Assert.Contains("e.KeyCode != Keys.Enter", metodo, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true;", metodo, StringComparison.Ordinal);
        Assert.Contains("e.SuppressKeyPress = true;", metodo, StringComparison.Ordinal);
        Assert.Contains("AtualizarDadosPedidoSelecionadoAsync()", metodo, StringComparison.Ordinal);
        // Wiring do KeyDown existe.
        Assert.Contains("pedidoComboBox.KeyDown += PedidoComboBox_KeyDown;", Form(), StringComparison.Ordinal);
    }

    [Fact] // B: saída do campo (Leave) dispara a carga.
    public void B_Leave_DisparaCarga()
    {
        string metodo = ExtrairMetodo(Form(), "private async void PedidoComboBox_Leave");
        Assert.Contains("AtualizarDadosPedidoSelecionadoAsync()", metodo, StringComparison.Ordinal);
        Assert.Contains("pedidoComboBox.Leave += PedidoComboBox_Leave;", Form(), StringComparison.Ordinal);
    }

    [Fact] // C: seleção na lista continua carregando (SelectedIndexChanged preservado).
    public void C_SelectedIndexChanged_Preservado()
    {
        Assert.Contains("pedidoComboBox.SelectedIndexChanged += PedidoComboBox_SelectedIndexChanged;", Form(), StringComparison.Ordinal);
        string metodo = ExtrairMetodo(Form(), "private async void PedidoComboBox_SelectedIndexChanged");
        Assert.Contains("AtualizarDadosPedidoSelecionadoAsync()", metodo, StringComparison.Ordinal);
    }

    [Fact] // Validated substituído: não há mais wiring nem duplicação por Leave+Validated.
    public void Validated_Removido()
    {
        Assert.DoesNotContain("pedidoComboBox.Validated += PedidoComboBox_Validated;", Form(), StringComparison.Ordinal);
        Assert.DoesNotContain("private async void PedidoComboBox_Validated", Form(), StringComparison.Ordinal);
    }

    [Fact] // D + E + G: caminho ÚNICO com idempotência — mesmo pedido não sincroniza duas vezes.
    public void D_E_G_Idempotencia_CaminhoUnico()
    {
        string metodo = ExtrairMetodo(Form(), "private async Task AtualizarDadosPedidoSelecionadoAsync");
        // G: guard "pedido já carregado" retorna cedo.
        Assert.Contains("if (PedidoJaCarregado(numeroPedido))", metodo, StringComparison.Ordinal);
        // D/E: troca atômica de CancellationTokenSource cancela a consulta anterior + serialização por gate.
        Assert.Contains("Interlocked.Exchange(", metodo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoCts", metodo, StringComparison.Ordinal);
        Assert.Contains("await _consultaPedidoGate.WaitAsync(cancellationToken)", metodo, StringComparison.Ordinal);
        Assert.Contains("PedidoSolicitadoAindaEhAtual(numeroPedido)", metodo, StringComparison.Ordinal);
        // Todos os gatilhos convergem no mesmo método reutilizável.
        Assert.Contains("PedidoJaCarregado", Form(), StringComparison.Ordinal);
    }

    [Fact] // F: texto vazio não dispara consulta SAP (limpa e retorna antes de permissão/consulta).
    public void F_TextoVazio_NaoConsulta()
    {
        string metodo = ExtrairMetodo(Form(), "private async Task AtualizarDadosPedidoSelecionadoAsync");
        int idxVazio = metodo.IndexOf("string.IsNullOrWhiteSpace(numeroPedido)", StringComparison.Ordinal);
        int idxConsulta = metodo.IndexOf("_controller.ConsultarPedidoAsync(", StringComparison.Ordinal);
        Assert.True(idxVazio >= 0, "guard de texto vazio ausente");
        Assert.True(idxConsulta >= 0, "chamada de consulta ausente");
        Assert.True(idxVazio < idxConsulta, "o guard de texto vazio deve preceder a consulta SAP");
    }

    [Fact] // H: endpoint Gate04 (API_PURCHASEORDER_2) e regra de negócio (PurchasingGroup 700) preservados.
    public void H_Endpoint_E_RegraNegocio_Preservados()
    {
        string escopo = LerFonte("Servicos", "IntegracaoSap", "EscopoPedidoSapJales.cs");
        Assert.Contains("GrupoCompra = \"700\"", escopo, StringComparison.Ordinal);

        string modelo = LerFonte("Modelo", "IntegracaoSap", "PedidoCompraSap.cs");
        Assert.Contains("API_PURCHASEORDER_2", modelo, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static string LerFonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && raiz is not null; i++)
        {
            foreach (string candidato in new[] { Path.Combine(raiz, Path.Combine(partes)), Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes)) })
            {
                if (File.Exists(candidato))
                {
                    return File.ReadAllText(candidato);
                }
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException($"Fonte não encontrada: {string.Join('/', partes)}");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte.Substring(inicio, i - inicio + 1);
                }
            }
        }

        return fonte[inicio..];
    }
}
