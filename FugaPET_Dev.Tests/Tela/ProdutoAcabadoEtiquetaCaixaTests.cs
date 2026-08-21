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

    // O ZPL específico da caixa é válido e contém os campos com RÓTULOS corretos + código de barras do código local.
    [Fact]
    public void Zpl_CaixaProdutoAcabado_ContemCamposCorretosEBarcode()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada(), "SACO PET 25KG");
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);

        Assert.StartsWith("^XA", zpl, StringComparison.Ordinal);
        Assert.EndsWith("^XZ", zpl.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("PRODUTO ACABADO - CAIXA", zpl, StringComparison.Ordinal);
        Assert.Contains("OP: 1001951", zpl, StringComparison.Ordinal);
        Assert.Contains("LOTE: L-TESTE", zpl, StringComparison.Ordinal);
        Assert.Contains("CAIXA No: 0004", zpl, StringComparison.Ordinal);
        Assert.Contains("COD. CAIXA: CX-1001951-0004", zpl, StringComparison.Ordinal);
        Assert.Contains("HU SAP: 300014352", zpl, StringComparison.Ordinal);
        // Código de barras Code128 VERTICAL (orientação B) do codigo_caixa_local (rastreabilidade por leitura).
        Assert.Contains("^BCB,120,Y,N,N^FDCX-1001951-0004", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^BCN,", zpl, StringComparison.Ordinal); // não permanece horizontal
        // NÃO usa os rótulos do layout de produção do Semi-Acabado.
        Assert.DoesNotContain("SAIDA ESTUFA", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("PACOTES", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void Zpl_CaixaSemHu_MostraPendente()
    {
        ProdutoAcabadoCaixa caixa = CaixaConfirmada();
        caixa.HandlingUnitExternalId = null;
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(caixa);
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(e);
        Assert.Contains("HU SAP: PENDENTE", zpl, StringComparison.Ordinal);
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

    // ===================== UX: mensagem explícita de sucesso + barcode vertical =====================

    // Impressão e reimpressão exibem feedback EXPLÍCITO de sucesso (diferenciado) sem habilitar reenvio SAP.
    [Fact]
    public void Form_ImpressaoEReimpressao_ExibemSucessoExplicitoDiferenciado()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        int inicioImp = form.IndexOf("private async Task ImprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        int fimImp = form.IndexOf("private async Task ReimprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        Assert.True(inicioImp >= 0 && fimImp > inicioImp);
        string impressao = form[inicioImp..fimImp];
        Assert.Contains("Etiqueta impressa com sucesso.", impressao, StringComparison.Ordinal);
        Assert.Contains("MessageBoxIcon.Information", impressao, StringComparison.Ordinal);
        // Sucesso só após concluir SEM exceção (dentro do try, antes do catch).
        Assert.True(impressao.IndexOf("Etiqueta impressa com sucesso.", StringComparison.Ordinal)
            < impressao.IndexOf("catch (Exception ex)", StringComparison.Ordinal));

        int inicioRe = form.IndexOf("private async Task ReimprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        int fimRe = form.IndexOf("private string ObterDescricaoMaterialAtual(", StringComparison.Ordinal);
        Assert.True(inicioRe >= 0 && fimRe > inicioRe);
        string reimpressao = form[inicioRe..fimRe];
        Assert.Contains("Etiqueta reimpressa com sucesso.", reimpressao, StringComparison.Ordinal);
        Assert.Contains("MessageBoxIcon.Information", reimpressao, StringComparison.Ordinal);

        // As mensagens de sucesso DIFERENCIAM impressão de reimpressão.
        Assert.NotEqual(
            impressao.Contains("Etiqueta impressa com sucesso.", StringComparison.Ordinal),
            reimpressao.Contains("Etiqueta impressa com sucesso.", StringComparison.Ordinal));

        // Nenhuma dessas mensagens habilita reenvio SAP.
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", impressao, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", reimpressao, StringComparison.Ordinal);
    }

    // RDLC oficial 8x5: o código de barras BarcodeBottom fica VERTICAL (WritingMode Rotate270) e o XML é válido.
    [Fact]
    public void Rdlc8x5_BarcodeBottom_Vertical()
    {
        string rdlc = LerFonte("Relatorio", "EtiquetaProduto8x5.rdlc");
        System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Parse(rdlc); // valida XML (compilável/abrível)
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

        System.Xml.Linq.XElement? barcodeBottom = doc.Descendants(ns + "Textbox")
            .FirstOrDefault(e => (string?)e.Attribute("Name") == "BarcodeBottom");
        Assert.NotNull(barcodeBottom);
        string? writingMode = barcodeBottom!.Descendants(ns + "WritingMode").FirstOrDefault()?.Value;
        Assert.Equal("Rotate270", writingMode);
        // Mantém a fonte de barras do layout oficial (não inventa novo barcode).
        Assert.Contains("Libre Barcode 128", barcodeBottom.Value, StringComparison.Ordinal);
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

    // Form: impressão é chamada APÓS a confirmação e é ISOLADA (try/catch) — falha não altera a HU/integração.
    [Fact]
    public void Form_ImpressaoIsolada_NaoAfetaHu()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        int inicio = form.IndexOf("private async Task ImprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        int fim = form.IndexOf("private async Task ReimprimirEtiquetaCaixaAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = form[inicio..fim];
        // O corpo captura qualquer exceção e NÃO chama nada de SAP.
        Assert.Contains("catch (Exception ex)", metodo, StringComparison.Ordinal);
        Assert.Contains("_impressaoProdutoAcabadoServico.ImprimirCaixaAsync(", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizarEnvioCaixaAsync", metodo, StringComparison.Ordinal);
        // A impressão só é disparada no ramo Confirmado, depois de persistir a confirmação.
        Assert.Contains("await ImprimirEtiquetaCaixaAsync(confirmada);", form, StringComparison.Ordinal);
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
