using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// UX de HU (botões / destaque), atualização do SALDO PENDENTE após confirmação da caixa e orientação
/// vertical dos códigos de barras da etiqueta 8x5. Testes de comportamento puro (regra de saldo) + fonte
/// (layout/estado do botão e RDLC), sem instanciar a Form nem tocar banco/SAP/Zebra física.
/// </summary>
public sealed class ProdutoAcabadoUxHuSaldoTests
{
    private static string RaizProjeto()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return dir;
    }

    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static ProdutoAcabadoCaixa Caixa(StatusIntegracaoCaixa status, decimal liquido = 60m) => new()
    {
        CodigoProdutoAcabadoCaixa = 1,
        Centro = "3007",
        PesoLiquidoKg = liquido,
        StatusIntegracao = status
    };

    // ===================== REGRA 4: saldo pendente =====================

    // 9. saldo inicial 300, confirmação de caixa 60 -> 240.
    [Fact]
    public void Saldo_UmaCaixaConfirmada_Abate()
        => Assert.Equal(240m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(
            300m, [Caixa(StatusIntegracaoCaixa.ConfirmadaSap)]));

    // 10. duas caixas confirmadas de 60 -> 180.
    [Fact]
    public void Saldo_DuasCaixasConfirmadas_Abate()
        => Assert.Equal(180m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(
            300m, [Caixa(StatusIntegracaoCaixa.ConfirmadaSap), Caixa(StatusIntegracaoCaixa.ConfirmadaSap)]));

    // 11-14: estados não confirmados NÃO reduzem o saldo.
    [Theory]
    [InlineData(StatusIntegracaoCaixa.Cancelada)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal)]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.EmPesagem)]
    public void Saldo_EstadoNaoConfirmado_NaoAbate(StatusIntegracaoCaixa status)
        => Assert.Equal(300m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(300m, [Caixa(status)]));

    // Mistura: só a CONFIRMADA_SAP abate (60), cancelada/aguardando não participam.
    [Fact]
    public void Saldo_Mistura_SoConfirmadaAbate()
    {
        ProdutoAcabadoCaixa[] caixas =
        [
            Caixa(StatusIntegracaoCaixa.ConfirmadaSap),
            Caixa(StatusIntegracaoCaixa.Cancelada),
            Caixa(StatusIntegracaoCaixa.AguardandoAutorizacaoSap),
            Caixa(StatusIntegracaoCaixa.IndeterminadoTimeout),
        ];
        Assert.Equal(240m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(300m, caixas));
    }

    // Nunca fica negativo.
    [Fact]
    public void Saldo_NuncaNegativo()
        => Assert.Equal(0m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(
            50m, [Caixa(StatusIntegracaoCaixa.ConfirmadaSap)]));

    // 15: a Form recalcula o saldo pela regra única após o envio (finally) e no carregamento — sem recarregar OP.
    [Fact]
    public void Form_AtualizaSaldoAposEnvio_ReusandoRegraUnica()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        // Regra única (não duplica aritmética na View): delega ao cálculo central.
        Assert.Contains("CalculoSaldoProdutoAcabado.SaldoPendenteExibido(_ordemAtual.QuantidadePendente, _caixasPesadas)", form, StringComparison.Ordinal);
        // Usada no carregamento da OP e chamada de refresh após o resultado definitivo do envio.
        Assert.Contains("classificationDateTextBox.Text = FormatarKg(CalcularSaldoPendenteExibido());", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarSaldoPendente();", form, StringComparison.Ordinal);
        // A Form não acessa banco/OData diretamente para isso.
        Assert.DoesNotContain("NpgsqlConnection", form, StringComparison.Ordinal);
    }

    // ===================== REGRA 3: destaque visual do botão =====================

    [Fact]
    public void Form_BotaoEnvio_ApareceDestacadoQuandoHabilitado()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private void AtualizarEstadoEnvioCaixaSap()", StringComparison.Ordinal);
        int fim = form.IndexOf("private decimal CalcularSaldoPendenteExibido()", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = form[inicio..fim];

        // A cor REFLETE (não define) a elegibilidade: verde quando habilitado, cinza quando não.
        Assert.Contains("_enviarCaixaSapButton.BackColor = habilitado", metodo, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(34, 166, 82)", metodo, StringComparison.Ordinal);   // verde positivo
        Assert.Contains("Color.FromArgb(156, 163, 175)", metodo, StringComparison.Ordinal); // cinza neutro
        // Enabled continua derivado SÓ das regras funcionais (gate HU + elegibilidade + centro).
        Assert.Contains("_enviarCaixaSapButton.Enabled = habilitado;", metodo, StringComparison.Ordinal);
        Assert.Contains("_controller.EnvioHuAutorizado && elegivelEnvio", metodo, StringComparison.Ordinal);
        Assert.Contains("RegraCentroPetProdutoAcabado.CentroPermitido(caixa.Centro)", metodo, StringComparison.Ordinal);
    }

    // ===================== REGRA 1: barcodes verticais =====================

    // 16/17/18: ZPL usa barcode VERTICAL (^BCB) e NÃO permanece horizontal (^BCN).
    [Fact]
    public void Zpl_Barcode_Vertical_SemRegressaoHorizontal()
    {
        string zebra = LerFonte("Servicos", "ServicoImpressoraZebra.cs");
        int inicio = zebra.IndexOf("public string ConstruirZplEtiquetaCaixaProdutoAcabado(", StringComparison.Ordinal);
        int fim = zebra.IndexOf("public string ConstruirZplEtiquetaMateriaPrimaGrafica(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = zebra[inicio..fim];
        Assert.Contains("^BCB", metodo, StringComparison.Ordinal);      // vertical (orientação B)
        Assert.DoesNotContain("^BCN", metodo, StringComparison.Ordinal); // sem regressão horizontal
    }

    // RDLC de referência: AMBOS os barcodes (superior e inferior) verticais (WritingMode Rotate270).
    [Fact]
    public void Rdlc8x5_AmbosBarcodes_Verticais()
    {
        string rdlc = LerFonte("Relatorio", "EtiquetaProduto8x5.rdlc");
        System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(rdlc); // valida XML (abrível)
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";
        foreach (string nome in new[] { "BarcodeTop", "BarcodeBottom" })
        {
            System.Xml.Linq.XElement? tb = doc.Descendants(ns + "Textbox")
                .FirstOrDefault(e => (string?)e.Attribute("Name") == nome);
            Assert.NotNull(tb);
            Assert.Equal("Rotate270", tb!.Descendants(ns + "WritingMode").FirstOrDefault()?.Value);
        }
    }

    // 19/20: não introduz DUN/GTIN hardcoded nem sequência artificial no ZPL produtivo.
    [Fact]
    public void Zpl_NaoIntroduzDunGtinNemSequenciaArtificial()
    {
        string zebra = LerFonte("Servicos", "ServicoImpressoraZebra.cs");
        int inicio = zebra.IndexOf("public string ConstruirZplEtiquetaCaixaProdutoAcabado(", StringComparison.Ordinal);
        int fim = zebra.IndexOf("public string ConstruirZplEtiquetaMateriaPrimaGrafica(", StringComparison.Ordinal);
        string metodo = zebra[inicio..fim];
        // O barcode do ZPL vem do contrato (codigo_caixa_local), não de um GTIN/DUN literal.
        Assert.Contains("etiqueta.CodigoCaixaLocal", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("7891000117446", metodo, StringComparison.Ordinal); // sem GTIN hardcoded
        Assert.DoesNotContain("nextval", metodo, StringComparison.OrdinalIgnoreCase); // sem sequence artificial
    }
}
