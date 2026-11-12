using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE R5 (FUGAPET-ENTRADA-MP-P5-OPERATIONAL-READINESS-R5): decisão P5 por pedido+item.
/// <para>
/// Somente "comprovadamente sem recebimento" libera a quantidade integral do pedido. Ausência de
/// registros com abrangência não comprovada NÃO vira P5=FALSE (regra 15) e o default UNKNOWN=BLOCK é
/// preservado (regra 16). Nenhum teste faz POST, HTTP ou banco.
/// </para>
/// </summary>
public sealed class EntradaMpP5EvidenciaR5Tests
{
    private const string Pedido = "4500000071";
    private const string ItemPedido = "00010";
    private const long Lancamento = 99L;

    // ======================================================================
    // 18.1 — nenhum recebimento local (e demais evidências completas) ⇒ LIBERA
    // ======================================================================

    [Fact]
    public void NenhumRecebimentoLocalEHistoricoCompleto_Libera()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: true, abrangenciaComprovada: true));

        Assert.Equal(CenarioEvidenciaRecebimento.SemRecebimentoComprovado, r.Cenario);
        Assert.True(r.Libera);
        Assert.False(r.ComoTriState());   // false = libera, no contrato do seam do R2
    }

    // ======================================================================
    // 18.2 — recebimento confirmado (local e remoto) ⇒ BLOQUEIA como impeditivo
    // ======================================================================

    [Fact]
    public void RecebimentoConfirmadoLocal_BloqueiaComoImpeditivo()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.Confirmado("lancamento 42 CONFIRMADO_SAP"),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.RecebimentoImpeditivo, r.Cenario);
        Assert.False(r.Libera);
        Assert.True(r.ComoTriState());    // true = ja recebido
    }

    [Fact]
    public void Entrada101LiquidaNoSap_BloqueiaComoImpeditivo()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            // 2 entradas 101, 1 estorno 102 ⇒ saldo 1 em aberto.
            ResultadoHistoricoMaterialSap.Lido(2, 1, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.RecebimentoImpeditivo, r.Cenario);
        Assert.Contains("saldo de recebimento em aberto", r.Motivo, StringComparison.Ordinal);
    }

    // ======================================================================
    // 18.3 — recebimento pendente/incerto ⇒ BLOQUEIA
    // ======================================================================

    [Fact]
    public void TentativaLocalPendente_Bloqueia()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.Pendente("lancamento 43 em ENVIADO_SAP"),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.TentativaLocalPendente, r.Cenario);
        Assert.Null(r.ComoTriState());    // null = desconhecido ⇒ bloqueia
    }

    // ======================================================================
    // 18.4 — consulta SAP indisponível ou com falha ⇒ BLOQUEIA
    // ======================================================================

    [Fact]
    public void HistoricoSapIndisponivelOuComFalha_Bloqueia()
    {
        foreach (ResultadoHistoricoMaterialSap historico in new[]
        {
            ResultadoHistoricoMaterialSap.Indisponivel("contrato de leitura ausente"),
            ResultadoHistoricoMaterialSap.Falha("HTTP 500")
        })
        {
            ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
                ResultadoExclusividadeItemPedido.Obtida(),
                ResultadoRecebimentoLocal.SemRegistro(),
                historico);

            Assert.Equal(CenarioEvidenciaRecebimento.FalhaConsulta, r.Cenario);
            Assert.Null(r.ComoTriState());
        }
    }

    [Fact] // paginação interrompida é falha de consulta, não "sem histórico"
    public void PaginacaoIncompleta_Bloqueia()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: false, abrangenciaComprovada: true));

        Assert.Equal(CenarioEvidenciaRecebimento.FalhaConsulta, r.Cenario);
        Assert.Contains("paginação interrompida", r.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultaLocalIndisponivel_Bloqueia()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.Indisponivel("banco desabilitado"),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.FalhaConsulta, r.Cenario);
    }

    // ======================================================================
    // 18.5 — histórico incompleto (regra 15): ausência SEM abrangência ⇒ BLOQUEIA
    // ======================================================================

    [Fact]
    public void SemRegistrosComAbrangenciaNaoComprovada_NaoViraP5False()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            // Zero movimentos, paginação completa, PORÉM abrangência não comprovada.
            ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: true, abrangenciaComprovada: false));

        Assert.Equal(CenarioEvidenciaRecebimento.AbrangenciaNaoComprovada, r.Cenario);
        Assert.False(r.Libera);
        Assert.Null(r.ComoTriState());
        Assert.Contains("não é prova de ausência de recebimento", r.Motivo, StringComparison.Ordinal);
    }

    // ======================================================================
    // 18.6 — dois envios concorrentes do mesmo pedido+item ⇒ o segundo BLOQUEIA
    // ======================================================================

    [Fact]
    public void ExclusividadeEmUsoPorOutroLancamento_Bloqueia()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.EmUso("lancamento 44 detem a exclusividade"),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.TentativaLocalPendente, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    [Fact] // mecanismo durável ausente ⇒ BLOQUEIA (precede qualquer outra evidência)
    public void ExclusividadeIndisponivel_BloqueiaMesmoComDemaisEvidenciasFavoraveis()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Indisponivel("estrutura duravel ausente"),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.ExclusividadeIndisponivel, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    // ======================================================================
    // 18.7 — falha após o POST com resultado remoto desconhecido ⇒ BLOQUEIA
    // ======================================================================

    [Fact] // registro durável da tentativa em aberto impede reenvio cego
    public void TentativaDuravelEmAbertoAposFalhaPosPost_BloqueiaReenvio()
    {
        // Após um POST cujo desfecho não foi confirmado, a tentativa durável permanece em aberto e a
        // fonte local a reporta como PENDENTE — o reenvio cego fica bloqueado.
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.EmUso("tentativa duravel 7 em aberto apos POST"),
            ResultadoRecebimentoLocal.Pendente("lancamento 99 em ENVIADO_SAP sem desfecho"),
            ResultadoHistoricoMaterialSap.Falha("indeterminado apos POST"));

        Assert.NotEqual(CenarioEvidenciaRecebimento.SemRecebimentoComprovado, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    [Fact] // 101 integralmente estornado é INDETERMINADO: exige reconciliação, não libera
    public void Entrada101IntegralmenteEstornada_ExigeReconciliacao()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(1, 1, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado, r.Cenario);
        Assert.Contains("reconciliação", r.Motivo, StringComparison.Ordinal);
    }

    [Fact] // mais estornos que entradas = histórico inconsistente ⇒ INDETERMINADO
    public void MaisEstornosQueEntradas_Indeterminado()
    {
        ResultadoEvidenciaRecebimento r = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(1, 2, true, true));

        Assert.Equal(CenarioEvidenciaRecebimento.ResultadoRemotoIndeterminado, r.Cenario);
        Assert.Contains("inconsistente", r.Motivo, StringComparison.Ordinal);
    }

    // ======================================================================
    // Provider: defaults de produção são FAIL-CLOSED (regra 16)
    // ======================================================================

    [Fact] // P5_UNKNOWN_BLOCKED: sem fontes aprovadas, o provider de produção BLOQUEIA
    public async Task ProvedorPadraoDeProducao_Bloqueia()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new();

        ResultadoEvidenciaRecebimento r = await provedor.AvaliarAsync(Pedido, ItemPedido, Lancamento);

        Assert.Equal(CenarioEvidenciaRecebimento.ExclusividadeIndisponivel, r.Cenario);
        Assert.Null(await provedor.ExisteRecebimentoConfirmadoAsync(Pedido, ItemPedido, Lancamento));
    }

    [Fact] // todas as três fontes favoráveis ⇒ o provider libera (prova de que P5=FALSE é alcançável)
    public async Task ProvedorComAsTresFontesFavoraveis_Libera()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida()),
            new FakeLocal(ResultadoRecebimentoLocal.SemRegistro()),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        Assert.True((await provedor.AvaliarAsync(Pedido, ItemPedido, Lancamento)).Libera);
        Assert.False(await provedor.ExisteRecebimentoConfirmadoAsync(Pedido, ItemPedido, Lancamento));
    }

    [Fact] // exceção numa fonte NÃO vaza: vira evidência indisponível ⇒ bloqueia
    public async Task ExcecaoEmUmaFonte_ViraEvidenciaIndisponivelEBloqueia()
    {
        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida()),
            new FakeLocal(null, () => throw new InvalidOperationException("falha de banco")),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        ResultadoEvidenciaRecebimento r = await provedor.AvaliarAsync(Pedido, ItemPedido, Lancamento);

        Assert.Equal(CenarioEvidenciaRecebimento.FalhaConsulta, r.Cenario);
        Assert.Null(r.ComoTriState());
    }

    [Fact] // cancelamento NÃO vira evidência: sobe, preservando a fronteira do R3B
    public async Task Cancelamento_NaoViraEvidencia_ESobe()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ProvedorEvidenciaRecebimentoItemPedido provedor = new(
            new FakeExclusividade(ResultadoExclusividadeItemPedido.Obtida()),
            new FakeLocal(null, () => throw new OperationCanceledException(cts.Token)),
            new FakeHistorico(ResultadoHistoricoMaterialSap.Lido(0, 0, true, true)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provedor.AvaliarAsync(Pedido, ItemPedido, Lancamento, cts.Token));
    }

    [Fact] // os três serviços FAIL-CLOSED declaram indisponibilidade, nunca ausência
    public async Task ServicosIndisponiveis_DeclaramIndisponibilidade()
    {
        Assert.False(new HistoricoMaterialDocumentSapIndisponivelServico().Disponivel);
        Assert.False(new ExclusividadeItemPedidoIndisponivelServico().Disponivel);

        Assert.Equal(
            CenarioRecebimentoLocal.Indisponivel,
            (await new EvidenciaRecebimentoLocalIndisponivelServico()
                .ConsultarAsync(Pedido, ItemPedido, Lancamento)).Cenario);
        Assert.Equal(
            CenarioHistoricoMaterialSap.Indisponivel,
            (await new HistoricoMaterialDocumentSapIndisponivelServico()
                .ConsultarPorPedidoItemAsync(Pedido, ItemPedido)).Cenario);
        Assert.Equal(
            CenarioExclusividadeItemPedido.Indisponivel,
            (await new ExclusividadeItemPedidoIndisponivelServico()
                .TentarObterAsync(Pedido, ItemPedido, Lancamento)).Cenario);
    }

    [Fact] // nenhum cenário de bloqueio pode ser construído como liberação
    public void CenarioDeBloqueioNaoPodeLiberar()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ResultadoEvidenciaRecebimento.Bloqueado(
            CenarioEvidenciaRecebimento.SemRecebimentoComprovado, "tentativa indevida"));

    // ======================================================================
    // Fakes — sem HTTP, sem banco, sem POST
    // ======================================================================

    private sealed class FakeExclusividade(ResultadoExclusividadeItemPedido resultado)
        : IExclusividadeItemPedidoServico
    {
        public bool Disponivel => true;

        public Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
            string numeroPedido, string numeroItemPedido, long codigoLancamento,
            CancellationToken cancellationToken = default)
            => Task.FromResult(resultado);
    }

    private sealed class FakeLocal(ResultadoRecebimentoLocal? resultado, Func<ResultadoRecebimentoLocal>? aoConsultar = null)
        : IEvidenciaRecebimentoLocalServico
    {
        public Task<ResultadoRecebimentoLocal> ConsultarAsync(
            string numeroPedido, string numeroItemPedido, long codigoLancamentoAtual,
            CancellationToken cancellationToken = default)
            => Task.FromResult(aoConsultar is not null ? aoConsultar() : resultado!);
    }

    private sealed class FakeHistorico(ResultadoHistoricoMaterialSap resultado)
        : IHistoricoMaterialDocumentSapServico
    {
        public bool Disponivel => true;

        public Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
            string numeroPedido, string numeroItemPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(resultado);
    }
}
