using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa: reordenar os módulos de Cadastro para Modelo de Etiqueta aparecer ANTES de Etiqueta,
/// com F6 abrindo Modelo e F7 abrindo Etiqueta. Inclui testes COMPORTAMENTAIS: o clique de cada cartão
/// (e de seus controles filhos) dispara o evento da rotina correta.
/// </summary>
public sealed class CadastroFormOrdemModulosTests
{
    // ---- comportamental: clique do cartão dispara o evento correto (independe da posição) ----

    [Fact]
    public void CliqueModeloCard_DisparaModeloEtiquetaRequested()
    {
        using CadastroForm form = new();
        var (modelo, etiqueta) = Assinar(form);

        DispararClick(form, "modeloEtiquetaCard");

        Assert.True(modelo(), "Clicar no cartão Modelo deve abrir Modelo de Etiqueta.");
        Assert.False(etiqueta(), "Clicar no cartão Modelo NÃO pode abrir Etiqueta.");
    }

    [Fact]
    public void CliqueEtiquetaCard_DisparaEtiquetaRequested()
    {
        using CadastroForm form = new();
        var (modelo, etiqueta) = Assinar(form);

        DispararClick(form, "etiquetaCard");

        Assert.True(etiqueta(), "Clicar no cartão Etiqueta deve abrir Etiqueta.");
        Assert.False(modelo(), "Clicar no cartão Etiqueta NÃO pode abrir Modelo.");
    }

    [Theory]
    [InlineData("modeloEtiquetaIconLabel")]
    [InlineData("modeloEtiquetaTitleLabel")]
    [InlineData("modeloEtiquetaDescriptionLabel")]
    [InlineData("modeloEtiquetaStatusLabel")]
    [InlineData("modeloEtiquetaShortcutLabel")]
    [InlineData("modeloEtiquetaArrowLabel")]
    public void CliqueFilhosDoModelo_UsamHandlerDeModelo(string campo)
    {
        using CadastroForm form = new();
        var (modelo, etiqueta) = Assinar(form);

        DispararClick(form, campo);

        Assert.True(modelo(), $"Clicar em {campo} deve abrir Modelo de Etiqueta.");
        Assert.False(etiqueta());
    }

    [Theory]
    [InlineData("etiquetaIconLabel")]
    [InlineData("etiquetaTitleLabel")]
    [InlineData("etiquetaDescriptionLabel")]
    [InlineData("etiquetaStatusLabel")]
    [InlineData("etiquetaShortcutLabel")]
    [InlineData("etiquetaArrowLabel")]
    public void CliqueFilhosDaEtiqueta_UsamHandlerDeEtiqueta(string campo)
    {
        using CadastroForm form = new();
        var (modelo, etiqueta) = Assinar(form);

        DispararClick(form, campo);

        Assert.True(etiqueta(), $"Clicar em {campo} deve abrir Etiqueta.");
        Assert.False(modelo());
    }

    private static (Func<bool> Modelo, Func<bool> Etiqueta) Assinar(CadastroForm form)
    {
        bool modeloFired = false;
        bool etiquetaFired = false;
        form.ModeloEtiquetaRequested += (_, _) => modeloFired = true;
        form.EtiquetaRequested += (_, _) => etiquetaFired = true;
        return (() => modeloFired, () => etiquetaFired);
    }

    // Dispara o evento Click real do controle (protected Control.OnClick), exercitando o wiring de WireCardClickEvents.
    private static void DispararClick(CadastroForm form, string nomeCampo)
    {
        FieldInfo? campo = typeof(CadastroForm).GetField(nomeCampo, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(campo is not null, $"Controle não encontrado: {nomeCampo}");
        object? controle = campo!.GetValue(form);
        Assert.True(controle is Control, $"{nomeCampo} não é um Control.");

        MethodInfo onClick = typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!;
        onClick.Invoke(controle, new object[] { EventArgs.Empty });
    }

    [Fact]
    public void ModeloEtiquetaCard_AdicionadoAntesDeEtiquetaCard()
    {
        string designer = LerArquivo("Tela", "CadastroForm.Designer.cs");
        int idxModelo = designer.IndexOf("modulesFlowLayoutPanel.Controls.Add(modeloEtiquetaCard)", StringComparison.Ordinal);
        int idxEtiqueta = designer.IndexOf("modulesFlowLayoutPanel.Controls.Add(etiquetaCard)", StringComparison.Ordinal);
        Assert.True(idxModelo >= 0 && idxEtiqueta >= 0, "Ambos os cards devem estar no FlowLayoutPanel.");
        Assert.True(idxModelo < idxEtiqueta, "Modelo de Etiqueta deve ser adicionado antes de Etiqueta.");
    }

    [Fact]
    public void ModeloExibeF6_EtiquetaExibeF7()
    {
        string designer = LerArquivo("Tela", "CadastroForm.Designer.cs");
        Assert.Contains("modeloEtiquetaShortcutLabel.Text = \"F6\"", designer, StringComparison.Ordinal);
        Assert.Contains("etiquetaShortcutLabel.Text = \"F7\"", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessCmdKey_F6AbreModelo_F7AbreEtiqueta()
    {
        string form = LerArquivo("Tela", "CadastroForm.cs");
        Assert.Contains("Keys.F6 && modeloEtiquetaCard.Visible", form, StringComparison.Ordinal);
        Assert.Contains("Keys.F7 && etiquetaCard.Visible", form, StringComparison.Ordinal);

        // F6 dispara Modelo; F7 dispara Etiqueta (ordem correta dos handlers).
        int idxF6 = form.IndexOf("Keys.F6 && modeloEtiquetaCard.Visible", StringComparison.Ordinal);
        int idxModeloClick = form.IndexOf("OnModeloEtiquetaClick", idxF6, StringComparison.Ordinal);
        int idxF7 = form.IndexOf("Keys.F7 && etiquetaCard.Visible", StringComparison.Ordinal);
        Assert.True(idxModeloClick > idxF6 && idxModeloClick < idxF7, "F6 deve chamar OnModeloEtiquetaClick.");

        int idxEtiquetaClick = form.IndexOf("OnEtiquetaClick", idxF7, StringComparison.Ordinal);
        Assert.True(idxEtiquetaClick > idxF7, "F7 deve chamar OnEtiquetaClick.");
    }

    [Fact]
    public void PainelInicial_CadastroKeyDown_F6ChamaModelo_F7ChamaEtiqueta()
    {
        // Causa da troca reportada: mapeamento de teclado F6/F7 do PainelInicialForm por posição (antigo).
        string painel = LerArquivo("Tela", "PainelInicialForm.cs");
        int bloco = painel.IndexOf("_currentContentView == _cadastroForm", StringComparison.Ordinal);
        Assert.True(bloco >= 0, "Bloco de teclado do Cadastro não encontrado.");

        int f6 = painel.IndexOf("Keys.F6", bloco, StringComparison.Ordinal);
        int f7 = painel.IndexOf("Keys.F7", bloco, StringComparison.Ordinal);
        Assert.True(f6 >= 0 && f7 > f6, "F6 e F7 do Cadastro devem existir, F6 antes de F7.");

        int modeloAposF6 = painel.IndexOf("CadastroForm_ModeloEtiquetaRequested", f6, StringComparison.Ordinal);
        Assert.True(modeloAposF6 > f6 && modeloAposF6 < f7, "F6 (Cadastro) deve chamar Modelo de Etiqueta.");

        int etiquetaAposF7 = painel.IndexOf("CadastroForm_EtiquetaRequested", f7, StringComparison.Ordinal);
        Assert.True(etiquetaAposF7 > f7, "F7 (Cadastro) deve chamar Etiqueta.");
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
