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
    // estar DENTRO do motor.
    //
    // CORRIGIDO EM 25/11/2026: este teste exigia base x 2,0 e dizia, no comentario, que isso "bate o
    // corpo das principais". NAO batia. Medindo Setor e Cargo montados em 1920x1080, os rotulos
    // equivalentes ficam em 10,5 a 12,1pt — eles escalam pelo tamanho do CARTAO e limitam com Clamp.
    // O Balanca em 17 e 18pt era o que o usuario via como "fora do padrao das outras duas telas".
    // O teste fixava uma crenca que a medicao desmente.
    //
    // O alvo passa a ser base x min(Escala, escala do cartao): com o cartao no tamanho de projeto
    // fica na base, e so cresce conforme o cartao cresce, ate o teto do fator global.
    [Theory]
    [InlineData(8.5f, 1.0f, 8.5f, 8.5f)]     // cartao no tamanho de projeto: fica na base
    [InlineData(8.5f, 1.45f, 8.5f, 12.33f)]  // escala REAL do cartao em 1920x1080: ~12pt, como Setor
    [InlineData(8.5f, 2.5f, 8.5f, 17f)]      // cartao enorme: para no teto do fator global
    [InlineData(7.75f, 1.0f, 8.5f, 8.5f)]    // piso maior que a base vence
    public void Balanca_MotorDeFonte_AcompanhaOCartao(
        float tamanhoBase, float escalaCartao, float minimo, float esperado)
    {
        float tamanho = InvocarDefinirFonte(tamanhoBase, escalaCartao, minimo);

        Assert.Equal(esperado, tamanho, 1);
        Assert.True(
            tamanho <= tamanhoBase * LegibilidadeRecebimentoMercadoria.Escala,
            "o fator global continua sendo o teto");
    }

    // Janela pequena: o texto nunca cai ABAIXO do tamanho de projeto.
    //
    // CORRIGIDO EM 25/11/2026 junto com o teste acima: antes se exigia que com 3/4 do cartao a fonte
    // ficasse ENTRE 8,5 e 17 — o que so fazia sentido quando o alvo era o dobro fixo. Agora o alvo
    // acompanha o cartao, entao cartao menor que o projeto simplesmente fica na base. O que importa,
    // e o que se cobra, e que o piso nunca seja furado.
    [Theory]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void Balanca_MotorDeFonte_CartaoPequenoNaoFuraOPiso(float escalaCartao)
    {
        float tamanho = InvocarDefinirFonte(8.5f, escalaCartao, 8.5f);

        Assert.True(tamanho >= 8.5f, $"o piso e 8,5 e veio {tamanho}");
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
