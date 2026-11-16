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

    // Na pesagem a etiqueta sai sem SAP: basta a caixa estar persistida.
    [Fact]
    public void Pesagem_CaixaPersistidaSemSapEElegivel()
    {
        ImpressaoProdutoAcabadoServico.ValidarCaixaPesadaElegivelParaEtiqueta(CaixaPesada());

        // Em qualquer estado anterior ao envio, continua elegível — o gatilho é a pesagem, não o SAP.
        foreach (StatusIntegracaoCaixa estado in new[]
        {
            StatusIntegracaoCaixa.EmPesagem,
            StatusIntegracaoCaixa.FinalizadaLocal,
            StatusIntegracaoCaixa.ProntaParaEnvio,
            StatusIntegracaoCaixa.ErroSap
        })
        {
            ProdutoAcabadoCaixa caixa = CaixaPesada();
            caixa.StatusIntegracao = estado;
            ImpressaoProdutoAcabadoServico.ValidarCaixaPesadaElegivelParaEtiqueta(caixa);
        }
    }

    // Sem lastro no banco, não há etiqueta: caixa física sem rastreabilidade continua barrada.
    [Fact]
    public void Pesagem_SemIdentidadePersistidaNaoImprime()
    {
        ProdutoAcabadoCaixa semId = CaixaPesada();
        semId.CodigoProdutoAcabadoCaixa = null;
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaPesadaElegivelParaEtiqueta(semId));

        ProdutoAcabadoCaixa semCodigo = CaixaPesada();
        semCodigo.CodigoCaixaLocal = "  ";
        Assert.Throws<ErroOperacionalEsperadoException>(() =>
            ImpressaoProdutoAcabadoServico.ValidarCaixaPesadaElegivelParaEtiqueta(semCodigo));
    }

    // A etiqueta da pesagem sai com a HU como PENDENTE — e isso precisa chegar ao ZPL.
    [Fact]
    public void Pesagem_HuAindaNaoExisteEVaiComoPendente()
    {
        DadosEtiquetaCaixaProdutoAcabado e =
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaPesada(), "SACO PET 25KG");
        Assert.Equal(string.Empty, e.HandlingUnitSap);

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
        // Codigo de barras Code128 do codigo_caixa_local, VERTICAL (orientacao B).
        Assert.Contains("^BCB,", zpl, StringComparison.Ordinal);
        Assert.Contains("^FDCX-1001951-0004^FS", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^BCN", zpl, StringComparison.Ordinal);
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
    /// O codigo de barras precisa caber DENTRO da etiqueta. Em ^BCB o desenho sobe a partir da
    /// origem, entao o comprimento do Code128 nao pode passar do topo, e a altura das barras nao
    /// pode passar da largura. Este teste reproduz a conta que estourou na primeira versao.
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

        int largura = int.Parse(Entre(zpl, "^PW", "\n"));
        int altura = int.Parse(Entre(zpl, "^LL", "\n"));

        // ^FO x,y ... ^BY m ^BCB, alturaBarras
        int fo = zpl.LastIndexOf("^FO", zpl.IndexOf("^BCB", StringComparison.Ordinal), StringComparison.Ordinal);
        string cabecalho = zpl[fo..zpl.IndexOf("^FD", fo, StringComparison.Ordinal)];
        string[] origem = Entre(cabecalho, "^FO", "^BY").Split(',');
        int x = int.Parse(origem[0]), y = int.Parse(origem[1]);
        int modulo = int.Parse(Entre(cabecalho, "^BY", "^BC"));
        int alturaBarras = int.Parse(Entre(cabecalho, "^BCB,", ",").Trim(','));

        // Code128 subset B: inicio + N simbolos + verificador + parada.
        int modulos = (11 * (codigoLocal.Length + 2)) + 13;
        int comprimento = modulos * modulo;

        Assert.True(y - comprimento >= 0,
            $"a barra sobe {comprimento} dots a partir de y={y} e passa do topo da etiqueta");
        Assert.True(y <= altura, $"a origem y={y} esta fora da etiqueta de {altura} dots");
        Assert.True(x + alturaBarras <= largura,
            $"as barras ocupam ate x={x + alturaBarras} numa etiqueta de {largura} dots");
    }

    /// <summary>
    /// GATE 126J: a origem da barra era cravada no RODAPE (`altura - P(0.07)`), entao o curso do
    /// Code128 — que varia com o tamanho do dado — deixava a barra pendurada embaixo, fora da
    /// estrutura da etiqueta. Apontado pelo operador em 16/11/2026.
    /// <para>
    /// Agora o TOPO e ancorado no topo da grade de dados (P(0,72) = 146 dots a 203 dpi). Consequencia
    /// que o teste fixa: um codigo mais CURTO termina mais em cima, nunca mais embaixo.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("CX-1001951-0004")]
    [InlineData("CX-1000403-0001")]
    [InlineData("CX-1-1")]
    public void Zpl_CodigoDeBarras_AncoradoNoTopoDaGrade_NaoNoRodape(string codigoLocal)
    {
        const int TopoDaGrade = 146;   // P(0,72) a 203 dpi
        const int OrigemAntigaNoRodape = 386;   // altura(400) - P(0,07)

        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.CodigoCaixaLocal = codigoLocal;
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa));

        int fo = zpl.LastIndexOf("^FO", zpl.IndexOf("^BCB", StringComparison.Ordinal), StringComparison.Ordinal);
        string cabecalho = zpl[fo..zpl.IndexOf("^FD", fo, StringComparison.Ordinal)];
        string[] origem = Entre(cabecalho, "^FO", "^BY").Split(',');
        int y = int.Parse(origem[1]);
        int modulo = int.Parse(Entre(cabecalho, "^BY", "^BC"));

        int comprimento = ((11 * (codigoLocal.Length + 2)) + 13) * modulo;
        int topo = y - comprimento;

        Assert.Equal(TopoDaGrade, topo);
        Assert.True(y < OrigemAntigaNoRodape,
            $"a origem y={y} continua no rodape; deveria ter subido para acompanhar a grade");
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
        Assert.Contains("HU: PENDENTE", zpl, StringComparison.Ordinal);
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

    // Form: a etiqueta sai a cada PESAGEM, logo após a caixa ser persistida, e a impressão é ISOLADA
    // (try/catch) — falha não altera a caixa nem a integração.
    [Fact]
    public void Form_ImpressaoNaPesagem_IsoladaENaoAfetaHu()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private async Task ImprimirEtiquetaCaixaPesadaAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0);
        int fim = form.IndexOf("private async Task ", inicio + 20, StringComparison.Ordinal);
        string metodo = fim > inicio ? form[inicio..fim] : form[inicio..];
        // O corpo captura qualquer exceção e NÃO chama nada de SAP.
        Assert.Contains("catch (Exception ex)", metodo, StringComparison.Ordinal);
        Assert.Contains("_impressaoProdutoAcabadoServico.ImprimirCaixaPesadaAsync(", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizarEnvioCaixaAsync", metodo, StringComparison.Ordinal);
        // Disparada no registro da caixa, depois da persistência — nao na confirmação SAP.
        Assert.Contains("await ImprimirEtiquetaCaixaPesadaAsync(resultado.Caixa);", form, StringComparison.Ordinal);
    }

    // A etiqueta nao e emitida duas vezes para a mesma caixa: o antigo disparo pos-confirmacao saiu.
    [Fact]
    public void Form_NaoImprimeSegundaEtiquetaAposConfirmacaoSap()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.DoesNotContain("await ImprimirEtiquetaCaixaAsync(confirmada);", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ImprimirEtiquetaCaixaAsync(caixaConfirmadaPipeline)", form, StringComparison.Ordinal);
        // A reimpressao manual continua existindo, para obter a etiqueta ja com a HU.
        Assert.Contains("ReimprimirEtiquetaCaixaAsync(", form, StringComparison.Ordinal);
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
