using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Cadastro;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 127A — identidade visual FugaPET nas telas promovidas ao padrao.
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
    /// <summary>
    /// Toda tela ja promovida ao padrao visual exibe o logo FugaPET. O caminho traz a PASTA porque a
    /// promocao saiu de Cadastro e alcancou Processo.
    /// </summary>
    public static TheoryData<string> TelasPromovidas() =>
    [
        "Cadastro/SetorForm.cs",
        "Cadastro/CargoForm.cs",
        "Cadastro/TipoTaraForm.cs",
        "Cadastro/TaraForm.cs",
        "Cadastro/CadastroUsuarioForm.cs",
        "Cadastro/BalancaForm.cs",
        "Processo/ProcessoConsumoMaterialForm.cs"
    ];

    /// <summary>
    /// Telas cuja tipografia vem da ESCALA compartilhada e cujos cartoes calculam geometria a partir do
    /// texto. A tela de Balanca NAO esta aqui: ela tem motor de fonte proprio (AjustarEscalaFontes), que
    /// recalcula tudo a cada Resize — nela o alinhamento foi feito dentro daquele motor, e e o teste
    /// <see cref="Balanca_MotorDeFonte_MiraNoCorpoDasTelasPrincipais"/> que o cobre.
    /// </summary>
    public static TheoryData<string> TelasComCartoesRecalculados() =>
    [
        "Cadastro/SetorForm.cs",
        "Cadastro/CargoForm.cs",
        "Cadastro/TipoTaraForm.cs",
        "Cadastro/TaraForm.cs"
    ];

    /// <summary><paramref name="caminhoRelativo"/> no formato "Pasta/Arquivo.cs" dentro de Tela/.</summary>
    private static string LerCadastro(string caminhoRelativo)
    {
        string[] partes = caminhoRelativo.Split('/');
        string raiz = AppContext.BaseDirectory;

        for (int i = 0; i < 9; i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine([raiz, "Tela", .. partes]),
                Path.Combine([raiz, "FugaPet_HML", "Tela", .. partes])
            })
            {
                if (File.Exists(cand))
                {
                    return File.ReadAllText(cand);
                }
            }

            raiz = Path.GetFullPath(Path.Combine(raiz, ".."));
        }

        throw new FileNotFoundException($"Fonte nao encontrado: Tela/{caminhoRelativo}");
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

    // GATE 127A — a tela de Tara cria controles em TEMPO DE EXECUCAO (campo de tamanho e mensagem de
    // estado vazio). A escala percorre a arvore de controles existente: aplicada antes da criacao,
    // esses dois nascem na fonte antiga e ficam visivelmente menores que o resto do cartao. Nenhuma
    // outra tela de cadastro tem esse problema, entao a ordem aqui e facil de desfazer num refactor.
    [Fact]
    public void Tara_EscalaVemDepoisDosControlesCriadosEmRuntime()
    {
        string src = LerCadastro("Cadastro/TaraForm.cs");

        int iCampo = src.IndexOf("CriarCampoTamanhoRuntime();", StringComparison.Ordinal);
        int iMensagem = src.IndexOf("CriarMensagemEstadoVazioRuntime();", StringComparison.Ordinal);
        int iEscala = src.IndexOf("AplicarEscalaDeConteudo(", StringComparison.Ordinal);

        Assert.True(iCampo >= 0, "CriarCampoTamanhoRuntime sumiu — reavaliar a ordem desta tela");
        Assert.True(iMensagem >= 0, "CriarMensagemEstadoVazioRuntime sumiu — reavaliar a ordem desta tela");
        Assert.True(
            iEscala > iCampo && iEscala > iMensagem,
            "a escala precisa vir DEPOIS dos controles criados em runtime, senao eles ficam na fonte antiga");
    }

    // GATE 127A — Cadastro de Usuario recebe o logo, mas NAO a escala de fonte compartilhada.
    //
    // Tentada em 17/11/2026 e reprovada na conferencia visual: o botao "Novo Usuario" estourava, os
    // campos do cartao de dados eram cortados pela borda (Cargo, Setor, Perfil e Situacao sumiam) e
    // os selos de situacao se desencontravam das linhas. Mesmo limite da tela de Consumo.
    //
    // Quando esta tela ganhar o ajuste de LARGURA que a de Recebimento tem, troque este teste pelos
    // de escala — nao o apague em silencio.
    [Fact]
    public void Usuario_RecebeLogoMasNaoAEscalaCompartilhada()
    {
        string src = LerCadastro("Cadastro/CadastroUsuarioForm.cs");

        Assert.Contains("IdentidadeVisualFugaPet.AplicarNoCabecalho(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AplicarEscalaDeConteudo(", src, StringComparison.Ordinal);
    }

    // GATE 127A — Consumo de Material recebe o logo, mas NAO a escala de fonte compartilhada.
    //
    // Nao e esquecimento. A escala foi aplicada em 17/11/2026 e REPROVADA na conferencia visual: os
    // rotulos se sobrepunham, os cabecalhos da grade viravam "O ...", e o painel lateral cortava o
    // texto. A tela de Recebimento aguenta 2x porque tem metodos dedicados que ajustam LARGURAS
    // (painel lateral, colunas da grade, toolbar); a adaptacao compartilhada so cresce ALTURAS.
    //
    // Este teste existe para que ninguem re-adicione a escala aqui sem antes dar a esta tela o
    // mesmo tratamento de largura. Quando esse trabalho for feito, troque este teste pelos de
    // escala — nao o apague em silencio.
    [Fact]
    public void Consumo_RecebeLogoMasNaoAEscalaCompartilhada()
    {
        string src = LerCadastro("Processo/ProcessoConsumoMaterialForm.cs");

        Assert.Contains("IdentidadeVisualFugaPet.AplicarNoCabecalho(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AplicarEscalaDeConteudo(", src, StringComparison.Ordinal);
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
