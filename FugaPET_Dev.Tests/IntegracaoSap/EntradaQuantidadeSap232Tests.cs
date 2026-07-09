using System.Globalization;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 23.2: a quantidade enviada ao SAP 101 é o peso líquido em KG, serializado com cultura
/// invariante (1,5 → "1.5"), NUNCA "1500". Caso real: bruto 2 KG − tara 0,500 KG = líquido 1,500 KG.
/// </summary>
public sealed class EntradaQuantidadeSap232Tests
{
    // ---- Ajuste 1: cálculo do peso líquido ----

    [Fact]
    public void Liquido_Bruto2_Tara05Kg_DaUmVirgulaCinco()
        => Assert.Equal(1.5m, EntradaProdutoPesagemCalculos.CalcularPesoLiquido(2m, 0.5m));

    [Fact]
    public void Liquido_Bruto2_Tara500Gramas_DaUmVirgulaCinco()
    {
        decimal taraKg = EntradaProdutoQuantidadeSap.ConverterTaraParaKg(500m, "G");
        Assert.Equal(0.5m, taraKg);
        Assert.Equal(1.5m, EntradaProdutoPesagemCalculos.CalcularPesoLiquido(2m, taraKg));
    }

    // ---- Ajuste 2/8/9: conversão de tara centralizada ----

    [Theory]
    [InlineData("G", 500, 0.5)]
    [InlineData("GR", 500, 0.5)]
    [InlineData("GRAMAS", 500, 0.5)]
    public void ConverterTara_Gramas_DividePorMil(string unidade, double valor, double esperado)
        => Assert.Equal((decimal)esperado, EntradaProdutoQuantidadeSap.ConverterTaraParaKg((decimal)valor, unidade));

    [Fact]
    public void ConverterTara_Kg_NaoDivideNovamente()
        => Assert.Equal(0.5m, EntradaProdutoQuantidadeSap.ConverterTaraParaKg(0.5m, "KG"));

    [Fact]
    public void ConverterTara_UnidadeVazia_AssumeKg()
        => Assert.Equal(0.5m, EntradaProdutoQuantidadeSap.ConverterTaraParaKg(0.5m, ""));

    // ---- Ajuste 4/5: serialização invariante ----

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("2", 2.0)]
    [InlineData("0.5", 0.5)]
    public void FormatarQuantidadeSap_UsaInvariantSemMilhar(string esperado, double valor)
        => Assert.Equal(esperado, EntradaProdutoQuantidadeSap.FormatarQuantidadeSap((decimal)valor));

    [Fact]
    public void FormatarQuantidadeSap_DecimalComZerosAFrente_1ponto500_DaUmPontoCinco()
    {
        decimal comEscala = decimal.Parse("1.500", CultureInfo.InvariantCulture); // scale 3
        Assert.Equal("1.5", EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(comEscala));
    }

    [Fact]
    public void FormatarQuantidadeSap_SobCulturaPtBr_NaoUsaVirgulaNem1500()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            string quantidade = EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(1.5m);
            Assert.Equal("1.5", quantidade);
            Assert.DoesNotContain(",", quantidade);
            Assert.NotEqual("1500", quantidade);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatarQuantidadeSap_2000PtBr_NaoVira2000()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("2", EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(2.000m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ---- Ajuste 5: guarda de coerência ----

    [Fact]
    public void QuantidadeCoerente_MesmoValor_True()
        => Assert.True(EntradaProdutoQuantidadeSap.QuantidadeCoerente(1.5m, 1.5m));

    [Fact]
    public void QuantidadeCoerente_1500Contra15_False()
        => Assert.False(EntradaProdutoQuantidadeSap.QuantidadeCoerente(1500m, 1.5m));

    // ---- Ajuste 3/8: payload final real (evidência) ----

    [Fact]
    public void Payload101_Liquido15_SerializaComo15_NuncaMil500()
    {
        MaterialDocumentSapRequest requisicao = new()
        {
            GoodsMovementCode = "01",
            PostingDate = new DateTime(2024, 1, 1),
            DocumentDate = new DateTime(2024, 1, 1),
            MaterialDocumentHeaderText = "FP 4500001424 L1",
            Itens =
            [
                new MaterialDocumentSapItemRequest
                {
                    Material = "1000046",
                    Plant = "3007",
                    StorageLocation = "PP01",
                    GoodsMovementType = "101",
                    GoodsMovementRefDocType = "B",
                    QuantityInEntryUnit = EntradaProdutoQuantidadeSap.FormatarQuantidadeSap(1.5m),
                    EntryUnit = "KG",
                    PurchaseOrder = "4500001424",
                    PurchaseOrderItem = "00010"
                }
            ]
        };

        string json = MaterialDocumentSapApiClient.SerializarPayload(requisicao);

        Assert.Contains("\"QuantityInEntryUnit\":\"1.5\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"QuantityInEntryUnit\":\"1500\"", json, StringComparison.Ordinal);
        Assert.Contains("\"EntryUnit\":\"KG\"", json, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementCode\":\"01\"", json, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementType\":\"101\"", json, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementRefDocType\":\"B\"", json, StringComparison.Ordinal);
    }

    // ---- Ajuste 7: confirmação mostra o peso líquido em KG ----

    [Fact]
    public void ConfirmacaoEnvio101_MostraPesoLiquidoEmKg()
    {
        string mensagem = ProcessoEntradaProdutoForm.MontarConfirmacaoEnvio101(1, 1.5m);
        Assert.Contains("peso líquido de 1,500 KG", mensagem, StringComparison.Ordinal);
    }

    // ---- Ajuste 3/6/7/9: wiring no controller e tela (source-scan) ----

    [Fact]
    public void Controller_QuantidadeVemDoPesoLiquido_ComGuardaEDiagnostico()
    {
        string controller = File.ReadAllText(Path.Combine(RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        // Ajuste 3: QuantityInEntryUnit a partir do peso líquido (não label/texto de tela).
        Assert.Contains("QuantityInEntryUnit = FormatarQuantidade(item.PesoLiquidoKg)", controller, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoQuantidadeSap.FormatarQuantidadeSap", controller, StringComparison.Ordinal);
        // Ajuste 5: guarda de coerência antes da reserva/POST.
        Assert.Contains("ValidarCoerenciaQuantidadeSap(codigoLancamento, itens)", controller, StringComparison.Ordinal);
        Assert.Contains("Quantidade SAP divergente do peso líquido calculado", controller, StringComparison.Ordinal);
        // Ajuste 6: diagnóstico com bruto/tara/líquido/quantity/entryunit.
        Assert.Contains("Peso bruto", controller, StringComparison.Ordinal);
        Assert.Contains("Tara convertida", controller, StringComparison.Ordinal);
        Assert.Contains("QuantityInEntryUnit", controller, StringComparison.Ordinal);
        // payload constants preservadas.
        Assert.Contains("EntryUnit = \"KG\"", controller, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementType = \"101\"", controller, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementCode = \"01\"", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_TaraConvertidaCentralmente_NaoUsaLabelComoFonteDoPayload()
    {
        string tela = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));
        Assert.Contains("EntradaProdutoQuantidadeSap.ConverterTaraParaKg(tara.PesoKg, \"KG\")", tela, StringComparison.Ordinal);
        Assert.Contains("MontarConfirmacaoEnvio101(codigoLancamento, pesoLiquidoTotalKg)", tela, StringComparison.Ordinal);
    }

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
