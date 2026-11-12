using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Entrada.P5;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R8BEvidenciaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly IdentidadeItemProdutoSapR8B Identidade = new(new(new("123", "10"), 900, "SYNTHETIC"), 41, 71);

    [Theory]
    [InlineData("ROH")]
    [InlineData("HIBE")]
    public void ColetaSimuladaCoerenteNaoSeTornaAutorizacao(string tipo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler(tipo);
        ClaimP5 tentativa = Claim();
        ReciboEvidenciaProdutoSapR8B recibo = Recibo(evidencia);
        Assert.Equal("SYNTHETIC", evidencia.Campos.Product);
        Assert.Equal(tipo, evidencia.Campos.ProductType);
        Assert.Equal("QAS110", evidencia.Contexto.Ambiente);
        Assert.Equal("110", evidencia.Contexto.Mandante);
        Assert.Equal("API_PRODUCT_SRV/A_Product", evidencia.Contexto.OrigemTecnica);
        Assert.Equal("COLETOR_SIMULADO", evidencia.Contexto.IdentidadeBackend);
        Assert.Equal(Inicio, evidencia.Contexto.ConsultaIniciadaEm);
        Assert.Equal(Inicio.AddSeconds(1), evidencia.Contexto.ConsultaConcluidaEm);
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.CoerenteSemAutoridade,
            Validar(evidencia, tentativa, recibo, Vinculo(tentativa, tipo)));
    }

    [Theory]
    [InlineData("2026-10-20T08:09:10.1234567", null)]
    [InlineData("2026-10-20T08:09:10.1234567-03:00", "-03:00")]
    [InlineData("2026-10-20T11:09:10Z", "Z")]
    [InlineData("/Date(1792494550123)/", null)]
    [InlineData("/Date(1792494550123+0300)/", "+0300")]
    [InlineData("/Date(1792494550123-0300)/", "-0300")]
    public void DataSapPreservaRepresentacaoEOffsetSemInferirFuso(string original, string? offset)
    {
        EvidenciaProdutoSapR8B evidencia = Ler(timestamp: original);
        Assert.Equal(original, evidencia.Campos.LastChangeDateTimeOriginal);
        Assert.Equal(offset, evidencia.Campos.OffsetLastChangeDateTimeOriginal);
        Assert.Equal("2026-10-20", evidencia.Campos.LastChangeDateOriginal);
        Assert.Equal("USUARIO_SIMULADO", evidencia.Campos.LastChangedByUser);
    }

    [Fact]
    public void EtagAusenteNaoGeraRevisaoSapOuValidadeInventada()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        Assert.Null(evidencia.ETagSap);
        Assert.Null(evidencia.Campos.OffsetLastChangeDateTimeOriginal);
    }

    [Fact]
    public void ProdutoDivergenteBloqueiaMesmoComReciboEVinculo()
    {
        EvidenciaProdutoSapR8B evidencia = Ler(produto: "OUTRO");
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.IdentidadeDivergente,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa)));
    }

    [Theory]
    [InlineData(" SYNTHETIC ", "ROH", "IdentidadeDivergente")]
    [InlineData("SYNTHETIC", " ROH ", "ClassificacaoNaoComprovada")]
    public void TextoOriginalNaoEMascaradoPorNormalizacao(string produto, string tipo, string esperado)
    {
        EvidenciaProdutoSapR8B evidencia = Ler(tipo, produto);
        ClaimP5 tentativa = Claim();
        Assert.Equal(produto, evidencia.Campos.Product);
        Assert.Equal(tipo, evidencia.Campos.ProductType);
        Assert.Equal(esperado, Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa, tipo)).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("DESCONHECIDO")]
    public void TipoAusenteOuDesconhecidoNaoPresumeHibe(string tipo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler(tipo);
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.ClassificacaoNaoComprovada,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa, tipo)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"d\":[]}")]
    [InlineData("{\"d\":{\"Product\":5}}")]
    [InlineData("{\"d\":{\"Product\":\"SYNTHETIC\",\"LastChangeDateTime\":3}}")]
    [InlineData("not-json")]
    [InlineData("{\"d\":{\"Product\":\"SYNTHETIC\",\"ProductType\":\"ROH\",\"ProductType\":\"HIBE\"}}")]
    [InlineData("{\"d\":{\"Product\":\"SYNTHETIC\"},\"d\":{\"Product\":\"OUTRO\"}}")]
    public void RespostaInvalidaNaoViraEvidencia(string json)
        => Assert.Null(LeitorEvidenciaProdutoSapR8B.Ler(json, Contexto()));

    [Fact]
    public void CamposSapNaoInformadosContinuamAusentes()
    {
        EvidenciaProdutoSapR8B evidencia = LeitorEvidenciaProdutoSapR8B.Ler(
            "{\"d\":{\"Product\":\"SYNTHETIC\"}}", Contexto())!;
        Assert.Equal(string.Empty, evidencia.Campos.ProductType);
        Assert.Null(evidencia.Campos.LastChangeDateTimeOriginal);
        Assert.Null(evidencia.Campos.LastChangeDateOriginal);
        Assert.Null(evidencia.Campos.LastChangedByUser);
    }

    [Fact]
    public void EvidenciaSomenteEmMemoriaContinuaNaoPersistida()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.NaoPersistida, Validar(evidencia, Claim(), null, null));
    }

    [Fact]
    public void ReciboSemConfirmacaoDePersistenciaBloqueia()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.NaoPersistida,
            Validar(evidencia, Claim(), Recibo(evidencia) with { RegistradaEmBanco = null }, null));
    }

    [Fact]
    public void EvidenciaRevogadaNaoPermiteReuso()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.Revogada,
            Validar(evidencia, tentativa, Recibo(evidencia) with { RevogadaEmBanco = Inicio.AddSeconds(2) }, Vinculo(tentativa)));
    }

    [Fact]
    public void TentativaSemVinculoBloqueia()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.SemVinculo, Validar(evidencia, Claim(), Recibo(evidencia), null));
    }

    [Fact]
    public void MudancaRohParaHibeNaoReclassificaTentativaAnterior()
    {
        EvidenciaProdutoSapR8B anterior = Ler();
        EvidenciaProdutoSapR8B atual = anterior with { Campos = anterior.Campos with { ProductType = "HIBE" } };
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.ClassificacaoAlterada,
            Validar(atual, tentativa, Recibo(anterior), Vinculo(tentativa)));
    }

    [Theory]
    [InlineData("TENTATIVA")]
    [InlineData("TOKEN")]
    [InlineData("CORRELACAO")]
    [InlineData("VERSAO_GUARDA")]
    [InlineData("GERACAO_RECOVERY")]
    public void FencingExigeIdentidadeExataSemEscolherUltimaTentativa(string campo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        ClaimP5 outra = new(Identidade.Escopo,
            campo == "TENTATIVA" ? 2 : tentativa.Tentativa,
            campo == "TOKEN" ? Guid.NewGuid() : tentativa.Token,
            campo == "CORRELACAO" ? Guid.NewGuid() : tentativa.Correlacao,
            campo == "GERACAO_RECOVERY" ? 1 : tentativa.GeracaoRecovery,
            campo == "VERSAO_GUARDA" ? 2 : tentativa.VersaoGuarda);
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.TentativaDivergente,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(outra)));
    }

    [Theory]
    [InlineData("PEDIDO")]
    [InlineData("ITEM")]
    [InlineData("LANCAMENTO")]
    [InlineData("MATERIAL")]
    [InlineData("PK_LOCAL")]
    [InlineData("FK_SAP")]
    public void IdentidadesLocaisDivergentesBloqueiam(string campo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        EscopoP5 escopo = Identidade.Escopo with
        {
            Chave = new(campo == "PEDIDO" ? "456" : "123", campo == "ITEM" ? "20" : "10"),
            Lancamento = campo == "LANCAMENTO" ? 901 : 900,
            Material = campo == "MATERIAL" ? "OUTRO" : "SYNTHETIC"
        };
        IdentidadeItemProdutoSapR8B outra = new(escopo, campo == "PK_LOCAL" ? 42 : 41, campo == "FK_SAP" ? 72 : 71);
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.IdentidadeDivergente,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa) with { Identidade = outra }));
    }

    [Theory]
    [InlineData("AMBIENTE")]
    [InlineData("MANDANTE")]
    [InlineData("ORIGEM")]
    [InlineData("COLETOR")]
    [InlineData("INTERVALO")]
    public void MetadadosNaoAtestaveisNaoViraramOrigemConfiavel(string campo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        evidencia = evidencia with { Contexto = evidencia.Contexto with
        {
            Ambiente = campo == "AMBIENTE" ? "OUTRO" : "QAS110",
            Mandante = campo == "MANDANTE" ? "999" : "110",
            OrigemTecnica = campo == "ORIGEM" ? "CACHE_DESKTOP" : "API_PRODUCT_SRV/A_Product",
            IdentidadeBackend = campo == "COLETOR" ? "" : "COLETOR_SIMULADO",
            ConsultaConcluidaEm = campo == "INTERVALO" ? Inicio.AddSeconds(-1) : Inicio.AddSeconds(1)
        }};
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.MetadadosInvalidos,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa)));
    }

    [Theory]
    [InlineData("REFERENCIA")]
    [InlineData("VERSAO")]
    [InlineData("COLETA")]
    [InlineData("SEM_DATA")]
    public void ReciboDeOutraEvidenciaOuVinculoNaoDuravelBloqueia(string campo)
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        VinculoEvidenciaTentativaP5R8B vinculo = Vinculo(tentativa) with
        {
            ReferenciaEvidencia = campo == "REFERENCIA" ? "OUTRA" : "EVIDENCIA_SIMULADA",
            VersaoEvidencia = campo == "VERSAO" ? 2 : 1,
            CorrelacaoColetaBackend = campo == "COLETA" ? Guid.NewGuid() : Contexto().CorrelacaoColetaBackend,
            VinculadaEmBanco = campo == "SEM_DATA" ? null : Inicio.AddSeconds(2)
        };
        Assert.NotEqual(ResultadoConsistenciaProdutoSapR8B.CoerenteSemAutoridade,
            Validar(evidencia, tentativa, Recibo(evidencia), vinculo));
    }

    [Fact]
    public void TokensEUsuarioSapNaoVazamPeloToString()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        Assert.DoesNotContain(tentativa.Token.ToString(), Vinculo(tentativa).ToString());
        Assert.DoesNotContain("USUARIO_SIMULADO", evidencia.ToString());
        Assert.DoesNotContain("USUARIO_SIMULADO", evidencia.Campos.ToString());
    }

    [Fact]
    public async Task CandidataCoerenteNaoRemoveBloqueioRohNemNoSegundoChamado()
    {
        EvidenciaProdutoSapR8B evidencia = Ler();
        ClaimP5 tentativa = Claim();
        Assert.Equal(ResultadoConsistenciaProdutoSapR8B.CoerenteSemAutoridade,
            Validar(evidencia, tentativa, Recibo(evidencia), Vinculo(tentativa)));
        MaterialDocumentSapItemRequest item = ClassificacaoMaterialDocumentP5.Registrar(new()
        {
            Material = "SYNTHETIC", PurchaseOrder = "123", PurchaseOrderItem = "00010"
        }, new ProdutoSapMestre
        {
            CodigoProduto = evidencia.Campos.Product, TipoMaterialSap = evidencia.Campos.ProductType, Consultado = true
        });
        int dispatches = 0;
        MaterialDocumentSapServico writer = new(EntradaMpP5R7Tests.Config(), null, null, dispatchHttp: (_, _) =>
        {
            dispatches++;
            return Task.FromResult(new ResultadoMaterialDocumentSap { Sucesso = true });
        });
        for (int repeticao = 0; repeticao < 2; repeticao++)
        {
            ResultadoMaterialDocumentSap retorno = await writer.CriarDocumentoMaterial101Async(new() { Itens = [item] }, "SIMULADO");
            Assert.False(retorno.Sucesso);
            Assert.False(retorno.PostTentado);
        }
        Assert.Equal(0, dispatches);
    }

    private static ResultadoConsistenciaProdutoSapR8B Validar(EvidenciaProdutoSapR8B evidencia, ClaimP5 tentativa,
        ReciboEvidenciaProdutoSapR8B? recibo, VinculoEvidenciaTentativaP5R8B? vinculo)
        => ValidadorEvidenciaProdutoSapR8B.VerificarConsistencia(Identidade, tentativa, Contexto(), evidencia, recibo, vinculo);

    private static ContextoBackendProdutoSapR8B Contexto() => new("QAS110", "110", "API_PRODUCT_SRV/A_Product",
        "COLETOR_SIMULADO", Guid.Parse("b1a68529-368a-4d7f-b7d8-34580d61d80f"), Inicio, Inicio.AddSeconds(1));

    private static ClaimP5 Claim() => new(Identidade.Escopo, 1, Guid.NewGuid(), Guid.NewGuid(), versaoGuarda: 1);

    private static EvidenciaProdutoSapR8B Ler(string tipo = "ROH", string produto = "SYNTHETIC", string timestamp = "2026-10-20T08:09:10")
        => LeitorEvidenciaProdutoSapR8B.Ler(JsonSerializer.Serialize(new { d = new
        {
            Product = produto, ProductType = tipo, LastChangeDateTime = timestamp,
            LastChangeDate = "2026-10-20", LastChangedByUser = "USUARIO_SIMULADO"
        }}), Contexto())!;

    private static ReciboEvidenciaProdutoSapR8B Recibo(EvidenciaProdutoSapR8B evidencia)
        => new(evidencia, "EVIDENCIA_SIMULADA", 1, Inicio.AddSeconds(2), null);

    private static VinculoEvidenciaTentativaP5R8B Vinculo(ClaimP5 tentativa, string tipo = "ROH")
        => new(Identidade, tentativa, "EVIDENCIA_SIMULADA", 1, tipo, Contexto().CorrelacaoColetaBackend, Inicio.AddSeconds(2));
}
