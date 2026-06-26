using System.Reflection;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EscritaSapBloqueadaTests
{
    [Fact]
    public async Task AtualizarPesoItemSapAsync_ComConfiguracaoDesabilitada_DeveBloquearSemInicializarCliente()
    {
        ConfiguracaoSap configuracao = new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            HostsPermitidos = ["sap.exemplo.local"],
            EscritaHabilitada = false
        };
        SincronizacaoPedidoCompraSapServico servico = new(configuracao, null!);

        ResultadoOperacao resultado = await servico.AtualizarPesoItemSapAsync(
            "4500000010",
            "10",
            29.9m,
            30m);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemEscritaBloqueada, resultado.Mensagem);
        Assert.False(ObterClienteLazy(servico).IsValueCreated);
    }

    [Fact]
    public async Task FinalizacaoLocal_ComEscritaPadraoDesativada_NaoDeveExecutarPatch()
    {
        ConfiguracaoSap configuracao = new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            HostsPermitidos = ["sap.exemplo.local"]
        };
        SincronizacaoPedidoCompraSapServico servico = new(configuracao, null!);

        ResultadoOperacao resultado = await servico.AtualizarPesoItemSapAsync(
            "4500000010",
            "10",
            29.9m,
            30m);

        Assert.False(configuracao.EscritaHabilitada);
        Assert.False(resultado.Sucesso);
        Assert.False(ObterClienteLazy(servico).IsValueCreated);
    }

    [Fact]
    public void Finalizacao_DeveSalvarLocalmenteAntesDeChamarEscritaSap()
    {
        // A coordenacao (persistencia local + criacao do documento de material 101) vive no controller.
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Controle",
            "Processo",
            "EntradaProdutoController.cs"));
        int salvarLocal = controller.IndexOf("EntradaProduto.RegistrarLancamentoAsync", StringComparison.Ordinal);
        int criarDocumento = controller.IndexOf("CriarDocumentoMaterialEntradaAsync", StringComparison.Ordinal);

        Assert.True(salvarLocal >= 0);
        Assert.True(criarDocumento > salvarLocal);

        // A Entrada NAO usa mais PATCH no Pedido de Compra: o controller nao chama AtualizarPesoItemSapAsync.
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", controller, StringComparison.Ordinal);

        // A tela tambem nao executa escrita SAP direta — quem coordena e o controller.
        string form = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EscritaSap_DeveUsarPermissaoOperacionalDeFinalizacao()
    {
        string arquivo = Path.Combine(
            RaizProjeto(),
            "Servicos",
            "IntegracaoSap",
            "SincronizacaoPedidoCompraSapServico.cs");
        string conteudo = File.ReadAllText(arquivo);

        Assert.Contains("PermissoesSistema.Modulos.ProcessoProducao", conteudo, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Rotinas.EntradaProduto", conteudo, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.EnviarSap", conteudo, StringComparison.Ordinal);
    }

    private static Lazy<PedidoCompraSapApiClient> ObterClienteLazy(
        SincronizacaoPedidoCompraSapServico servico)
    {
        FieldInfo campo = typeof(SincronizacaoPedidoCompraSapServico)
            .GetField("_clienteSap", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("_clienteSap");

        return (Lazy<PedidoCompraSapApiClient>)campo.GetValue(servico)!;
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
