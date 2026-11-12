using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ControleApontamentosResultadoSemiAcabadoTests
{
    [Fact]
    public void ResultadoApontamento_DeveTerTelaEFechamentoNaoConcluido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.Contains("public partial class ProcessoResultadoApontamentoForm : Form", form, StringComparison.Ordinal);
        Assert.Contains("ContextoApontamentoProcesso contexto", form, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcesso ResultadoExecucaoApontamento", form, StringComparison.Ordinal);
        Assert.Contains("= ResultadoExecucaoProcesso.NaoConcluido;", form, StringComparison.Ordinal);
        Assert.Contains("Close();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DeveCarregarDefinicaoSemReferenciaHardcoded()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");

        Assert.Contains("CodigoPerfilResultado", servico, StringComparison.Ordinal);
        Assert.Contains("ListarDefinicoesAsync(codigoPerfilResultado", servico, StringComparison.Ordinal);
        Assert.Contains("operacao_resultado_definicao", repositorio, StringComparison.Ordinal);
        Assert.Contains("referencia", repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("Referencia = 5m", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("TEMPO_MINUTO_PADRAO", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DevePersistirHeaderItensEmTransacaoERetornarCodigo()
    {
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", repositorio, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO operacao_resultado_registro", repositorio, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO operacao_resultado_registro_item", repositorio, StringComparison.Ordinal);
        Assert.Contains("RETURNING codigo_resultado", repositorio, StringComparison.Ordinal);
        Assert.Contains("resultado.IdGerado", form, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DeveEntrarNoFluxoAtualDeVinculo1N()
    {
        string controleForm = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        Assert.Contains("TipoProcessoOperacao.ResultadoApontamento => AbrirResultadoApontamento(contexto)", controleForm, StringComparison.Ordinal);
        Assert.Contains("using ProcessoResultadoApontamentoForm form = new(contexto);", controleForm, StringComparison.Ordinal);
        Assert.Contains("RegistrarVinculoProcessoAsync", servico, StringComparison.Ordinal);
        Assert.Contains("apontamento?.TipoProcesso", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_PerfilAusenteContinuaFailClosed()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");
        string controle = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");

        Assert.Contains("contexto.CodigoPerfilResultado is not long codigoPerfilResultado", servico, StringComparison.Ordinal);
        Assert.Contains("PERFIL_NAO_RESOLVIDO", servico, StringComparison.Ordinal);
        Assert.Contains("PERFIL_RESULTADO_NAO_RESOLVIDO", controle, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_PerfilAtivoZeroDefinicoesHabilitaRegistroHeaderOnly()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

        Assert.Contains("_definicoesCarregadasComPerfilResolvido = _contexto.CodigoPerfilResultado is long", form, StringComparison.Ordinal);
        Assert.Contains("bool permiteHeaderOnly = !possuiDefinicoes && _definicoesCarregadasComPerfilResolvido", form, StringComparison.Ordinal);
        Assert.Contains("gravarResultadoButton.Enabled = possuiDefinicoes || permiteHeaderOnly", form, StringComparison.Ordinal);
        Assert.Contains("Nenhum resultado a informar para esta operação.", form, StringComparison.Ordinal);
        Assert.Contains("itens.Count == 0", servico, StringComparison.Ordinal);
        Assert.Contains("MensagemNenhumResultadoAInformar", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_ErroCarregamentoNaoViraZeroDefinicoes()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

        Assert.Contains("_definicoesCarregadasComPerfilResolvido = false", form, StringComparison.Ordinal);
        Assert.Contains("throw;", servico, StringComparison.Ordinal);
        Assert.Contains("ERRO AO CARREGAR", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_HeaderOnlyPersisteSemItensEValorFicticio()
    {
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

        Assert.Contains("INSERT INTO operacao_resultado_registro", repositorio, StringComparison.Ordinal);
        Assert.Contains("foreach (ResultadoApontamentoItem item in registro.Itens", repositorio, StringComparison.Ordinal);
        Assert.Contains("Itens = itens.OrderBy(item => item.OrdemExibicao).ToArray()", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("TEMPO_OPERACAO", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("Referencia = 5m", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DefinicaoObrigatoriaPermaneceFailClosed()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

        Assert.Contains("if (item.Obrigatorio && item.Resultado is null)", servico, StringComparison.Ordinal);
        Assert.Contains("Informe o resultado para", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_AgregadoUsaRegistroLocalDaOcorrenciaSemSap()
    {
        string controle = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");
        string avaliador = LerArquivoProjeto("Servicos", "Processo", "AvaliadorConclusaoAgregada.cs");
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("TipoProcessoOperacao.ResultadoApontamento", controle, StringComparison.Ordinal);
        Assert.Contains("AvaliarResultadoApontamento(vinculos)", controle, StringComparison.Ordinal);
        Assert.Contains("EhResultadoApontamentoTerminal", avaliador, StringComparison.Ordinal);
        Assert.Contains("processo.CodigoRegistroProcesso > 0", avaliador, StringComparison.Ordinal);
        Assert.Contains("WHERE v.codigo_apontamento = @codigo", repositorio, StringComparison.Ordinal);

        int indiceBranchResultado = controle.IndexOf(
            "if (string.Equals(apontamento.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))",
            StringComparison.Ordinal);
        int indiceRetornoLocal = controle.IndexOf(
            "return AvaliadorConclusaoAgregada.AvaliarResultadoApontamento(vinculos);",
            StringComparison.Ordinal);
        int indiceConsultaSap = controle.IndexOf(
            "ConsultarOrdemAsync(apontamento.NumeroOrdem",
            StringComparison.Ordinal);

        Assert.True(indiceBranchResultado >= 0);
        Assert.True(indiceRetornoLocal > indiceBranchResultado);
        Assert.True(indiceConsultaSap > indiceRetornoLocal);
        Assert.True(
            indiceRetornoLocal < indiceConsultaSap,
            "RESULTADO_APONTAMENTO deve retornar pelo agregado local antes de qualquer consulta SAP.");
    }

    [Fact]
    public void ResultadoApontamento_RegistroLocalTerminalConcluiAgregado()
    {
        ApontamentoProcesso vinculo = new()
        {
            CodigoApontamento = 100,
            TipoProcesso = TipoProcessoOperacao.ResultadoApontamento,
            CodigoRegistroProcesso = 200
        };

        Assert.Equal(
            EstadoConclusaoAgregada.Concluida,
            AvaliadorConclusaoAgregada.AvaliarResultadoApontamento([vinculo]));
    }

    [Fact]
    public void ResultadoApontamento_SemRegistroNaoConcluiAgregado()
    {
        Assert.Equal(
            EstadoConclusaoAgregada.Pendente,
            AvaliadorConclusaoAgregada.AvaliarResultadoApontamento([]));
    }

    [Fact]
    public void ResultadoApontamento_RegistroDeOutraOcorrenciaNaoPodeSatisfazer()
    {
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("WHERE v.codigo_apontamento = @codigo", repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(codigo_resultado)", repositorio, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY registrado_em DESC", repositorio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultadoApontamento_NaoAdicionaSapWrite()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");

        Assert.DoesNotContain("IntegracaoSap", form, StringComparison.Ordinal);
        Assert.DoesNotContain("IntegracaoSap", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarDocumento", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", repositorio, StringComparison.Ordinal);
    }
    [Fact]
    public void Saida0140_PreservaPesagemImpressaoESap101Existentes()
    {
        string semiForm = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string impressao = LerArquivoProjeto("Servicos", "Operacao", "ImpressaoSemiAcabadoServico.cs");
        string servico = LerArquivoProjeto("Servicos", "Operacao", "SemiAcabadoServico.cs");

        Assert.Contains("RegistrarPesagemSemiAcabadoAsync", semiForm, StringComparison.Ordinal);
        Assert.Contains("ImpressaoSemiAcabadoServico.GerarCodigoEtiqueta", semiForm, StringComparison.Ordinal);
        Assert.Contains("await _impressaoSemiAcabadoServico.ImprimirNovaPesagemAsync", semiForm, StringComparison.Ordinal);
        Assert.Contains("ImprimirEtiquetaProducaoAsync", impressao, StringComparison.Ordinal);
        Assert.Contains("SalvarEEnviarSap101Async", servico, StringComparison.Ordinal);
        Assert.Contains("CriarDocumentoMaterial101Async", servico, StringComparison.Ordinal);
        Assert.Contains("TentarReservarEnvioSapAsync", servico, StringComparison.Ordinal);
    }
    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

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
