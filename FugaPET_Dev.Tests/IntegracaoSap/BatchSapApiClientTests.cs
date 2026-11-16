using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 126F: leitura das datas de LOTE no SAP (API_BATCH_SRV).
/// <para>
/// O JSON de <see cref="RespostaRealDev110"/> e a resposta LITERAL do S/4 do DEV110 em 16/11/2026,
/// copiada do Postman — nao e um exemplo inventado. Testar contra um contrato presumido foi
/// exatamente o erro que custou tempo na norma de embalagem.
/// </para>
/// </summary>
public sealed class BatchSapApiClientTests
{
    private const string RespostaRealDev110 = """
    {
        "d": {
            "results": [
                {
                    "__metadata": {
                        "id": "https://host/sap/opu/odata/sap/API_BATCH_SRV/Batch(Material='4000548',BatchIdentifyingPlant='',Batch='61008561F')",
                        "type": "cds_api_batch_srv.BatchType"
                    },
                    "Material": "4000548",
                    "BatchIdentifyingPlant": "",
                    "Batch": "61008561F",
                    "BatchIsMarkedForDeletion": false,
                    "MatlBatchAvailabilityDate": "/Date(1787270400000)/",
                    "ShelfLifeExpirationDate": "/Date(1836950400000)/",
                    "ManufactureDate": "/Date(1787270400000)/",
                    "FreeDefinedDate1": null,
                    "CreationDateTime": "/Date(1787316769669+0000)/"
                }
            ]
        }
    }
    """;

    [Fact]
    public void InterpretaARespostaRealDoDev110()
    {
        IReadOnlyList<LoteSapDatas> lotes = BatchSapApiClient.Interpretar(RespostaRealDev110);

        LoteSapDatas lote = Assert.Single(lotes);
        Assert.Equal("4000548", lote.Material);
        Assert.Equal("61008561F", lote.Lote);

        // /Date(1787270400000)/ e /Date(1836950400000)/ convertidos de milissegundos de epoca, em UTC.
        Assert.Equal(new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc), lote.DataFabricacao!.Value);
        Assert.Equal(new DateTime(2028, 3, 18, 0, 0, 0, DateTimeKind.Utc), lote.DataVencimento!.Value);

        // 575 dias de validade remanescente na fabricacao. Confere com o 12/007 que o SAP devolveu
        // no 101 em 16/11/2026: exigia 500 dias e faltavam 12 — coerente com este lote.
        Assert.Equal(575, (lote.DataVencimento.Value - lote.DataFabricacao.Value).Days);
    }

    /// <summary>
    /// Lote sem data cadastrada: o SAP manda null. Tem de virar null, NAO data de hoje nem mínima —
    /// a etiqueta precisa poder dizer "NAO INFORMADA".
    /// </summary>
    [Fact]
    public void DataNulaNoSapViraNuloEnaoDataInventada()
    {
        IReadOnlyList<LoteSapDatas> lotes = BatchSapApiClient.Interpretar("""
        { "d": { "results": [ { "Material": "4000548", "Batch": "SEM-DATAS",
          "ManufactureDate": null, "ShelfLifeExpirationDate": null } ] } }
        """);

        LoteSapDatas lote = Assert.Single(lotes);
        Assert.Null(lote.DataFabricacao);
        Assert.Null(lote.DataVencimento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao e json")]
    [InlineData("{}")]
    [InlineData("""{ "d": {} }""")]
    [InlineData("""{ "d": { "results": [] } }""")]
    public void RespostaVaziaOuMalformadaDevolveListaVazia(string? json)
        => Assert.Empty(BatchSapApiClient.Interpretar(json));

    /// <summary>Linha sem Batch nao identifica lote nenhum: e descartada em vez de virar lote vazio.</summary>
    [Fact]
    public void LinhaSemLoteEDescartada()
        => Assert.Empty(BatchSapApiClient.Interpretar(
            """{ "d": { "results": [ { "Material": "4000548" } ] } }"""));

    [Fact]
    public void UrlUsaAColecaoBatchEFiltraPorMaterialELote()
    {
        string url = BatchSapApiClient.MontarUrl(
            "https://host/sap/opu/odata/sap/API_BATCH_SRV/", "4000548", "61008561F", "110");

        // A colecao e "Batch": pedir "A_Batch" devolveu 404 no DEV110.
        Assert.Contains("/API_BATCH_SRV/Batch?", url, StringComparison.Ordinal);
        Assert.DoesNotContain("A_Batch", url, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("Material eq '4000548' and Batch eq '61008561F'"),
            url, StringComparison.Ordinal);

        // Sem $select DE PROPOSITO: um campo desconhecido derruba a consulta inteira com 404.
        Assert.DoesNotContain("$select", url, StringComparison.Ordinal);

        // O nivel de lote neste sistema e por MATERIAL (BatchIdentifyingPlant vem vazio): filtrar por
        // centro nao acharia o lote.
        Assert.DoesNotContain("BatchIdentifyingPlant", url, StringComparison.Ordinal);
        Assert.DoesNotContain("Plant", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlNaoDuplicaBarraNemExigeBarraFinal()
    {
        string comBarra = BatchSapApiClient.MontarUrl("https://host/API_BATCH_SRV/", "M", "L", null);
        string semBarra = BatchSapApiClient.MontarUrl("https://host/API_BATCH_SRV", "M", "L", null);

        Assert.Equal(comBarra, semBarra);
        Assert.DoesNotContain("//Batch", comBarra, StringComparison.Ordinal);
        Assert.DoesNotContain("sap-client", comBarra, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aspas simples delimitam literal no OData e DOBRAM dentro do valor. Sem isso, um lote com
    /// apostrofo quebraria a consulta — ou, pior, mudaria o filtro silenciosamente.
    /// </summary>
    [Fact]
    public void ApostrofoNoLoteEEscapado()
    {
        Assert.Equal("LOTE''X", BatchSapApiClient.EscaparLiteralOData("LOTE'X"));

        string url = BatchSapApiClient.MontarUrl("https://host/API_BATCH_SRV", "M", "LOTE'X", null);
        Assert.Contains(Uri.EscapeDataString("Batch eq 'LOTE''X'"), url, StringComparison.Ordinal);
    }
}
