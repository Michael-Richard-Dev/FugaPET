using System.Text.Json;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Line-ID do item do documento de material.
/// <para>
/// O SAP recusou o POST real em 13/11/2026 com HTTP 400
/// <c>MM_IM_ODATA_API_MDOC/027 — "Provide Line-Ids when using Business Context
/// MaterialDocumentItem"</c>. Ao postar pelo contexto de negócio MaterialDocumentItem, cada posição
/// precisa de uma linha própria. O nome do campo veio da <c>$metadata</c> da própria instalação
/// (<c>MaterialDocumentLine</c>, Edm.String MaxLength=6), não de suposição.
/// </para>
/// </summary>
public sealed class MaterialDocumentLineIdTests
{
    private static MaterialDocumentSapItemRequest Item(int linha)
    {
        ClassificacaoEntradaMaterial classificacao =
            ClassificadorItemEntradaMaterial.ClassificarPorProductType("ROH");
        ResultadoAlocacao101 alocacao =
            EntradaProdutoAlocacao101.Decidir(classificacao, null, "KG", 9.5m, "1000090");
        return EntradaProdutoController.MontarItemMaterialDocument(
            "4500002441",
            new EntradaProdutoItemEnvioSap
            {
                Material = "1000090", Centro = "3007", Deposito = "PP01",
                Unidade = "KG", NumeroItem = "10"
            },
            comLote: false,
            alocacao,
            linha);
    }

    [Fact]
    public void ItemLevaALinhaInformada()
        => Assert.Equal("1", Item(1).MaterialDocumentLine);

    [Fact]
    public void LinhasSaoDistintasEntrePosicoes()
    {
        // Duas posicoes no MESMO payload nao podem compartilhar a linha: e justamente a
        // identidade que o SAP pediu.
        Assert.NotEqual(Item(1).MaterialDocumentLine, Item(2).MaterialDocumentLine);
        Assert.Equal("2", Item(2).MaterialDocumentLine);
    }

    [Fact]
    public void LinhaRespeitaOFacetDoSap()
    {
        // Edm.String MaxLength=6 na $metadata.
        Assert.True(Item(999999).MaterialDocumentLine!.Length <= 6);
    }

    private static string PayloadReal(params MaterialDocumentSapItemRequest[] itens)
        => FugaPET_Dev.Servicos.IntegracaoSap.MaterialDocumentSapApiClient.SerializarPayload(
            new MaterialDocumentSapRequest
            {
                GoodsMovementCode = "01",
                PostingDate = new DateTime(2026, 11, 13),
                DocumentDate = new DateTime(2026, 11, 13),
                MaterialDocumentHeaderText = "FP 4500002441 L35",
                Itens = itens
            });

    // ESTE e o teste que faltava. A primeira versao serializava o DTO direto e passava, enquanto o
    // SAP continuava recusando: o corpo do POST e montado por ALLOWLIST EXPLICITA em
    // MaterialDocumentSapApiClient.SerializarPayload, e preencher a propriedade do DTO NAO faz o
    // campo viajar. Testar o DTO em vez do payload real e testar a coisa errada.
    [Fact]
    public void PayloadREAL_CarregaOLineId()
    {
        string json = PayloadReal(Item(1));
        Assert.Contains("\"MaterialDocumentLine\":\"1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadREAL_DaUmaLinhaParaCadaPosicao()
    {
        string json = PayloadReal(Item(1), Item(2));
        Assert.Contains("\"MaterialDocumentLine\":\"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"MaterialDocumentLine\":\"2\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadREAL_NaoEnviaMaterialDocumentItem()
    {
        // MaterialDocumentItem e gerado pelo SAP; nao pode ser preenchido por nos. A chave
        // "to_MaterialDocumentItem" (navegacao) nao conta.
        string json = PayloadReal(Item(1)).Replace("to_MaterialDocumentItem", "NAV", StringComparison.Ordinal);
        Assert.DoesNotContain("\"MaterialDocumentItem\":", json, StringComparison.Ordinal);
    }
}
