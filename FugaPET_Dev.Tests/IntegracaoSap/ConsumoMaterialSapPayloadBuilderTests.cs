using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Preview do movimento de consumo 261 (API_MATERIAL_DOCUMENT_SRV). Somente MONTAGEM: o builder
/// nao faz POST, nao busca CSRF, nao cria documento. Verifica payload e validacoes.
/// </summary>
public sealed class ConsumoMaterialSapPayloadBuilderTests
{
    private static readonly DateTime DataUtc = new(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc);

    private static ConsumoMaterialItem Item(
        decimal consumida = 5.5m, string unidade = "KG", string material = "QM002", string lote = "LOTE-261")
        => new()
        {
            NumeroOrdem = "1000009",
            CodigoMaterial = material,
            Centro = "3007",
            DepositoConsumo = "PP01",
            NumeroReserva = "6676",
            ItemReserva = "2",
            Lote = lote,
            QuantidadeConsumidaLocal = consumida,
            Unidade = unidade,
            TipoMovimentoSap = "261",
            StatusItem = "PENDENTE_SAP"
        };

    private static ConsumoMaterialLancamento Lancamento(string status, params ConsumoMaterialItem[] itens)
        => new()
        {
            NumeroOrdem = "1000009",
            Centro = "3007",
            MaterialProduzido = "MAT-12345",
            Unidade = "PC",
            StatusLancamento = status,
            Itens = itens
        };

    private static ResultadoPreviewConsumoSap261 Preview(ConsumoMaterialLancamento lancamento)
        => new ConsumoMaterialSapPayloadBuilder().MontarPreview261(lancamento, DataUtc);

    [Fact]
    public void Preview_PendenteKgValido_GeraHeaderEItem261()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP", Item()));

        Assert.True(r.Sucesso);
        Assert.Equal("03", r.Payload!.GoodsMovementCode);          // teste 1
        Assert.Single(r.Itens);
        ConsumoMaterialSap261ItemRequest item = r.Itens[0];
        Assert.Equal("261", item.GoodsMovementType);              // teste 2
        Assert.Equal("1000009", item.ManufacturingOrder);        // teste 3
        Assert.Equal("QM002", item.Material);                    // teste 4
        Assert.Equal("3007", item.Plant);
        Assert.Equal("PP01", item.StorageLocation);
        Assert.Equal("6676", item.Reservation);
        Assert.Equal("2", item.ReservationItem);
        Assert.Equal("LOTE-261", item.Batch);
        Assert.Equal("5.500", item.QuantityInEntryUnit);         // testes 5 e 6 (ponto, invariant)
        Assert.Equal("KG", item.EntryUnit);
        Assert.Contains("\"Batch\": \"LOTE-261\"", r.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_DeveFormatarDatasEmODataV2EHeaderAte25()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP", Item()));

        Assert.Equal(DataUtc, r.Payload!.PostingDate);
        Assert.StartsWith("/Date(", ConsumoMaterialSapPayloadBuilder.FormatarDataODataV2(DataUtc), StringComparison.Ordinal);
        Assert.Contains("\"PostingDate\": \"/Date(", r.PayloadJson, StringComparison.Ordinal);   // teste 7
        Assert.Contains("\"DocumentDate\": \"/Date(", r.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("FP CONS 1000009", r.Payload.MaterialDocumentHeaderText);
        Assert.True(r.Payload.MaterialDocumentHeaderText.Length <= 25);                          // teste 8
    }

    [Fact]
    public void Preview_HeaderTextLongo_TruncaPara25()
    {
        ConsumoMaterialLancamento lanc = new()
        {
            NumeroOrdem = "1000009999999999999999",
            StatusLancamento = "PENDENTE_SAP",
            Itens = [Item()]
        };
        ResultadoPreviewConsumoSap261 r = new ConsumoMaterialSapPayloadBuilder().MontarPreview261(lanc, DataUtc);

        Assert.True(r.Sucesso);
        Assert.Equal(25, r.Payload!.MaterialDocumentHeaderText.Length);
    }

    [Fact]
    public void Preview_PayloadJson_ContemToMaterialDocumentItemResults()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP", Item()));

        Assert.Contains("to_MaterialDocumentItem", r.PayloadJson, StringComparison.Ordinal);     // teste 13
        Assert.Contains("\"results\"", r.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementCode\": \"03\"", r.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_SemItemValido_RetornaFalha()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP"));

        Assert.False(r.Sucesso);                                                                  // teste 9
        Assert.Empty(r.PayloadJson);
        Assert.Null(r.Payload);
    }

    [Fact]
    public void Preview_UnidadeDiferenteKg_RetornaFalha()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP", Item(unidade: "L")));

        Assert.False(r.Sucesso);                                                                  // teste 10
        Assert.NotEmpty(r.ErrosValidacao);
        Assert.Empty(r.PayloadJson);
    }

    [Fact]
    public void Preview_LoteVazio_BloqueiaAntesDoPostENaoEnviaBatchVazio()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("PENDENTE_SAP", Item(lote: string.Empty)));

        Assert.False(r.Sucesso);
        Assert.Null(r.Payload);
        Assert.Empty(r.PayloadJson);
        Assert.Contains(r.ErrosValidacao, erro => erro.Contains("Informe o lote do componente", StringComparison.Ordinal));
        Assert.DoesNotContain("\"Batch\": \"\"", r.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_QuantidadeZero_IgnoraItem_ESeTodosZeroFalha()
    {
        // Um item zero (ignorado) + um valido -> sucesso com 1 item.
        ResultadoPreviewConsumoSap261 comUmValido = Preview(Lancamento("PENDENTE_SAP", Item(consumida: 0m), Item(consumida: 3m)));
        Assert.True(comUmValido.Sucesso);
        Assert.Single(comUmValido.Itens);

        // Todos zero -> falha (teste 11).
        ResultadoPreviewConsumoSap261 todosZero = Preview(Lancamento("PENDENTE_SAP", Item(consumida: 0m)));
        Assert.False(todosZero.Sucesso);
    }

    [Fact]
    public void Preview_StatusDiferentePendente_NaoGeraPreview()
    {
        ResultadoPreviewConsumoSap261 r = Preview(Lancamento("CONFIRMADO_SAP", Item()));

        Assert.False(r.Sucesso);                                                                  // teste 12
        Assert.Empty(r.PayloadJson);
    }

    [Fact]
    public void PreviewConsumo261_NaoEnviaSapNemBuscaCsrf()
    {
        string builder = LerArquivoProjeto("Servicos", "IntegracaoSap", "ConsumoMaterialSapPayloadBuilder.cs");
        string servico = LerArquivoProjeto("Servicos", "Operacao", "ConsumoMaterialServico.cs");
        string controller = LerArquivoProjeto("Controle", "Processo", "ProcessoConsumoMaterialController.cs");

        foreach (string fonte in new[] { builder, servico, controller })
        {
            Assert.DoesNotContain("HttpMethod.Post", fonte, StringComparison.Ordinal);            // teste 15
            Assert.DoesNotContain("HttpMethod.Patch", fonte, StringComparison.Ordinal);           // teste 17
            Assert.DoesNotContain("X-CSRF", fonte, StringComparison.Ordinal);                     // teste 16
            Assert.DoesNotContain("HttpClient", fonte, StringComparison.Ordinal);                 // teste 14
        }

        // Preview NAO confirma nem grava documento material (testes 18 e 19).
        Assert.DoesNotContain("CONFIRMADO_SAP", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("documento_material_sap", builder, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

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
