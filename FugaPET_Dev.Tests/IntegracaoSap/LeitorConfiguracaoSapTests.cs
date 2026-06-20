using System.Text.Json;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class LeitorConfiguracaoSapTests : IDisposable
{
    private readonly string _arquivoTemporario = Path.GetTempFileName();

    [Fact]
    public void Carregar_DeveIgnorarCredenciaisPresentesNoArquivo()
    {
        string marcadorUsuario = Guid.NewGuid().ToString("N");
        string marcadorSenha = Guid.NewGuid().ToString("N");
        GravarConfiguracao(marcadorUsuario, marcadorSenha);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            _ => null);

        Assert.Empty(configuracao.Usuario);
        Assert.Empty(configuracao.Senha);
        Assert.False(configuracao.Configurado);
    }

    [Fact]
    public void Carregar_DeveUsarCredenciaisSomenteDasVariaveisFugapet()
    {
        string marcadorUsuario = Guid.NewGuid().ToString("N");
        string marcadorSenha = Guid.NewGuid().ToString("N");
        GravarConfiguracao(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_USERNAME"] = marcadorUsuario,
            ["FUGAPET_SAP_PASSWORD"] = marcadorSenha,
            ["FUGAPET_SAP_ALLOWED_HOSTS"] = "sap.exemplo.local"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            nome => ambiente.GetValueOrDefault(nome));

        Assert.Equal(marcadorUsuario, configuracao.Usuario);
        Assert.Equal(marcadorSenha, configuracao.Senha);
        Assert.True(configuracao.Configurado);
    }

    [Fact]
    public async Task SincronizarSemSegredos_DeveFalharComMensagemSegura()
    {
        SincronizacaoPedidoCompraSapServico servico = new(new ConfiguracaoSap(), null!);

        ResultadoOperacao resultado = await servico.SincronizarPedidoAsync("4500000010");

        Assert.False(resultado.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemConfiguracaoAusente, resultado.Mensagem);
    }

    [Fact]
    public void Carregar_SemChaveDeEscrita_DeveManterEscritaDesabilitada()
    {
        GravarConfiguracao(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            _ => null);

        Assert.False(configuracao.EscritaHabilitada);
    }

    [Fact]
    public void Carregar_ComChaveDeEscritaTrue_DeveLerValorSemLiberarFluxo()
    {
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                base_url = "https://sap.exemplo.local/odata",
                hosts_permitidos = new[] { "sap.exemplo.local" },
                escrita_habilitada = true
            }
        });
        File.WriteAllText(_arquivoTemporario, json);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            _ => null);

        Assert.True(configuracao.EscritaHabilitada);
    }

    [Fact]
    public void Carregar_ComVariavelDeEscritaTrue_DeveSobrescreverArquivo()
    {
        GravarConfiguracao(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_WRITE_ENABLED"] = "true"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            nome => ambiente.GetValueOrDefault(nome));

        Assert.True(configuracao.EscritaHabilitada);
    }

    [Fact]
    public void ObterVariavelAmbiente_SemValorNoProcesso_DeveUsarPerfilDoUsuario()
    {
        string? valor = LeitorConfiguracaoSap.ObterVariavelAmbiente(
            "FUGAPET_SAP_WRITE_ENABLED",
            _ => null,
            _ => "true");

        Assert.Equal("true", valor);
    }

    [Fact]
    public void ObterVariavelAmbiente_ComValorNoProcesso_DeveTerPrioridade()
    {
        string? valor = LeitorConfiguracaoSap.ObterVariavelAmbiente(
            "FUGAPET_SAP_WRITE_ENABLED",
            _ => "false",
            _ => "true");

        Assert.Equal("false", valor);
    }

    [Fact]
    public void Carregar_ArquivoInexistente_ComVariaveis_DeveCarregarDoAmbiente()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_BASE_URL"] = "https://sap.exemplo.local/odata",
            ["FUGAPET_SAP_USERNAME"] = "usuario",
            ["FUGAPET_SAP_PASSWORD"] = "senha",
            ["FUGAPET_SAP_ALLOWED_HOSTS"] = "sap.exemplo.local"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            inexistente,
            nome => ambiente.GetValueOrDefault(nome));

        Assert.True(configuracao.Configurado);
    }

    [Fact]
    public void Carregar_ArquivoInexistente_SemVariaveis_NaoDeveConfigurar()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(inexistente, _ => null);

        Assert.False(configuracao.Configurado);
    }

    [Fact]
    public void Carregar_ArquivoMalformado_DeveLancarConfiguracaoInvalida()
    {
        // JSON malformado e ERRO DE IMPLANTACAO: erro controlado, nunca "nao configurado".
        File.WriteAllText(_arquivoTemporario, "{ isto nao e json valido ");

        Assert.Throws<ConfiguracaoSapInvalidaException>(
            () => LeitorConfiguracaoSap.Carregar(_arquivoTemporario, _ => null));
    }

    public void Dispose()
    {
        File.Delete(_arquivoTemporario);
    }

    private void GravarConfiguracao(string marcadorUsuario, string marcadorSenha)
    {
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                base_url = "https://sap.exemplo.local/odata",
                usuario = marcadorUsuario,
                senha = marcadorSenha,
                sap_client = "000",
                hosts_permitidos = new[] { "sap.exemplo.local" },
                timeout_segundos = 30
            }
        });

        File.WriteAllText(_arquivoTemporario, json);
    }
}
