using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 103Y — correção fail-closed: um envio 101 recusado por duplicidade (EnvioDuplicadoBloqueado)
/// NUNCA conclui o apontamento, nem quando o lançamento persistido consultado está CONFIRMADO_SAP.
/// Só o caminho NORMAL do 101 (sucesso inequívoco) devolve ConfirmadoSap.
///
/// A decisão do resultado do apontamento no caminho duplicado é isolada em
/// <see cref="ProcessoSemiAcabadoForm.ResolverResultadoApontamentoEnvioDuplicadoBloqueado"/>
/// (internal static, testável direto sem banco/SAP).
/// </summary>
public sealed class ProcessoSemiAcabadoDuplicateBlock103YTests
{
    // ---------- decisão pura do caminho duplicado (§7 / §8.C-D-E) ----------

    // §8.C — EnvioDuplicadoBloqueado + persistido CONFIRMADO_SAP NÃO conclui.
    [Fact]
    public void Duplicado_ConfirmadoSap_NaoConclui()
    {
        ResultadoExecucaoProcesso r = ProcessoSemiAcabadoForm
            .ResolverResultadoApontamentoEnvioDuplicadoBloqueado("CONFIRMADO_SAP", 4242, "duplicado");

        Assert.Equal(ResultadoExecucaoProcessoApontamento.NaoConcluido, r.Resultado);
        Assert.False(r.AtividadeConcluida);
        Assert.False(r.IndicadorConfirmadoSap);
    }

    // §8.D — duplicado + DIVERGENCIA_SAP não conclui (reflete DivergenciaSap).
    [Fact]
    public void Duplicado_DivergenciaSap_NaoConclui()
    {
        ResultadoExecucaoProcesso r = ProcessoSemiAcabadoForm
            .ResolverResultadoApontamentoEnvioDuplicadoBloqueado("DIVERGENCIA_SAP", 10, "divergência");

        Assert.Equal(ResultadoExecucaoProcessoApontamento.DivergenciaSap, r.Resultado);
        Assert.False(r.AtividadeConcluida);
        Assert.False(r.IndicadorConfirmadoSap);
    }

    // §8.E — duplicado + ERRO_SAP não conclui (reflete ErroSap).
    [Fact]
    public void Duplicado_ErroSap_NaoConclui()
    {
        ResultadoExecucaoProcesso r = ProcessoSemiAcabadoForm
            .ResolverResultadoApontamentoEnvioDuplicadoBloqueado("ERRO_SAP", 11, "erro");

        Assert.Equal(ResultadoExecucaoProcessoApontamento.ErroSap, r.Resultado);
        Assert.False(r.AtividadeConcluida);
        Assert.False(r.IndicadorConfirmadoSap);
    }

    // §5 — ENVIANDO_SAP / default não concluem e nunca viram ConfirmadoSap.
    [Theory]
    [InlineData("ENVIANDO_SAP")]
    [InlineData("")]
    [InlineData("QUALQUER_OUTRO")]
    public void Duplicado_OutrosStatus_NaoConcluem(string status)
    {
        ResultadoExecucaoProcesso r = ProcessoSemiAcabadoForm
            .ResolverResultadoApontamentoEnvioDuplicadoBloqueado(status, 99, "msg");

        Assert.NotEqual(ResultadoExecucaoProcessoApontamento.ConfirmadoSap, r.Resultado);
        Assert.False(r.AtividadeConcluida);
    }

    // ---------- contrato de fonte (§4 / §6) ----------

    // O método do caminho duplicado NÃO pode promover o apontamento a ConfirmadoSap.
    [Fact]
    public void TratarEnvioDuplicadoBloqueado_NaoPromoveConfirmadoSap()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task TratarEnvioDuplicadoBloqueadoAsync");

        Assert.DoesNotContain("ResultadoExecucaoProcessoApontamento.ConfirmadoSap", metodo, StringComparison.Ordinal);
        Assert.Contains("ResolverResultadoApontamentoEnvioDuplicadoBloqueado", metodo, StringComparison.Ordinal);
    }

    // §6 — o caminho NORMAL do 101 permanece como único que conclui com ConfirmadoSap + PK real.
    [Fact]
    public void CaminhoNormal101_PreservaConfirmadoSapComPkReal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync");

        Assert.Contains("resultado.CodigoLancamento is > 0", metodo, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConfirmadoSap", metodo, StringComparison.Ordinal);
        Assert.Contains("resultado.CodigoLancamento,", metodo, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do método não encontrado: {assinatura}");

        return fonte[inicio..proximoMetodo];
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
