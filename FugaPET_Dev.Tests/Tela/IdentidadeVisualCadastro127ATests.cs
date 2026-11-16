using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Cadastro;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 127A — identidade visual FugaPET nas telas de CADASTRO.
/// <para>
/// POR QUE EXISTE: ate 16/11/2026 so a tela de Recebimento de Mercadoria trocava o logo Fuga Couros pelo
/// FugaPET e ampliava a fonte; o codigo vivia PRIVADO nela. As telas de cadastro exibiam o logo antigo em
/// fonte menor. A correcao foi extrair a identidade para <c>Tela/Comum/IdentidadeVisualFugaPet.cs</c> e
/// liga-la tela a tela.
/// </para>
/// <para>
/// As listas abaixo CRESCEM a cada tela promovida. Sem elas, uma tela pode ser ligada e depois perder a
/// chamada num refactor sem ninguem notar — foi assim que a copia privada ficou presa numa tela so.
/// </para>
/// </summary>
public sealed class IdentidadeVisualCadastro127ATests
{
    /// <summary>Toda tela de cadastro ja promovida ao padrao visual exibe o logo FugaPET.</summary>
    public static TheoryData<string> TelasPromovidas() =>
    [
        "SetorForm.cs",
        "CargoForm.cs",
        "BalancaForm.cs"
    ];

    /// <summary>
    /// Telas cuja tipografia vem da ESCALA compartilhada e cujos cartoes calculam geometria a partir do
    /// texto. A tela de Balanca NAO esta aqui: ela tem motor de fonte proprio (AjustarEscalaFontes), que
    /// recalcula tudo a cada Resize — nela o alinhamento foi feito dentro daquele motor, e e o teste
    /// <see cref="Balanca_MotorDeFonte_MiraNoCorpoDasTelasPrincipais"/> que o cobre.
    /// </summary>
    public static TheoryData<string> TelasComCartoesRecalculados() =>
    [
        "SetorForm.cs",
        "CargoForm.cs"
    ];

    private static string LerCadastro(string arquivo)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 9; i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine(raiz, "Tela", "Cadastro", arquivo),
                Path.Combine(raiz, "FugaPet_HML", "Tela", "Cadastro", arquivo)
            })
            {
                if (File.Exists(cand))
                {
                    return File.ReadAllText(cand);
                }
            }

            raiz = Path.GetFullPath(Path.Combine(raiz, ".."));
        }

        throw new FileNotFoundException($"Fonte nao encontrado: Tela/Cadastro/{arquivo}");
    }

    [Theory]
    [MemberData(nameof(TelasPromovidas))]
    public void Tela_AplicaLogoFugaPet(string arquivo)
    {
        string src = LerCadastro(arquivo);

        Assert.Contains("IdentidadeVisualFugaPet.AplicarNoCabecalho(", src, StringComparison.Ordinal);
        Assert.Contains("companyLogoPictureBox, headerDividerLabel", src, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TelasComCartoesRecalculados))]
    public void Tela_EscalaVaiNaAreaDeConteudo(string arquivo)
    {
        string src = LerCadastro(arquivo);

        // A escala vai na AREA DE CONTEUDO, nunca no formulario: passar 'this' inflaria a barra de
        // titulo e o rodape, que sao comuns a todas as telas.
        Assert.Contains("IdentidadeVisualFugaPet.AplicarEscalaDeConteudo(", src, StringComparison.Ordinal);
        Assert.Contains("contentPanel,", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AplicarEscalaDeConteudo(this", src, StringComparison.Ordinal);
    }

    // Os cartoes calculam geometria a partir do texto. Se o recalculo nao vier DEPOIS da escala, o
    // conteudo em fonte maior fica dentro de caixas dimensionadas para a fonte antiga.
    [Theory]
    [MemberData(nameof(TelasComCartoesRecalculados))]
    public void Tela_RecalculaCartoesDepoisDaEscala(string arquivo)
    {
        string src = LerCadastro(arquivo);

        int iEscala = src.IndexOf("AplicarEscalaDeConteudo(", StringComparison.Ordinal);
        Assert.True(iEscala >= 0, $"{arquivo} nao aplica a escala de conteudo");

        string depois = src[iEscala..];
        int iFechamento = depois.IndexOf("});", StringComparison.Ordinal);
        Assert.True(iFechamento > 0, $"{arquivo} nao passa o recalculo de layout como continuacao");

        string continuacao = depois[..iFechamento];
        foreach (string layout in new[] { "LayoutProfilesCard();", "LayoutDetailsCard();", "LayoutSummaryCard();" })
        {
            Assert.Contains(layout, continuacao, StringComparison.Ordinal);
        }
    }

    // GATE 127A — a tela de Balanca tem motor de fonte PROPRIO, que recalcula tudo a cada Resize. Uma
    // escala aplicada no construtor seria apagada no primeiro redimensionamento, entao o alvo tem de
    // estar DENTRO do motor. Antes ele parava em ~12pt, e por isso a tela parecia menor que as outras.
    [Theory]
    [InlineData(8.5f, 1.0f, 8.5f, 17f)]    // cartao no tamanho de projeto: bate o corpo das principais
    [InlineData(8.5f, 2.5f, 8.5f, 17f)]    // cartao maior NAO amplia alem do alvo
    [InlineData(7.75f, 1.0f, 8.5f, 15.5f)] // rotulo: alvo = base x escala
    public void Balanca_MotorDeFonte_MiraNoCorpoDasTelasPrincipais(
        float tamanhoBase, float escalaCartao, float minimo, float esperado)
    {
        float tamanho = InvocarDefinirFonte(tamanhoBase, escalaCartao, minimo);
        Assert.Equal(esperado, tamanho, 2);
        Assert.Equal(tamanhoBase * LegibilidadeRecebimentoMercadoria.Escala, tamanho, 2);
    }

    // Janela pequena: o texto ENCOLHE em vez de ser cortado, mas nunca abaixo do tamanho de projeto.
    [Fact]
    public void Balanca_MotorDeFonte_CartaoPequenoReduzSemPassarDoPiso()
    {
        float meio = InvocarDefinirFonte(8.5f, 0.5f, 8.5f);
        Assert.Equal(8.5f, meio, 2);

        float tresQuartos = InvocarDefinirFonte(8.5f, 0.75f, 8.5f);
        Assert.True(tresQuartos > 8.5f && tresQuartos < 17f, $"esperado entre 8,5 e 17; veio {tresQuartos}");
    }

    private static float InvocarDefinirFonte(float tamanhoBase, float escala, float minimo)
    {
        MethodInfo? metodo = typeof(BalancaForm).GetMethod(
            "DefinirFonte",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(metodo);

        float resultado = 0f;
        Thread t = new(() =>
        {
            using Label alvo = new() { Font = new Font("Segoe UI", 1F) };
            metodo!.Invoke(null, [alvo, tamanhoBase, escala, minimo, FontStyle.Regular]);
            resultado = alvo.Font.Size;
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        return resultado;
    }
}
