namespace FugaPET_Dev.Tests.Tela;

public sealed class EntradaProdutoMensagensH20Tests
{
    [Fact]
    public void Finalizacao_DeveInformarGravacaoLocalESapSeparado()
    {
        string form = LerForm();

        Assert.Contains("Lan\u00e7amento local", form, StringComparison.Ordinal);
        Assert.Contains(
            "O envio ao SAP deve ser executado pela rotina autorizada de integra\u00e7\u00e3o em homologa\u00e7\u00e3o.",
            form,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "peso(s) atualizado(s) no SAP",
            form,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "PATCH conclu\u00eddo",
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
        Assert.Contains("SAP HML: AGUARDANDO GRAVA\u00c7\u00c3O LOCAL", form, StringComparison.Ordinal);
        Assert.Contains("SAP HML: LIBERADO PARA ENVIO", form, StringComparison.Ordinal);
        Assert.Contains("SAP HML: ENVIADO", form, StringComparison.Ordinal);
        Assert.Contains("SAP HML: FALHA", form, StringComparison.Ordinal);
        Assert.Contains("SAP HML: PARCIAL", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvioSapHml_DeveExigirPermissaoConfirmacaoESerSeparadoDaFinalizacao()
    {
        string form = LerForm();

        Assert.Contains(
            "productionActionsButton.Visible = diagnostico.UsuarioTemPermissao",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "PermissoesSistema.Acoes.EnviarSap",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "Confirma o envio do lan\u00e7amento",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "SAP DE HOMOLOGA\u00c7\u00c3O",
            form,
            StringComparison.Ordinal);

        string metodoFinalizacao = ExtrairMetodo(
            form,
            "private async Task GravarPesagensAsync()",
            "private EntradaProdutoLancamento MontarLancamentoDoGrid()");

        Assert.DoesNotContain(
            "EnviarPesoEntradaParaSapHomologacaoAsync",
            metodoFinalizacao,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveBloquearAcessoDiretoESemCortarStatusSap()
    {
        string form = LerForm();
        string designer = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.Designer.cs"));

        Assert.Contains("PermissoesSistema.Acoes.Consultar", form, StringComparison.Ordinal);
        Assert.Contains("abrir diretamente a Entrada de Produto", form, StringComparison.Ordinal);
        Assert.Contains("sapStatusPanel.Size = new Size(452, 27)", designer, StringComparison.Ordinal);
        Assert.Contains("sapStatusLabel.Size = new Size(411, 17)", designer, StringComparison.Ordinal);
        Assert.Contains("sapStatusLabel.AutoSize = false", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Enabled = false", form, StringComparison.Ordinal);
        Assert.Contains("diagnostico.PodeEnviar", form, StringComparison.Ordinal);
    }

    [Fact]
    public void LeituraLocal_NaoDeveDependerDePermissaoDeImpressao()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(
            form,
            "private void SetReadWeightEnabled(bool enabled)",
            "private void SetDeleteActionsEnabled");

        Assert.Contains("PermissoesSistema.Acoes.Executar", metodo, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.PesoManual", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PossuiPermissaoImpressao", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PermissoesSistema.Acoes.Imprimir", metodo, StringComparison.Ordinal);
        Assert.Contains("lerEtiquetaButton.Enabled = leituraBalancaHabilitada", metodo, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Enabled = leituraManualHabilitada", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void LeituraBalanca_DeveRegistrarPesoAntesDaImpressaoNaoBloqueante()
    {
        string form = LerForm();
        string metodo = ExtrairMetodo(
            form,
            "private async void ReadWeightLegend_Click",
            "private static string GetFriendlyErrorMessage");

        int leituraPeso = metodo.IndexOf("_balancaLeituraServico.LerPesoAsync", StringComparison.Ordinal);
        int registrarPeso = metodo.IndexOf("RegistrarPesoLido", StringComparison.Ordinal);
        int tentarImprimir = metodo.IndexOf("TentarImprimirEtiquetaAposLeituraAsync", StringComparison.Ordinal);

        Assert.True(leituraPeso >= 0);
        Assert.True(registrarPeso > leituraPeso);
        Assert.True(tentarImprimir > registrarPeso);
        Assert.DoesNotContain("GarantirImpressoraDisponivelAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("Peso registrado, mas a etiqueta n\u00e3o foi impressa.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalizacaoSemLeitura_DeveOrientarOperadorEDetectarPesoVisualSemRastreio()
    {
        string form = LerForm();

        Assert.Contains(
            "Nenhuma leitura foi registrada. Clique em Iniciar Leitura e use Ler Peso, Leitura Manual ou Pesagem M\u00faltipla antes de finalizar.",
            form,
            StringComparison.Ordinal);
        Assert.Contains(
            "H\u00e1 peso visual na grade, mas n\u00e3o h\u00e1 leitura rastre\u00e1vel vinculada. Refa\u00e7a a leitura.",
            form,
            StringComparison.Ordinal);
        Assert.Contains("ExistePesoVisualSemLeituraRastreavel", form, StringComparison.Ordinal);
    }

    private static string LerForm()
        => File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));

    private static string ExtrairMetodo(string conteudo, string inicio, string fim)
    {
        int indiceInicio = conteudo.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0, $"Trecho inicial nao encontrado: {inicio}");

        int indiceFim = conteudo.IndexOf(fim, indiceInicio, StringComparison.Ordinal);
        Assert.True(indiceFim > indiceInicio, $"Trecho final nao encontrado: {fim}");

        return conteudo[indiceInicio..indiceFim];
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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}