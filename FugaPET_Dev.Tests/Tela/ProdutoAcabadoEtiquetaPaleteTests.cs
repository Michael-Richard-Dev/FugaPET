using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Etiqueta do PALETE de Produto Acabado: numeração sequencial, código de barras, totais,
/// quebra por lote e declaração honesta do que não tem origem.
/// </summary>
public sealed class ProdutoAcabadoEtiquetaPaleteTests
{
    private static ProdutoAcabadoCaixa Caixa(int numero, string lote, decimal bruto, int produtos)
        => new()
        {
            CodigoProdutoAcabadoCaixa = numero,
            NumeroCaixa = numero,
            CodigoCaixaLocal = $"CX-1000403-{numero:0000}",
            NumeroOrdemProducao = "1000403",
            ItemOrdemProducao = "0001",
            Material = "4000316",
            Lote = lote,
            PesoBrutoKg = bruto,
            TaraKg = 0.5m,
            PesoLiquidoKg = bruto - 0.5m,
            UnidadePeso = "KG",
            QuantidadeProdutos = produtos,
            UnidadeQuantidade = "UN",
            HandlingUnitExternalId = $"3000007{numero:00}",
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
            Terminal = "TERMINAL_TI_TESTE",
            CriadoEm = new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero)
        };

    private static ProdutoAcabadoPalete Palete(params ProdutoAcabadoCaixa[] caixas)
        => new()
        {
            CodigoPaleteLocal = "1",
            PrimeiraCaixa = caixas.Length == 0 ? 0 : caixas.Min(c => c.NumeroCaixa),
            UltimaCaixa = caixas.Length == 0 ? 0 : caixas.Max(c => c.NumeroCaixa),
            PesoBrutoKg = caixas.Sum(c => c.PesoBrutoKg),
            PesoLiquidoKg = caixas.Sum(c => c.PesoLiquidoKg),
            TaraKg = caixas.Sum(c => c.TaraKg),
            PackagingMaterial = "PALLET01",
            Caixas = caixas
        };

    // ---------------------------------------------------------------- numeração

    [Theory]
    [InlineData(1L, "2000000001")]
    [InlineData(17747L, "2000017747")]
    [InlineData(17748L, "2000017748")]
    [InlineData(99999999L, "2099999999")]
    public void CodigoBarrasEPrefixoMaisSequencialComZeros(long codigo, string esperado)
        => Assert.Equal(esperado, ImpressaoPaleteProdutoAcabadoServico.MontarCodigoBarras(codigo));

    [Fact]
    public void CodigoBarrasRecusaSequencialSemCodigoOuMaiorQueOFormato()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ImpressaoPaleteProdutoAcabadoServico.MontarCodigoBarras(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ImpressaoPaleteProdutoAcabadoServico.MontarCodigoBarras(-5));
        // Truncar geraria um codigo igual ao de outro palete: recusa em vez de truncar.
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoPaleteProdutoAcabadoServico.MontarCodigoBarras(100000000));
    }

    [Fact]
    public void NumeroExibidoEOSequencialSemPrefixo()
    {
        DadosEtiquetaPaleteProdutoAcabado e = ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(
            Palete(Caixa(1, "61198561F", 10m, 20)), 17747, "TERMINAL_TI_TESTE");
        Assert.Equal("17747", e.Numero);
        Assert.Equal("2000017747", e.CodigoBarras);
    }

    // ---------------------------------------------------------------- totais e lotes

    [Fact]
    public void TotaisSomamCaixasEProdutosDoPalete()
    {
        ProdutoAcabadoPalete palete = Palete(
            Caixa(1, "L-A", 10.000m, 20), Caixa(2, "L-A", 10.500m, 20), Caixa(3, "L-B", 9.000m, 18));

        DadosEtiquetaPaleteProdutoAcabado e =
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(palete, 1, "TERMINAL_TI_TESTE");

        Assert.Equal("3", e.QuantidadeCaixas);
        Assert.Equal("58", e.QuantidadeProdutos);
        // Como no modelo impresso: vírgula, sem unidade, zeros à direita suprimidos até duas casas.
        Assert.Equal("29,50", e.PesoBruto);
        Assert.Equal("28,00", e.PesoLiquido);
        Assert.Equal("4000316", e.Material);
    }

    [Theory]
    [InlineData(527.450, "527,45")]
    [InlineData(427.322, "427,322")]
    [InlineData(28.800, "28,80")]
    [InlineData(10.000, "10,00")]
    [InlineData(0.005, "0,005")]
    public void PesoSegueAFormatacaoDoModelo(decimal valor, string esperado)
        => Assert.Equal(esperado, ImpressaoPaleteProdutoAcabadoServico.FormatarPeso(valor));

    [Fact]
    public void LotesSaemAgrupadosComAQuantidadeDeCaixasDeCadaUm()
    {
        ProdutoAcabadoPalete palete = Palete(
            Caixa(1, "022 26", 10m, 20), Caixa(2, "022 26", 10m, 20), Caixa(3, "315 25", 10m, 20));

        DadosEtiquetaPaleteProdutoAcabado e =
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(palete, 1, "TERMINAL_TI_TESTE");

        Assert.Equal(2, e.Lotes.Count);
        Assert.Equal("022 26", e.Lotes[0].Lote);
        Assert.Equal(2, e.Lotes[0].QuantidadeCaixas);
        Assert.Equal("315 25", e.Lotes[1].Lote);
        Assert.Equal(1, e.Lotes[1].QuantidadeCaixas);
        // A soma das caixas por lote tem de fechar com o total do palete.
        Assert.Equal(palete.Caixas.Count, e.Lotes.Sum(l => l.QuantidadeCaixas));
    }

    [Fact]
    public void CaixaSemLoteApareceNomeadaEmVezDeSumir()
    {
        DadosEtiquetaPaleteProdutoAcabado e = ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(
            Palete(Caixa(1, "  ", 10m, 20)), 1, "TERMINAL_TI_TESTE");
        Assert.Equal("SEM LOTE", Assert.Single(e.Lotes).Lote);
    }

    // ---------------------------------------------------------------- ausências declaradas

    [Fact]
    public void DatasDeFabricacaoEVencimentoSaemDeclaradasComoAusentes()
    {
        DadosEtiquetaPaleteProdutoAcabado e = ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(
            Palete(Caixa(1, "61198561F", 10m, 20)), 1, "TERMINAL_TI_TESTE");

        LinhaLoteEtiquetaPalete lote = Assert.Single(e.Lotes);
        Assert.Equal(ImpressaoPaleteProdutoAcabadoServico.NaoInformada, lote.DataFabricacao);
        Assert.Equal(ImpressaoPaleteProdutoAcabadoServico.NaoInformada, lote.DataVencimento);
    }

    [Fact]
    public void DescricaoAusenteCedeOLugarAoMaterialEmVezDeDeixarVazio()
    {
        var zebra = new ServicoImpressoraZebra();
        ProdutoAcabadoPalete palete = Palete(Caixa(1, "61198561F", 10m, 20));

        string semDescricao = zebra.ConstruirZplEtiquetaPaleteProdutoAcabado(
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(palete, 1, "T"));
        Assert.Contains("^FD4000316^FS", semDescricao, StringComparison.Ordinal);

        string comDescricao = zebra.ConstruirZplEtiquetaPaleteProdutoAcabado(
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(palete, 1, "T", "OSSO NO 9 GALINHA"));
        Assert.Contains("OSSO NO 9 GALINHA", comDescricao, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- ZPL

    [Fact]
    public void ZplTemTodosOsCamposDoModeloEUmaLinhaPorLote()
    {
        ProdutoAcabadoPalete palete = Palete(
            Caixa(1, "022 26", 10m, 20), Caixa(2, "026 26", 10m, 20), Caixa(3, "315 25", 10m, 20));
        DadosEtiquetaPaleteProdutoAcabado e =
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(palete, 17747, "TERMINAL_TI_TESTE");

        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaPaleteProdutoAcabado(e);

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.EndsWith("^XZ", zpl, StringComparison.Ordinal);
        // Rotulos e grafia exatamente como no modelo impresso.
        Assert.Contains("Data/Hora de Impressao:", zpl, StringComparison.Ordinal);
        Assert.Contains("^FDFUGA PET^FS", zpl, StringComparison.Ordinal);
        Assert.Contains("Numero: 17747", zpl, StringComparison.Ordinal);
        Assert.Contains("Data: ", zpl, StringComparison.Ordinal);
        Assert.Contains("Qt Cx./Sacos: 3", zpl, StringComparison.Ordinal);
        Assert.Contains("Qt Pct/PC: 60", zpl, StringComparison.Ordinal);
        Assert.Contains("Peso B.: 30,00", zpl, StringComparison.Ordinal);
        Assert.Contains("Peso L.: 28,50", zpl, StringComparison.Ordinal);
        // Code128 do codigo de barras do palete, com leitura humana (Y) dentro da moldura.
        Assert.Contains("^BCN,120,Y,N,N", zpl, StringComparison.Ordinal);
        Assert.Contains("^FD2000017747^FS", zpl, StringComparison.Ordinal);
        // Titulo, numero e totais centralizados/alinhados por bloco (^FB), como no modelo.
        Assert.Contains(",1,0,C^FDFUGA PET^FS", zpl, StringComparison.Ordinal);
        // Uma linha por lote, com as quatro colunas do modelo.
        foreach (string lote in new[] { "022 26", "026 26", "315 25" })
            Assert.Contains($"LOTE: {lote}", zpl, StringComparison.Ordinal);
        Assert.Contains("DT. FABRI.:", zpl, StringComparison.Ordinal);
        Assert.Contains("DT. VCTO.:", zpl, StringComparison.Ordinal);
        Assert.Contains("QTDE. CX.:", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void AlturaDaEtiquetaCresceComAQuantidadeDeLotes()
    {
        var zebra = new ServicoImpressoraZebra();
        static ProdutoAcabadoCaixa C(int n) => Caixa(n, $"LOTE-{n:00}", 10m, 20);

        string poucos = zebra.ConstruirZplEtiquetaPaleteProdutoAcabado(
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(Palete(C(1)), 1, "T"));
        string muitos = zebra.ConstruirZplEtiquetaPaleteProdutoAcabado(
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(
                Palete([.. Enumerable.Range(1, 14).Select(C)]), 1, "T"));

        static int Altura(string zpl)
        {
            int i = zpl.IndexOf("^LL", StringComparison.Ordinal) + 3;
            int fim = zpl.IndexOf('\n', i);
            return int.Parse(zpl[i..fim]);
        }

        // Com muitos lotes a etiqueta precisa ser mais alta, senao as ultimas linhas sao cortadas.
        Assert.True(Altura(muitos) > Altura(poucos),
            $"altura com 14 lotes ({Altura(muitos)}) deveria superar a de 1 lote ({Altura(poucos)})");
        Assert.Contains("LOTE: LOTE-14", muitos, StringComparison.Ordinal);
    }

    [Fact]
    public void PaleteSemCaixasNaoFingeConteudo()
    {
        DadosEtiquetaPaleteProdutoAcabado e =
            ImpressaoPaleteProdutoAcabadoServico.CriarEtiqueta(Palete(), 1, "T");
        Assert.Equal("0", e.QuantidadeCaixas);
        Assert.Equal("0", e.QuantidadeProdutos);
        Assert.Empty(e.Lotes);
        Assert.Equal(ImpressaoPaleteProdutoAcabadoServico.NaoInformada, e.Data);

        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaPaleteProdutoAcabado(e);
        Assert.Contains("NENHUM LOTE IDENTIFICADO", zpl, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- isolamento

    [Fact]
    public void ImpressaoDoPaleteNaoTocaSap()
    {
        string servico = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "Operacao",
            "ImpressaoPaleteProdutoAcabadoServico.cs"));
        foreach (string proibido in new[] { "Int012", "Gateway", "EnviarPalete", "POST", "HttpClient" })
            Assert.DoesNotContain(proibido, servico, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaImprimeNaGravacaoEIsolaAFalha()
    {
        string form = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Processo", "PaletizacaoForm.cs"));
        // A impressao vem DEPOIS da persistencia, que e quem define o sequencial.
        int persistencia = form.IndexOf("persistencia.Sucesso", StringComparison.Ordinal);
        int impressao = form.IndexOf("ImprimirEtiquetaPaleteAsync(palete", StringComparison.Ordinal);
        Assert.True(persistencia >= 0 && impressao > persistencia);
        Assert.Contains("catch (Exception ex)", form, StringComparison.Ordinal);
        Assert.Contains("palete sem código persistido", form, StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
