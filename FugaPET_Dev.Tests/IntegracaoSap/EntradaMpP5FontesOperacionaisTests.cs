using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// P5/056: fontes operacionais do recebimento ROH — exclusividade durável por pedido+item,
/// evidência local pela consulta D2 e atestação humana de abrangência.
/// <para>
/// O que estes testes protegem: nenhuma das três pode liberar por omissão, e a atestação
/// cobre apenas a dúvida sobre COBERTURA da consulta — jamais a existência de um recebimento.
/// </para>
/// </summary>
public sealed class EntradaMpP5FontesOperacionaisTests
{
    private static DateTimeOffset Agora => new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static AtestacaoAbrangenciaRecebimento Atestacao(
        string pedido = "4500000071", string item = "00010", int horasValidade = 8)
        => new(pedido, item, "operador.teste", "conferência manual no SAP antes do teste assistido",
            Agora.AddHours(horasValidade));

    private static ResultadoHistoricoMaterialSap HistoricoLimpo()
        => ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: true, abrangenciaComprovada: false);

    // ---------------------------------------------------------------- exclusividade

    [Theory]
    [InlineData("4500000071", "10", "00010")]
    [InlineData("4500000071", "00010", "00010")]
    [InlineData("4500000071", "0", "00000")]
    public void ExclusividadeNormalizaItemEmCincoDigitos(string pedido, string item, string esperado)
    {
        Assert.True(ExclusividadeItemPedidoDuravelServico.TentarNormalizar(
            pedido, item, 1, out string pedidoNorm, out string itemNorm, out _));
        Assert.Equal(pedido, pedidoNorm);
        Assert.Equal(esperado, itemNorm);
    }

    [Theory]
    [InlineData("", "10", 1L)]
    [InlineData("450000007X", "10", 1L)]
    [InlineData("4500000071", "ABC", 1L)]
    [InlineData("4500000071", "10", 0L)]
    [InlineData("4500000071", "10", -5L)]
    public void ExclusividadeRecusaChaveMalformada(string pedido, string item, long lancamento)
    {
        Assert.False(ExclusividadeItemPedidoDuravelServico.TentarNormalizar(
            pedido, item, lancamento, out _, out _, out string motivo));
        Assert.False(string.IsNullOrWhiteSpace(motivo));
    }

    [Fact]
    public async Task ExclusividadeComBancoDesabilitadoBloqueia()
    {
        var servico = new ExclusividadeItemPedidoDuravelServico(
            new FabricaConexaoNaoChamada(), bancoHabilitado: false);
        Assert.False(servico.Disponivel);
        ResultadoExclusividadeItemPedido resultado =
            await servico.TentarObterAsync("4500000071", "00010", 1, CancellationToken.None);
        Assert.Equal(CenarioExclusividadeItemPedido.Indisponivel, resultado.Cenario);
    }

    // ---------------------------------------------------------------- evidência local

    [Fact]
    public void LocalSemEntradaOuPreparacaoCorrenteNaoBloqueia()
    {
        Assert.Equal(CenarioRecebimentoLocal.SemRegistro,
            EvidenciaRecebimentoLocalServico.Classificar(["SEM_ENTRADA_LOCAL_NAO_PROVA_SAP"]).Cenario);
        Assert.Equal(CenarioRecebimentoLocal.SemRegistro,
            EvidenciaRecebimentoLocalServico.Classificar(["PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO"]).Cenario);
    }

    [Theory]
    [InlineData("RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK", CenarioRecebimentoLocal.Confirmado)]
    [InlineData("ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK", CenarioRecebimentoLocal.Pendente)]
    [InlineData("CANCELAMENTO_LOCAL_SEM_PROVA_ESTORNO_BLOCK", CenarioRecebimentoLocal.Pendente)]
    [InlineData("OUTRO_LANCAMENTO_LOCAL_BLOCK", CenarioRecebimentoLocal.Pendente)]
    [InlineData("CHAVE_INVALIDA_BLOCK", CenarioRecebimentoLocal.Indisponivel)]
    [InlineData("STATUS_NAO_CLASSIFICADO_BLOCK", CenarioRecebimentoLocal.Indisponivel)]
    public void LocalTraduzCadaCategoriaDoContrato(string categoria, CenarioRecebimentoLocal esperado)
        => Assert.Equal(esperado, EvidenciaRecebimentoLocalServico.Classificar([categoria]).Cenario);

    [Fact]
    public void LocalMantemACategoriaMaisRestritiva()
    {
        Assert.Equal(CenarioRecebimentoLocal.Confirmado,
            EvidenciaRecebimentoLocalServico.Classificar(
                ["SEM_ENTRADA_LOCAL_NAO_PROVA_SAP", "RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK"]).Cenario);
        Assert.Equal(CenarioRecebimentoLocal.Confirmado,
            EvidenciaRecebimentoLocalServico.Classificar(
                ["ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK", "RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK"]).Cenario);
    }

    [Fact]
    public void LocalTrataCategoriaDesconhecidaOuAusenciaDeLinhaComoIndisponivel()
    {
        // Uma categoria nova no SQL nunca deve liberar por ser nova.
        Assert.Equal(CenarioRecebimentoLocal.Indisponivel,
            EvidenciaRecebimentoLocalServico.Classificar(["CATEGORIA_QUE_AINDA_NAO_EXISTE"]).Cenario);
        Assert.Equal(CenarioRecebimentoLocal.Indisponivel,
            EvidenciaRecebimentoLocalServico.Classificar([]).Cenario);
    }

    [Fact]
    public void ConsultaLocalNaoContemEscritaNemPrepareGlobal()
    {
        string sql = EvidenciaRecebimentoLocalServico.ConsultaD2;
        foreach (string proibido in new[] { "INSERT", "UPDATE", "DELETE", "PREPARE", "CREATE", "DROP" })
            Assert.DoesNotContain(proibido, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("categoria_local_p5", sql, StringComparison.Ordinal);
        Assert.Contains("@pedido", sql, StringComparison.Ordinal);
        Assert.Contains("@item", sql, StringComparison.Ordinal);
        Assert.Contains("@lancamento", sql, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- atestação

    [Fact]
    public void AtestacaoExigeTodosOsCamposEValidade()
    {
        Assert.Null(AtestacaoAbrangenciaRecebimento.Interpretar("{}"));
        Assert.Null(AtestacaoAbrangenciaRecebimento.Interpretar("nao e json"));
        Assert.Null(AtestacaoAbrangenciaRecebimento.Interpretar(
            """{"numeroPedido":"4500000071","numeroItem":"00010","operador":"x","motivo":"y"}"""));

        AtestacaoAbrangenciaRecebimento? lida = AtestacaoAbrangenciaRecebimento.Interpretar(
            """
            {"numeroPedido":"4500000071","numeroItem":"00010","operador":"michael",
             "motivo":"conferido no SAP","validoAteUtc":"2026-11-03T23:00:00Z"}
            """);
        Assert.NotNull(lida);
        Assert.Equal("michael", lida!.Operador);
    }

    [Fact]
    public void AtestacaoValeApenasParaOItemAtestadoEDentroDoPrazo()
    {
        AtestacaoAbrangenciaRecebimento a = Atestacao();
        Assert.True(a.Vale("4500000071", "10", Agora));
        Assert.True(a.Vale("4500000071", "00010", Agora));
        Assert.False(a.Vale("4500000071", "00020", Agora));
        Assert.False(a.Vale("4500000099", "00010", Agora));
        Assert.False(a.Vale("4500000071", "00010", Agora.AddHours(9)));
    }

    [Fact]
    public async Task AtestacaoComprovaAbrangenciaSomenteComHistoricoLimpo()
    {
        var servico = new HistoricoMaterialDocumentSapAtestadoServico(
            new HistoricoFixo(HistoricoLimpo()), Atestacao(), agoraUtc: () => Agora);

        ResultadoHistoricoMaterialSap resultado =
            await servico.ConsultarPorPedidoItemAsync("4500000071", "00010", CancellationToken.None);

        Assert.True(resultado.AbrangenciaComprovada);
        Assert.Equal(CenarioHistoricoMaterialSap.Lido, resultado.Cenario);
        Assert.Equal(0, resultado.Documentos101);
    }

    [Fact]
    public async Task AtestacaoNaoEncobreMovimentoExistenteNemLeituraIncompleta()
    {
        ResultadoHistoricoMaterialSap[] casos =
        [
            ResultadoHistoricoMaterialSap.Lido(1, 0, true, false),          // ja existe 101
            ResultadoHistoricoMaterialSap.Lido(1, 1, true, false),          // 101 estornado
            ResultadoHistoricoMaterialSap.Lido(0, 1, true, false),          // 102 sem 101
            ResultadoHistoricoMaterialSap.Lido(0, 0, false, false),         // paginacao incompleta
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, false, true),    // documento incerto
            ResultadoHistoricoMaterialSap.Falha("timeout"),
            ResultadoHistoricoMaterialSap.Indisponivel("sem contrato"),
        ];

        foreach (ResultadoHistoricoMaterialSap caso in casos)
        {
            var servico = new HistoricoMaterialDocumentSapAtestadoServico(
                new HistoricoFixo(caso), Atestacao(), agoraUtc: () => Agora);
            ResultadoHistoricoMaterialSap resultado =
                await servico.ConsultarPorPedidoItemAsync("4500000071", "00010", CancellationToken.None);
            Assert.False(resultado.AbrangenciaComprovada);
        }
    }

    [Fact]
    public async Task SemAtestacaoOuComAtestacaoVencidaOBloqueioPermanece()
    {
        var sem = new HistoricoMaterialDocumentSapAtestadoServico(
            new HistoricoFixo(HistoricoLimpo()), atestacao: null, agoraUtc: () => Agora);
        Assert.False((await sem.ConsultarPorPedidoItemAsync("4500000071", "00010", CancellationToken.None))
            .AbrangenciaComprovada);

        var vencida = new HistoricoMaterialDocumentSapAtestadoServico(
            new HistoricoFixo(HistoricoLimpo()), Atestacao(horasValidade: -1), agoraUtc: () => Agora);
        Assert.False((await vencida.ConsultarPorPedidoItemAsync("4500000071", "00010", CancellationToken.None))
            .AbrangenciaComprovada);
    }

    // ---------------------------------------------------------------- ponta a ponta

    [Fact]
    public void AsTresFontesOperacionaisLiberamOEnvio()
    {
        ResultadoEvidenciaRecebimento decisao = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: true, abrangenciaComprovada: true));

        Assert.True(decisao.Libera);
        Assert.Equal(CenarioEvidenciaRecebimento.SemRecebimentoComprovado, decisao.Cenario);
        Assert.False(decisao.ComoTriState());
    }

    [Fact]
    public void SemAbrangenciaOEnvioContinuaBloqueadoMesmoComAsOutrasDuas()
    {
        ResultadoEvidenciaRecebimento decisao = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.Obtida(),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, paginacaoCompleta: true, abrangenciaComprovada: false));

        Assert.False(decisao.Libera);
        Assert.Equal(CenarioEvidenciaRecebimento.AbrangenciaNaoComprovada, decisao.Cenario);
        Assert.Null(decisao.ComoTriState());
    }

    [Fact]
    public void ExclusividadeDeOutroLancamentoBloqueiaAntesDeQualquerOutraFonte()
    {
        ResultadoEvidenciaRecebimento decisao = AvaliadorEvidenciaRecebimentoItemPedido.Avaliar(
            ResultadoExclusividadeItemPedido.EmUso("lancamento 99 detem o item"),
            ResultadoRecebimentoLocal.SemRegistro(),
            ResultadoHistoricoMaterialSap.Lido(0, 0, true, true));

        Assert.False(decisao.Libera);
        Assert.Equal(CenarioEvidenciaRecebimento.TentativaLocalPendente, decisao.Cenario);
    }

    // ---------------------------------------------------------------- auxiliares

    private sealed class HistoricoFixo(ResultadoHistoricoMaterialSap resultado)
        : IHistoricoMaterialDocumentSapServico
    {
        public bool Disponivel => true;

        public Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
            string numeroPedido, string numeroItemPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(resultado);
    }

    private sealed class FabricaConexaoNaoChamada : AcessoDados.Banco.IFabricaConexaoBanco
    {
        public Npgsql.NpgsqlConnection CriarConexao()
            => throw new InvalidOperationException("a conexao nao deveria ser aberta");

        public Task<Npgsql.NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("a conexao nao deveria ser aberta");

        public string ObterConnectionString()
            => throw new InvalidOperationException("a conexao nao deveria ser aberta");
    }
}
