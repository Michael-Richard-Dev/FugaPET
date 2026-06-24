namespace FugaPET_Dev.Tests.Cadastro;

public sealed class BalancaFormContratoTests
{
    private static readonly string RaizProjeto = ObterRaizProjeto();

    [Fact]
    public void Designer_DeveDeclararCamposPrincipaisSemControlesLegados()
    {
        string designer = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Tela",
            "Cadastro",
            "BalancaForm.Designer.cs"));

        string[] camposObrigatorios =
        [
            "nomeBalancaTextBox",
            "setorComboBox",
            "identificacaoLocalTextBox",
            "situacaoTextBox",
            "tipoConexaoComboBox",
            "enderecoIpTextBox",
            "portaTcpTextBox",
            "portaSerialTextBox",
            "baudRateTextBox",
            "dataBitsTextBox",
            "paridadeComboBox",
            "stopBitsComboBox",
            "flowControlComboBox",
            "protocoloTextBox",
            "observacaoTextBox"
        ];

        foreach (string campo in camposObrigatorios)
        {
            Assert.Contains(campo, designer, StringComparison.Ordinal);
        }

        string[] controlesProibidos =
        [
            "TxtPeso",
            "profilesDataGridView",
            "novoPerfilButton",
            "duplicarButton",
            "heroPanel",
            "nomePerfilTextBox",
            "summaryPerfilValueLabel",
            "profileRow"
        ];

        foreach (string controle in controlesProibidos)
        {
            Assert.DoesNotContain(controle, designer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Form_DeveManterSegurancaEstadosEBloqueioConcorrente()
    {
        string form = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Tela",
            "Cadastro",
            "BalancaForm.cs"));

        string[] contratos =
        [
            "PodeVisualizarRotina",
            "RegistrarAcessoDiretoNegadoSeguroAsync",
            "_operacaoEmAndamento",
            "ExecutarOperacaoProtegidaAsync",
            "PrepararNovaBalanca",
            "AplicarFiltro",
            "AtualizarCamposPorTipoConexao",
            "Inativar Balança",
            "Reativar Balança"
        ];

        foreach (string contrato in contratos)
        {
            Assert.Contains(contrato, form, StringComparison.Ordinal);
        }

        string designer = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Tela",
            "Cadastro",
            "BalancaForm.Designer.cs"));

        Assert.Contains("Salvar Alterações", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_DeveManterLimitesECombosFechados()
    {
        string designer = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Tela",
            "Cadastro",
            "BalancaForm.Designer.cs"));

        Assert.Contains("nomeBalancaTextBox.MaxLength = 80", designer, StringComparison.Ordinal);
        Assert.Contains("identificacaoLocalTextBox.MaxLength = 120", designer, StringComparison.Ordinal);
        Assert.Contains("enderecoIpTextBox.MaxLength = 45", designer, StringComparison.Ordinal);
        Assert.Contains("portaSerialTextBox.MaxLength = 50", designer, StringComparison.Ordinal);
        Assert.Contains("protocoloTextBox.MaxLength = 50", designer, StringComparison.Ordinal);
        Assert.Contains("observacaoTextBox.MaxLength = 255", designer, StringComparison.Ordinal);
        Assert.Contains("searchTextBox.MaxLength = 120", designer, StringComparison.Ordinal);
        Assert.Contains("DropDownStyle = ComboBoxStyle.DropDownList", designer, StringComparison.Ordinal);
        // Situacao agora e um TextBox somente leitura (corrige bug de render do combo desabilitado).
        Assert.Contains("situacaoTextBox.ReadOnly = true", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void CadastroForm_DeveOcultarBalancaEBloquearF3QuandoCardInvisivel()
    {
        string cadastroForm = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Tela",
            "CadastroForm.cs"));

        Assert.Contains(
            "ConfigurarVisibilidadeCadastro(",
            cadastroForm,
            StringComparison.Ordinal);
        Assert.Contains(
            "PermissoesSistema.Rotinas.Balanca",
            cadastroForm,
            StringComparison.Ordinal);
        Assert.Contains(
            "keyData == Keys.F3 && balancaCard.Visible",
            cadastroForm,
            StringComparison.Ordinal);
    }

    private static string ObterRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
        {
            diretorio = diretorio.Parent;
        }

        return diretorio?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
