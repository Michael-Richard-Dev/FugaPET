using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ProdutoAcabadoPipelineResultadoPresenterTests
{
    [Fact]
    public void CasoReal0012_261ErroSap_MostraDetalheOperacionalESemAvanco()
    {
        ResultadoPipelineProdutoAcabado resultado = ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento261, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Erro,
            HttpStatus261 = 400,
            Mensagem261Sanitizada = "Etapa POST_CONSUMO_261: HTTP 400. Código: MM_IM_ODATA_API_MDOC/014. Mensagem: Material Document processing failed. Detalhes: M8/147: Account determination for entry PCFG GBB 0001 VBR 3050 not possible | MM_IM_ODATA_API_MDOC/014: Material Document processing failed."
        });

        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(resultado);

        Assert.Equal("SAP — Movimento 261 não realizado", msg.Titulo);
        Assert.Contains("261", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("400", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("MM_IM_ODATA_API_MDOC/014", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("M8/147", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Account determination for entry PCFG GBB 0001 VBR 3050 not possible", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("A etapa 101 não foi executada", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("A HU não foi executada", msg.Mensagem, StringComparison.Ordinal);
        AssertValoresAdversariaisRemovidos(msg.Mensagem);
    }

    [Fact]
    public void Erro101Sap_Mostra261ConfirmadoEHuNaoExecutada()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento101, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Confirmado,
            MaterialDocument261 = "4900000010",
            MaterialDocumentYear261 = "2026",
            Estado101 = EstadoMovimentoSap.Erro,
            HttpStatus101 = 400,
            Mensagem101Sanitizada = "Etapa POST_101: HTTP 400. Código: MM_IM_ODATA_API_MDOC/014. Mensagem: Material Document processing failed. Detalhes: M7/021: Deficit of SL Unrestricted-use 10 KG."
        }));

        Assert.Equal("SAP — Entrada 101 não realizada", msg.Titulo);
        Assert.Contains("261 confirmado: 4900000010/2026", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("101", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("M7/021", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("A HU não foi executada", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void HuErro_Mostra261E101ConfirmadosEHuNaoCriada()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.HandlingUnit, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Confirmado,
            MaterialDocument261 = "4900000010",
            MaterialDocumentYear261 = "2026",
            Estado101 = EstadoMovimentoSap.Confirmado,
            MaterialDocument101 = "5000000020",
            MaterialDocumentYear101 = "2026",
            EstadoHu = StatusIntegracaoCaixa.ErroSap,
            HandlingUnitSap = "HU123"
        }, "HU retornou ERRO_SAP."));

        Assert.Equal("SAP — HU não criada", msg.Titulo);
        Assert.Contains("261 confirmado: 4900000010/2026", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("101 confirmado: 5000000020/2026", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Estado HU: ERRO_SAP", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Indeterminado261_OrientaNaoReenviar()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento261, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.IndeterminadoTimeout,
            HttpStatus261 = 504,
            Mensagem261Sanitizada = "Resultado indeterminado após possível POST."
        }));

        Assert.Equal("Atenção — Resultado indeterminado", msg.Titulo);
        Assert.Contains("RESULTADO INDETERMINADO", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Não tente enviar novamente", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void BloqueioReconciliacao_SemSnapshot_OrientaNaoReenviar()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(new ResultadoPipelineProdutoAcabado(false, EtapaPipelineProdutoAcabado.Bloqueada, "Não tente enviar novamente. Reconciliação obrigatória.", null));

        Assert.Equal("Atenção — Reconciliação necessária", msg.Titulo);
        Assert.Contains("Bloqueada", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Não tente enviar novamente", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void ErroLocalPrePost_NaoInventaHttpOuSap()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento261, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Erro,
            Mensagem261Sanitizada = "261 sem OP ou sem componentes: fail-closed (nenhum POST)."
        }));

        Assert.Contains("ERRO LOCAL", msg.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("HTTP:", msg.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("Código SAP:", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void SucessoTotal_MostraDocumentosEHu()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Concluido, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Confirmado,
            MaterialDocument261 = "4900000010",
            MaterialDocumentYear261 = "2026",
            Estado101 = EstadoMovimentoSap.Confirmado,
            MaterialDocument101 = "5000000020",
            MaterialDocumentYear101 = "2026",
            EstadoHu = StatusIntegracaoCaixa.ConfirmadaSap,
            HandlingUnitSap = "800000000123"
        }));

        Assert.Equal("SAP — Envio concluído", msg.Titulo);
        Assert.Contains("261 confirmado: 4900000010/2026", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("101 confirmado: 5000000020/2026", msg.Mensagem, StringComparison.Ordinal);
        Assert.Contains("HU confirmada: 800000000123", msg.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizacao_RemoveValoresAdversariaisIdentificaveis()
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento261, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Erro,
            HttpStatus261 = 400,
            Mensagem261Sanitizada = "Authorization: Basic ABC123 senha=SEGREDO123 cookie=COOKIE456 csrf=TOKEN789 claim_token=CLAIM999 client_secret=SECRETXYZ Host=servidor-interno Connection String=Host=servidor-interno;Password=SEGREDO123"
        }));

        AssertValoresAdversariaisRemovidos(msg.Mensagem);
        Assert.Contains("Authorization: [REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("senha=[REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cookie: [REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("csrf=[REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("claim_token=[REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("client_secret=[REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Host=[REMOVIDO]", msg.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("senha=SEGREDO123")]
    [InlineData("senha: SEGREDO123")]
    [InlineData("senha = SEGREDO123")]
    [InlineData("senha : SEGREDO123")]
    [InlineData("PASSWORD=SEGREDO123")]
    [InlineData("passwd: SEGREDO123")]
    [InlineData("pwd = SEGREDO123")]
    [InlineData("Authorization: Basic ABC123")]
    [InlineData("Authorization: Bearer TOKEN789")]
    [InlineData("Cookie: COOKIE456")]
    [InlineData("Set-Cookie: COOKIE456")]
    [InlineData("X-CSRF-Token: TOKEN789")]
    [InlineData("client_secret = SECRETXYZ")]
    [InlineData("Host = servidor-interno")]
    public void Sanitizacao_CobreFormatosDeCampoSensivel(string entrada)
    {
        ApresentacaoResultadoPipelineProdutoAcabado msg = ProdutoAcabadoPipelineResultadoPresenter.Construir(ResultadoComSnapshot(EtapaPipelineProdutoAcabado.Movimento261, new ProdutoAcabadoPipelineSnapshot
        {
            Estado261 = EstadoMovimentoSap.Erro,
            HttpStatus261 = 400,
            Mensagem261Sanitizada = entrada
        }));

        AssertValoresAdversariaisRemovidos(msg.Mensagem);
        Assert.Contains("[REMOVIDO]", msg.Mensagem, StringComparison.Ordinal);
    }

    private static ResultadoPipelineProdutoAcabado ResultadoComSnapshot(EtapaPipelineProdutoAcabado etapa, ProdutoAcabadoPipelineSnapshot snapshot, string mensagem = "resultado")
        => new(false, etapa, mensagem, snapshot);

    private static void AssertValoresAdversariaisRemovidos(string mensagem)
    {
        Assert.DoesNotContain("ABC123", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SEGREDO123", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COOKIE456", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOKEN789", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CLAIM999", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRETXYZ", mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("servidor-interno", mensagem, StringComparison.OrdinalIgnoreCase);
    }
}

