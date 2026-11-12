using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ControleApontamentosWorkCenterCode05Tests
{
    [Fact]
    public void WC01_RotaLocalUsaPlantEWorkCenterSemOperacao()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");
        string metodo = ExtrairMetodo(repo, "public async Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoRotaPorWorkCenterAsync");

        Assert.Contains("AND centro = @centro", metodo, StringComparison.Ordinal);
        Assert.Contains("AND centro_trabalho = @centro_trabalho", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("operacao_sap =", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suboperacao_sap =", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WC02_ServicoNaoUsaMaisLookupPorOperacaoParaDestino()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        Assert.Contains("ObterConfiguracaoRotaPorWorkCenterAsync", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("ObterConfiguracaoOperacaoAsync(\n            ordemSap.Centro", servico.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void WC03_PredecessorNaoDependeDeExigeOperacaoAnterior()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");
        string inicio = ExtrairMetodo(servico, "private async Task<ResultadoLeituraApontamento> ProcessarInicioAsync");

        Assert.Contains("ObterOperacaoAnterior(ordemSap, operacao)", inicio, StringComparison.Ordinal);
        Assert.DoesNotContain("configuracao.ExigeOperacaoAnterior", inicio, StringComparison.Ordinal);
    }

    [Fact]
    public void WC04_ResultadoApontamentoListaDefinicoesPorPerfil()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");
        string metodo = ExtrairMetodo(repo, "public async Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync");

        Assert.Contains("codigo_perfil_resultado = @codigo_perfil_resultado", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("operacao_sap", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tipo_processo", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suboperacao_sap", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WC05_ContextoCarregaCodigoPerfilResultado()
    {
        string contexto = LerArquivoProjeto("Modelo", "Processo", "ContextoApontamentoProcesso.cs");
        string apontamento = LerArquivoProjeto("Modelo", "Processo", "OperacaoProducaoApontamento.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        Assert.Contains("public long? CodigoPerfilResultado", contexto, StringComparison.Ordinal);
        Assert.Contains("public long? CodigoPerfilResultado", apontamento, StringComparison.Ordinal);
        Assert.Contains("CodigoPerfilResultado = apontamento.CodigoPerfilResultado", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void WC06_WorkCenterSapUsaInternalIdETypeCode()
    {
        string client = LerArquivoProjeto("Servicos", "IntegracaoSap", "WorkCenterSapApiClient.cs");

        Assert.Contains("WorkCenterInternalID", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenterTypeCode", client, StringComparison.Ordinal);
        Assert.Contains("A_WorkCenters", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenterIsToBeDeleted", client, StringComparison.Ordinal);
    }

    [Fact]
    public void WC07_WorkCenterFalhaFechadaSemDadosMestres()
    {
        string client = LerArquivoProjeto("Servicos", "IntegracaoSap", "WorkCenterSapApiClient.cs");

        Assert.Contains("WorkCenterInternalID ausente", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenterTypeCode ausente", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenter master não encontrado", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenter master ambíguo", client, StringComparison.Ordinal);
        Assert.Contains("marcado para eliminação", client, StringComparison.Ordinal);
    }

    [Fact]
    public void WC08_WorkCenterDivergenciaPlantOuCentroBloqueia()
    {
        string client = LerArquivoProjeto("Servicos", "IntegracaoSap", "WorkCenterSapApiClient.cs");

        Assert.Contains("Plant divergente", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenter divergente", client, StringComparison.Ordinal);
        Assert.Contains("Plant do WorkCenter ausente", client, StringComparison.Ordinal);
        Assert.Contains("WorkCenter ausente no master", client, StringComparison.Ordinal);
    }

    [Fact]
    public void WC09_OrdinalWorkCenterContaOcorrenciaNaSequenciaTecnicaGlobal()
    {
        OrdemProducaoSap ordem = new()
        {
            Centro = "3007",
            Operacoes =
            [
                Operacao("0010", "0050", "10"),
                Operacao("0020", "0070", "11"),
                Operacao("0030", "0100", "10"),
                Operacao("0040", "0105", "10")
            ]
        };

        Assert.Equal(1, ProcessoControleApontamentosServico.CalcularOrdemOcorrenciaWorkCenter(ordem, ordem.Operacoes[0]));
        Assert.Equal(2, ProcessoControleApontamentosServico.CalcularOrdemOcorrenciaWorkCenter(ordem, ordem.Operacoes[2]));
        Assert.Equal(3, ProcessoControleApontamentosServico.CalcularOrdemOcorrenciaWorkCenter(ordem, ordem.Operacoes[3]));
    }

    [Fact]
    public void WC10_AnteriorManualUsaListaManualFiltradaGlobal()
    {
        OrdemProducaoSap ordemManual = new()
        {
            Centro = "3007",
            Operacoes =
            [
                Operacao("0050", "0050", "10"),
                Operacao("0070", "0070", "20"),
                Operacao("0090", "0090", "10")
            ]
        };

        OperacaoOrdemProducaoSap? anterior = ProcessoControleApontamentosServico.ObterOperacaoAnterior(
            ordemManual, ordemManual.Operacoes[2]);

        Assert.NotNull(anterior);
        Assert.Equal("0070", anterior.Operacao);
    }

    [Fact]
    public void WC11_ProfileQueryUsaRotaEOcorrenciaWorkCenter()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");
        string metodo = ExtrairMetodo(repo, "public async Task<ResultadoPerfilResultado> ObterPerfilResultadoAsync");

        Assert.Contains("codigo_configuracao_rota = @codigo_configuracao_rota", metodo, StringComparison.Ordinal);
        Assert.Contains("ordem_ocorrencia_workcenter = @ordem_ocorrencia_workcenter", metodo, StringComparison.Ordinal);
        Assert.Contains("codigo_perfil_resultado", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void WC12_BarcodePermaneceUsadoSomenteParaOperacaoRealSap()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        Assert.Contains("ResolverOperacao(ordemSap, codigo.Operacao)", servico, StringComparison.Ordinal);
        // GATE 095F / seam 095E-R1: o Marcador recebe o roteiro REDUZIDO (pós-correlação Op+Plant+WorkCenter),
        // não mais o roteiro completo. O barcode continua sendo usado só para resolver a operação real.
        Assert.Contains("ClassificarOperacao(correlacao.RoteiroReduzido, operacao.Operacao)", servico, StringComparison.Ordinal);
        Assert.Contains("ObterConfiguracaoRotaPorWorkCenterAsync", servico, StringComparison.Ordinal);
    }

    private static OperacaoOrdemProducaoSap Operacao(string sequencia, string operacao, string workCenter)
        => new()
        {
            Sequencia = sequencia,
            Operacao = operacao,
            Suboperacao = string.Empty,
            Centro = "3007",
            CentroTrabalho = workCenter,
            WorkCenterInternalId = $"ID-{workCenter}",
            WorkCenterTypeCode = "A"
        };

    private static string LerArquivoProjeto(params string[] partes)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            string candidato = Path.Combine(dir, "FugaPET_Dev.csproj");
            if (File.Exists(candidato))
            {
                return File.ReadAllText(Path.Combine(new[] { dir }.Concat(partes).ToArray()));
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Projeto FugaPET_Dev não localizado para source-scan.");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");

        int abre = fonte.IndexOf('{', inicio);
        Assert.True(abre >= 0, $"Corpo não encontrado: {assinatura}");

        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
            {
                profundidade++;
            }
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte[inicio..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Fim do método não encontrado: {assinatura}");
    }
}