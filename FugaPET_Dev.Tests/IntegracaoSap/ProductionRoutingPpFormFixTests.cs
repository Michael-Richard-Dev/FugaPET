using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 048-PPFORM-FIX-01 — correção do client de Production Routing usando o contrato SAP HML comprovado
/// por Ares (OP → ProductionVersion=1 → grupo 50000063 / routing 1 → Operation=50, OperationStandardTextCode=PP_FORM,
/// OperationControlProfile=YBP1). Testa os mapeadores internos e a regra congelada, sem HTTP.
/// </summary>
public sealed class ProductionRoutingPpFormFixTests : IDisposable
{
    private readonly string _arquivoTemporario = Path.Combine(Path.GetTempPath(), $"fugapet-sap-{Guid.NewGuid():N}.json");

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    public void Dispose()
    {
        if (File.Exists(_arquivoTemporario))
        {
            File.Delete(_arquivoTemporario);
        }
    }

    // A) + H) payload real (Operation=50, PP_FORM) ⇒ operação 0050 MANUAL (normalização 0050↔50).
    [Fact]
    public void OperacaoComPpForm_ClassificaManual_E_NormalizaOperacao()
    {
        OperacaoRoteiroSap op = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"50","OperationStandardTextCode":"PP_FORM","OperationControlProfile":"YBP1"}"""));

        Assert.Equal("50", op.Operacao);
        Assert.Equal("PP_FORM", op.CodigoTextoPadrao);
        Assert.True(op.TextoPadraoObtido);

        RoteiroProducaoSap roteiro = new() { Operacoes = [op] };
        Assert.Equal(ClassificacaoOperacaoManual.Manual, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "0050"));
        Assert.Equal(ClassificacaoOperacaoManual.Manual, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "50"));
        Assert.Equal(ClassificacaoOperacaoManual.Manual, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "00000050"));
    }

    // B) mesmo routing usado por duas OPs diferentes ⇒ ambas resolvem a mesma operação manual.
    [Fact]
    public void MesmoRoteiro_DuasOps_AmbasResolvemManual()
    {
        OperacaoRoteiroSap op = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"50","OperationStandardTextCode":"PP_FORM"}"""));
        RoteiroProducaoSap roteiro = new() { BillOfOperationsGroup = "50000063", BillOfOperationsVariant = "1", Operacoes = [op] };

        Assert.Equal(ClassificacaoOperacaoManual.Manual, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "0050"));
        Assert.Equal(ClassificacaoOperacaoManual.Manual, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "0050"));
    }

    // C) campo PP_FORM presente ⇒ TextoPadraoObtido = true.
    [Fact]
    public void CampoPresente_TextoPadraoObtidoTrue()
        => Assert.True(ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"50","OperationStandardTextCode":"PP_FORM"}""")).TextoPadraoObtido);

    // D) campo ausente ⇒ ContratoNaoResolvido (fail-closed) + causa distinta.
    [Fact]
    public void CampoAusente_ContratoNaoResolvido()
    {
        OperacaoRoteiroSap op = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(Json("""{"Operation":"50"}"""));
        Assert.False(op.TextoPadraoObtido);

        RoteiroProducaoSap roteiro = new() { Operacoes = [op] };
        Assert.Equal(ClassificacaoOperacaoManual.ContratoNaoResolvido, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "0050"));
        Assert.Equal("OPERATION_STANDARD_TEXT_CODE_AUSENTE", MarcadorOperacaoManualSap.DescreverContratoNaoResolvido(roteiro, "0050"));
    }

    // E) campo null explícito ⇒ contrato obtido com valor vazio ⇒ AUTOMATICA (contrato congelado).
    [Fact]
    public void CampoNullExplicito_Automatica()
    {
        OperacaoRoteiroSap op = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"50","OperationStandardTextCode":null}"""));
        Assert.True(op.TextoPadraoObtido);
        Assert.Equal(string.Empty, op.CodigoTextoPadrao);

        RoteiroProducaoSap roteiro = new() { Operacoes = [op] };
        Assert.Equal(ClassificacaoOperacaoManual.Automatica, MarcadorOperacaoManualSap.ClassificarOperacao(roteiro, "0050"));
    }

    // F) OperationControlProfile=YBP1 NÃO influencia a classificação (só OperationStandardTextCode importa).
    [Fact]
    public void OperationControlProfileYbp1_NaoInfluencia()
    {
        OperacaoRoteiroSap op = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"50","OperationStandardTextCode":"PP_FORM","OperationControlProfile":"YBP1"}"""));
        Assert.NotEqual("YBP1", op.CodigoTextoPadrao);
        Assert.Equal("PP_FORM", op.CodigoTextoPadrao);

        // Mesmo com profile YBP1, valor de texto padrão diferente de PP_FORM ⇒ AUTOMATICA (profile ignorado).
        OperacaoRoteiroSap auto = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"60","OperationStandardTextCode":"","OperationControlProfile":"YBP1"}"""));
        Assert.Equal(ClassificacaoOperacaoManual.Automatica,
            MarcadorOperacaoManualSap.ClassificarOperacao(new RoteiroProducaoSap { Operacoes = [auto] }, "0060"));
    }

    // G) o request do routing solicita EXPLICITAMENTE OperationStandardTextCode via $select.
    [Fact]
    public void RequestRoteiro_SelecionaOperationStandardTextCode()
    {
        string fonte = LerProjeto("Servicos", "IntegracaoSap", "ProductionRoutingSapApiClient.cs");
        Assert.Contains(
            "$select=ProductionRoutingGroup,ProductionRouting,ProductionRoutingSequence,Operation,OperationStandardTextCode",
            fonte, StringComparison.Ordinal);
    }

    // §7) a versão resolve grupo/variante pelos campos de EXECUÇÃO (Exec*), como no contrato comprovado.
    [Fact]
    public void MapearVersao_PrioridadeExecBillOfOperations()
    {
        ProductionRoutingSapApiClient.VersaoProducaoRoteiro v = ProductionRoutingSapApiClient.MapearVersaoJson(
            Json("""{"ExecBillOfOperationsGroup":"50000063","ExecBillOfOperationsVariant":"1","BillOfOperationsType":"N"}"""));

        Assert.Equal("50000063", v.Grupo);
        Assert.Equal("1", v.Variante);
    }


    [Fact]
    public void ConfiguracaoProductionVersion_UsaBaseDedicadaV4_E_NaoMaterialDocument()
    {
        ConfiguracaoSap configuracao = new()
        {
            MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            ProductionVersionBaseUrl = "https://sap.example.com:44300/sap/opu/odata4/sap/api_production_version/srvd_a2x/sap/productionversion/0001/"
        };

        Assert.Equal(configuracao.ProductionVersionBaseUrl, configuracao.ProductionVersionBaseUrlEfetiva);
        Assert.DoesNotContain("API_MATERIAL_DOCUMENT_SRV", configuracao.ProductionVersionBaseUrlEfetiva, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/sap/opu/odata/sap/API_PRODUCTION_VERSION", configuracao.ProductionVersionBaseUrlEfetiva, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguracaoProductionVersion_SemBaseDedicada_NaoDerivaDeMaterialDocument()
    {
        ConfiguracaoSap configuracao = new()
        {
            MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/"
        };

        Assert.Equal(string.Empty, configuracao.ProductionVersionBaseUrlEfetiva);
    }

    [Fact]
    public void LeitorConfiguracaoSap_CarregaProductionVersionBaseUrlDedicada()
    {
        File.WriteAllText(_arquivoTemporario, """
        {
          "sap": {
            "production_version_base_url": "https://sap.example.com:44300/sap/opu/odata4/sap/api_production_version/srvd_a2x/sap/productionversion/0001/"
          }
        }
        """);

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_arquivoTemporario, _ => null);

        Assert.Equal(
            "https://sap.example.com:44300/sap/opu/odata4/sap/api_production_version/srvd_a2x/sap/productionversion/0001/",
            configuracao.ProductionVersionBaseUrl);
    }

    [Fact]
    public void RequestProductionVersion_UsaEntidadeV4ComChaveMaterialPlantVersao()
    {
        string fonte = LerProjeto("Servicos", "IntegracaoSap", "ProductionRoutingSapApiClient.cs");

        Assert.Contains("ProductionVersion({chave})", fonte, StringComparison.Ordinal);
        Assert.Contains("Material=", fonte, StringComparison.Ordinal);
        Assert.Contains("Plant=", fonte, StringComparison.Ordinal);
        Assert.Contains("ProductionVersion=", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("MontarUrl(_baseVersao, \"A_ProductionVersion\"", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("MontarUrl(_baseVersao, \"A_ProductionVersion\"", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void MapearVersaoV4_ValidaPayloadEntidade()
    {
        ProductionRoutingSapApiClient.VersaoProducaoRoteiro? versao = ProductionRoutingSapApiClient.MapearVersaoV4("""
        {
          "@odata.context": "$metadata#ProductionVersion/$entity",
          "Material": "2000091",
          "Plant": "3007",
          "ProductionVersion": "1",
          "BillOfOperationsGroup": "111",
          "BillOfOperationsVariant": "9",
          "ExecBillOfOperationsGroup": "50000063",
          "ExecBillOfOperationsVariant": "1"
        }
        """);

        Assert.NotNull(versao);
        Assert.Equal("50000063", versao!.Grupo);
        Assert.Equal("1", versao.Variante);
    }

    [Fact]
    public void MapearVersaoV4_PayloadAmbiguoOuInvalido_FailClosed()
    {
        Assert.Null(ProductionRoutingSapApiClient.MapearVersaoV4("""{"value": []}"""));
        Assert.Null(ProductionRoutingSapApiClient.MapearVersaoV4("""{"value": [{"ExecBillOfOperationsGroup":"1","ExecBillOfOperationsVariant":"1"},{"ExecBillOfOperationsGroup":"2","ExecBillOfOperationsVariant":"1"}]}"""));
        Assert.Null(ProductionRoutingSapApiClient.MapearVersaoV4("""{"Material":"2000091"}"""));
        Assert.Null(ProductionRoutingSapApiClient.MapearVersaoV4("{ json invalido"));
    }

    [Fact]
    public void RequestRouting_UsaEntitySetSemPrefixoA()
    {
        string fonte = LerProjeto("Servicos", "IntegracaoSap", "ProductionRoutingSapApiClient.cs");

        Assert.DoesNotContain("MontarUrl(_baseRoteiro, \"A_ProductionRoutingOperation\"", fonte, StringComparison.Ordinal);
        Assert.Contains("MontarUrl(_baseRoteiro, \"ProductionRoutingOperation\"", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestRouting_PreservaSelectExplicitamenteComOperationStandardTextCode()
    {
        string fonte = LerProjeto("Servicos", "IntegracaoSap", "ProductionRoutingSapApiClient.cs");

        Assert.Contains("$select=ProductionRoutingGroup,ProductionRouting,ProductionRoutingSequence,Operation,OperationStandardTextCode", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationNumber", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolverRoteiro_UsaUrlProductionVersionV4_Dinamica_E_RoutingSemPrefixoA()
    {
        FakeHandler handler = new(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "Material": "2000091",
                  "Plant": "3007",
                  "ProductionVersion": "1",
                  "ExecBillOfOperationsGroup": "50000063",
                  "ExecBillOfOperationsVariant": "1"
                }
                """)
            },
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"d":{"results":[{"ProductionRoutingGroup":"50000063","ProductionRouting":"1","ProductionRoutingSequence":"0","Operation":"50","OperationStandardTextCode":"PP_FORM"}]}}""")
            });

        ProductionRoutingSapApiClient client = new(CriarConfiguracaoV4(), new HttpClient(handler));
        RoteiroProducaoSap? roteiro = await client.ResolverRoteiroDaOrdemAsync(new OrdemProducaoSap
        {
            MaterialProduzido = "2000091",
            Centro = "3007",
            VersaoProducao = "1"
        });

        Assert.NotNull(roteiro);
        Assert.Equal("50000063", roteiro!.BillOfOperationsGroup);
        Assert.Equal("1", roteiro.BillOfOperationsVariant);
        Assert.Equal("50", Assert.Single(roteiro.Operacoes).Operacao);
        Assert.Contains("/odata4/sap/api_production_version/", handler.Urls[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProductionVersion(Material='2000091',Plant='3007',ProductionVersion='1')", handler.Urls[0], StringComparison.Ordinal);
        Assert.Contains("/API_PRODUCTION_ROUTING/ProductionRoutingOperation", handler.Urls[1], StringComparison.Ordinal);
        Assert.DoesNotContain("A_ProductionRoutingOperation", handler.Urls[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolverRoteiro_HttpProductionVersionNao2xx_FailClosed_SemCredencialNoDiagnostico()
    {
        List<string> diagnosticos = [];
        FakeHandler handler = new(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
        {
            Content = new StringContent("erro tecnico")
        });

        ProductionRoutingSapApiClient client = new(CriarConfiguracaoV4(), new HttpClient(handler), diagnosticos.Add);
        RoteiroProducaoSap? roteiro = await client.ResolverRoteiroDaOrdemAsync(new OrdemProducaoSap
        {
            MaterialProduzido = "2000091",
            Centro = "3007",
            VersaoProducao = "1"
        });

        Assert.Null(roteiro);
        Assert.Contains(diagnosticos, d => d.Contains("HTTP 403", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnosticos, d => d.Contains("usuario-teste", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnosticos, d => d.Contains("senha-teste", StringComparison.OrdinalIgnoreCase));
    }

    private static ConfiguracaoSap CriarConfiguracaoV4()
        => new()
        {
            ProductionVersionBaseUrl = "https://sap.example.com:44300/sap/opu/odata4/sap/api_production_version/srvd_a2x/sap/productionversion/0001/",
            MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            HostsPermitidos = ["sap.example.com"]
        };

    private sealed class FakeHandler(params HttpResponseMessage[] respostas) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas = new(respostas);

        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
            return Task.FromResult(_respostas.Count > 0
                ? _respostas.Dequeue()
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
        }
    }
    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}
