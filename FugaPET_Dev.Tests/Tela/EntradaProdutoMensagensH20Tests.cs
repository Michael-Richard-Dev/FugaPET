namespace FugaPET_Dev.Tests.Tela;

public sealed class EntradaProdutoMensagensH20Tests
{
    [Fact]
    public void Finalizacao_DeveInformarGravacaoLocalESapSeparado()
    {
        string form = LerForm();

        Assert.Contains("Lançamento local", form, StringComparison.Ordinal);
        Assert.Contains(
            "O envio ao SAP deve ser executado pela rotina autorizada de integração em homologação.",
            form,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "peso(s) atualizado(s) no SAP",
            form,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "PATCH concluído",
            form,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "pedido alterado no SAP",
            form,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_DeveExibirEstadosLocaisESapSeparados()
    {
        string form = LerForm();

        Assert.Contains("LOCAL PENDENTE", form, StringComparison.Ordinal);
        Assert.Contains("LOCAL GRAVADO", form, StringComparison.Ordinal);
        Assert.Contains("INTEGRAÇÃO SAP HML: PENDENTE", form, StringComparison.Ordinal);
        Assert.Contains("INTEGRAÇÃO SAP HML: ENVIADA", form, StringComparison.Ordinal);
        Assert.Contains("INTEGRAÇÃO SAP HML: FALHA", form, StringComparison.Ordinal);
        Assert.Contains("INTEGRAÇÃO SAP HML: PARCIAL", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvioSapHml_DeveExigirPermissaoConfirmacaoESerSeparadoDaFinalizacao()
    {
        string form = LerForm();

        Assert.Contains(
            "productionActionsButton.Visible = podeEnviarSap",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "PermissoesSistema.Acoes.EnviarSap",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "Confirma o envio do lançamento",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "SAP DE HOMOLOGAÇÃO",
            form,
            StringComparison.Ordinal);

        int inicioFinalizacao = form.IndexOf(
            "private async Task GravarPesagensAsync()",
            StringComparison.Ordinal);
        int fimFinalizacao = form.IndexOf(
            "private EntradaProdutoLancamento MontarLancamentoDoGrid()",
            inicioFinalizacao,
            StringComparison.Ordinal);
        string metodoFinalizacao = form[inicioFinalizacao..fimFinalizacao];

        Assert.DoesNotContain(
            "EnviarPesoEntradaParaSapHomologacaoAsync",
            metodoFinalizacao,
            StringComparison.Ordinal);
    }

    private static string LerForm()
        => File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
