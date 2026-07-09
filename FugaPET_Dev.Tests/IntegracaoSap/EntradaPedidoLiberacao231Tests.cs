using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 23.1: só permite operar Pedido de Compra aprovado/liberado no SAP
/// (PurchasingProcessingStatus == "05" e ReleaseIsNotCompleted != true); bloqueia por segurança nos demais casos.
/// </summary>
public sealed class EntradaPedidoLiberacao231Tests
{
    private static PedidoCompraSap Pedido(string status, bool? releaseNaoConcluida = null, string numero = "4500001424")
        => new()
        {
            Numero = numero,
            StatusProcessamentoCompraSap = status,
            LiberacaoNaoConcluidaSap = releaseNaoConcluida
        };

    // ---- Ajuste 4: validador central ----

    [Fact]
    public void Status05_Permite()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido("05"));
        Assert.True(r.Liberado);
        Assert.Equal("05", r.CodigoStatus);
        Assert.Equal(string.Empty, r.MotivoBloqueio);
    }

    [Fact]
    public void Status05_ComReleaseNaoConcluidaFalse_Permite()
        => Assert.True(ValidadorLiberacaoPedidoCompra.Validar(Pedido("05", releaseNaoConcluida: false)).Liberado);

    [Theory]
    [InlineData("03", ValidadorLiberacaoPedidoCompra.MotivoEmAprovacao)]
    [InlineData("04", ValidadorLiberacaoPedidoCompra.MotivoAguardandoLiberacao)]
    public void Status03e04_Bloqueiam(string status, string motivoEsperado)
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido(status));
        Assert.False(r.Liberado);
        Assert.Equal(motivoEsperado, r.MotivoBloqueio);
    }

    [Fact]
    public void Status08_BloqueiaComoRejeitado()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido("08"));
        Assert.False(r.Liberado);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoRejeitado, r.MotivoBloqueio);
        Assert.Equal("Rejeitado", r.DescricaoStatus);
    }

    [Fact]
    public void ReleaseNaoConcluidaTrue_Bloqueia_MesmoComSaldoEStatus05()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido("05", releaseNaoConcluida: true));
        Assert.False(r.Liberado);
        Assert.True(r.LiberacaoNaoConcluida);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoLiberacaoNaoConcluida, r.MotivoBloqueio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StatusVazio_BloqueiaPorSeguranca(string status)
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido(status));
        Assert.False(r.Liberado);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoNaoValidado, r.MotivoBloqueio);
    }

    [Fact]
    public void StatusDesconhecido_BloqueiaPorSeguranca()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(Pedido("99"));
        Assert.False(r.Liberado);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoStatusNaoLiberado, r.MotivoBloqueio);
    }

    [Fact]
    public void PedidoNulo_BloqueiaPorSeguranca()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(null);
        Assert.False(r.Liberado);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoNaoValidado, r.MotivoBloqueio);
    }

    // ---- Ajuste 9: descrição amigável ----

    [Theory]
    [InlineData("05", "Liberado/Aprovado")]
    [InlineData("03", "Em aprovação/liberação")]
    [InlineData("04", "Aguardando liberação/aprovação")]
    [InlineData("08", "Rejeitado")]
    [InlineData("99", "Status SAP não mapeado")]
    public void Descrever_MapeiaStatus(string status, string esperado)
        => Assert.Equal(esperado, ValidadorLiberacaoPedidoCompra.DescreverPurchasingProcessingStatus(status));

    // ---- Ajuste 2/3: SELECT + parse dos campos de aprovação/liberação ----

    [Fact]
    public void ApiClient_SelectCabecalho_IncluiCamposDeLiberacao()
    {
        string cliente = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "IntegracaoSap", "PedidoCompraSapApiClient.cs"));
        Assert.Contains("PurchasingProcessingStatus", cliente, StringComparison.Ordinal);
        Assert.Contains("ReleaseIsNotCompleted", cliente, StringComparison.Ordinal);
        Assert.Contains("PurchasingCompletenessStatus", cliente, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClient_Parse_LeStatusEReleaseDoCabecalho()
    {
        const string json = """
        {"PurchaseOrder":"4500001424","PurchasingProcessingStatus":"04","ReleaseIsNotCompleted":true,"PurchasingCompletenessStatus":"9"}
        """;

        PedidoCompraSap? pedido = PedidoCompraSapApiClient.MapearPedidoEspecifico(json);

        Assert.NotNull(pedido);
        Assert.Equal("04", pedido!.StatusProcessamentoCompraSap);
        Assert.True(pedido.LiberacaoNaoConcluidaSap);
        Assert.Equal("9", pedido.StatusCompletudeCompraSap);
    }

    [Fact]
    public void ApiClient_Parse_SemReleaseIsNotCompleted_NaoQuebra()
    {
        const string json = """{"PurchaseOrder":"4500001424","PurchasingProcessingStatus":"05"}""";

        PedidoCompraSap? pedido = PedidoCompraSapApiClient.MapearPedidoEspecifico(json);

        Assert.NotNull(pedido);
        Assert.Equal("05", pedido!.StatusProcessamentoCompraSap);
        Assert.Null(pedido.LiberacaoNaoConcluidaSap); // ausente → null (não bloqueia por si só)
    }

    // ---- Ajuste 5: mensagem amigável ----

    [Fact]
    public void Mensagem_Status04_MostraStatusEMotivo()
    {
        string msg = ProcessoEntradaProdutoForm.MontarMensagemPedidoNaoLiberado(new ResultadoConsultaPedido
        {
            NumeroPedido = "4500001424",
            StatusProcessamento = "04",
            DescricaoStatusProcessamento = "Aguardando liberação/aprovação",
            PedidoLiberado = false
        });

        Assert.Contains("Pedido: 4500001424", msg, StringComparison.Ordinal);
        Assert.Contains("Status atual: 04 - Aguardando liberação/aprovação", msg, StringComparison.Ordinal);
        Assert.Contains("não pode ser realizada", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Mensagem_Status08_MostraRejeitado()
    {
        string msg = ProcessoEntradaProdutoForm.MontarMensagemPedidoNaoLiberado(new ResultadoConsultaPedido
        {
            NumeroPedido = "4500001424",
            StatusProcessamento = "08",
            DescricaoStatusProcessamento = "Rejeitado",
            PedidoLiberado = false
        });

        Assert.Contains("rejeitado no SAP", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("08 - Rejeitado", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Mensagem_ReleaseNaoConcluida_MostraLiberacaoNaoConcluida()
    {
        string msg = ProcessoEntradaProdutoForm.MontarMensagemPedidoNaoLiberado(new ResultadoConsultaPedido
        {
            NumeroPedido = "4500001424",
            StatusProcessamento = "05",
            LiberacaoNaoConcluida = true,
            PedidoLiberado = false
        });

        Assert.Contains("liberação não concluída", msg, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Ajuste 6: bloqueio wired no controller e na tela ----

    [Fact]
    public void Controller_ConsultarPedido_ValidaLiberacaoEBloqueiaItens()
    {
        string controller = File.ReadAllText(Path.Combine(RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));
        Assert.Contains("ObterCabecalhoSapParaValidacaoAsync(numeroPedido", controller, StringComparison.Ordinal);
        Assert.Contains("ValidadorLiberacaoPedidoCompra.Validar(cabecalhoSap)", controller, StringComparison.Ordinal);
        Assert.Contains("if (!validacao.Liberado)", controller, StringComparison.Ordinal);
        Assert.Contains("ItensAutorizados = []", controller, StringComparison.Ordinal);
        Assert.Contains("PedidoLiberado = false", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_BloqueiaPedidoNaoLiberado_SemCarregarOperacao()
    {
        string tela = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));
        int bloqueio = tela.IndexOf("if (!resultado.PedidoLiberado)", StringComparison.Ordinal);
        int carregaItens = tela.IndexOf("PreencherItensPedidoCompra(resultado.ItensAutorizados)", StringComparison.Ordinal);

        Assert.True(bloqueio >= 0, "Bloqueio de pedido não liberado ausente na tela.");
        Assert.True(carregaItens > bloqueio, "O bloqueio deve ocorrer ANTES de carregar itens operacionais.");
        Assert.Contains("MontarMensagemPedidoNaoLiberado(resultado)", tela, StringComparison.Ordinal);
        Assert.Contains("Pedido de Compra não liberado", tela, StringComparison.Ordinal);
    }

    // ---- Ajuste 8: diagnóstico ----

    [Fact]
    public void Validador_RegistraDiagnosticoDaDecisao()
    {
        string validador = File.ReadAllText(Path.Combine(RaizProjeto(), "Modelo", "IntegracaoSap", "ValidadorLiberacaoPedidoCompra.cs"));
        Assert.Contains("[Entrada][ValidacaoPedidoCompra]", validador, StringComparison.Ordinal);
        Assert.Contains("PurchasingProcessingStatus:", validador, StringComparison.Ordinal);
        Assert.Contains("ReleaseIsNotCompleted:", validador, StringComparison.Ordinal);
        Assert.Contains("Decisao:", validador, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
