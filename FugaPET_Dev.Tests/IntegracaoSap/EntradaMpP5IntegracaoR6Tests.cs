using System.Globalization;
using System.Net;
using System.Text;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE R6 (FUGAPET-ENTRADA-MP-P5-INTEGRATION-R6): integração operacional do P5.
/// <para>
/// T1/T2 — disponibilidade declarada prevalece sobre evidência favorável. T3–T8 — leitura OData V2 do
/// histórico 101/102 com paginação e classificação fail-closed. T9–T11 — exclusividade, desfecho
/// desconhecido e cancelamento. T12–T14 — contrato empresarial, HIBE e defaults sem banco migrado.
/// </para>
/// Nenhum POST SAP, nenhum HTTP real, nenhuma alteração PostgreSQL.
/// </summary>
public sealed class EntradaMpP5IntegracaoR6Tests
{
    private const string Pedido = "4500000071";
    private const string Item = "00010";
    private const string Material = "1000111";
    private const string Centro = "3007";
    private const long Lancamento = 99L;
    private const string HostQ = "vhfufqs4ci.sap.fugacouros.com.br";
    private const string BaseMaterialDocument =
        "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/";

    // ======================================================================
    // T1 / T2 — disponibilidade vs evidência favorável (defeito do QA R5)
    // ======================================================================

    [Fact] // T1: fonte com Disponivel=false devolvendo evidência FAVORÁVEL ⇒ prevalece o BLOQUEIO.
    public async Task T1_FonteIndisponivelComDadosFavoraveis_Bloqueia()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            // Declara indisponibilidade E devolve "Obtida": inconsistência deliberada.
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida(), disponivel: false),
            new FakeLocal(ResultadoRecebimentoLocal.SemRegistro()),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true), disponivel: false));

        ResultadoEvidenciaRecebimento r = await provedor.AvaliarAsync(Pedido, Item, Lancamento);

        Assert.Equal(CenarioEvidenciaRecebimento.ExclusividadeIndisponivel, r.Cenario);
        Assert.Null(r.ComoTriState());
        Assert.False(r.Libera);
    }

    [Fact] // T1b: inconsistência nomeada em cada normalizador, isoladamente.
    public void T1b_InconsistenciaEntreDisponibilidadeEResultado_EhNomeada()
    {
        ResultadoExclusividadeItemPedido exclusividade =
            ProvedorEvidenciaRecebimentoItemPedido.NormalizarExclusividade(
                disponivel: false, ResultadoExclusividadeItemPedido.Obtida());
        Assert.Equal(CenarioExclusividadeItemPedido.Indisponivel, exclusividade.Cenario);
        Assert.Contains("INCONSISTENCIA", exclusividade.Motivo, StringComparison.Ordinal);

        ResultadoHistoricoMaterialSap historico =
            ProvedorEvidenciaRecebimentoItemPedido.NormalizarHistorico(
                disponivel: false, ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));
        Assert.Equal(CenarioHistoricoMaterialSap.Indisponivel, historico.Cenario);
        Assert.Contains("INCONSISTENCIA", historico.Motivo, StringComparison.Ordinal);

        // Fonte disponível não é alterada pelo normalizador.
        Assert.Equal(
            CenarioHistoricoMaterialSap.Lido,
            ProvedorEvidenciaRecebimentoItemPedido.NormalizarHistorico(
                true, ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)).Cenario);
    }

    [Fact] // T2: fonte LOCAL indisponível ⇒ bloqueia (disponibilidade via cenário, contrato existente).
    public async Task T2_FonteLocalIndisponivel_Bloqueia()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida()),
            new FakeLocal(ResultadoRecebimentoLocal.Indisponivel("contrato R5B nao implantado")),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        ResultadoEvidenciaRecebimento r = await provedor.AvaliarAsync(Pedido, Item, Lancamento);

        Assert.Equal(CenarioEvidenciaRecebimento.FalhaConsulta, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    // ======================================================================
    // T3 / T4 — GET OData V2 com d.results e paginação d.__next
    // ======================================================================

    [Fact] // T3: envelope d.results interpretado; item 10 == 00010.
    public void T3_EnvelopeV2DResults_InterpretadoComItemNormalizado()
    {
        string json = Envelope([Doc("101", "5000000001", itemPedido: "10")], proxima: null);

        PaginaHistoricoMaterial pagina =
            HistoricoMaterialDocumentSapApiClient.InterpretarPagina(json, Pedido, "00010");

        Assert.True(pagina.Valida);
        MovimentoHistoricoMaterial movimento = Assert.Single(pagina.Movimentos);
        Assert.True(movimento.EhEntrada101);
        Assert.Equal("5000000001", movimento.MaterialDocument);
        Assert.Null(pagina.ProximaPagina);
        // Normalização simétrica.
        Assert.True(HistoricoMaterialDocumentSapApiClient.MesmoItemPedido("10", "00010"));
        Assert.True(HistoricoMaterialDocumentSapApiClient.MesmoItemPedido(" 010 ", "10"));
        Assert.False(HistoricoMaterialDocumentSapApiClient.MesmoItemPedido("20", "00010"));
    }

    [Fact] // T4: paginação d.__next seguida até o fim e movimentos acumulados.
    public async Task T4_PaginacaoNextCompleta_AcumulaEClassifica()
    {
        string pagina2 = BaseMaterialDocument + "A_MaterialDocumentItem?$skiptoken='2'&sap-client=110";
        FakeHttp http = new(
        [
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000001")], proxima: pagina2)),
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000002")], proxima: null))
        ]);

        HistoricoMaterialDocumentSapServico servico = Servico(http);
        Assert.True(servico.Disponivel);

        ResultadoHistoricoMaterialSap r = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);

        Assert.Equal(2, http.Chamadas);
        Assert.Equal(CenarioHistoricoMaterialSap.Lido, r.Cenario);
        Assert.True(r.PaginacaoCompleta);
        Assert.Equal(2, r.Documentos101);
        // ABRANGÊNCIA nunca é afirmada pela consulta.
        Assert.False(r.AbrangenciaComprovada);
    }

    [Fact] // T5: falha de paginação (HTTP, envelope e nextLink de outro destino) ⇒ bloqueia.
    public async Task T5_FalhaDePaginacao_Bloqueia()
    {
        // (a) HTTP 500 na segunda página.
        HistoricoMaterialDocumentSapServico comErroHttp = Servico(new FakeHttp(
        [
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000001")],
                proxima: BaseMaterialDocument + "A_MaterialDocumentItem?$skiptoken='2'&sap-client=110")),
            (HttpStatusCode.InternalServerError, "{}")
        ]));
        ResultadoHistoricoMaterialSap erroHttp = await comErroHttp.ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.Equal(CenarioHistoricoMaterialSap.FalhaConsulta, erroHttp.Cenario);
        Assert.False(erroHttp.PaginacaoCompleta);

        // (b) envelope inválido.
        ResultadoHistoricoMaterialSap envelope = await Servico(new FakeHttp(
            [(HttpStatusCode.OK, """{"value":[]}""")])).ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.Equal(CenarioHistoricoMaterialSap.FalhaConsulta, envelope.Cenario);
        Assert.Contains("envelope", envelope.Motivo, StringComparison.OrdinalIgnoreCase);

        // (c) __next apontando para OUTRO host ⇒ rejeitado por segurança.
        ResultadoHistoricoMaterialSap intruso = await Servico(new FakeHttp(
        [
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000001")],
                proxima: "https://intruso.exemplo.test/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/x"))
        ])).ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.Equal(CenarioHistoricoMaterialSap.FalhaConsulta, intruso.Cenario);
        Assert.Contains("seguranca", intruso.Motivo, StringComparison.OrdinalIgnoreCase);

        // (d) __next com mandante divergente ⇒ rejeitado.
        Assert.Null(HistoricoMaterialDocumentSapServico.ResolverProximaPagina(
            BaseMaterialDocument + "A_MaterialDocumentItem?sap-client=100",
            new Uri(BaseMaterialDocument), "110"));
    }

    [Fact] // T5b: repetição da mesma página (loop) ⇒ bloqueia.
    public async Task T5b_LoopDePaginacao_Bloqueia()
    {
        string mesma = BaseMaterialDocument + "A_MaterialDocumentItem?$skiptoken='1'&sap-client=110";
        HistoricoMaterialDocumentSapServico servico = Servico(new FakeHttp(
        [
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000001")], proxima: mesma)),
            (HttpStatusCode.OK, Envelope([Doc("101", "5000000002")], proxima: mesma))
        ]));

        ResultadoHistoricoMaterialSap r = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);

        Assert.Equal(CenarioHistoricoMaterialSap.FalhaConsulta, r.Cenario);
        Assert.Contains("loop", r.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    // ======================================================================
    // T6 — histórico "vazio" com abrangência não comprovada
    // ======================================================================

    [Fact] // T6: zero documentos + paginação completa, abrangência NÃO comprovada ⇒ BLOQUEIA.
    public async Task T6_HistoricoSemDocumentosComAbrangenciaNaoComprovada_Bloqueia()
    {
        HistoricoMaterialDocumentSapServico servico = Servico(new FakeHttp(
            [(HttpStatusCode.OK, Envelope([], proxima: null))]));

        ResultadoHistoricoMaterialSap historico = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.True(historico.PaginacaoCompleta);
        Assert.False(historico.AbrangenciaComprovada);
        Assert.Equal(0, historico.Documentos101);

        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            historico);

        Assert.Equal(CenarioEvidenciaRecebimento.AbrangenciaNaoComprovada, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    // ======================================================================
    // T7 / T8 — 101 anterior e estorno ambíguo
    // ======================================================================

    [Fact] // T7: 101 anterior do mesmo item ⇒ BLOQUEIA como impeditivo.
    public async Task T7_Entrada101Anterior_Bloqueia()
    {
        HistoricoMaterialDocumentSapServico servico = Servico(new FakeHttp(
            [(HttpStatusCode.OK, Envelope([Doc("101", "5000000001")], proxima: null))]));

        ResultadoHistoricoMaterialSap historico = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.Equal(1, historico.Documentos101);

        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            historico);

        Assert.Equal(CenarioEvidenciaRecebimento.RecebimentoImpeditivo, r.Cenario);
        Assert.True(r.ComoTriState());
    }

    [Fact] // T8: estorno 102 SEM vínculo documental ⇒ INCERTO ⇒ bloqueia (não se presume correspondência).
    public async Task T8_Estorno102SemVinculoDocumental_Bloqueia()
    {
        HistoricoMaterialDocumentSapServico servico = Servico(new FakeHttp(
        [
            (HttpStatusCode.OK, Envelope(
                [Doc("101", "5000000001"), Doc("102", "5000000009", estorna: null)], proxima: null))
        ]));

        ResultadoHistoricoMaterialSap historico = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);
        Assert.True(historico.TemDocumentoIncerto);

        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            historico);

        Assert.Equal(CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado, r.Cenario);
        Assert.Contains("não é presumida", r.Motivo, StringComparison.Ordinal);
    }

    [Fact] // T8b: estorno COM vínculo a documento não observado também é incerto.
    public void T8b_EstornoComVinculoDesconhecido_EhIncerto()
    {
        ResultadoHistoricoMaterialSap r = ClassificadorHistoricoMaterialSap.Classificar(
        [
            Movimento("101", "5000000001"),
            Movimento("102", "5000000009", estorna: "9999999999")
        ], paginacaoCompleta: true, abrangenciaComprovada: true);

        Assert.True(r.TemDocumentoIncerto);
    }

    [Fact] // T8c: estorno COM vínculo ao 101 observado é correlacionado — e saldo zero exige reconciliação.
    public void T8c_EstornoCorrelacionado_ContabilizaEExigeReconciliacao()
    {
        ResultadoHistoricoMaterialSap historico = ClassificadorHistoricoMaterialSap.Classificar(
        [
            Movimento("101", "5000000001"),
            Movimento("102", "5000000009", estorna: "5000000001")
        ], paginacaoCompleta: true, abrangenciaComprovada: true);

        Assert.False(historico.TemDocumentoIncerto);
        Assert.Equal(1, historico.Documentos101);
        Assert.Equal(1, historico.Movimentos102);

        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            historico);
        Assert.Equal(CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado, r.Cenario);
    }

    [Fact] // T8d: material/centro divergentes no mesmo item do pedido ⇒ anomalia, não descarte silencioso.
    public void T8d_MaterialOuCentroDivergente_EhIncerto()
    {
        ResultadoHistoricoMaterialSap r = ClassificadorHistoricoMaterialSap.Classificar(
            [Movimento("101", "5000000001", material: "OUTRO")],
            paginacaoCompleta: true,
            abrangenciaComprovada: true,
            materialEsperado: Material,
            centroEsperado: Centro);

        Assert.True(r.TemDocumentoIncerto);
        Assert.Equal(0, r.Documentos101);
    }

    // ======================================================================
    // T9 / T10 / T11 — exclusividade, desfecho desconhecido e cancelamento
    // ======================================================================

    [Fact] // T9: duas instâncias disputando o mesmo pedido+item — só UMA obtém o claim.
    public async Task T9_DuasInstanciasDisputandoPedidoItem_SomenteUmaObtemClaim()
    {
        FakeExclusividadeConcorrente claim = new();
        ProvedorEvidenciaRecebimentoItemPedido primeira = new(
            claim,
            new FakeLocal(ResultadoRecebimentoLocal.SemRegistro()),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));
        ProvedorEvidenciaRecebimentoItemPedido segunda = new(
            claim,
            new FakeLocal(ResultadoRecebimentoLocal.SemRegistro()),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        ResultadoEvidenciaRecebimento r1 = await primeira.AvaliarAsync(Pedido, Item, 101L);
        ResultadoEvidenciaRecebimento r2 = await segunda.AvaliarAsync(Pedido, Item, 202L);

        // A primeira passa da exclusividade (e para na abrangência); a segunda é barrada pelo claim.
        Assert.NotEqual(CenarioEvidenciaRecebimento.TentativaLocalPendente, r1.Cenario);
        Assert.Equal(CenarioEvidenciaRecebimento.TentativaLocalPendente, r2.Cenario);
        Assert.Equal(1, claim.Concedidos);
        // Exclusividade é o ponto: NUNCA as duas autorizadas ao mesmo tempo. Com todas as evidências
        // favoráveis, a detentora do claim pode liberar; a concorrente NÃO.
        Assert.True(r1.Libera);
        Assert.False(r2.Libera);
        Assert.Null(r2.ComoTriState());
    }

    [Fact] // T10: desfecho remoto desconhecido após POST ⇒ tentativa permanece INDETERMINADA e bloqueia.
    public async Task T10_ResultadoRemotoDesconhecido_MantemIndeterminadoEBloqueia()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.EmUso("tentativa 7 em aberto apos POST")),
            new FakeLocal(ResultadoRecebimentoLocal.Pendente("lancamento 99 ENVIADO_SAP sem desfecho")),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Falha("desfecho remoto nao comprovado")));

        ResultadoEvidenciaRecebimento r = await provedor.AvaliarAsync(Pedido, Item, Lancamento);

        Assert.False(r.Libera);
        Assert.Null(r.ComoTriState());   // nunca vira retry cego
    }

    [Fact] // T11: cancelamento pré-POST sobe como cancelamento (compensação é da fronteira do R3B).
    public async Task T11_CancelamentoPrePost_SobeComoCancelamento()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida()),
            new FakeLocal(null, () => throw new OperationCanceledException(cts.Token)),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provedor.AvaliarAsync(Pedido, Item, Lancamento, cts.Token));
    }

    // ======================================================================
    // T12 / T13 / T14 — contrato empresarial, HIBE e defaults
    // ======================================================================

    [Fact] // T12: 1000.000 KG no campo padrão e peso líquido DIFERENTE nos campos ZZ1, preservados.
    public void T12_ContratoEmpresarialPreservado()
    {
        ResultadoAlocacao101 alocacao = EntradaProdutoAlocacao101.Decidir(
            FugaPET_Dev.Modelo.Processo.ClassificacaoEntradaMaterial.MateriaPrima,
            quantidadePedidoItem: 1000.000m,
            unidadeQuantidadePedido: "KG",
            pesoLiquidoKg: 296.500m,
            descricaoItem: Item);

        Assert.Equal(CenarioAlocacao101.MateriaPrimaComQuantidadeDoPedido, alocacao.Cenario);
        Assert.Equal("1000.000", alocacao.QuantidadePadrao);
        Assert.Equal("KG", alocacao.UnidadePadrao);
        Assert.Equal("296.500", alocacao.PesoLiquidoCustomizado);
        Assert.Equal("KG", alocacao.UnidadePesoCustomizado);
        Assert.NotEqual(alocacao.QuantidadePadrao, alocacao.PesoLiquidoCustomizado);
    }

    [Fact] // T13: HIBE sem regressão — legado e sem campos customizados.
    public void T13_HibeSemRegressao()
    {
        ResultadoAlocacao101 alocacao = EntradaProdutoAlocacao101.Decidir(
            FugaPET_Dev.Modelo.Processo.ClassificacaoEntradaMaterial.Quimico,
            1000.000m, "KG", 296.500m, Item);

        Assert.Equal(CenarioAlocacao101.Legado, alocacao.Cenario);
        Assert.Equal("296.5", alocacao.QuantidadePadrao);
        Assert.Null(alocacao.PesoLiquidoCustomizado);
        Assert.Null(alocacao.UnidadePesoCustomizado);
    }

    [Fact] // T14: sem banco migrado e sem contrato de histórico, os defaults BLOQUEIAM.
    public async Task T14_DefaultsSemBancoMigrado_Bloqueiam()
    {
        ProvedorEvidenciaRecebimentoItemPedido padrao = new();

        ResultadoEvidenciaRecebimento r = await padrao.AvaliarAsync(Pedido, Item, Lancamento);

        Assert.Equal(CenarioEvidenciaRecebimento.ExclusividadeIndisponivel, r.Cenario);
        Assert.Null(await padrao.ExisteRecebimentoConfirmadoAsync(Pedido, Item, Lancamento));
        // E o serviço real de histórico também é indisponível sem configuração.
        Assert.False(new HistoricoMaterialDocumentSapServico(new ConfiguracaoSap()).Disponivel);
    }

    [Fact] // R6: serviço de histórico sem destino/credencial declara INDISPONÍVEL, nunca "sem histórico".
    public async Task R6_HistoricoSemConfiguracao_DeclaraIndisponivel()
    {
        HistoricoMaterialDocumentSapServico servico = new(new ConfiguracaoSap());

        ResultadoHistoricoMaterialSap r = await servico.ConsultarPorPedidoItemAsync(Pedido, Item);

        Assert.Equal(CenarioHistoricoMaterialSap.Indisponivel, r.Cenario);
        Assert.False(r.PaginacaoCompleta);
        Assert.False(r.AbrangenciaComprovada);
    }

    [Fact] // R6: a URL do GET respeita entidade, filtro por PurchaseOrder, $format=json e sap-client.
    public void R6_UrlDoGet_RespeitaContratoR5A()
    {
        Uri url = HistoricoMaterialDocumentSapApiClient.MontarUrlHistoricoPorPedido(
            new Uri(BaseMaterialDocument), Pedido, "110");

        Assert.Equal(HostQ, url.Host);
        Assert.Equal(44300, url.Port);
        Assert.Contains("A_MaterialDocumentItem", url.ToString(), StringComparison.Ordinal);
        Assert.Contains("sap-client=110", url.ToString(), StringComparison.Ordinal);
        Assert.Contains("$format=json", url.ToString(), StringComparison.Ordinal);
        // Robusto ao encoding: new Uri(...) normaliza %20/%27 no ToString(), como no
        // ProductMasterSapApiClient já existente. O que importa é o filtro efetivo.
        string decodificada = Uri.UnescapeDataString(url.ToString());
        Assert.Contains($"PurchaseOrder eq '{Pedido}'", decodificada, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementType", decodificada, StringComparison.Ordinal);
        Assert.Contains("ReversedMaterialDocument", decodificada, StringComparison.Ordinal);
    }

    // ======================================================================
    // Helpers e fakes — sem HTTP real, sem banco, sem POST
    // ======================================================================

    private static HistoricoMaterialDocumentSapServico Servico(FakeHttp http)
        => new(
            new ConfiguracaoSap
            {
                MaterialDocumentBaseUrl = BaseMaterialDocument,
                HostsPermitidos = [HostQ],
                SapClient = "110",
                Usuario = "u",
                Senha = "p",
                TimeoutSegundos = 30
            },
            materialEsperado: null,
            centroEsperado: null,
            fabricaHttpClient: () => new HttpClient(http));

    private static string Doc(
        string tipo, string documento, string? estorna = null, string itemPedido = Item, string material = Material)
        => $$"""
        {
          "PurchaseOrder": "{{Pedido}}",
          "PurchaseOrderItem": "{{itemPedido}}",
          "Material": "{{material}}",
          "Plant": "{{Centro}}",
          "GoodsMovementType": "{{tipo}}",
          "MaterialDocument": "{{documento}}",
          "MaterialDocumentItem": "0001",
          "MaterialDocumentYear": "2026"
          {{(estorna is null ? string.Empty : $",\"ReversedMaterialDocument\": \"{estorna}\"")}}
        }
        """;

    private static string Envelope(IEnumerable<string> documentos, string? proxima)
    {
        string results = string.Join(",", documentos);
        string next = proxima is null ? string.Empty : $",\"__next\":\"{proxima}\"";
        return $"{{\"d\":{{\"results\":[{results}]{next}}}}}";
    }

    private static MovimentoHistoricoMaterial Movimento(
        string tipo, string documento, string? estorna = null, string material = Material)
        => new(tipo, documento, "0001", "2026", material, Centro, estorna, false, string.Empty);

    private sealed class FakeHttp(IReadOnlyList<(HttpStatusCode Status, string Corpo)> respostas)
        : HttpMessageHandler
    {
        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method); // nenhuma escrita, jamais
            int indice = Math.Min(Chamadas, respostas.Count - 1);
            Chamadas++;
            (HttpStatusCode status, string corpo) = respostas[indice];
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeExclusividade(ResultadoExclusividadeItemPedido resultado, bool disponivel = true)
        : IExclusividadeItemPedidoServico
    {
        public bool Disponivel => disponivel;

        public Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
            string numeroPedido, string numeroItemPedido, long codigoLancamento,
            CancellationToken cancellationToken = default)
            => Task.FromResult(resultado);
    }

    /// <summary>Claim de uso ÚNICO por pedido+item: concede ao primeiro, recusa aos demais.</summary>
    private sealed class FakeExclusividadeConcorrente : IExclusividadeItemPedidoServico
    {
        private readonly HashSet<string> _emUso = new(StringComparer.Ordinal);
        public int Concedidos { get; private set; }
        public bool Disponivel => true;

        public Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
            string numeroPedido, string numeroItemPedido, long codigoLancamento,
            CancellationToken cancellationToken = default)
        {
            string chave = numeroPedido.Trim() + "|"
                + HistoricoMaterialDocumentSapApiClient.NormalizarItemPedido(numeroItemPedido);

            if (!_emUso.Add(chave))
            {
                return Task.FromResult(ResultadoExclusividadeItemPedido.EmUso(
                    "pedido+item ja possui tentativa em aberto"));
            }

            Concedidos++;
            return Task.FromResult(ResultadoExclusividadeItemPedido.Obtida());
        }
    }

    private sealed class FakeLocal(
        ResultadoRecebimentoLocal? resultado, Func<ResultadoRecebimentoLocal>? aoConsultar = null)
        : IEvidenciaRecebimentoLocalServico
    {
        public Task<ResultadoRecebimentoLocal> ConsultarAsync(
            string numeroPedido, string numeroItemPedido, long codigoLancamentoAtual,
            CancellationToken cancellationToken = default)
            => Task.FromResult(aoConsultar is not null ? aoConsultar() : resultado!);
    }

    private sealed class FakeHistorico(ResultadoHistoricoMaterialSap resultado, bool disponivel = true)
        : IHistoricoMaterialDocumentSapServico
    {
        public bool Disponivel => disponivel;

        public Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
            string numeroPedido, string numeroItemPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(resultado);
    }
}
