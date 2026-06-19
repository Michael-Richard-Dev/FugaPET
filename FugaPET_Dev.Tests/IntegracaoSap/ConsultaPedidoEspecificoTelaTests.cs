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

        Assert.Contains(
            "_pedidoCompraServico.SincronizarPedidoAsync(",
            conteudo,
            StringComparison.Ordinal);
        Assert.Contains("string numeroPedido = pedidoComboBox.Text.Trim();", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void NovaConsulta_DeveCancelarAnteriorEImpedirSimultaneidade()
    {
        string conteudo = LerTela();

        Assert.Contains("Interlocked.Exchange(", conteudo, StringComparison.Ordinal);
        Assert.Contains("consultaAnterior?.Cancel();", conteudo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoGate.WaitAsync(cancellationToken)", conteudo, StringComparison.Ordinal);
        Assert.Contains("_consultaPedidoGate.Release();", conteudo, StringComparison.Ordinal);
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
