using System.Text.Json;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class LeitorConfiguracaoSapTests : IDisposable
{
    private readonly string _arquivoTemporario = Path.GetTempFileName();

    [Fact]
    public void Carregar_DeveLerCredenciaisDoArquivoQuandoAmbienteNaoInformado()
    {
        string marcadorUsuario = Guid.NewGuid().ToString("N");
        string marcadorSenha = Guid.NewGuid().ToString("N");
        GravarConfiguracao(marcadorUsuario, marcadorSenha);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            _ => null);

        Assert.Equal(marcadorUsuario, configuracao.Usuario);
        Assert.Equal(marcadorSenha, configuracao.Senha);
        Assert.True(configuracao.Configurado);
    }

    [Fact]
    public void Carregar_DevePriorizarCredenciaisDasVariaveisFugapet()
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
        Assert.Equal("base_url n?o configurada no configuracao.sap.json.", resultado.Mensagem);
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


    [Fact]
    public void Carregar_ArquivoInexistente_SemVariaveis_DeveRetornarMensagemClara()
    {
        string inexistente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(inexistente, _ => null);

        Assert.False(configuracao.ArquivoConfiguracaoSapEncontrado);
        Assert.Contains("Arquivo configuracao.sap.json n?o encontrado", configuracao.MensagemConfiguracaoBaseAusente(), StringComparison.Ordinal);
        Assert.Contains("configuracao.sap.exemplo.json", configuracao.MensagemConfiguracaoBaseAusente(), StringComparison.Ordinal);
    }

    [Fact]
    public void Carregar_DeveLerProductBaseUrlDoArquivo()
    {
        GravarConfiguracao(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_arquivoTemporario, _ => null);

        Assert.Equal("https://sap.exemplo.local/sap/opu/odata/sap/API_PRODUCT_SRV/", configuracao.ProductBaseUrl);
        Assert.Equal(configuracao.ProductBaseUrl, configuracao.ProductMasterBaseUrlEfetiva);
    }

    [Fact]
    public void Mensagens_DeChavesObrigatorias_DeveSerClara()
    {
        ConfiguracaoSap semProductionOrder = new() { ArquivoConfiguracaoSapEncontrado = true };
        ConfiguracaoSap semMaterialDocument = new() { ArquivoConfiguracaoSapEncontrado = true };
        ConfiguracaoSap semProduct = new() { ArquivoConfiguracaoSapEncontrado = true };

        Assert.Equal("production_order_base_url n?o configurada no configuracao.sap.json.", semProductionOrder.MensagemProductionOrderAusente());
        Assert.Equal("material_document_base_url n?o configurada no configuracao.sap.json.", semMaterialDocument.MensagemMaterialDocumentAusente());
        Assert.Equal("product_base_url n?o configurada no configuracao.sap.json.", semProduct.MensagemProductMasterAusente());
    }

    [Fact]
    public void Packaging_DeveLerUrlEHostsDoArquivoECredenciaisSomenteDoAmbiente()
    {
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                base_url = "https://sap.exemplo.local/odata",
                hosts_permitidos = new[] { "sap.exemplo.local" },
                packaging_base_url = "https://suite.cfapps.br10.hana.ondemand.com/http/packaging/GetPackagingSet",
                packaging_hosts_permitidos = new[] { "suite.cfapps.br10.hana.ondemand.com" },
                usuario = "std-user",
                senha = "std-pass"
            }
        });
        File.WriteAllText(_arquivoTemporario, json);

        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_PACKAGING_USERNAME"] = "pkg-user",
            ["FUGAPET_SAP_PACKAGING_PASSWORD"] = "pkg-pass"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario, nome => ambiente.GetValueOrDefault(nome));

        Assert.Equal("https://suite.cfapps.br10.hana.ondemand.com/http/packaging/GetPackagingSet", configuracao.PackagingBaseUrl);
        Assert.Contains("suite.cfapps.br10.hana.ondemand.com", configuracao.PackagingHostsPermitidos);
        Assert.Equal("pkg-user", configuracao.PackagingUsuario);
        Assert.Equal("pkg-pass", configuracao.PackagingSenha);
        Assert.True(configuracao.PackagingConfigurado);
    }


    [Fact]
    public void Carregar_DeveLerConfiguracaoInt012DoArquivoQuandoAmbienteNaoInformado()
    {
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                base_url = "https://sap.exemplo.local/odata",
                hosts_permitidos = new[] { "sap.exemplo.local" },
                pa_pipeline_habilitado = true,
                pallet_int012_base_url = "https://cpi.exemplo.local/http/int012",
                pallet_int012_hosts_permitidos = new[] { "cpi.exemplo.local" }
            }
        });
        File.WriteAllText(_arquivoTemporario, json);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
            _arquivoTemporario,
            _ => null);

        Assert.True(configuracao.ProdutoAcabadoPipelineHabilitado);
        Assert.Equal("https://cpi.exemplo.local/http/int012", configuracao.PalletInt012BaseUrl);
        Assert.Contains("cpi.exemplo.local", configuracao.PalletInt012HostsPermitidos);
    }
    [Fact]
    public void Packaging_CredenciaisNuncaVemDoArquivo_NemDasStandard()
    {
        // usuario/senha standard no arquivo NAO devem virar credencial da embalagem.
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                packaging_base_url = "https://suite.local/GetPackagingSet",
                packaging_hosts_permitidos = new[] { "suite.local" },
                usuario = "std-user",
                senha = "std-pass"
            }
        });
        File.WriteAllText(_arquivoTemporario, json);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_arquivoTemporario, _ => null);

        Assert.Equal(string.Empty, configuracao.PackagingUsuario);
        Assert.Equal(string.Empty, configuracao.PackagingSenha);
        Assert.False(configuracao.PackagingConfigurado);        // sem credencial propria
        Assert.True(configuracao.PackagingCredencialAusente);   // url+host ok, so falta credencial
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
                material_document_base_url = "https://sap.exemplo.local/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
                production_order_base_url = "https://sap.exemplo.local/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/",
                product_base_url = "https://sap.exemplo.local/sap/opu/odata/sap/API_PRODUCT_SRV/",
                sap_client = "000",
                hosts_permitidos = new[] { "sap.exemplo.local" },
                timeout_segundos = 30
            }
        });

        File.WriteAllText(_arquivoTemporario, json);
    }
}
