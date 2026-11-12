using System.Text.Json;
using System.Text.RegularExpressions;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Entrada.P5;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R8AD2Tests
{
    [Fact]
    public void ComandoD2ProjetaFkLocalComNomeEsperadoPeloLeitor()
    {
        using NpgsqlConnection conexao = new();
        using NpgsqlCommand comando = EntradaMpP5D2Repositorio.CriarComando(
            EntradaMpP5D2Repositorio.SqlLocal, new("123", "10"), 900, conexao);
        Match projecao = Regex.Match(comando.CommandText, @"\)\s*SELECT\s+(?<colunas>.*?)\s+FROM p\s+CROSS JOIN agregado_sap", RegexOptions.Singleline);
        Assert.True(projecao.Success);
        Assert.Matches(@"(?m)^\s*li\.codigo_sap_pedido_compra_item\s*,\s*$", projecao.Groups["colunas"].Value);
        Assert.Single(Regex.Matches(projecao.Groups["colunas"].Value, @"\bcodigo_sap_pedido_compra_item\b").Cast<Match>());
        Assert.Equal(System.Data.ConnectionState.Closed, conexao.State);
    }

    [Fact]
    public void PreparacaoComFkBigintCoerenteNaoCriaImpedimentoLocal()
        => Assert.Equal(CenarioRecebimentoLocal.SemRegistro, Classificar(Local()).Cenario);

    [Fact]
    public void FkAusenteNaoEReconstruidaPeloCache()
    {
        Dictionary<string, object?> local = Local();
        local.Remove("codigo_sap_pedido_compra_item");
        Assert.Equal(CenarioRecebimentoLocal.Pendente, Classificar(local).Cenario);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(72L)]
    [InlineData(71)]
    [InlineData("71")]
    public void FkNulaInvalidaOuDivergenteBloqueia(object? fk)
    {
        Dictionary<string, object?> local = Local();
        local["codigo_sap_pedido_compra_item"] = fk;
        Assert.Equal(CenarioRecebimentoLocal.Pendente, Classificar(local).Cenario);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"material\":\"OUTRO\",\"sap_item_id\":71}]")]
    [InlineData("[{\"material\":\"SYNTHETIC\"}]")]
    [InlineData("[{\"material\":\"SYNTHETIC\",\"sap_item_id\":71},{\"material\":\"SYNTHETIC\",\"sap_item_id\":71}]")]
    public void CacheNaoSubstituiIdentidadeLocal(string cache)
    {
        Dictionary<string, object?> local = Local();
        local["cache_itens"] = cache;
        Assert.Equal(CenarioRecebimentoLocal.Pendente, Classificar(local).Cenario);
    }

    [Fact]
    public void OutroLancamentoBloqueiaMesmoComFkCoerente()
    {
        Dictionary<string, object?> local = Local();
        local["eh_lancamento_atual"] = false;
        Assert.Equal(CenarioRecebimentoLocal.Pendente, Classificar(local).Cenario);
    }

    [Fact]
    public void PreparacaoLocalNaoConstituiProvaNegativaSap()
    {
        Dictionary<string, object?> recovery = Recovery();
        recovery["p5_local_decision"] = "NULL_BLOCK_NO_LOCAL_GUARD";
        Assert.Equal(CenarioRecebimentoLocal.Pendente,
            EntradaMpP5D2Repositorio.Classificar([Local()], [recovery]).Cenario);
    }

    [Theory]
    [InlineData("ROH", ClassificacaoEntradaMaterial.MateriaPrima, true)]
    [InlineData("HIBE", ClassificacaoEntradaMaterial.Quimico, false)]
    [InlineData("", ClassificacaoEntradaMaterial.Indefinido, true)]
    [InlineData("DESCONHECIDO", ClassificacaoEntradaMaterial.Outro, true)]
    public void ProductTypeODataChegaAoClassificadorSemInferenciaPorZz1(
        string productType, ClassificacaoEntradaMaterial esperado, bool bloqueia)
    {
        string json = JsonSerializer.Serialize(new { d = new { Product = "SYNTHETIC", ProductType = productType } });
        SapProductMasterDto dto = Assert.IsType<SapProductMasterDto>(ProductMasterSapApiClient.ParsearProduto(json));
        ProdutoSapMestre mestre = ProdutoSapMestre.DeDto(dto);
        MaterialDocumentSapItemRequest item = ClassificacaoMaterialDocumentP5.Registrar(Item(), mestre);
        Assert.Equal(esperado, ClassificacaoMaterialDocumentP5.Obter(item));
        Assert.Equal(bloqueia, ClassificacaoMaterialDocumentP5.Bloqueia(item));
    }

    [Theory]
    [InlineData("OUTRO", true)]
    [InlineData("SYNTHETIC", false)]
    public void MestreDivergenteOuNaoConsultadoNaoLiberaHibe(string material, bool consultado)
    {
        MaterialDocumentSapItemRequest item = ClassificacaoMaterialDocumentP5.Registrar(Item(), new()
        {
            CodigoProduto = material, TipoMaterialSap = "HIBE", Consultado = consultado
        });
        Assert.Equal(ClassificacaoEntradaMaterial.Indefinido, ClassificacaoMaterialDocumentP5.Obter(item));
        Assert.True(ClassificacaoMaterialDocumentP5.Bloqueia(item));
    }

    [Fact]
    public void EvidenciaEmMemoriaNaoSobreviveASerializacaoComoProvaPersistida()
    {
        MaterialDocumentSapItemRequest item = ClassificacaoMaterialDocumentP5.Registrar(Item(), new()
        {
            CodigoProduto = "SYNTHETIC", TipoMaterialSap = "HIBE", Consultado = true
        });
        MaterialDocumentSapItemRequest reidratado = JsonSerializer.Deserialize<MaterialDocumentSapItemRequest>(
            JsonSerializer.Serialize(item))!;
        Assert.False(ClassificacaoMaterialDocumentP5.Bloqueia(item));
        Assert.Equal(ClassificacaoEntradaMaterial.Indefinido, ClassificacaoMaterialDocumentP5.Obter(reidratado));
        Assert.True(ClassificacaoMaterialDocumentP5.Bloqueia(reidratado));
    }

    private static MaterialDocumentSapItemRequest Item() => new()
    {
        Material = "SYNTHETIC", PurchaseOrder = "123", PurchaseOrderItem = "00010", GoodsMovementRefDocType = "B"
    };

    private static ResultadoRecebimentoLocal Classificar(Dictionary<string, object?> local)
        => EntradaMpP5D2Repositorio.Classificar([local], [Recovery()]);

    private static Dictionary<string, object?> Local() => new()
    {
        ["chave_valida"] = true, ["prova_negativa_sap"] = false,
        ["categoria_local_p5"] = "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO",
        ["lancamentos_sem_item_no_pedido"] = 0L, ["cache_item_count"] = 1L,
        ["eh_lancamento_atual"] = true, ["material_local"] = "SYNTHETIC",
        ["codigo_sap_pedido_compra_item"] = 71L,
        ["cache_itens"] = "[{\"material\":\"SYNTHETIC\",\"sap_item_id\":71}]"
    };

    private static Dictionary<string, object?> Recovery() => new()
    {
        ["chave_valida"] = true, ["prova_negativa_sap"] = false,
        ["p5_local_decision"] = "NULL_BLOCK_NEEDS_AUTHORITATIVE_SAP_PROOF_AND_ATOMIC_GUARD"
    };
}
