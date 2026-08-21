using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Testes comportamentais da CONSULTA REAL (somente leitura) da norma de embalagem do Produto Acabado
/// (contrato INT012). URL por $filter (§6), credenciais/allowlist PRÓPRIAS da embalagem (§4), parse
/// defensivo I/M e _PkgInstructionItems (§7), cenários tipados (§8) e distinção na tela (§9). Nenhum
/// POST/PATCH; materiais permanecem strings (zeros à esquerda preservados).
/// </summary>
public sealed class NormaEmbalagemProdutoAcabadoTests
{
    // Espelha a resposta HTTP 200 real do material de exemplo (fixture — não é hardcode de produção).
    private const string JsonReal =
        """
        {
          "Material": "4000108",
          "PackagingInstruction": "",
          "PkgInstructionItems": [
            { "Material": "3000009", "Material Type": "P", "Quantity": "1", "QtyUOM": "ST", "Item": "10" },
            { "Material": "4000108", "Material Type": "I", "Quantity": "60", "QtyUOM": "UN", "Item": "20" }
          ]
        }
        """;

    private const string HostSuite = "api.cfapps.br10.hana.ondemand.com";
    private const string EndpointSuite = "https://api.cfapps.br10.hana.ondemand.com/http/packaging/ZAPI_PACKAGING_SRV/GetPackagingSet";

    private static ConsultaNormaEmbalagemSapItemResponse Item(string material, string tipo, string qtd, string uom, string item)
        => new() { Material = material, MaterialType = tipo, Quantity = qtd, QtyUOM = uom, Item = item };

    private static ConsultaNormaEmbalagemSapResponse Resposta(
        string material, string packagingInstruction, params ConsultaNormaEmbalagemSapItemResponse[] itens)
        => new() { Material = material, PackagingInstruction = packagingInstruction, PkgInstructionItems = itens };

    private static ConfiguracaoSap ConfigEmbalagem(
        string baseUrl = EndpointSuite,
        string? hostPermitido = HostSuite,
        string packagingUsuario = "pkg-user",
        string packagingSenha = "pkg-pass",
        string sapClient = "")
        => new()
        {
            // Credenciais standard PROPOSITALMENTE diferentes — nunca devem ser reutilizadas pela embalagem.
            Usuario = "std-user",
            Senha = "std-pass",
            HostsPermitidos = ["standard.sap.local"],
            PackagingBaseUrl = baseUrl,
            PackagingUsuario = packagingUsuario,
            PackagingSenha = packagingSenha,
            PackagingHostsPermitidos = hostPermitido is null ? [] : [hostPermitido],
            PackagingSapClientOpcional = sapClient
        };

    private static ProdutoAcabadoNormaEmbalagemSapServico ServicoComResposta(
        ConfiguracaoSap config, int? status, string? corpo, Action<HttpRequestMessage>? inspecionar = null)
        => new(config, (req, _) =>
        {
            inspecionar?.Invoke(req);
            return Task.FromResult(new RespostaHttpNorma(status, corpo));
        });

    // ======================= INTERPRETAÇÃO (§5/§6/§7) =======================

    [Fact]
    public void A_RespostaReal_InterpretaMaterialCaixaQuantidadeEUnidade()
    {
        ConsultaNormaEmbalagemSapResponse? resposta = ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(JsonReal);
        Assert.NotNull(resposta);

        ResultadoNormaEmbalagemProdutoAcabado r =
            InterpretadorNormaEmbalagemProdutoAcabado.Interpretar("4000108", resposta);

        Assert.True(r.Sucesso);
        NormaEmbalagemProdutoAcabado norma = r.Norma!;
        Assert.Equal("3000009", norma.MaterialCaixa);
        Assert.Equal(60m, norma.QuantidadePorCaixa);
        Assert.Equal("UN", norma.UnidadeQuantidade);
        Assert.Equal(string.Empty, norma.CodigoNorma);
        Assert.Equal(OrigemNormaEmbalagem.ConsultadaSap, norma.Origem);
        Assert.Equal("CONSULTADA SAP", norma.Status);
        Assert.NotEmpty(norma.Pendencias);
    }

    [Fact]
    public void A_Parse_RespeitaNomeJsonMaterialType()
    {
        ConsultaNormaEmbalagemSapResponse? resposta = ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(JsonReal);
        Assert.Equal("P", resposta!.PkgInstructionItems[0].MaterialType);
        Assert.Equal("I", resposta.PkgInstructionItems[1].MaterialType);
    }

    [Fact]
    public void B_SemItemP_Bloqueia()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108", Resposta("4000108", "", Item("4000108", "I", "60", "UN", "20")));

        Assert.False(r.Sucesso);
        Assert.Contains("4000108", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("tipo P", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void C_DoisItensP_BloqueiaAmbiguo()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108",
            Resposta("4000108", "",
                Item("3000009", "P", "1", "ST", "10"),
                Item("3000010", "P", "1", "ST", "15"),
                Item("4000108", "I", "60", "UN", "20")));

        Assert.False(r.Sucesso);
        Assert.Contains("ambígua", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void D_ItemProdutoDeOutroMaterial_Bloqueia()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108",
            Resposta("4000108", "",
                Item("3000009", "P", "1", "ST", "10"),
                Item("9999999", "I", "60", "UN", "20")));

        Assert.False(r.Sucesso);
        Assert.Contains("tipo I/M", r.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    public void E_QuantidadePorCaixaInvalida_Bloqueia(string quantidade)
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108",
            Resposta("4000108", "",
                Item("3000009", "P", "1", "ST", "10"),
                Item("4000108", "I", quantidade, "UN", "20")));

        Assert.False(r.Sucesso);
        Assert.Contains("por caixa", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void F_PackagingInstructionPreenchido_ExibeCodigo()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108",
            Resposta("4000108", "NORMA-01",
                Item("3000009", "P", "1", "ST", "10"),
                Item("4000108", "I", "60", "UN", "20")));

        Assert.True(r.Sucesso);
        Assert.Equal("NORMA-01", r.Norma!.CodigoNorma);
        Assert.Empty(r.Norma.Pendencias);
    }

    [Fact]
    public void G_MateriaisPreservamStringEZerosAEsquerda()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "000123",
            Resposta("000123", "",
                Item("00099", "P", "1", "ST", "10"),
                Item("000123", "I", "24", "UN", "20")));

        Assert.True(r.Sucesso);
        Assert.Equal("00099", r.Norma!.MaterialCaixa);
        Assert.Equal("000123", r.Norma.MaterialProduto);
    }

    // Produto tipo M (além de I) é aceito como produto acondicionado (§7).
    [Fact]
    public void F_ProdutoTipoM_Aceito()
    {
        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar(
            "4000108",
            Resposta("4000108", "",
                Item("3000009", "P", "1", "ST", "10"),
                Item("4000108", "M", "60", "UN", "20")));

        Assert.True(r.Sucesso);
        Assert.Equal("3000009", r.Norma!.MaterialCaixa);
        Assert.Equal(60m, r.Norma.QuantidadePorCaixa);
    }

    // ======================= URL POR FILTRO (§6 / §10A / §10B) =======================

    [Fact]
    public void UrlA_ConsultaPorFiltroNuncaPorChave()
    {
        Uri url = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(new Uri(EndpointSuite), "4000108");
        string texto = url.ToString();
        string decodificada = Uri.UnescapeDataString(texto);

        Assert.DoesNotContain("('4000108')", texto, StringComparison.Ordinal);   // nunca por chave
        Assert.Contains("$filter=", texto, StringComparison.Ordinal);
        Assert.Contains("4000108", texto, StringComparison.Ordinal);
        Assert.Contains("Material eq '4000108'", decodificada, StringComparison.Ordinal);
        Assert.DoesNotContain("sap-client", texto, StringComparison.OrdinalIgnoreCase);  // não configurado
    }

    [Fact]
    public void UrlA_SapClientSomenteQuandoConfigurado()
    {
        Uri url = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(new Uri(EndpointSuite), "4000108", sapClientOpcional: "110");
        Assert.Contains("sap-client=110", url.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UrlA_PreservaZerosAEsquerdaEEscapaApostrofo()
    {
        Uri url = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(new Uri(EndpointSuite), "000123");
        Assert.Contains("Material eq '000123'", Uri.UnescapeDataString(url.ToString()), StringComparison.Ordinal);

        Uri comApostrofo = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(new Uri(EndpointSuite), "A'B");
        Assert.Contains("Material eq 'A''B'", Uri.UnescapeDataString(comApostrofo.ToString()), StringComparison.Ordinal);
    }

    [Fact]
    public void UrlB_PackagingInstructionOpcional()
    {
        Uri semInstrucao = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(new Uri(EndpointSuite), "4000108");
        Assert.DoesNotContain("PackagingInstruction", semInstrucao.ToString(), StringComparison.Ordinal);

        Uri comInstrucao = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(
            new Uri(EndpointSuite), "4000108", packagingInstruction: "NORMA-01");
        string decodificada = Uri.UnescapeDataString(comInstrucao.ToString());
        Assert.Contains("Material eq '4000108' and PackagingInstruction eq 'NORMA-01'", decodificada, StringComparison.Ordinal);
    }

    // ======================= CREDENCIAIS PRÓPRIAS (§4 / §10C) =======================

    [Fact]
    public async Task C_UsaCredenciaisProprias_NaoAsStandard()
    {
        AuthenticationHeaderValue? auth = null;
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(
            ConfigEmbalagem(packagingUsuario: "pkg-user", packagingSenha: "pkg-pass"),
            200, JsonReal,
            req => auth = req.Headers.Authorization);

        await servico.ObterNormaAsync("4000108");

        Assert.NotNull(auth);
        Assert.Equal("Basic", auth!.Scheme);
        string credencial = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!));
        Assert.Equal("pkg-user:pkg-pass", credencial);          // credencial PRÓPRIA da embalagem
        Assert.DoesNotContain("std-user", credencial, StringComparison.Ordinal);   // nunca a standard
        Assert.DoesNotContain("std-pass", credencial, StringComparison.Ordinal);
    }

    [Fact]
    public async Task C_SemCredencialPropria_CenarioCredencialAusente()
    {
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(
            ConfigEmbalagem(packagingUsuario: "", packagingSenha: ""), 200, JsonReal);

        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(CenarioConsultaNormaEmbalagem.CredencialAusente, r.Cenario);
    }

    // ======================= HOST INTEGRATION SUITE (§10D) =======================

    [Fact]
    public async Task D_HostForaDaAllowlist_Bloqueia_SemChamarHttp()
    {
        bool chamou = false;
        ProdutoAcabadoNormaEmbalagemSapServico servico = new(
            ConfigEmbalagem(hostPermitido: "outro.host.local"),
            (_, _) => { chamou = true; return Task.FromResult(new RespostaHttpNorma(200, JsonReal)); });

        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(CenarioConsultaNormaEmbalagem.HostNaoPermitido, r.Cenario);
        Assert.False(chamou);
    }

    [Fact]
    public async Task D_HostNaAllowlist_ProsegueAoHttp()
    {
        bool chamou = false;
        ProdutoAcabadoNormaEmbalagemSapServico servico = new(
            ConfigEmbalagem(hostPermitido: HostSuite),
            (_, _) => { chamou = true; return Task.FromResult(new RespostaHttpNorma(200, JsonReal)); });

        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.True(chamou);
        Assert.Equal(CenarioConsultaNormaEmbalagem.Encontrada, r.Cenario);
    }

    // ======================= CLASSIFICAÇÃO HTTP (§8 / §10E) =======================

    [Theory]
    [InlineData(200, CenarioConsultaNormaEmbalagem.Encontrada)]
    [InlineData(401, CenarioConsultaNormaEmbalagem.ErroAutenticacao)]
    [InlineData(403, CenarioConsultaNormaEmbalagem.ErroAutorizacao)]
    [InlineData(404, CenarioConsultaNormaEmbalagem.NaoEncontrada)]
    [InlineData(500, CenarioConsultaNormaEmbalagem.ErroHttp)]
    public async Task E_StatusHttp_ClassificaCenario(int status, CenarioConsultaNormaEmbalagem esperado)
    {
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(ConfigEmbalagem(), status, JsonReal);
        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(esperado, r.Cenario);
        if (status != 200)
        {
            Assert.Equal(status, r.HttpStatus);
        }
    }

    [Fact]
    public async Task E_Http200SemRegistro_CenarioNaoEncontrada()
    {
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(ConfigEmbalagem(), 200, """{ "value": [] }""");
        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(CenarioConsultaNormaEmbalagem.NaoEncontrada, r.Cenario);
    }

    [Fact]
    public async Task E_Http200JsonInvalido_CenarioRespostaInvalida()
    {
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(ConfigEmbalagem(), 200, "{ nao-e-json ");
        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(CenarioConsultaNormaEmbalagem.RespostaInvalida, r.Cenario);
    }

    [Fact]
    public async Task E_SomenteGet_NuncaPostOuPatch()
    {
        HttpMethod? metodo = null;
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(
            ConfigEmbalagem(), 200, JsonReal, req => metodo = req.Method);
        await servico.ObterNormaAsync("4000108");
        Assert.Equal(HttpMethod.Get, metodo);
    }

    // ======================= PARSE DEFENSIVO (§7 / §10F) =======================

    [Fact]
    public void F_ParsePkgInstructionItemsPadrao()
    {
        ConsultaNormaEmbalagemSapResponse? r = ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(JsonReal);
        Assert.Equal(2, r!.PkgInstructionItems.Count);
    }

    [Fact]
    public void F_ParseUnderscorePkgInstructionItems_ETipoM()
    {
        const string json =
            """
            { "Material": "4000108", "PackagingInstruction": "",
              "_PkgInstructionItems": [
                { "Material": "3000009", "Material Type": "P", "Quantity": "1", "QtyUOM": "ST", "Item": "10" },
                { "Material": "4000108", "Material Type": "M", "Quantity": "60", "QtyUOM": "UN", "Item": "20" }
              ] }
            """;

        ConsultaNormaEmbalagemSapResponse? resposta = ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(json);
        Assert.Equal(2, resposta!.PkgInstructionItems.Count);
        Assert.Equal("M", resposta.PkgInstructionItems[1].MaterialType);

        ResultadoNormaEmbalagemProdutoAcabado r = InterpretadorNormaEmbalagemProdutoAcabado.Interpretar("4000108", resposta);
        Assert.True(r.Sucesso);
        Assert.Equal("3000009", r.Norma!.MaterialCaixa);
    }

    [Fact]
    public void F_ParseColecaoOData_PegaPrimeiro()
    {
        const string json = """{ "d": { "results": [ { "Material": "4000108", "PackagingInstruction": "NORMA-01", "PkgInstructionItems": [] } ] } }""";
        ConsultaNormaEmbalagemSapResponse? resposta = ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(json);
        Assert.Equal("4000108", resposta!.Material);
        Assert.Equal("NORMA-01", resposta.PackagingInstruction);
    }

    // ======================= TELA POR CENÁRIO (§9 / §10G) =======================

    [Fact]
    public void G_Tela_Encontrada_HabilitaLeitura()
    {
        ResultadoConsultaNormaEmbalagemSap consulta = ResultadoConsultaNormaEmbalagemSap.DeEncontrada(
            Resposta("4000108", "",
                Item("3000009", "P", "1", "ST", "10"),
                Item("4000108", "I", "60", "UN", "20")),
            200, "https://host/GetPackagingSet", "abc123");

        ProdutoAcabadoNormaEmbalagem tela = ProdutoAcabadoController.MapearNormaParaTela("4000108", consulta);
        Assert.Equal("CONSULTADA SAP", tela.Status);
        Assert.True(tela.NormaValida);
        Assert.Equal("3000009", tela.MaterialCaixa);
        Assert.Equal(60, tela.QuantidadeProdutosPorCaixa);
    }

    [Theory]
    [InlineData(CenarioConsultaNormaEmbalagem.NaoConfigurada, "NAO CONFIGURADA")]
    [InlineData(CenarioConsultaNormaEmbalagem.CredencialAusente, "NAO CONFIGURADA")]
    [InlineData(CenarioConsultaNormaEmbalagem.HostNaoPermitido, "NAO CONFIGURADA")]
    [InlineData(CenarioConsultaNormaEmbalagem.ErroAutenticacao, "ERRO DE AUTENTICACAO")]
    [InlineData(CenarioConsultaNormaEmbalagem.ErroAutorizacao, "ACESSO NAO AUTORIZADO")]
    [InlineData(CenarioConsultaNormaEmbalagem.NaoEncontrada, "SEM NORMA CADASTRADA")]
    [InlineData(CenarioConsultaNormaEmbalagem.ErroHttp, "ERRO NA CONSULTA")]
    [InlineData(CenarioConsultaNormaEmbalagem.Timeout, "ERRO NA CONSULTA")]
    [InlineData(CenarioConsultaNormaEmbalagem.RespostaInvalida, "ERRO NA CONSULTA")]
    public void G_Tela_DistingueCenarios_SemHabilitarLeitura(CenarioConsultaNormaEmbalagem cenario, string rotuloEsperado)
    {
        Assert.Equal(rotuloEsperado, ProdutoAcabadoController.RotuloStatusNorma(cenario));

        ResultadoConsultaNormaEmbalagemSap consulta = ConsultaComCenario(cenario);
        ProdutoAcabadoNormaEmbalagem tela = ProdutoAcabadoController.MapearNormaParaTela("4000108", consulta);
        Assert.Equal(rotuloEsperado, tela.Status);
        Assert.False(tela.NormaValida);                       // nenhum cenário não-Encontrada habilita leitura
        Assert.Equal(string.Empty, tela.MaterialCaixa);       // nunca inventa material
        Assert.Equal(0, tela.QuantidadeProdutosPorCaixa);
    }

    // ======================= SEGURANÇA (§10H) + CONTROLLER (§8) =======================

    [Fact]
    public async Task H_NenhumaCredencialEmMensagemOuUrlSanitizada()
    {
        ProdutoAcabadoNormaEmbalagemSapServico servico = ServicoComResposta(
            ConfigEmbalagem(packagingUsuario: "segredo-user", packagingSenha: "segredo-pass"), 401, "erro");
        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");

        foreach (string proibido in new[] { "segredo-user", "segredo-pass", "Authorization", "Basic ", "cookie", "token" })
        {
            Assert.DoesNotContain(proibido, r.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(proibido, r.UrlSanitizada, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("?", r.UrlSanitizada, StringComparison.Ordinal);   // sem query (sem material na URL de diagnóstico)
    }

    [Fact]
    public async Task H_NaoConfigurado_NaoFazHttp()
    {
        IProdutoAcabadoNormaEmbalagemSapServico servico = new ProdutoAcabadoNormaEmbalagemSapNaoConfiguradoServico();
        Assert.False(servico.Configurado);
        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("4000108");
        Assert.Equal(CenarioConsultaNormaEmbalagem.NaoConfigurada, r.Cenario);
    }

    [Fact]
    public async Task I_Controller_NormaValida_Mapeada()
    {
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapInativo(),
            normaEmbalagemSapServico: new NormaSapFake(ResultadoConsultaNormaEmbalagemSap.DeEncontrada(
                Resposta("4000108", "",
                    Item("3000009", "P", "1", "ST", "10"),
                    Item("4000108", "I", "60", "UN", "20")),
                200, "https://host/GetPackagingSet", "cid")));

        ResultadoNormaEmbalagemProdutoAcabado r = await controller.ConsultarNormaEmbalagemAsync("4000108");
        Assert.True(r.Sucesso);
        Assert.Equal("3000009", r.Norma!.MaterialCaixa);
    }

    [Fact]
    public async Task I_Controller_NaoEncontrada_MapeiaSemNormaCadastrada()
    {
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapInativo(),
            normaEmbalagemSapServico: new NormaSapFake(
                ResultadoConsultaNormaEmbalagemSap.DeNaoEncontrada(404, "https://host/GetPackagingSet", "cid")));

        ResultadoNormaEmbalagemProdutoAcabado r = await controller.ConsultarNormaEmbalagemAsync("4000108");
        Assert.False(r.Sucesso);
    }

    private static ResultadoConsultaNormaEmbalagemSap ConsultaComCenario(CenarioConsultaNormaEmbalagem cenario)
        => cenario switch
        {
            CenarioConsultaNormaEmbalagem.NaoConfigurada => ResultadoConsultaNormaEmbalagemSap.DeNaoConfigurada(),
            CenarioConsultaNormaEmbalagem.CredencialAusente => ResultadoConsultaNormaEmbalagemSap.DeCredencialAusente(),
            CenarioConsultaNormaEmbalagem.HostNaoPermitido => ResultadoConsultaNormaEmbalagemSap.DeHostNaoPermitido("https://host/GetPackagingSet", "cid"),
            CenarioConsultaNormaEmbalagem.NaoEncontrada => ResultadoConsultaNormaEmbalagemSap.DeNaoEncontrada(404, "https://host/GetPackagingSet", "cid"),
            CenarioConsultaNormaEmbalagem.ErroAutenticacao => ResultadoConsultaNormaEmbalagemSap.DeErroAutenticacao(401, "https://host/GetPackagingSet", "cid"),
            CenarioConsultaNormaEmbalagem.ErroAutorizacao => ResultadoConsultaNormaEmbalagemSap.DeErroAutorizacao(403, "https://host/GetPackagingSet", "cid"),
            CenarioConsultaNormaEmbalagem.Timeout => ResultadoConsultaNormaEmbalagemSap.DeTimeout("https://host/GetPackagingSet", "cid"),
            CenarioConsultaNormaEmbalagem.RespostaInvalida => ResultadoConsultaNormaEmbalagemSap.DeRespostaInvalida(200, "https://host/GetPackagingSet", "cid"),
            _ => ResultadoConsultaNormaEmbalagemSap.DeErroHttp(500, "https://host/GetPackagingSet", "cid")
        };

    // ---------- Fakes ----------
    private sealed class NormaSapFake : IProdutoAcabadoNormaEmbalagemSapServico
    {
        private readonly ResultadoConsultaNormaEmbalagemSap _resultado;
        public NormaSapFake(ResultadoConsultaNormaEmbalagemSap resultado) => _resultado = resultado;

        public bool Configurado => true;

        public Task<ResultadoConsultaNormaEmbalagemSap> ObterNormaAsync(
            string material, CancellationToken cancellationToken = default)
            => Task.FromResult(_resultado);
    }

    private sealed class ProductionOrderSapInativo : IProductionOrderSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => false;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoEncontrada());
    }
}
