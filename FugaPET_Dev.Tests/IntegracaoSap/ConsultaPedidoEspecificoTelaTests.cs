namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class ConsultaPedidoEspecificoTelaTests
{
    [Fact]
    public void Tela_NaoDeveExecutarSincronizacaoAmplaAutomatica()
    {
        string conteudo = LerTela();

        Assert.DoesNotContain("InicializarPedidosCompraAsync", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("SincronizarPedidosCompraEmSegundoPlanoAsync", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("_pedidoCompraServico.SincronizarAsync(", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveConsultarSomentePedidoInformado()
    {
        string conteudo = LerTela();

        // H9 Etapa 3: a tela captura o numero e delega ao controller; o SincronizarPedidoAsync
        // (consulta de um unico pedido) vive no controller.
        Assert.Contains("string numeroPedido = pedidoComboBox.Text.Trim();", conteudo, StringComparison.Ordinal);
        Assert.Contains("_controller.ConsultarPedidoAsync(", conteudo, StringComparison.Ordinal);

        string controller = LerController();
        Assert.Contains("Sap.SincronizarPedidoAsync(", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void NovaConsulta_DeveCancelarAnteriorEImpedirSimultaneidade()
    {
        string conteudo = LerTela();

        Assert.Contains("private Task _consultaPedidoTask = Task.CompletedTask;", conteudo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();", conteudo, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(", conteudo, StringComparison.Ordinal);
        Assert.Contains("consultaAnterior?.Cancel();", conteudo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoGate.WaitAsync(cancellationToken)", conteudo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoGate.Release();", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void RespostaAntiga_NaoDevePreencherPedidoAtual()
    {
        string conteudo = LerTela();

        Assert.Contains(
            "PedidoSolicitadoAindaEhAtual(numeroPedido)",
            conteudo,
            StringComparison.Ordinal);
        Assert.Contains(
            "resultado.NumeroPedido",
            conteudo,
            StringComparison.Ordinal);
        Assert.Contains(
            "pedidoComboBox.Text.Trim()",
            conteudo,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_DeveUsarDtoAgregadoSemQuatroConsultasIndependentes()
    {
        string controller = LerController();

        Assert.Contains("ObterPedidoAgregadoAsync(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ObterFornecedorPorPedidoAsync(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ObterDataPorPedidoAsync(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ObterTipoPorPedidoAsync(", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("ListarItensPorPedidoAsync(", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_DeveLerCabecalhoEItensEmUmUnicoComando()
    {
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "SapPedidoCompraRepositorio.cs"));
        int inicio = repositorio.IndexOf(
            "public async Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(",
            StringComparison.Ordinal);
        int fim = repositorio.IndexOf(
            "/// <summary>",
            inicio,
            StringComparison.Ordinal);
        string metodo = repositorio[inicio..fim];

        Assert.Equal(1, ContarOcorrencias(metodo, "new(sql, conexao)"));
        Assert.Contains("LEFT JOIN sap_pedido_compra_item", metodo, StringComparison.Ordinal);
        Assert.Contains("while (await leitor.ReadAsync(cancellationToken))", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void DuploEvento_DeveReutilizarPedidoJaCarregado()
    {
        string conteudo = LerTela();

        Assert.Contains("if (PedidoJaCarregado(numeroPedido))", conteudo, StringComparison.Ordinal);
    }

    private static string LerTela()
        => File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));

    private static string LerController()
        => File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Controle",
            "Processo",
            "EntradaProdutoController.cs"));

    private static int ContarOcorrencias(string texto, string valor)
    {
        int quantidade = 0;
        int indice = 0;
        while ((indice = texto.IndexOf(valor, indice, StringComparison.Ordinal)) >= 0)
        {
            quantidade++;
            indice += valor.Length;
        }

        return quantidade;
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
