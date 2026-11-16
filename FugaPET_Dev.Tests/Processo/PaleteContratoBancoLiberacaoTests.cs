using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 126L: os VALORES que o banco aceita nas primitivas de recovery do palete.
/// <para>
/// POR QUE ESTE ARQUIVO EXISTE: o servico de recovery mandava strings que
/// fn_pa_045_palete_registrar_reconciliacao e fn_pa_045_palete_liberar_reprocessamento NAO aceitam
/// ("CONFIRMADO_NO_SAP", "NAO_EXECUTADO_NO_SAP", "INDETERMINADO", "RECONCILIADO_AUSENTE_NO_SAP").
/// O banco levantava PA045_PALETE_RECONCILIACAO_INVALIDA e PA045_PALETE_LIBERACAO_INVALIDA.
/// </para>
/// <para>
/// O defeito atravessou a suite inteira porque os testes exercitavam o servico com operacoes
/// FALSAS, que aceitam qualquer string — o contrato real so existia no corpo das funcoes SQL.
/// Estes testes trazem esse contrato para o codigo: uma lista fechada, conferida contra as funcoes
/// do DEV110 em 16/11/2026, que quebra se alguem inventar um valor novo.
/// </para>
/// </summary>
public sealed class PaleteContratoBancoLiberacaoTests
{
    /// <summary>Valores aceitos por fn_pa_045_palete_registrar_reconciliacao.p_resultado.</summary>
    private static readonly string[] ResultadosAceitos =
        ["CONFIRMADO_EXISTE", "CONFIRMADO_NAO_EXISTE", "DIVERGENTE"];

    /// <summary>Valores aceitos por fn_pa_045_palete_liberar_reprocessamento.p_tipo_liberacao.</summary>
    private static readonly string[] TiposDeLiberacaoAceitos =
        ["POS_RECONCILIACAO_NAO_ENCONTRADO", "DECISAO_OPERACIONAL_FORMAL"];

    [Fact]
    public void TipoDeLiberacaoDaDecisaoOperacional_EAceitoPeloBanco()
        => Assert.Contains(
            ProdutoAcabadoController.TipoLiberacaoDecisaoOperacional, TiposDeLiberacaoAceitos);

    [Fact]
    public void TipoDeLiberacaoAposReconciliacao_EAceitoPeloBanco()
        => Assert.Contains(
            ProdutoAcabadoPaleteRecoveryServico.TipoLiberacaoAusenciaComprovada, TiposDeLiberacaoAceitos);

    /// <summary>
    /// O tipo usado apos reconciliacao NAO pode ser o da decisao operacional: aquele exige
    /// status_reconciliacao='CONFIRMADO_NAO_EXISTE', e e essa exigencia que amarra a liberacao a
    /// uma reconciliacao de verdade. Trocar um pelo outro passaria a compilar e a rodar, e
    /// silenciosamente removeria a amarra.
    /// </summary>
    [Fact]
    public void OsDoisCaminhosDeLiberacaoNaoSeConfundem()
        => Assert.NotEqual(
            ProdutoAcabadoController.TipoLiberacaoDecisaoOperacional,
            ProdutoAcabadoPaleteRecoveryServico.TipoLiberacaoAusenciaComprovada);

    /// <summary>
    /// Auditoria de fonte: o mapeamento de veredito para resultado tem de usar SOMENTE os tres
    /// valores aceitos. Olha o fonte porque o metodo e privado ao fluxo e so dispara com banco real.
    /// </summary>
    [Fact]
    public void MapeamentoDeVeredicto_UsaSomenteResultadosAceitos()
    {
        string fonte = LerFonte("Servicos", "Operacao", "ProdutoAcabadoPaleteRecoveryServico.cs");
        int inicio = fonte.IndexOf("string resultadoTexto", StringComparison.Ordinal);
        Assert.True(inicio > 0, "o mapeamento de veredito precisa existir no fonte");

        string bloco = fonte[inicio..fonte.IndexOf("};", inicio, StringComparison.Ordinal)];

        foreach (string aceito in ResultadosAceitos)
        {
            Assert.Contains($"\"{aceito}\"", bloco, StringComparison.Ordinal);
        }

        // Os valores antigos, recusados pelo banco, nao podem voltar.
        foreach (string recusado in new[] { "CONFIRMADO_NO_SAP", "NAO_EXECUTADO_NO_SAP", "\"INDETERMINADO\"" })
        {
            Assert.DoesNotContain(recusado, bloco, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A liberacao do palete em ERRO_SAP NAO pode passar por ReconciliarAsync: as tres ramificacoes
    /// de fn_pa_045_palete_registrar_reconciliacao exigem status INDETERMINADO_TIMEOUT, entao a
    /// chamada nao teria efeito nenhum sobre um palete recusado.
    /// </summary>
    [Fact]
    public void LiberacaoDeErroSap_NaoPassaPorReconciliacao()
    {
        string fonte = LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs");
        int inicio = fonte.IndexOf("public async Task<ResultadoRecoveryPalete> LiberarReenvioPaleteAsync",
            StringComparison.Ordinal);
        Assert.True(inicio > 0);

        string metodo = fonte[inicio..fonte.IndexOf("internal const string TipoLiberacaoDecisaoOperacional",
            inicio, StringComparison.Ordinal)];

        // Procura a CHAMADA, nao a mencao: o proprio metodo explica em comentario por que nao
        // usa a reconciliacao, e um DoesNotContain cru reprovaria o texto que documenta a decisao.
        Assert.DoesNotContain("_recoveryPalete.ReconciliarAsync(", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain(".ReconciliarAsync(", metodo, StringComparison.Ordinal);
        Assert.Contains("AdquirirRecoveryPaleteAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("LiberarReprocessamentoPaleteAsync", metodo, StringComparison.Ordinal);
    }

    /// <summary>
    /// GATE 126L: o guarda que o orquestrador consulta tem de enxergar a liberacao. Classificar so
    /// pelo status deixava o palete bloqueado DEPOIS de liberado — o banco deixava enviar e o C#
    /// nao. Foi o ultimo elo que faltava, e so apareceu na tela.
    /// </summary>
    [Theory]
    [InlineData("ERRO_SAP")]
    [InlineData("BLOQUEADO")]
    public void ComReprocessamentoLiberado_OGuardaPermiteOEnvio(string status)
    {
        PendenciaPalete p = ProdutoAcabadoPaleteRecoveryServico.ClassificarPendencia(
            [LinhaEstado(status, podeReprocessar: true)]);

        Assert.Equal(PendenciaPalete.LiberadoParaReenvio, p);
        Assert.True(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(p));
    }

    /// <summary>Sem liberacao, ERRO_SAP continua exigindo a decisao explicita.</summary>
    [Fact]
    public void SemLiberacao_ErroSapContinuaBloqueado()
    {
        PendenciaPalete p = ProdutoAcabadoPaleteRecoveryServico.ClassificarPendencia(
            [LinhaEstado("ERRO_SAP", podeReprocessar: false)]);

        Assert.Equal(PendenciaPalete.PendenteLiberacao, p);
        Assert.False(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(p));
    }

    /// <summary>
    /// A flag NAO pode destravar os estados que nunca admitem segundo POST. Este e o teste que
    /// impede a correcao de virar uma porta dos fundos.
    /// </summary>
    [Theory]
    [InlineData("ENVIADO_SAP")]
    [InlineData("CONFIRMADO_SAP")]
    [InlineData("CANCELADO")]
    [InlineData("INDETERMINADO_TIMEOUT")]
    [InlineData("")]
    public void ReprocessamentoLiberadoNaoDestravaOutrosEstados(string status)
        => Assert.False(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(
            ProdutoAcabadoPaleteRecoveryServico.ClassificarPendencia(
                [LinhaEstado(status, podeReprocessar: true)])));

    [Fact]
    public void EstadoAusenteOuIlegivel_ContinuaFailClosed()
    {
        Assert.False(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(
            ProdutoAcabadoPaleteRecoveryServico.ClassificarPendencia(null)));
        Assert.False(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(
            ProdutoAcabadoPaleteRecoveryServico.ClassificarPendencia([])));
    }

    private static AcessoDados.Repositorio.Linha045 LinhaEstado(string status, bool podeReprocessar)
        => new(new Dictionary<string, object?>
        {
            ["status_hu_palete"] = status,
            ["pode_reprocessar"] = podeReprocessar
        });

    private static string LerFonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && !File.Exists(Path.Combine(raiz, "FugaPET_Dev.csproj")); i++)
        {
            raiz = Path.GetFullPath(Path.Combine(raiz, ".."));
        }

        return File.ReadAllText(Path.Combine(new[] { raiz }.Concat(partes).ToArray()));
    }
}
