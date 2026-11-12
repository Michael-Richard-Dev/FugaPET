namespace FugaPET_Dev.Tests.Processo.OutOfSnapshot;

public sealed class ControleApontamentosResultadoSemiAcabadoOutOfSnapshotTests
{
    [Fact]
    public void Saida0140_SemiAcabadoRoteiaParaProcessoSemiAcabadoForm()
    {
        string controleForm = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string tipos = LerArquivoProjeto("Modelo", "Processo", "ConfiguracaoOperacaoProcesso.cs");

        Assert.Contains("SemiAcabado = \"SEMI_ACABADO\"", tipos, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.SemiAcabado => AbrirSemiAcabado(contexto)", controleForm, StringComparison.Ordinal);
        Assert.Contains("using ProcessoSemiAcabadoForm form = new(contexto);", controleForm, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoProcessoOperacao.SemiAcabado => AbrirConsumo", controleForm, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoProcessoOperacao.ConsumoSemiAcabado => AbrirConsumo(contexto, ModoConsumoMaterial.SemiAcabado)", controleForm, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"0140\"", controleForm, StringComparison.Ordinal);
    }

    [Fact]
    public void Saida0140_ProcessoSemiAcabadoRecebeContextoERetornaLancamento()
    {
        string semiForm = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "SemiAcabadoRepositorio.cs");

        Assert.Contains("public ProcessoSemiAcabadoForm(ContextoApontamentoProcesso contexto)", semiForm, StringComparison.Ordinal);
        Assert.Contains("private readonly ContextoApontamentoProcesso? _contextoApontamento;", semiForm, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcesso ResultadoExecucaoApontamento", semiForm, StringComparison.Ordinal);
        Assert.Contains("pedidoComboBox.Text = _contextoApontamento.NumeroOrdem;", semiForm, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConfirmadoSap", semiForm, StringComparison.Ordinal);
        Assert.Contains("resultado.CodigoLancamento", semiForm, StringComparison.Ordinal);
        Assert.Contains("RETURNING codigo_semi_acabado_lancamento", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void Saida0140_FechamentoOuCancelamentoNaoFabricaConclusao()
    {
        string semiForm = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("= ResultadoExecucaoProcesso.NaoConcluido;", semiForm, StringComparison.Ordinal);
        Assert.Contains("if (confirmacao != DialogResult.Yes)", semiForm, StringComparison.Ordinal);
        Assert.Contains("statusLabel.Text = \"Confirmação cancelada.\";", semiForm, StringComparison.Ordinal);
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