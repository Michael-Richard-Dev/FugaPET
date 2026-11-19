using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Tela do vinculo produto × etiqueta: invariantes de DESENHO, verificadas no fonte.
/// <para>
/// Sao assercoes de fonte, e nao de instancia, porque instanciar WinForms exige STA e o projeto de
/// teste nao tem esse arranjo. O que elas protegem sao decisoes que se perdem numa edicao distraida
/// — e, no caso do DPI, uma regressao que so apareceria na maquina do operador.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiquetaTela065Tests
{
    private const string Form = "VinculoProdutoEtiquetaForm.cs";

    /// <summary>
    /// A tela nasceu DEPOIS da lição de DPI: nenhuma geometria cravada em pixel. Tudo passa pelo
    /// fator do DeviceDpi ou por layout fluido. Este teste falha se alguem voltar a cravar.
    /// </summary>
    [Fact]
    public void Tela_NaoUsaGeometriaCravadaEmPixel()
    {
        string fonte = LerTela();

        Assert.DoesNotContain("SetBounds(", fonte, StringComparison.Ordinal);
        Assert.Contains("DeviceDpi / 96f", fonte, StringComparison.Ordinal);

        // Todo tamanho literal tem de passar pela funcao de escala. "new Size(900" direto seria pixel
        // cravado; "new Size(Escalar(900)" e o que se espera.
        foreach (string trecho in new[] { "new Size(", "new Padding(", "Width = ", "Height = " })
        {
            int posicao = 0;
            while ((posicao = fonte.IndexOf(trecho, posicao, StringComparison.Ordinal)) >= 0)
            {
                string resto = fonte[(posicao + trecho.Length)..];
                posicao += trecho.Length;

                // So interessa valor literal numerico logo apos o trecho.
                if (resto.Length == 0 || !char.IsAsciiDigit(resto[0])) continue;

                // Zero e aceitavel em qualquer lugar: nao escala.
                if (resto[0] == '0' && (resto.Length < 2 || !char.IsAsciiDigit(resto[1]))) continue;

                Assert.Fail($"geometria cravada em pixel perto de: {trecho}{resto[..Math.Min(40, resto.Length)]}");
            }
        }
    }

    /// <summary>A tela leva a marca FugaPET, pelo caminho compartilhado e nao por imagem propria.</summary>
    [Fact]
    public void Tela_LevaOLogoFugaPetPeloCaminhoCompartilhado()
    {
        string fonte = LerTela();
        Assert.Contains("IdentidadeVisualFugaPet.AplicarNoCabecalho", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// O aviso do processo tem de DESCREVER o envio unico, e nunca mais dizer que ele nao existe.
    /// <para>
    /// Ate 19/11/2026 este teste exigia o oposto — que a tela avisasse que o envio unico ainda nao
    /// estava implementado —, e estava certo enquanto era verdade. Implementado o processo, a guarda
    /// inverte: o perigo deixou de ser o operador supor que funciona e passou a ser o aviso velho
    /// dizendo que nao funciona. Alerta desatualizado e pior que nenhum, porque ensina a nao ler.
    /// </para>
    /// </summary>
    [Fact]
    public void Tela_DescreveOEnvioUnicoSemDizerQueEleNaoExiste()
    {
        string fonte = LerTela();

        Assert.Contains("Envio único:", fonte, StringComparison.Ordinal);
        Assert.Contains("uma Handling Unit por caixa", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("ainda não está implementado", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("não há caminho de envio", fonte, StringComparison.Ordinal);
    }

    /// <summary>O cartao do Cadastro clona o cartao vivo, em vez de repetir numeros do Designer.</summary>
    [Fact]
    public void CartaoDeCadastro_HerdaGeometriaDoCartaoExistente()
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "CadastroForm.cs"));

        Assert.Contains("CriarCartaoVinculoProdutoEtiqueta", fonte, StringComparison.Ordinal);
        Assert.Contains("Size = etiquetaCard.Size", fonte, StringComparison.Ordinal);
        Assert.Contains("Margin = etiquetaCard.Margin", fonte, StringComparison.Ordinal);
        Assert.Contains("VinculoProdutoEtiquetaRequested", fonte, StringComparison.Ordinal);
    }

    private static string LerTela()
        => File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Cadastro", Form));

    private static string RaizProjeto()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return dir;
    }
}
