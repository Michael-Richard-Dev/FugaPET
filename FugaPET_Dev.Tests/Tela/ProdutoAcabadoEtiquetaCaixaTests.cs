using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Impressão/reimpressão da etiqueta da CAIXA de Produto Acabado. Prova o contrato da etiqueta (dados
/// corretos a partir da caixa PERSISTIDA), o ZPL específico da caixa (reaproveitando o driver Zebra), o
/// bloqueio de reimpressão sem identidade persistida, a diferenciação impressão×reimpressão para auditoria,
/// e que a impressão/reimpressão são efeitos LOCAIS que nunca tocam o SAP (claim/POST/HU). Testes de
/// comportamento puro + fonte (não instanciam a Form, não imprimem em hardware, não tocam banco/SAP).
/// </summary>
public sealed class ProdutoAcabadoEtiquetaCaixaTests
{
    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static ProdutoAcabadoCaixa CaixaConfirmada() => new()
    {
        CodigoProdutoAcabadoCaixa = 4,
        NumeroCaixa = 4,
        CodigoCaixaLocal = "CX-1001951-0004",
        NumeroOrdemProducao = "1001951",
        ItemOrdemProducao = "0001",
        Material = "4000108",
        Lote = "L-TESTE",
        PesoBrutoKg = 10.5m,
        TaraKg = 0.5m,
        PesoLiquidoKg = 10.0m,
        UnidadePeso = "KG",
        QuantidadeProdutos = 60,
        UnidadeQuantidade = "UN",
        HandlingUnitExternalId = "300014352",
        StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
        Terminal = "TERM-01"
    };

    /// <summary>Caixa recém-pesada: persistida, sem HU e sem confirmação SAP.</summary>
    private static ProdutoAcabadoCaixa CaixaPesada()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        caixa.StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal;
        return caixa;
    }

    /// <summary>
    /// A REGRA INVERTEU em 19/11/2026: antes a caixa recem-pesada era elegivel e a etiqueta saia com a
    /// HU "PENDENTE". Agora NENHUM estado anterior a confirmacao imprime, porque a HU — que identifica a
    /// caixa fisica — so e gerada pelo SAP depois do envio.
    /// </summary>
    [Fact]
    public void AntesDaConfirmacaoSap_NenhumEstadoImprime()
    {
        foreach (StatusIntegracaoCaixa estado in new[]
        {
            StatusIntegracaoCaixa.EmPesagem,
            StatusIntegracaoCaixa.FinalizadaLocal,
            StatusIntegracaoCaixa.PreviewHuGerado,
            StatusIntegracaoCaixa.AguardandoAutorizacaoSap,
            StatusIntegracaoCaixa.ProntaParaEnvio,
            StatusIntegracaoCaixa.EnviandoSap,
            StatusIntegracaoCaixa.ErroSap,
            StatusIntegracaoCaixa.IndeterminadoTimeout
        })
        {
            ProdutoAcabadoCaixa caixa = CaixaPesada();
            caixa.StatusIntegracao = estado;

            ErroOperacionalEsperadoException erro = Assert.Throws<ErroOperacionalEsperadoException>(
                () => ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(caixa));
            Assert.Contains("CONFIRMADA_SAP", erro.Message, StringComparison.Ordinal);
        }
    }

    // Sem lastro no banco, não há etiqueta: caixa física sem rastreabilidade continua barrada.
    [Fact]
    public void SemIdentidadePersistidaNaoImprime()
    {
        ProdutoAcabadoCaixa semId = CaixaConfirmada();
        semId.CodigoProdutoAcabadoCaixa = null;
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(semId));

        ProdutoAcabadoCaixa semCodigo = CaixaConfirmada();
        semCodigo.CodigoCaixaLocal = "  ";
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(semCodigo));
    }

    /// <summary>
    /// A ETIQUETA CARREGA A HU REAL. Era este o motivo de mover a impressao para depois do envio: antes
    /// o campo saia vazio e a etiqueta precisaria ser trocada quando a HU aparecesse.
    /// </summary>
    [Fact]
    public void EtiquetaDaCaixaConfirmada_LevaAHuReal()
    {
        DadosEtiquetaCaixaProdutoAcabado e =
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada(), "SACO PET 25KG");

        Assert.False(string.IsNullOrWhiteSpace(e.HandlingUnitSap));

        // O restante da identidade da caixa continua completo na etiqueta.
        Assert.Equal("1001951", e.OrdemProducao);
        Assert.Equal("CX-1001951-0004", e.CodigoCaixaLocal);
        Assert.Equal("0004", e.NumeroCaixa);
    }

    // A reimpressão continua exigindo SAP: é ela que emite a etiqueta definitiva, com HU.
    [Fact]
    public void Reimpressao_ContinuaExigindoConfirmacaoSap()
    {
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(CaixaPesada()));
        ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(CaixaConfirmada());
    }

    // A etiqueta é montada a partir da caixa PERSISTIDA (dados corretos, rastreabilidade pela caixa).
    [Fact]
    public void CriarEtiqueta_MapeiaDadosDaCaixaPersistida()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada(), "SACO PET 25KG");
        Assert.Equal("1001951", e.OrdemProducao);
        Assert.Equal("0001", e.ItemOrdem);
        Assert.Equal("4000108", e.Material);
        Assert.Equal("SACO PET 25KG", e.DescricaoMaterial);
        Assert.Equal("L-TESTE", e.Lote);
        Assert.Equal("0004", e.NumeroCaixa);
        Assert.Equal("CX-1001951-0004", e.CodigoCaixaLocal);
        Assert.Equal("10.5 KG", e.PesoBruto);
        Assert.Equal("0.5 KG", e.Tara);
        Assert.Equal("10 KG", e.PesoLiquido);
        Assert.Equal("60 UN", e.Quantidade);
        Assert.Equal("300014352", e.HandlingUnitSap);
        Assert.Equal("TERM-01", e.Terminal);
    }

    // HU ausente (caixa ainda não confirmada) ⇒ campo HU vazio no contrato (a etiqueta mostra "PENDENTE").
    [Fact]
    public void CriarEtiqueta_SemHu_DeixaHuVazia()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa);
        Assert.Equal(string.Empty, e.HandlingUnitSap);
    }

    // O ZPL da caixa segue o layout 8x5 (Relatorio/EtiquetaProduto8x5.rdlc), com os rotulos do modelo.
    [Fact]
    public void Zpl_CaixaProdutoAcabado_SegueOLayout8x5()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada(), "SACO PET 25KG");
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.EndsWith("^XZ", zpl.TrimEnd(), StringComparison.Ordinal);
        // Rotulos na grafia do modelo impresso.
        foreach (string rotulo in new[]
        {
            "CODIGO DO PRODUTO", "PESO BRUTO", "DATA DE FABRICACAO",
            "PESO LIQUIDO", "DATA DE VALIDADE", "Qtd. Pacotes:", "Lote:"
        })
            Assert.Contains(rotulo, zpl, StringComparison.Ordinal);

        Assert.Contains("Lote: L-TESTE", zpl, StringComparison.Ordinal);
        Assert.Contains("SACO PET 25KG", zpl, StringComparison.Ordinal);
        // GATE 130B: o Code128 e VERTICAL (orientacao B). Desde 19/11/2026 ele carrega a HU do SAP,
        // e o codigo local vive no rodape, ao lado dela — nunca mais sozinho.
        Assert.Contains("^BCB,", zpl, StringComparison.Ordinal);
        Assert.Contains("CX-1001951-0004", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^BCN", zpl, StringComparison.Ordinal);
        // A HU deixou de ser texto solto e virou celula da grade, ao lado da validade.
        Assert.Contains("HU SAP", zpl, StringComparison.Ordinal);
        // NAO usa os rotulos do layout de producao do Semi-Acabado.
        Assert.DoesNotContain("SAIDA ESTUFA", zpl, StringComparison.Ordinal);
    }

    // A etiqueta tem o tamanho do modelo: 8x5 cm a 203 dpi.
    [Fact]
    public void Zpl_CaixaProdutoAcabado_TemOTamanhoDoModelo8x5()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada());
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);
        Assert.Contains("^PW639", zpl, StringComparison.Ordinal);   // 3,15 pol
        Assert.Contains("^LL400", zpl, StringComparison.Ordinal);   // 1,97 pol
    }

    /// <summary>
    /// O codigo de barras precisa caber DENTRO da etiqueta. Em ^BCB o simbolo DESCE a partir da
    /// origem — medido na calibracao impressa em 18/11/2026, contra o que o codigo original dizia —
    /// e as barras correm para a direita. Era essa inversao que jogava a barra na etiqueta seguinte.
    /// </summary>
    [Theory]
    [InlineData("CX-1001951-0004")]
    [InlineData("CX-1000403-0001")]
    [InlineData("CX-1000403-9999")]
    public void Zpl_CodigoDeBarrasCabeDentroDaEtiqueta(string codigoLocal)
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);

        Assert.True(barra.Y + barra.Comprimento <= barra.Altura,
            $"o simbolo desce {barra.Comprimento} dots a partir de y={barra.Y} e passa do rodape "
            + $"({barra.Y + barra.Comprimento} numa etiqueta de {barra.Altura})");
        Assert.True(barra.X + barra.AlturaBarras <= barra.Largura,
            $"as barras vao ate x={barra.X + barra.AlturaBarras} numa etiqueta de {barra.Largura} dots");
    }

    /// <summary>
    /// A barra nasce COLADA NO TOPO, como o operador pediu em 18/11/2026, e nao no rodape.
    /// <para>
    /// Este teste falha contra a versao anterior, em que a origem era <c>altura - P(0,03)</c>: como o
    /// simbolo desce, aquela origem jogava a barra inteira para fora, na etiqueta seguinte do rolo.
    /// </para>
    /// </summary>
    [Fact]
    public void Zpl_CodigoDeBarras_NasceNoTopoDaEtiqueta()
    {
        string codigoLocal = "CX-1001951-0004";
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);

        // 19/11/2026, com a etiqueta impressa na mao: colada na borda ficava com cara de corte. O
        // usuario pediu 1 cm de folga, que a 203 dpi sao 80 pontos.
        const int UmCentimetro = 80;
        Assert.Equal(UmCentimetro, barra.Y);

        // E a folga de cima nao pode ter custado o modulo: a barra continua no piso legivel.
        Assert.True(barra.Modulo >= ServicoImpressoraZebra.ModuloBarrasCaixa,
            $"a margem de 1 cm estreitou a barra para o modulo {barra.Modulo}");

        // Termina dentro da etiqueta, com a folga de baixo respeitada.
        Assert.True(barra.Y + barra.Comprimento <= barra.Altura - ServicoImpressoraZebra.RodapeFaixaBarra,
            $"o simbolo termina em {barra.Y + barra.Comprimento} numa etiqueta de {barra.Altura}");
    }

    /// <summary>
    /// A barra carrega o codigo SEM o prefixo <c>CX-</c>, e a leitura humana mostra o codigo INTEIRO.
    /// Decisao do usuario em 18/11/2026: e o que faz o simbolo caber na vertical.
    /// </summary>
    [Fact]
    public void Zpl_BarraCarregaAHuDoSap_ENaoOCodigoLocal()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = "CX-1001951-0004";
        caixa.HandlingUnitExternalId = "300015994";
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        int campoBarra = zpl.IndexOf("^BCB", StringComparison.Ordinal);
        string linhaBarra = zpl[campoBarra..zpl.IndexOf('\n', campoBarra)];

        // Bipar devolve a HU — era o codigo local que saia, e ele nao identifica a caixa no SAP.
        Assert.Contains("^FD300015994^FS", linhaBarra, StringComparison.Ordinal);
        Assert.DoesNotContain("1001951-0004", linhaBarra, StringComparison.Ordinal);

        // No rodape, a HU primeiro (convencao: o numero sob a barra e o conteudo dela) e o codigo
        // local logo depois, porque e como a caixa aparece no nosso sistema.
        Assert.Contains("^FDHU 300015994  -  CX-1001951-0004^FS", zpl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sem HU nao se imprime barra nenhuma: codificar "PENDENTE" devolveria ao leitor um texto que
    /// nao identifica caixa alguma. Na pratica a elegibilidade ja exige HU — esta e a defesa de quem
    /// montar o ZPL por outro caminho.
    /// </summary>
    [Fact]
    public void Zpl_SemHu_NaoImprimeBarra()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        // Nenhum campo de barras no ZPL — e, portanto, nada codificado.
        Assert.DoesNotContain("^BCB", zpl, StringComparison.Ordinal);

        // O campo de texto "HU SAP" da grade continua mostrando PENDENTE, que é informação honesta
        // para quem lê; o que não pode existir é uma BARRA devolvendo essa palavra ao leitor.
        Assert.Contains("PENDENTE", zpl, StringComparison.Ordinal);

        // O codigo local continua legivel no rodape, para a caixa nao ficar sem identificacao.
        Assert.Contains("^FDCX-", zpl, StringComparison.Ordinal);
    }

    /// <summary>
    /// O operador pediu, em 18/11/2026, barra VERTICAL medindo entre 4 e 4,5 cm — o que cabe nos 5 cm
    /// de altura da etiqueta deixando margem. Este teste guarda essa faixa em pontos a 203 dpi.
    /// </summary>
    [Theory]
    [InlineData("CX-1001951-0004")]
    [InlineData("CX-1000403-0001")]
    [InlineData("CX-1000403-9999")]
    public void Zpl_CodigoDeBarras_CabeNaAlturaDaEtiqueta(string codigoLocal)
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        caixa.HandlingUnitExternalId = "300015994";   // HU real do SAP: 9 digitos
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);
        int cursoDisponivel = barra.Altura - ServicoImpressoraZebra.TopoFaixaBarra - ServicoImpressoraZebra.RodapeFaixaBarra;

        // 9 digitos em subconjunto B sao 134 modulos. A modulo 2 isso da 268 dots (3,4 cm); a modulo 3
        // daria 402 (5,0 cm), que NAO CABE na etiqueta de 400 dots. Nao existe valor entre os dois,
        // porque ^BY so aceita modulo inteiro — logo, 3,4 cm e o maximo possivel para uma HU de 9
        // caracteres. Os 4,2 cm que o usuario tinha antes vinham de um dado de 12 caracteres.
        Assert.Equal(268, barra.Comprimento);
        Assert.True(barra.Comprimento <= cursoDisponivel);
        Assert.Equal(ServicoImpressoraZebra.ModuloBarrasCaixa, barra.Modulo);
    }

    /// <summary>
    /// A ESTIMATIVA NAO PODE SUPOR COMPRESSAO DE DIGITOS. Esta impressora nao comprime — medido com
    /// regua na calibracao de 18/11/2026 — e a suposicao, que dormia no codigo porque o dado de entao
    /// tinha hifen, previu metade do simbolo quando a barra passou a carregar a HU, so digitos. A
    /// barra saiu com 6,6 cm numa etiqueta de 5,0 cm.
    /// </summary>
    [Theory]
    [InlineData("300015994", 134)]        // 9 digitos: 11*(1+9+1)+13
    [InlineData("12345678901234", 189)]   // 14 digitos: nenhuma compressao
    [InlineData("CX-1001951-0004", 200)]  // 15 alfanumericos, o caso ja medido em 2026-11-18
    public void Estimativa_NaoSupoeCompressaoDeDigitos(string dados, int modulosEsperados)
    {
        Assert.Equal(modulosEsperados, ServicoImpressoraZebra.EstimarLarguraCode128Dots(dados, 1));
    }

    /// <summary>
    /// A barra usa o MAIOR modulo que cabe: subir um ponto estouraria o curso da faixa. E o que
    /// mantem as barras o mais largas possivel para o leitor de mao, qualquer que seja o tamanho da
    /// HU — que quem define e o SAP, nao nos.
    /// </summary>
    [Theory]
    [InlineData("300015994")]           // HU real, 9 digitos
    [InlineData("HU00BA55C4")]          // HU simulada, 10 caracteres alfanumericos
    [InlineData("3000159941234567")]    // hipotetica, bem mais longa
    public void Zpl_CodigoDeBarras_UsaOMaiorModuloQueCabe(string hu)
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = hu;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);
        int cursoDisponivel = barra.Altura - ServicoImpressoraZebra.TopoFaixaBarra - ServicoImpressoraZebra.RodapeFaixaBarra;

        Assert.True(barra.Comprimento <= cursoDisponivel,
            $"o simbolo mede {barra.Comprimento} dots e a faixa tem {cursoDisponivel}");

        // Subir um ponto de modulo nao caberia — logo, este e o maior que cabe.
        if (barra.Modulo < ServicoImpressoraZebra.ModuloBarrasCaixaMaximo)
        {
            int comprimentoMaior = ServicoImpressoraZebra.EstimarLarguraCode128Dots(hu, barra.Modulo + 1);
            Assert.True(comprimentoMaior > cursoDisponivel,
                $"caberia modulo {barra.Modulo + 1} ({comprimentoMaior} dots) e foi usado {barra.Modulo}");
        }

        // E nunca abaixo do piso legivel, salvo quando nem ele cabe.
        Assert.True(barra.Modulo >= ServicoImpressoraZebra.ModuloBarrasCaixa
            || ServicoImpressoraZebra.EstimarLarguraCode128Dots(hu, ServicoImpressoraZebra.ModuloBarrasCaixa) > cursoDisponivel);
    }

    /// <summary>
    /// GATE 130A, apontado pelo operador em 18/11/2026: o leitor nao lia a barra da caixa.
    /// <para>
    /// A causa era o modulo de 1 ponto — 0,125 mm a 203 dpi, metade do minimo que um leitor de
    /// Code128 resolve. Este teste fixa o piso de 2 pontos e a altura de barra, que sao as duas
    /// grandezas de que a leitura depende. Falha contra a versao anterior.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("CX-1001951-0004")]
    [InlineData("CX-1000403-0001")]
    [InlineData("CX-1-1")]
    public void Zpl_CodigoDeBarras_TemModuloLegivelPeloLeitor(string codigoLocal)
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);

        Assert.True(barra.Modulo >= 2, $"modulo de {barra.Modulo} ponto: fino demais para o leitor");
        Assert.True(barra.AlturaBarras >= 40, $"barras de {barra.AlturaBarras} dots: baixas demais para mirar");
    }

    /// <summary>
    /// A faixa da barra e EXCLUSIVA dela: nenhum campo de texto da grade pode comecar dentro dela.
    /// Foi o que estourou na primeira versao vertical, quando a barra invadia a coluna dos pesos.
    /// </summary>
    [Fact]
    public void Zpl_FaixaDaBarra_NaoEInvadidaPorTexto()
    {
        string codigoLocal = "CX-1001951-0004";
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        BarraCaixa barra = LerBarra(zpl);

        foreach (string linha in zpl.Split('\n'))
        {
            // So campos de texto e molduras; o proprio campo de barras e pulado.
            if (!linha.StartsWith("^FO", StringComparison.Ordinal) || linha.Contains("^BC", StringComparison.Ordinal))
            {
                continue;
            }

            int x = int.Parse(Entre(linha, "^FO", ",").Trim(','));
            int fim = linha.Contains("^GB", StringComparison.Ordinal)
                ? x + int.Parse(Entre(linha, "^GB", ",").Trim(','))
                : linha.Contains("^FB", StringComparison.Ordinal)
                    ? x + int.Parse(Entre(linha, "^FB", ",").Trim(','))
                    : x;

            Assert.True(fim <= barra.X,
                $"campo termina em x={fim} e entra na faixa da barra, que comeca em {barra.X}: {linha}");
        }
    }

    private readonly record struct BarraCaixa(
        int X, int Y, int Modulo, int AlturaBarras, int Comprimento, int Largura, int Altura);

    /// <summary>
    /// Le a geometria do campo de barras do ZPL: ^FO x,y ^BY m ^BCB,alturaBarras ^FD dado.
    /// <para>
    /// O dado medido e o que esta CODIFICADO no ZPL, lido do proprio ^FD. Ate 19/11/2026 este helper
    /// recebia o codigo local e reaplicava a regra de encurtamento — media uma copia da regra, nao o
    /// que a impressora receberia. Lendo do ZPL, ele mede o que sai.
    /// </para>
    /// </summary>
    private static BarraCaixa LerBarra(string zpl)
    {
        int bcb = zpl.IndexOf("^BCB", StringComparison.Ordinal);
        Assert.True(bcb >= 0, "o ZPL nao tem campo de codigo de barras");
        int fo = zpl.LastIndexOf("^FO", bcb, StringComparison.Ordinal);
        int fd = zpl.IndexOf("^FD", fo, StringComparison.Ordinal);
        string cabecalho = zpl[fo..fd];
        string[] origem = Entre(cabecalho, "^FO", "^BY").Split(',');
        int modulo = int.Parse(Entre(cabecalho, "^BY", "^BC"));

        string dadoCodificado = zpl[(fd + 3)..zpl.IndexOf("^FS", fd, StringComparison.Ordinal)];

        // GATE 130C: a calibracao de 18/11/2026 provou que o modo automatico NAO comprime nesta
        // impressora, entao o subconjunto B puro e a conta certa.
        int comprimento = ServicoImpressoraZebra.EstimarLarguraCode128Dots(dadoCodificado, modulo);

        return new BarraCaixa(
            X: int.Parse(origem[0]),
            Y: int.Parse(origem[1]),
            Modulo: modulo,
            AlturaBarras: int.Parse(Entre(cabecalho, "^BCB,", ",").Trim(',')),
            Comprimento: comprimento,
            Largura: int.Parse(Entre(zpl, "^PW", "\n")),
            Altura: int.Parse(Entre(zpl, "^LL", "\n")));
    }

    private static string Entre(string texto, string inicio, string fim)
    {
        int i = texto.IndexOf(inicio, StringComparison.Ordinal) + inicio.Length;
        int f = texto.IndexOf(fim, i, StringComparison.Ordinal);
        return f < 0 ? texto[i..] : texto[i..f];
    }

    // O EAN do produto nao existe no sistema: nenhuma barra e emitida no lugar dele.
    [Fact]
    public void Zpl_SemEanCadastrado_NaoInventaCodigoDeBarras()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada());
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);

        Assert.Contains("EAN DO PRODUTO", zpl, StringComparison.Ordinal);
        Assert.Contains("NAO CADASTRADO", zpl, StringComparison.Ordinal);
        // Uma unica barra na etiqueta: a do codigo local da caixa.
        Assert.Equal(1, zpl.Split("^BCB,").Length - 1);
    }

    // Fabricacao e validade nao tem origem no Produto Acabado: saem declaradas.
    [Fact]
    public void Zpl_DatasSemOrigemSaemDeclaradas()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada());
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);
        Assert.Equal(2, zpl.Split("NAO INFORMADA").Length - 1);
    }

    [Fact]
    public void Zpl_CaixaSemHu_MostraPendente()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa);
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);
        // GATE 130A: a HU virou celula da grade — rotulo "HU SAP" em cima, valor embaixo.
        Assert.Contains("HU SAP", zpl, StringComparison.Ordinal);
        Assert.Contains("PENDENTE", zpl, StringComparison.Ordinal);
    }

    // Rastreabilidade: reimpressão de caixa SEM identidade persistida é bloqueada (nunca imprime "no escuro").
    [Fact]
    public async Task Reimpressao_SemIdentidadePersistida_Bloqueada()
    {
        ImpressaoProdutoAcabadoServico servico = new();
        ProdutoAcabadoCaixa semCodigo = CaixaConfirmada();
        semCodigo.CodigoCaixaLocal = string.Empty;
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ReimprimirCaixaAsync(semCodigo));

        ProdutoAcabadoCaixa semId = CaixaConfirmada();
        semId.CodigoProdutoAcabadoCaixa = null;
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ReimprimirCaixaAsync(semId));
    }

    // ===================== elegibilidade central: SOMENTE CONFIRMADA_SAP + HU =====================

    // CONFIRMADA_SAP + HU válida ⇒ impressão/reimpressão PERMITIDA (validador não lança).
    [Fact]
    public void Elegibilidade_ConfirmadaComHu_Permite()
    {
        Exception? erro = Record.Exception(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(CaixaConfirmada()));
        Assert.Null(erro);
    }

    // Todos os estados != CONFIRMADA_SAP ⇒ impressão/reimpressão BLOQUEADA no SERVICE (antes do driver).
    [Theory]
    [InlineData(StatusIntegracaoCaixa.Cancelada)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio)]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)]
    [InlineData(StatusIntegracaoCaixa.PreviewHuGerado)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal)]
    [InlineData(StatusIntegracaoCaixa.EmPesagem)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    public async Task Elegibilidade_EstadoNaoConfirmado_Bloqueia(StatusIntegracaoCaixa status)
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.StatusIntegracao = status;
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(caixa));

        // Também via reimpressão (bloqueia ANTES do driver Zebra).
        ImpressaoProdutoAcabadoServico servico = new();
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ReimprimirCaixaAsync(caixa));
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ImprimirCaixaAsync(caixa));
    }

    // CONFIRMADA_SAP porém SEM HU ⇒ impressão/reimpressão BLOQUEADA (nunca gera etiqueta operacional "PENDENTE").
    [Fact]
    public async Task Elegibilidade_ConfirmadaSemHu_Bloqueia()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaElegivelParaEtiqueta(caixa));

        ImpressaoProdutoAcabadoServico servico = new();
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ReimprimirCaixaAsync(caixa));
        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(() => servico.ImprimirCaixaAsync(caixa));
    }

    // ===================== auditoria estruturada (dados_contexto) =====================

    [Fact]
    public void Auditoria_ContextoImpressao_ContemDadosDaCaixa()
    {
        string json = ImpressaoProdutoAcabadoServico.MontarContextoAuditoriaJson(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoImpressao);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement raiz = doc.RootElement;
        Assert.Equal(4, raiz.GetProperty("codigo_hu_caixa").GetInt64());
        Assert.Equal("CX-1001951-0004", raiz.GetProperty("codigo_caixa_local").GetString());
        Assert.Equal(4, raiz.GetProperty("numero_caixa").GetInt32());
        Assert.Equal("IMPRESSAO", raiz.GetProperty("tipo_operacao").GetString());
        Assert.Equal("TERM-01", raiz.GetProperty("terminal").GetString());
        Assert.Equal("300014352", raiz.GetProperty("hu_sap").GetString());
    }

    [Fact]
    public void Auditoria_ContextoReimpressao_ContemDadosDaCaixa()
    {
        string json = ImpressaoProdutoAcabadoServico.MontarContextoAuditoriaJson(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoReimpressao);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("REIMPRESSAO", doc.RootElement.GetProperty("tipo_operacao").GetString());
        Assert.Equal("CX-1001951-0004", doc.RootElement.GetProperty("codigo_caixa_local").GetString());
    }

    [Fact]
    public void Auditoria_Contexto_DiferenciaImpressaoDeReimpressao()
    {
        string impressao = ImpressaoProdutoAcabadoServico.MontarContextoAuditoriaJson(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoImpressao);
        string reimpressao = ImpressaoProdutoAcabadoServico.MontarContextoAuditoriaJson(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoReimpressao);
        Assert.NotEqual(impressao, reimpressao);
        Assert.Contains("\"tipo_operacao\":\"IMPRESSAO\"", impressao, StringComparison.Ordinal);
        Assert.Contains("\"tipo_operacao\":\"REIMPRESSAO\"", reimpressao, StringComparison.Ordinal);
        // Nunca vaza segredo no contexto.
        foreach (string proibido in new[] { "senha", "password", "token", "cookie", "csrf", "authorization" })
        {
            Assert.DoesNotContain(proibido, impressao, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===================== auditoria de SUCESSO e FALHA por caixa =====================

    private static ImpressaoProdutoAcabadoServico ServicoComAuditoriaFake(
        List<(ProdutoAcabadoCaixa Caixa, string Tipo, string Resultado)> registros)
        => new(new ImpressoraEtiquetaServico(), (c, t, r) =>
        {
            registros.Add((c, t, r));
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Impressao_Sucesso_AuditaSucesso()
    {
        List<(ProdutoAcabadoCaixa, string, string)> registros = [];
        ImpressaoProdutoAcabadoServico svc = ServicoComAuditoriaFake(registros);
        await svc.ExecutarImpressaoComAuditoriaAsync(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoImpressao, () => Task.CompletedTask);

        (_, string tipo, string resultado) = Assert.Single(registros);
        Assert.Equal("IMPRESSAO", tipo);
        Assert.Equal("SUCESSO", resultado);
    }

    [Fact]
    public async Task Reimpressao_Sucesso_AuditaSucesso()
    {
        List<(ProdutoAcabadoCaixa, string, string)> registros = [];
        ImpressaoProdutoAcabadoServico svc = ServicoComAuditoriaFake(registros);
        await svc.ExecutarImpressaoComAuditoriaAsync(
            CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoReimpressao, () => Task.CompletedTask);

        (_, string tipo, string resultado) = Assert.Single(registros);
        Assert.Equal("REIMPRESSAO", tipo);
        Assert.Equal("SUCESSO", resultado);
    }

    [Fact]
    public async Task Impressao_Falha_AuditaFalhaComContexto_ERelancaOriginal()
    {
        List<(ProdutoAcabadoCaixa Caixa, string Tipo, string Resultado)> registros = [];
        ImpressaoProdutoAcabadoServico svc = ServicoComAuditoriaFake(registros);
        InvalidOperationException original = new("zebra offline (simulado)");

        InvalidOperationException capturada = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ExecutarImpressaoComAuditoriaAsync(
                CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoImpressao,
                () => throw original));

        Assert.Same(original, capturada); // exceção ORIGINAL propaga após a auditoria
        (ProdutoAcabadoCaixa caixa, string tipo, string resultado) = Assert.Single(registros);
        Assert.Equal("IMPRESSAO", tipo);
        Assert.Equal("FALHA", resultado);
        // Contexto de FALHA contém os campos da caixa.
        string contexto = ImpressaoProdutoAcabadoServico.MontarContextoAuditoriaJson(caixa, tipo);
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(contexto);
        Assert.Equal(4, doc.RootElement.GetProperty("codigo_hu_caixa").GetInt64());
        Assert.Equal("CX-1001951-0004", doc.RootElement.GetProperty("codigo_caixa_local").GetString());
        Assert.Equal(4, doc.RootElement.GetProperty("numero_caixa").GetInt32());
        Assert.Equal("IMPRESSAO", doc.RootElement.GetProperty("tipo_operacao").GetString());
        Assert.Equal("TERM-01", doc.RootElement.GetProperty("terminal").GetString());
        Assert.Equal("300014352", doc.RootElement.GetProperty("hu_sap").GetString());
    }

    [Fact]
    public async Task Reimpressao_Falha_AuditaFalhaComContexto_ERelancaOriginal()
    {
        List<(ProdutoAcabadoCaixa Caixa, string Tipo, string Resultado)> registros = [];
        ImpressaoProdutoAcabadoServico svc = ServicoComAuditoriaFake(registros);
        InvalidOperationException original = new("falha de escrita (simulado)");

        InvalidOperationException capturada = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ExecutarImpressaoComAuditoriaAsync(
                CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoReimpressao,
                () => throw original));

        Assert.Same(original, capturada);
        (_, string tipo, string resultado) = Assert.Single(registros);
        Assert.Equal("REIMPRESSAO", tipo);
        Assert.Equal("FALHA", resultado);
    }

    // Auditoria indisponível (seam lança) NÃO substitui/mascara a exceção original de impressão.
    [Fact]
    public async Task AuditoriaIndisponivel_NaoMascaraExcecaoOriginal()
    {
        ImpressaoProdutoAcabadoServico svc = new(new ImpressoraEtiquetaServico(),
            (_, _, _) => throw new InvalidOperationException("auditoria indisponível"));
        InvalidOperationException original = new("zebra offline (simulado)");

        InvalidOperationException capturada = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ExecutarImpressaoComAuditoriaAsync(
                CaixaConfirmada(), ImpressaoProdutoAcabadoServico.TipoOperacaoImpressao,
                () => throw original));
        Assert.Same(original, capturada); // exceção ORIGINAL (não a da auditoria)
    }

    // O comentário da Form NÃO afirma que CANCELADA/INDETERMINADO_TIMEOUT são reimprimíveis.
    [Fact]
    public void Form_ComentarioReimpressao_NaoAfirmaCanceladaOuTimeoutReimprimivel()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("Reimpressão explícita da etiqueta", StringComparison.Ordinal);
        int fim = form.IndexOf("private async Task ReimprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string comentario = form[inicio..fim];
        Assert.DoesNotContain("CONFIRMADA_SAP/CANCELADA/INDETERMINADO_TIMEOUT são reimprim", comentario, StringComparison.Ordinal);
        // Deixa explícito que só CONFIRMADA_SAP + HU é imprimível/reimprimível.
        Assert.Contains("SOMENTE CONFIRMADA_SAP", comentario, StringComparison.Ordinal);
        Assert.Contains("CANCELADA = NÃO", comentario, StringComparison.Ordinal);
        Assert.Contains("INDETERMINADO_TIMEOUT = NÃO", comentario, StringComparison.Ordinal);
    }

    // Impressão e reimpressão usam operações DISTINTAS (diferenciáveis na auditoria IMPRESSAO_ZEBRA_DIAGNOSTICO)
    // e exigem permissões distintas (Imprimir vs Reimprimir), reaproveitando o pipeline Zebra existente.
    [Fact]
    public void ImpressoraServico_ImpressaoEReimpressao_DistintasEComPermissao()
    {
        string servico = LerFonte("Servicos", "Operacao", "ImpressoraEtiquetaServico.cs");
        Assert.Contains("ImprimirEtiquetaCaixaProdutoAcabadoAsync", servico, StringComparison.Ordinal);
        Assert.Contains("ReimprimirEtiquetaCaixaProdutoAcabadoAsync", servico, StringComparison.Ordinal);
        Assert.Contains("\"imprimir etiqueta de caixa produto acabado\"", servico, StringComparison.Ordinal);
        Assert.Contains("\"reimprimir etiqueta de caixa produto acabado\"", servico, StringComparison.Ordinal);
        // Permissões distintas.
        Assert.Contains("PermissoesSistema.Acoes.Imprimir, \"impressão de etiqueta de caixa\"", servico, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Reimprimir, \"reimpressão de etiqueta de caixa\"", servico, StringComparison.Ordinal);
        // Reaproveita o driver existente (não duplica): delega ao ServicoImpressoraZebra.
        Assert.Contains("_servicoImpressoraZebra.ImprimirEtiquetaCaixaProdutoAcabado(impressora, etiqueta)", servico, StringComparison.Ordinal);
    }

    // A orquestração de impressão é um efeito LOCAL: NÃO referencia SAP/HU/claim/gateway/POST.
    [Fact]
    public void ImpressaoServico_NaoTocaSap()
    {
        string servico = LerFonte("Servicos", "Operacao", "ImpressaoProdutoAcabadoServico.cs");
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("ClaimEnvio", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizarEnvio", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarHandlingUnitCaixaAsync", servico, StringComparison.Ordinal);
    }

    /// <summary>
    /// A impressao e ISOLADA: captura qualquer excecao e nao chama nada de SAP. Falha de impressora nao
    /// altera a caixa, nao desfaz a confirmacao e — no envio unico — nao interrompe o lote.
    /// </summary>
    [Fact]
    public void Form_ImpressaoAposConfirmacao_IsoladaENaoTocaSap()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private async Task ImprimirEtiquetaCaixaConfirmadaAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "o metodo de impressao pos-confirmacao sumiu");
        int fim = form.IndexOf("private async Task ", inicio + 20, StringComparison.Ordinal);
        string metodo = fim > inicio ? form[inicio..fim] : form[inicio..];

        Assert.Contains("catch (Exception ex)", metodo, StringComparison.Ordinal);
        Assert.Contains("_impressaoProdutoAcabadoServico.ImprimirCaixaAsync(", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizarEnvioCaixaAsync", metodo, StringComparison.Ordinal);
    }

    /// <summary>
    /// A REGRA INVERTEU em 19/11/2026. Este teste exigia o oposto — que a etiqueta saisse na pesagem e
    /// NAO na confirmacao — e estava certo enquanto a politica era essa. Agora trava o contrario: a
    /// pesagem nao imprime, os DOIS caminhos de envio imprimem na confirmacao, e a entrada que imprimia
    /// sem HU nao pode voltar.
    /// </summary>
    [Fact]
    public void Form_ImprimeNaConfirmacaoENaoNaPesagem()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // Os dois caminhos de envio imprimem: HU isolada e 261→101→HU.
        Assert.Contains("await ImprimirEtiquetaCaixaConfirmadaAsync(confirmada);", form, StringComparison.Ordinal);
        Assert.Contains("ImprimirEtiquetaCaixaConfirmadaAsync(caixaConfirmadaPipeline)", form, StringComparison.Ordinal);

        // E a pesagem nao imprime mais, por nenhuma porta.
        Assert.DoesNotContain("ImprimirCaixaPesadaAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ImprimirEtiquetaCaixaPesadaAsync", form, StringComparison.Ordinal);

        // A reimpressao manual continua existindo.
        Assert.Contains("ReimprimirEtiquetaCaixaAsync(", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// O caminho do pipeline so imprime com ele INTEGRALMENTE concluido. Etapa parcial nao tem HU, e
    /// imprimir ali produziria etiqueta sem o dado que a justifica.
    /// </summary>
    [Fact]
    public void Form_PipelineSoImprimeComPipelineConcluido()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains(
            "if (pipelineConcluido && caixaConfirmadaPipeline is not null)",
            form, StringComparison.Ordinal);
    }

    // Form: reimpressão é ação EXPLÍCITA (duplo clique + confirmação) e NÃO chama SAP.
    [Fact]
    public void Form_Reimpressao_AcaoExplicita_SemSap()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        // Duplo clique na grid dispara a reimpressão.
        Assert.Contains("productionDataGridView.CellDoubleClick += ProductionDataGridView_CellDoubleClick;", form, StringComparison.Ordinal);
        Assert.Contains("await ReimprimirEtiquetaCaixaAsync(_caixasPesadas[e.RowIndex]);", form, StringComparison.Ordinal);

        int inicio = form.IndexOf("private async Task ReimprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        int fim = form.IndexOf("private string ObterDescricaoMaterialAtual(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = form[inicio..fim];
        // Confirmação humana pelo diálogo padrão + reimpressão delegada (permissão Reimprimir no serviço).
        Assert.Contains("ConfirmarReimpressaoEtiquetaForm", metodo, StringComparison.Ordinal);
        Assert.Contains("_impressaoProdutoAcabadoServico.ReimprimirCaixaAsync(", metodo, StringComparison.Ordinal);
        // NUNCA chama SAP a partir da reimpressão.
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizarEnvioCaixaAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarHandlingUnitCaixaAsync", metodo, StringComparison.Ordinal);
    }
}
