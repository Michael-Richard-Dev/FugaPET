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
/// Este teste e uma LISTA que cresce: cada tela de cadastro promovida ao padrao entra aqui. Sem ele, a
/// proxima tela pode ser ligada e depois perder a chamada num refactor sem ninguem notar — foi exatamente
/// assim que a copia privada ficou presa numa tela so.
/// </para>
/// </summary>
public sealed class IdentidadeVisualCadastro127ATests
{
    /// <summary>Telas de cadastro que ja foram promovidas ao padrao visual.</summary>
    public static TheoryData<string> TelasPromovidas() =>
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
    public void Tela_AplicaLogoFugaPetEEscalaDoConteudo(string arquivo)
    {
        string src = LerCadastro(arquivo);

        Assert.Contains(
            "IdentidadeVisualFugaPet.AplicarNoCabecalho(",
            src,
            StringComparison.Ordinal);
        Assert.Contains(
            "companyLogoPictureBox, headerDividerLabel",
            src,
            StringComparison.Ordinal);

        // A escala vai na AREA DE CONTEUDO, nunca no formulario: passar 'this' inflaria a barra de
        // titulo e o rodape, que sao comuns a todas as telas.
        Assert.Contains(
            "IdentidadeVisualFugaPet.AplicarEscalaDeConteudo(",
            src,
            StringComparison.Ordinal);
        Assert.Contains("contentPanel,", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AplicarEscalaDeConteudo(\r\n            this", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AplicarEscalaDeConteudo(this", src, StringComparison.Ordinal);
    }

    // Os cartoes calculam geometria a partir do texto. Se o recalculo nao vier DEPOIS da escala, o
    // conteudo em fonte maior fica dentro de caixas dimensionadas para a fonte antiga.
    [Theory]
    [MemberData(nameof(TelasPromovidas))]
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
}
