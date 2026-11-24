using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A LIGAÇÃO da pesagem automática na tela de Produto Acabado (24/11/2026).
/// <para>
/// A decisão de quando pesar é provada em <c>CicloPesagemAutomaticaTests</c>, sem tela e sem
/// balança. O que se vigia AQUI é o que só a tela pode errar: deixar o laço vivo depois de fechar,
/// permitir dois caminhos registrando a mesma caixa, errar calado, ou cravar pixel no botão novo.
/// </para>
/// </summary>
public sealed class PesagemAutomaticaTelaTests
{
    private static string Parcial() => Fonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.PesagemAutomatica.cs");
    private static string Tela() => Fonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

    /// <summary>
    /// O laço morre junto com a tela. Vivo, ele seguiria lendo a balança e tentando registrar caixa
    /// numa tela que não existe mais.
    /// </summary>
    [Fact]
    public void AoFecharATela_OLacoEEncerrado()
    {
        string tela = Tela();

        int ondeFecha = tela.IndexOf(
            "private void ProcessoProdutoAcabadoForm_FormClosing", StringComparison.Ordinal);
        Assert.True(ondeFecha >= 0, "o tratador de fechamento nao foi encontrado");

        int ondePara = tela.IndexOf("PararPesagemAutomatica(", ondeFecha, StringComparison.Ordinal);
        Assert.True(ondePara > ondeFecha && ondePara - ondeFecha < 1200,
            "o fechamento da tela nao encerra a pesagem automatica");
    }

    /// <summary>
    /// Com o automático ligado, LER PESO e DIGITAR PESO saem de cena. Os dois caminhos registrando a
    /// mesma caixa dariam pesagem dobrada, e o operador não teria como saber qual foi.
    /// </summary>
    [Fact]
    public void ComOAutomaticoLigado_OsComandosManuaisFicamDesabilitados()
    {
        string parcial = Parcial();

        Assert.Contains("lerEtiquetaButton.Enabled = !ligada;", parcial, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Enabled = !ligada;", parcial, StringComparison.Ordinal);
    }

    /// <summary>
    /// Falha de leitura DESLIGA o modo e avisa. Um laço que erra calado deixaria o operador
    /// colocando caixa atrás de caixa sem nada acontecer, achando que o sistema está pesando.
    /// </summary>
    [Fact]
    public void FalhaDeLeitura_DesligaOModoEAvisa()
    {
        string parcial = Parcial();

        Assert.Contains("if (!leitura.Sucesso || !TryParsePesoKg(", parcial, StringComparison.Ordinal);
        Assert.Contains("Pesagem automática desligada:", parcial, StringComparison.Ordinal);
    }

    /// <summary>
    /// O botão novo não traz pixel cravado: posição e tamanho vêm dos controles VIVOS, que o DPI já
    /// ajustou. É a causa raiz conhecida de tela que quebra em máquina de outra resolução.
    /// </summary>
    [Fact]
    public void OBotaoNovo_NaoTemPixelCravado()
    {
        string parcial = Parcial();

        Assert.Contains("int passo = leituraManualButton.Top - lerEtiquetaButton.Top;", parcial, StringComparison.Ordinal);
        Assert.Contains("Size = leituraManualButton.Size,", parcial, StringComparison.Ordinal);

        // Nenhum new Point(numero, numero) — so posicoes derivadas de outro controle.
        Assert.DoesNotMatch(@"new Point\(\s*\d+\s*,\s*\d+\s*\)", parcial);
    }

    /// <summary>
    /// Não couber não pode virar sumir: um recurso invisível é pior do que um recurso fora de ordem.
    /// </summary>
    [Fact]
    public void SeNaoCouberNaColuna_OBotaoVaiParaOFim_NaoDesaparece()
    {
        string parcial = Parcial();

        Assert.Contains("sidePanel.ClientSize.Height", parcial, StringComparison.Ordinal);
        Assert.Contains("excluirCodigoButton.Top + passo", parcial, StringComparison.Ordinal);
        Assert.Contains("sidePanel.Controls.Add(botao);", parcial, StringComparison.Ordinal);
    }

    /// <summary>
    /// A DECISÃO de pesar não é reimplementada na tela: a tela chama o ciclo. Duas cópias da mesma
    /// regra divergem — foi assim que a tolerância passou um dia inteiro sem valer.
    /// </summary>
    [Fact]
    public void ATela_NaoReimplementaADecisao_ChamaOCiclo()
    {
        string parcial = Parcial();

        Assert.Contains("_cicloAutomatico.Observar(pesoBrutoKg, DateTimeOffset.Now)", parcial, StringComparison.Ordinal);
        Assert.Contains("decisao.PesoParaRegistrar is decimal peso", parcial, StringComparison.Ordinal);

        // A tela nao deve conter a aritmetica da janela de estabilidade.
        Assert.DoesNotContain("TempoParado", parcial, StringComparison.Ordinal);
        Assert.DoesNotContain("VariacaoMaximaKg", parcial, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tudo o que poderia interromper o operador é conferido ANTES do laço — permissão, OP, balança
    /// e tara. Uma caixa de 12 kg parando para perguntar a tara seria pior que o clique que o modo
    /// veio substituir.
    /// </summary>
    [Fact]
    public void AsValidacoes_AcontecemAntesDoLaco()
    {
        string parcial = Parcial();

        int ondeLiga = parcial.IndexOf("private async Task LigarPesagemAutomaticaAsync", StringComparison.Ordinal);
        int ondeLaco = parcial.IndexOf("_ = ObservarBalancaAsync(", ondeLiga, StringComparison.Ordinal);
        Assert.True(ondeLiga >= 0 && ondeLaco > ondeLiga);

        string corpo = parcial[ondeLiga..ondeLaco];
        Assert.Contains("BloquearAcaoSemPermissaoAsync", corpo, StringComparison.Ordinal);
        Assert.Contains("ValidarPodePesar()", corpo, StringComparison.Ordinal);
        Assert.Contains("GarantirBalancaProdutoAcabadoConfigurada()", corpo, StringComparison.Ordinal);
        Assert.Contains("GarantirTaraCaixaSelecionadaAsync()", corpo, StringComparison.Ordinal);
    }

    /// <summary>O modo também é alcançável por tecla, para parar sem procurar o mouse.</summary>
    [Fact]
    public void F11_AlternaOModo()
        => Assert.Contains("await AlternarPesagemAutomaticaAsync();", Tela(), StringComparison.Ordinal);

    private static string Fonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
