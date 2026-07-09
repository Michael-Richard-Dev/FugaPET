using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class GovernancaPedidoCompraSapTests
{
    [Fact]
    public void DemonstracaoSegura_DeveSelecionarMockExplicitamente()
    {
        IPedidoCompraSapServico servico =
            FabricaPedidoCompraSapServico.Criar(
                bancoHabilitado: false,
                modoDemonstracao: true,
                ambienteDemonstrativo: true);

        Assert.True(servico.EhSimulado);
        Assert.False(servico.SapConfigurado);
    }

    [Fact]
    public void HomologacaoOuProducao_DeveSelecionarImplementacaoReal()
    {
        IPedidoCompraSapServico servico =
            FabricaPedidoCompraSapServico.Criar(
                bancoHabilitado: true,
                modoDemonstracao: false,
                ambienteDemonstrativo: false);

        Assert.False(servico.EhSimulado);
        Assert.IsType<PedidoCompraSapGovernadoServico>(servico);
    }

    [Fact]
    public void HomologacaoSemConfiguracao_NuncaDeveUsarMockSilencioso()
    {
        IPedidoCompraSapServico servico =
            FabricaPedidoCompraSapServico.Criar(
                bancoHabilitado: true,
                modoDemonstracao: false,
                ambienteDemonstrativo: false);

        Assert.False(servico.EhSimulado);
        Assert.IsNotType<PedidoCompraSapMockServico>(servico);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void CombinacaoNaoPermitida_NuncaDeveSelecionarMock(
        bool bancoHabilitado,
        bool modoDemonstracao)
    {
        IPedidoCompraSapServico servico =
            FabricaPedidoCompraSapServico.Criar(
                bancoHabilitado,
                modoDemonstracao,
                ambienteDemonstrativo: true);

        Assert.False(servico.EhSimulado);
        Assert.IsNotType<PedidoCompraSapMockServico>(servico);
    }

    [Fact]
    public void Composicao_NaoDeveInstanciarImplementacaoSapConcreta()
    {
        // H9 Etapa 5: a composicao SAP (escolha mock/real via fabrica) vive no controller; a tela
        // nao decide mock/real nem fala direto com a implementacao SAP.
        string form = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));
        string seam = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Servicos", "IntegracaoSap", "IntegracaoEntradaSapServico.cs"));

        // A fabrica (decisao mock/real) fica no controller, nunca na tela.
        Assert.Contains("FabricaPedidoCompraSapServico.Criar()", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("FabricaPedidoCompraSapServico.Criar()", form, StringComparison.Ordinal);

        // Ninguem instancia implementacao SAP concreta diretamente.
        foreach (string conteudo in new[] { form, controller, seam })
        {
            Assert.DoesNotContain("new SincronizacaoPedidoCompraSapServico", conteudo, StringComparison.Ordinal);
            Assert.DoesNotContain("new IntegracaoSapMockServico", conteudo, StringComparison.Ordinal);
        }

        // O seam recebe a abstracao por construtor (nao escolhe implementacao).
        Assert.Contains("IPedidoCompraSapServico", seam, StringComparison.Ordinal);
    }

    [Fact]
    public void ServicoReal_NaoDeveGerarMockAutomaticamente()
    {
        string arquivo = Path.Combine(
            RaizProjeto(),
            "Servicos",
            "IntegracaoSap",
            "SincronizacaoPedidoCompraSapServico.cs");
        string conteudo = File.ReadAllText(arquivo);

        Assert.DoesNotContain("Mock", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Simular", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChaveCentralDesativada_DeveBloquear()
    {
        EstadoIntegracaoSapServico estado = CriarEstado(
            integracaoAtiva: false,
            configurado: true,
            autorizado: true);

        var resultado = await estado.ValidarAsync(OperacaoIntegracaoSap.Consulta);

        Assert.False(resultado.Sucesso);
        Assert.Contains("desativada", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfiguracaoSapInvalida_DeveBloquear()
    {
        EstadoIntegracaoSapServico estado = CriarEstado(
            integracaoAtiva: true,
            configurado: false,
            autorizado: true);

        var resultado = await estado.ValidarAsync(OperacaoIntegracaoSap.Sincronizacao);

        Assert.False(resultado.Sucesso);
        Assert.Equal("base_url n?o configurada no configuracao.sap.json.", resultado.Mensagem);
    }

    [Fact]
    public async Task UsuarioNaoAutorizado_DeveBloquear()
    {
        EstadoIntegracaoSapServico estado = CriarEstado(
            integracaoAtiva: true,
            configurado: true,
            autorizado: false);

        var resultado = await estado.ValidarAsync(OperacaoIntegracaoSap.Escrita);

        Assert.False(resultado.Sucesso);
        Assert.Contains("permissao", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TodasCondicoesValidas_DevemPermitirTodasOperacoes()
    {
        EstadoIntegracaoSapServico estado = CriarEstado(
            integracaoAtiva: true,
            configurado: true,
            autorizado: true);

        foreach (OperacaoIntegracaoSap operacao in Enum.GetValues<OperacaoIntegracaoSap>())
        {
            var resultado = await estado.ValidarAsync(operacao);
            Assert.True(resultado.Sucesso);
        }
    }

    private static EstadoIntegracaoSapServico CriarEstado(
        bool integracaoAtiva,
        bool configurado,
        bool autorizado)
    {
        ConfiguracaoSap configuracao = configurado
            ? new ConfiguracaoSap
            {
                BaseUrl = "https://sap.exemplo.local/odata",
                Usuario = "usuario-teste",
                Senha = "senha-teste",
                HostsPermitidos = ["sap.exemplo.local"]
            }
            : new ConfiguracaoSap();

        return new EstadoIntegracaoSapServico(
            configuracao,
            _ => Task.FromResult<bool?>(integracaoAtiva),
            _ => autorizado,
            auditoriaServico: null,
            ambientePermitido: true);
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
