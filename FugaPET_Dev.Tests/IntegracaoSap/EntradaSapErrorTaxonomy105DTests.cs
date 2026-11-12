using System.Net;
using System.Security.Authentication;
using System.Text;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 105D — taxonomia de falhas da consulta SAP na Entrada.
///
/// Defeito eliminado: falha TÉCNICA (401/403/rede/TLS/timeout/5xx/config) era engolida por
/// catch(Exception) → null → ValidadorLiberacaoPedidoCompra.Validar(null) → PedidoLiberado=false →
/// "Pedido de Compra não liberado". Após este gate, falha técnica NUNCA vira status de negócio.
///
/// Provas por unidade, sem DB/SAP real (HttpMessageHandler falso e tabela de mensagens de aplicação).
/// </summary>
public sealed class EntradaSapErrorTaxonomy105DTests
{
    // ---------------- infraestrutura ----------------

    private static ConfiguracaoSap CriarConfiguracao()
        => new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            SapClient = "000",
            HostsPermitidos = ["sap.exemplo.local"]
        };

    private sealed class RespostaHandler(HttpResponseMessage resposta) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(resposta);
    }

    private sealed class ExcecaoHandler(Exception excecao) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(excecao);
    }

    private sealed class EsperaCancelamentoHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static PedidoCompraSapApiClient ClienteComResposta(HttpStatusCode status, string corpo = "{}")
        => new(
            CriarConfiguracao(),
            new HttpClient(new RespostaHandler(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            })));

    private static PedidoCompraSapApiClient ClienteComExcecao(Exception excecao)
        => new(CriarConfiguracao(), new HttpClient(new ExcecaoHandler(excecao)));

    private static async Task<ConsultaSapException> CapturarFalhaAsync(PedidoCompraSapApiClient cliente)
        => await Assert.ThrowsAsync<ConsultaSapException>(() => cliente.ConsultarPedidoAsync("4500000010"));

    // Textos de negócio que JAMAIS podem aparecer numa falha técnica.
    private static void AssertSemCausaDeNegocio(string texto)
    {
        Assert.DoesNotContain("não liberado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nao liberado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ainda não liberado/aprovado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rejeitado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aprovação", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSemCausaDeCredencial(string texto)
    {
        Assert.DoesNotContain("credencia", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("senha", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuário", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // §13 — classificação técnica na borda (cliente HTTP)
    // ==================================================================

    [Fact] // 401 → NAO_AUTENTICADO
    public async Task Http401_ClassificaNaoAutenticado()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(ClienteComResposta(HttpStatusCode.Unauthorized));
        Assert.Equal(CenarioFalhaConsultaSap.NaoAutenticado, erro.Cenario);
        Assert.Equal(401, erro.HttpStatus);
    }

    [Fact] // 403 → SEM_AUTORIZACAO
    public async Task Http403_ClassificaSemAutorizacao()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(ClienteComResposta(HttpStatusCode.Forbidden));
        Assert.Equal(CenarioFalhaConsultaSap.SemAutorizacao, erro.Cenario);
        Assert.Equal(403, erro.HttpStatus);
    }

    [Fact] // 404 → contrato de ausência preservado (null), NÃO é falha técnica.
    public async Task Http404_RetornaNuloSemExcecao()
    {
        PedidoCompraSap? pedido = await ClienteComResposta(HttpStatusCode.NotFound)
            .ConsultarPedidoAsync("4500000010");
        Assert.Null(pedido);
    }

    [Theory] // 5xx → SAP_INDISPONIVEL
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    [InlineData(HttpStatusCode.BadGateway, 502)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task Http5xx_ClassificaSapIndisponivel(HttpStatusCode status, int esperado)
    {
        ConsultaSapException erro = await CapturarFalhaAsync(ClienteComResposta(status));
        Assert.Equal(CenarioFalhaConsultaSap.SapIndisponivel, erro.Cenario);
        Assert.Equal(esperado, erro.HttpStatus);
    }

    [Fact] // outros não-sucesso → HTTP_NAO_CLASSIFICADO
    public async Task HttpOutro_ClassificaNaoClassificado()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(ClienteComResposta(HttpStatusCode.Conflict));
        Assert.Equal(CenarioFalhaConsultaSap.HttpNaoClassificado, erro.Cenario);
    }

    [Fact] // 2xx com JSON inválido → RESPOSTA_INVALIDA
    public async Task Http2xxJsonInvalido_ClassificaRespostaInvalida()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(
            ClienteComResposta(HttpStatusCode.OK, "{ json-invalido"));
        Assert.Equal(CenarioFalhaConsultaSap.RespostaInvalida, erro.Cenario);
    }

    [Fact] // rede sem status → CONECTIVIDADE (e não credencial)
    public async Task RedeSemStatus_ClassificaConectividade()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(
            ClienteComExcecao(new HttpRequestException("falha de socket")));
        Assert.Equal(CenarioFalhaConsultaSap.Conectividade, erro.Cenario);
    }

    [Fact] // AuthenticationException na cadeia → TLS
    public async Task HandshakeTls_ClassificaTls()
    {
        ConsultaSapException erro = await CapturarFalhaAsync(
            ClienteComExcecao(new HttpRequestException(
                "falha TLS", new AuthenticationException("handshake"))));
        Assert.Equal(CenarioFalhaConsultaSap.Tls, erro.Cenario);
    }

    [Fact] // timeout do HttpClient SEM cancelamento do chamador → TIMEOUT
    public async Task TimeoutSemCancelamentoDoChamador_ClassificaTimeout()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler()) { Timeout = TimeSpan.FromMilliseconds(50) };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ConsultaSapException erro = await Assert.ThrowsAsync<ConsultaSapException>(
            () => cliente.ConsultarPedidoAsync("4500000010"));
        Assert.Equal(CenarioFalhaConsultaSap.Timeout, erro.Cenario);
    }

    [Fact] // cancelamento do chamador → propagado, SEM erro técnico falso
    public async Task CancelamentoDoChamador_PropagaSemFalhaTecnica()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);
        using CancellationTokenSource cancelamento = new();
        await cancelamento.CancelAsync();

        Exception erro = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ConsultarPedidoAsync("4500000010", cancelamento.Token));
        Assert.IsNotType<ConsultaSapException>(erro);
    }

    // ==================================================================
    // §13 — CONFIGURACAO_INVALIDA (consulta nem pode ser executada)
    // ==================================================================

    [Fact]
    public async Task ConfiguracaoNaoConfigurada_ClassificaConfiguracaoInvalida()
    {
        // Configuração vazia ⇒ Configurado == false: a consulta nem pode ser executada.
        ConfiguracaoSap semConfiguracao = new();
        using HttpClient http = new(new RespostaHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        SincronizacaoPedidoCompraSapServico servico = new(
            semConfiguracao,
            null!,
            new PedidoCompraSapApiClient(CriarConfiguracao(), http));

        ConsultaSapException erro = await Assert.ThrowsAsync<ConsultaSapException>(
            () => servico.ObterCabecalhoSapParaValidacaoAsync("4500000010"));

        Assert.Equal(CenarioFalhaConsultaSap.ConfiguracaoInvalida, erro.Cenario);
    }

    // ==================================================================
    // §10/§11/§18 — mensagens de aplicação: causa correta, sem contaminação de negócio
    // ==================================================================

    [Fact] // O defeito original: 401 jamais pode falar em pedido não liberado.
    public void Mensagem401_FalaAutenticacao_NuncaPedidoNaoLiberado()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.NaoAutenticado, "4500000010");

        Assert.Equal("Acesso ao SAP não autorizado", titulo);
        AssertSemCausaDeNegocio(titulo);
        AssertSemCausaDeNegocio(mensagem);
        Assert.Contains("credenciais", mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // 403 fala em AUTORIZAÇÃO — nunca em senha incorreta.
    public void Mensagem403_FalaAutorizacao_NuncaSenhaIncorreta()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.SemAutorizacao);

        Assert.Equal("Acesso ao serviço SAP negado", titulo);
        Assert.Contains("autorização", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("senha", mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact]
    public void Mensagem404_IdentificaPedido()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.NaoEncontrado, "4500000010");

        Assert.Equal("Pedido de Compra não encontrado", titulo);
        Assert.Contains("4500000010", mensagem, StringComparison.Ordinal);
    }

    [Fact] // Timeout não fala em credencial nem em pedido não liberado.
    public void MensagemTimeout_SemCredencialSemNegocio()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.Timeout);

        Assert.Equal("SAP não respondeu", titulo);
        AssertSemCausaDeCredencial(mensagem);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact] // Rede não fala em credencial nem em pedido não liberado.
    public void MensagemConectividade_SemCredencialSemNegocio()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.Conectividade);

        Assert.Equal("Falha de comunicação com o SAP", titulo);
        AssertSemCausaDeCredencial(mensagem);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact]
    public void MensagemTls_FalaConexaoSegura()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.Tls);

        Assert.Equal("Falha de segurança na conexão com o SAP", titulo);
        Assert.Contains("conexão segura", mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact] // 5xx não pode falar em pedido não liberado.
    public void Mensagem5xx_SapIndisponivel_SemNegocio()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.SapIndisponivel);

        Assert.Equal("SAP indisponível", titulo);
        AssertSemCausaDeNegocio(mensagem);
        AssertSemCausaDeCredencial(mensagem);
    }

    [Fact]
    public void MensagemRespostaInvalida_BloqueiaEPedeSuporte()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.RespostaInvalida);

        Assert.Equal("Resposta do SAP inválida", titulo);
        Assert.Contains("bloqueada", mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact]
    public void MensagemConfiguracaoInvalida_PedeSuporte()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.ConfiguracaoInvalida);

        Assert.Equal("Configuração da integração SAP inválida", titulo);
        Assert.Contains("configuração", mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemCausaDeNegocio(mensagem);
    }

    [Fact]
    public void MensagemStatusDesconhecido_NaoAfirmaNaoLiberado()
    {
        (string titulo, string mensagem) =
            MensagensFalhaConsultaSap.Descrever(CenarioFalhaConsultaSap.StatusNegocioDesconhecido);

        Assert.Equal("Status do pedido não pôde ser validado", titulo);
        Assert.Contains("não foi possível determinar", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ainda não liberado/aprovado", mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // §9 — status FUNCIONAIS preservados (resposta SAP tecnicamente válida)
    // ==================================================================

    private static PedidoCompraSap PedidoComStatus(string status, bool? liberacaoNaoConcluida = false)
        => new()
        {
            Numero = "4500000010",
            StatusProcessamentoCompraSap = status,
            LiberacaoNaoConcluidaSap = liberacaoNaoConcluida
        };

    [Fact] // 05 → liberado (fluxo normal)
    public void Status05_Liberado()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(PedidoComStatus("05"));
        Assert.True(r.Liberado);
        Assert.False(r.StatusDesconhecido);
    }

    [Theory] // 03/04/08 → bloqueio FUNCIONAL real, com motivo de negócio e status conhecido
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("08")]
    public void StatusFuncionaisBloqueantes_MantemSemanticaDeNegocio(string status)
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(PedidoComStatus(status));

        Assert.False(r.Liberado);
        Assert.False(r.StatusDesconhecido); // status CONHECIDO: é bloqueio de negócio, não indeterminação
        Assert.Equal(status, r.CodigoStatus);
        Assert.NotEmpty(r.MotivoBloqueio);
    }

    [Fact] // 08 → rejeitado
    public void Status08_Rejeitado()
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(PedidoComStatus("08"));
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoRejeitado, r.MotivoBloqueio);
    }

    [Fact] // ReleaseIsNotCompleted=true bloqueia mesmo com 05
    public void ReleaseIsNotCompleted_BloqueiaMesmoCom05()
    {
        ResultadoValidacaoPedidoCompra r =
            ValidadorLiberacaoPedidoCompra.Validar(PedidoComStatus("05", liberacaoNaoConcluida: true));

        Assert.False(r.Liberado);
        Assert.True(r.LiberacaoNaoConcluida);
        Assert.False(r.StatusDesconhecido);
        Assert.Equal(ValidadorLiberacaoPedidoCompra.MotivoLiberacaoNaoConcluida, r.MotivoBloqueio);
    }

    [Theory] // status vazio ou não mapeado em resposta VÁLIDA → indeterminação, não "não liberado"
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("99")]
    public void StatusVazioOuDesconhecido_MarcaIndeterminacao(string status)
    {
        ResultadoValidacaoPedidoCompra r = ValidadorLiberacaoPedidoCompra.Validar(PedidoComStatus(status));

        Assert.False(r.Liberado);
        Assert.True(r.StatusDesconhecido);
        Assert.NotEqual(ValidadorLiberacaoPedidoCompra.MotivoAguardandoLiberacao, r.MotivoBloqueio);
    }

    // ==================================================================
    // §2/§8/§18 — seam do controller: falha técnica NÃO vira veredito de negócio
    // ==================================================================

    [Theory] // O coração do defeito: 401/403/5xx/rede/TLS/timeout/config nunca marcam "pedido não liberado".
    [InlineData(CenarioFalhaConsultaSap.NaoAutenticado, 401)]
    [InlineData(CenarioFalhaConsultaSap.SemAutorizacao, 403)]
    [InlineData(CenarioFalhaConsultaSap.SapIndisponivel, 500)]
    [InlineData(CenarioFalhaConsultaSap.Conectividade, null)]
    [InlineData(CenarioFalhaConsultaSap.Tls, null)]
    [InlineData(CenarioFalhaConsultaSap.Timeout, null)]
    [InlineData(CenarioFalhaConsultaSap.RespostaInvalida, 200)]
    [InlineData(CenarioFalhaConsultaSap.ConfiguracaoInvalida, null)]
    public void Controller_FalhaTecnica_NaoMarcaPedidoNaoLiberado(
        CenarioFalhaConsultaSap cenario,
        int? httpStatus)
    {
        ResultadoConsultaPedido r = FugaPET_Dev.Controle.Processo.EntradaProdutoController
            .MontarFalhaConsultaSap(null, "4500000010", cenario, httpStatus, "corr-1");

        // Consulta não concluída tecnicamente…
        Assert.False(r.Sucesso);
        Assert.False(r.ConsultaTecnicaOk);
        Assert.True(r.FalhaTecnicaSap);
        Assert.Equal(cenario, r.CenarioFalhaSap);
        Assert.Equal(httpStatus, r.HttpStatusFalha);

        // …e NENHUM veredito de negócio é emitido.
        Assert.True(r.PedidoLiberado);                 // não afirma "não liberado"
        Assert.Empty(r.MotivoBloqueioLiberacao);
        Assert.Empty(r.StatusProcessamento);
        Assert.Empty(r.DescricaoStatusProcessamento);

        // Título/mensagem são os do cenário, sem contaminação de negócio.
        Assert.Equal(MensagensFalhaConsultaSap.Descrever(cenario, "4500000010").Titulo, r.TituloFalha);
        AssertSemCausaDeNegocio(r.TituloFalha);
        AssertSemCausaDeNegocio(r.Mensagem);
    }

    [Fact] // 404 é resultado conhecido: "não encontrado", nunca "não liberado".
    public void Controller_NaoEncontrado_UsaTituloProprio()
    {
        ResultadoConsultaPedido r = FugaPET_Dev.Controle.Processo.EntradaProdutoController
            .MontarFalhaConsultaSap(null, "4500000010", CenarioFalhaConsultaSap.NaoEncontrado, 404, null);

        Assert.Equal("Pedido de Compra não encontrado", r.TituloFalha);
        Assert.Contains("4500000010", r.Mensagem, StringComparison.Ordinal);
        Assert.True(r.PedidoLiberado);
        Assert.Empty(r.MotivoBloqueioLiberacao);
    }

    // ==================================================================
    // §2/§18 — invariante central: nenhum cenário TÉCNICO produz texto de negócio
    // ==================================================================

    [Theory]
    [InlineData(CenarioFalhaConsultaSap.NaoAutenticado)]
    [InlineData(CenarioFalhaConsultaSap.SemAutorizacao)]
    [InlineData(CenarioFalhaConsultaSap.Timeout)]
    [InlineData(CenarioFalhaConsultaSap.Conectividade)]
    [InlineData(CenarioFalhaConsultaSap.Tls)]
    [InlineData(CenarioFalhaConsultaSap.SapIndisponivel)]
    [InlineData(CenarioFalhaConsultaSap.RespostaInvalida)]
    [InlineData(CenarioFalhaConsultaSap.ConfiguracaoInvalida)]
    [InlineData(CenarioFalhaConsultaSap.HttpNaoClassificado)]
    public void FalhaTecnica_NuncaViraStatusDeNegocio(CenarioFalhaConsultaSap cenario)
    {
        (string titulo, string mensagem) = MensagensFalhaConsultaSap.Descrever(cenario, "4500000010");

        AssertSemCausaDeNegocio(titulo);
        AssertSemCausaDeNegocio(mensagem);
    }
}
