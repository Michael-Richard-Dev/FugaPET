using System.Globalization;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 121A: a unidade exibida nos campos de QUANTIDADE DE PRODUCAO deve ser a ProductionUnit
/// REAL da Ordem de Producao, e nao "KG" cravado. Os campos de PESO FISICO continuam em KG.
/// <para>
/// Nenhuma conversao numerica e introduzida: a correcao e exclusivamente de apresentacao.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoUnidadeProducaoVisual121ATests
{
    private static string Numero(decimal v) => v.ToString("0.000", CultureInfo.GetCultureInfo("pt-BR"));

    // ===================== A / B / C: ProductionUnit=UN =====================

    [Fact]
    public void A_Planejada_ComUnidadeUN_ExibeUN()
    {
        // OP 1000242: TotalQuantity=9016, ProductionUnit=UN
        Assert.Equal(
            $"{Numero(9016m)} UN",
            ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(9016m, "UN"));
    }

    [Fact]
    public void B_Entregue_ComUnidadeUN_ExibeUN()
        => Assert.Equal(
            $"{Numero(8m)} UN",
            ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(8m, "UN"));

    [Fact]
    public void C_Pendente_ComUnidadeUN_ExibeUN()
        => Assert.Equal(
            $"{Numero(9008m)} UN",
            ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(9008m, "UN"));

    [Fact]
    public void ABC_ComUN_NenhumDosTresExibeKG()
    {
        foreach (decimal valor in new[] { 9016m, 8m, 9008m, 16m })
        {
            string texto = ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(valor, "UN");
            Assert.DoesNotContain("KG", texto, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(" UN", texto, StringComparison.Ordinal);
        }
    }

    // ===================== D: ProductionUnit=KG =====================

    [Fact]
    public void D_ComUnidadeKG_OsTresExibemKG()
    {
        foreach (decimal valor in new[] { 9016m, 8m, 9008m })
        {
            Assert.Equal(
                $"{Numero(valor)} KG",
                ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(valor, "KG"));
        }
    }

    [Theory]
    [InlineData("kg", "KG")]
    [InlineData(" UN ", "UN")]
    [InlineData("un", "UN")]
    [InlineData("TO", "TO")]
    [InlineData("G", "G")]
    [InlineData("KGM", "KGM")]
    public void UnidadeRealEhRespeitadaENormalizadaEmMaiuscula(string informada, string esperada)
        => Assert.Equal(
            $"{Numero(5m)} {esperada}",
            ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(5m, informada));

    // ===================== E / F / G: pesos permanecem KG =====================

    [Fact]
    public void EFG_PesoBrutoTaraELiquido_PermanecemEmKG()
    {
        // Caixa conhecida: bruto 14,400 / tara 0,050 / liquido 14,350 KG.
        string fonte = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // Os tres campos fisicos continuam formatados por FormatarKg (que anexa " KG").
        Assert.Contains("FormatarKg(caixa.PesoBrutoKg)", fonte, StringComparison.Ordinal);
        Assert.Contains("FormatarKg(caixa.TaraKg)", fonte, StringComparison.Ordinal);
        Assert.Contains("FormatarKg(caixa.PesoLiquidoKg)", fonte, StringComparison.Ordinal);

        // E NENHUM deles passou a usar o formatador de quantidade de producao.
        Assert.DoesNotContain("FormatarQuantidadeProducao(caixa.PesoBrutoKg", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatarQuantidadeProducao(caixa.TaraKg", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatarQuantidadeProducao(caixa.PesoLiquidoKg", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void EFG_FormatarKgContinuaAnexandoKGLiteral()
    {
        string fonte = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.Contains(
            """private static string FormatarKg(decimal valor)""",
            fonte, StringComparison.Ordinal);
        Assert.Contains(
            """{valor.ToString("0.000", CultureInfo.GetCultureInfo("pt-BR"))} KG""",
            fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void EFG_SomaDePesoLiquidoDasCaixasSegueEmKG()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        // No bloco "PESO REGISTRADO" a soma fisica continua em KG.
        Assert.Contains("decimal liquido = _caixasPesadas.Sum(caixa => caixa.PesoLiquidoKg);", fonte, StringComparison.Ordinal);
        Assert.Contains("packagesCounterLabel.Text = FormatarKg(liquido);", fonte, StringComparison.Ordinal);
    }

    // ===================== H: unidade ausente => NAO assume KG =====================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void H_UnidadeAusenteOuVazia_NaoAssumeKG(string? unidade)
    {
        string texto = ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(9016m, unidade);

        Assert.DoesNotContain("KG", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UN", texto, StringComparison.OrdinalIgnoreCase);
        // Comportamento neutro: so o numero, sem sufixo inventado.
        Assert.Equal(Numero(9016m), texto);
    }

    [Fact]
    public void H_SourceNaoTemMaisFallbackParaKGNaUnidadeDaOp()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // O default silencioso para KG foi removido — NENHUM lugar do fonte inventa a unidade.
        Assert.DoesNotContain("""?? "KG" """.Trim(), fonte, StringComparison.Ordinal);
        // E a propriedade de unidade tambem nao inventa KG.
        Assert.Contains("_ordemAtual?.Unidade?.Trim() ?? string.Empty", fonte, StringComparison.Ordinal);

        // Em 27/11/2026 a regra "esta OP e medida por peso?" saiu de dentro do resumo e virou um
        // metodo proprio, porque a GUARDA de saldo pendente precisava enxergar a MESMA regra — ela
        // comparava quilo com unidade e barrava caixa legitima. A assercao que exigia o literal
        // "_ordemAtual?.Unidade ?? string.Empty" dentro do resumo ia junto com o texto antigo; o que
        // importa e que a unidade continue vindo da OP, sem default, e que exista um lugar so.
        Assert.Contains("private bool OrdemMedidaPorPeso() => OrdemMedidaPorPeso(_ordemAtual?.Unidade);", fonte, StringComparison.Ordinal);
        Assert.Contains("(unidadeDaOrdem ?? string.Empty).Trim().ToUpperInvariant()", fonte, StringComparison.Ordinal);
    }

    // ===================== I: nenhum valor numerico foi alterado =====================

    [Fact]
    public void I_NenhumaConversaoNumerica_OFormatoNumericoEhIdenticoAoDePeso()
    {
        // Mesmo valor, formatadores diferentes: APENAS o sufixo difere.
        const decimal valor = 14.350m;
        string producao = ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(valor, "UN");
        string esperadoNumero = Numero(valor);

        Assert.StartsWith(esperadoNumero, producao, StringComparison.Ordinal);
        Assert.Equal($"{esperadoNumero} UN", producao);
        // Trocar a unidade NAO muda o numero (nada de UN <-> KG).
        Assert.Equal(
            esperadoNumero,
            ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(valor, "KG")
                .Replace(" KG", string.Empty, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UN")]
    [InlineData("KG")]
    [InlineData("TO")]
    [InlineData(null)]
    public void I_OValorNumericoEhInvarianteAUnidade(string? unidade)
    {
        string texto = ProcessoProdutoAcabadoForm.FormatarQuantidadeProducao(9016m, unidade);
        Assert.StartsWith(Numero(9016m), texto, StringComparison.Ordinal);
    }

    [Fact]
    public void I_FormatadorDeProducaoNaoContemAritmetica()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        int i = fonte.IndexOf("internal static string FormatarQuantidadeProducao", StringComparison.Ordinal);
        Assert.True(i >= 0, "FormatarQuantidadeProducao nao encontrado.");
        int fim = fonte.IndexOf("\n    private", i + 40, StringComparison.Ordinal);
        string corpo = fim > i ? fonte[i..fim] : fonte[i..];

        foreach (string proibido in new[] { "*", "/", "Math.", "Convert." })
        {
            Assert.DoesNotContain(proibido, corpo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void I_SaldoPendenteNaoDescontaPesoDeCaixaConfirmadaSap()
    {
        ProdutoAcabadoCaixa confirmada = new()
        {
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
            PesoLiquidoKg = 14.350m,
            QuantidadeProdutos = 8
        };

        Assert.Equal(
            9016m,
            CalculoSaldoProdutoAcabado.SaldoPendenteExibido(9016m, [confirmada], "UN"));
    }

    // ===================== J: fluxo 261/101/HU intocado =====================

    [Fact]
    public void J_FluxoDoPipelineNaoFoiAlterado()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // O 261 continua recebendo o DELTA do alocador (118B) e a origem segue o mesmo contrato.
        Assert.Contains("Quantidade = item.Delta261", fonte, StringComparison.Ordinal);
        Assert.Contains("await _controller.CalcularAlocacao261FrescaAsync(caixa)", fonte, StringComparison.Ordinal);
        Assert.Contains("alocacao.Cenario != CenarioAllocator261.Ok", fonte, StringComparison.Ordinal);

        // O 101 continua enviando QuantidadeProdutos na UnidadeQuantidade da caixa — sem passar
        // pelo formatador visual (o formatador NAO entra em payload SAP).
        Assert.Contains("QuantityInEntryUnit = caixa.QuantidadeProdutos.ToString(CultureInfo.InvariantCulture)", fonte, StringComparison.Ordinal);
        Assert.Contains("EntryUnit = caixa.UnidadeQuantidade", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void J_FormatadorVisualNaoEhUsadoEmPayloadSap()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // Nenhum campo de payload (QuantityInEntryUnit/EntryUnit/Quantidade) e alimentado pelo
        // formatador de apresentacao.
        Assert.DoesNotContain("QuantityInEntryUnit = FormatarQuantidadeProducao", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("EntryUnit = FormatarQuantidadeProducao", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Quantidade = FormatarQuantidadeProducao", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatarQuantidadeProducao", SomenteOrigemPipeline(fonte), StringComparison.Ordinal);
    }

    /// <summary>Recorta o metodo que monta a origem do pipeline, para provar que ele nao formata nada.</summary>
    private static string SomenteOrigemPipeline(string fonte)
    {
        int i = fonte.IndexOf("internal static ProdutoAcabadoPipelineOrigem MontarOrigemPipelineRuntime", StringComparison.Ordinal);
        Assert.True(i >= 0, "MontarOrigemPipelineRuntime nao encontrado.");
        int fim = fonte.IndexOf("\n    private static DateTime NormalizarDataLancamentoPipeline", i, StringComparison.Ordinal);
        return fim > i ? fonte[i..fim] : fonte[i..];
    }

    // ===================== os tres campos realmente trocaram de formatador =====================

    [Fact]
    public void OsTresCamposDeProducaoUsamOFormatadorDeProducao()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // Saldo pendente (classificationDate), Planejada (manufacturingDate), Entregue (expirationDate).
        Assert.Contains("classificationDateTextBox.Text =\r\n            FormatarQuantidadeProducao(CalcularSaldoPendenteExibido(), UnidadeProducaoAtual)", fonte, StringComparison.Ordinal);
        Assert.Contains("FormatarQuantidadeProducao(_ordemAtual.QuantidadePlanejada, UnidadeProducaoAtual)", fonte, StringComparison.Ordinal);
        Assert.Contains("FormatarQuantidadeProducao(recebidaSap, UnidadeProducaoAtual)", fonte, StringComparison.Ordinal);

        // E NENHUM dos tres usa mais FormatarKg.
        Assert.DoesNotContain("FormatarKg(CalcularSaldoPendenteExibido())", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatarKg(_ordemAtual.QuantidadePlanejada)", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatarKg(recebidaSap)", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void RodapeNaoDuplicaMaisOSufixoDeUnidade()
    {
        string fonte = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // Antes: $"de {FormatarKg(pendentePeso)} KG" => "9016,000 KG KG".
        Assert.DoesNotContain("""{FormatarKg(pendentePeso)} KG""", fonte, StringComparison.Ordinal);
        Assert.Contains("""de {FormatarQuantidadeProducao(CalcularSaldoPendenteExibido(), UnidadeProducaoAtual)}""", fonte, StringComparison.Ordinal);
    }

    // ===================== escopo: o formatador e local desta tela =====================

    [Fact]
    public void FormatadorDeProducaoNaoVazouParaOutrasTelas()
    {
        foreach (string tela in new[] { "ProcessoSemiAcabadoForm.cs", "ProcessoConsumoMaterialForm.cs", "PaletizacaoForm.cs" })
        {
            string fonte = LerFonte("Tela", "Processo", tela);
            Assert.DoesNotContain("FormatarQuantidadeProducao", fonte, StringComparison.Ordinal);
        }
    }

    // ===================== infra =====================

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        Assert.False(string.IsNullOrWhiteSpace(dir), "Raiz do projeto nao localizada.");
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"//[^\r\n]*", string.Empty);
    }
}
