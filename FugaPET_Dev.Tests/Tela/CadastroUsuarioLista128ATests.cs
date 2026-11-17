using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Cadastro;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 128A — a lista da tela de usuarios cresce com o cadastro.
/// <para>
/// POR QUE EXISTE: ate 17/11/2026 a tela tinha QUATRO paineis de linha fixos, vindos do Designer, e
/// o laco de carga preenchia no maximo esses quatro. A consulta sempre trouxe todos os usuarios — o
/// teto era so da tela. Com o cadastro do QAS110 copiado para o DEV110 (2 para 35 usuarios) isso
/// ficou visivel: o rodape dizia "Mostrando 4 usuarios cadastrados".
/// </para>
/// <para>
/// Este teste exercita o mecanismo REAL de criacao de linhas, nao o texto do fonte.
/// </para>
/// </summary>
public sealed class CadastroUsuarioLista128ATests
{
    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try { acao(); }
            catch (Exception ex) { erro = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (erro is not null) throw erro;
    }

    private static T Campo<T>(object alvo, string nome)
    {
        FieldInfo? f = alvo.GetType().GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(f);
        return (T)f!.GetValue(alvo)!;
    }

    private static void GarantirLinhas(CadastroUsuarioForm form, int quantidade)
    {
        MethodInfo? m = typeof(CadastroUsuarioForm).GetMethod(
            "GarantirLinhasDeUsuario", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(m);
        m!.Invoke(form, [quantidade]);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(35)]
    [InlineData(120)]
    public void Lista_CriaUmaLinhaPorUsuario_SemTetoDeQuatro(int quantidade)
        => ExecutarEmSta(() =>
        {
            using CadastroUsuarioForm form = new();

            GarantirLinhas(form, quantidade);

            var linhas = Campo<System.Collections.IList>(form, "_linhasUsuario");
            Assert.True(
                linhas.Count >= quantidade,
                $"esperado ao menos {quantidade} linhas; a tela criou {linhas.Count}");
        });

    // Sem rolagem, da quinta linha em diante o conteudo nasce fora da area visivel: existiria no
    // controle e continuaria invisivel para o operador — o mesmo sintoma, por outra causa.
    [Fact]
    public void Lista_LigaARolagemAoCriarLinhasAlemDasFixas()
        => ExecutarEmSta(() =>
        {
            using CadastroUsuarioForm form = new();
            GarantirLinhas(form, 30);

            Panel tabela = Campo<Panel>(form, "profilesTablePanel");
            Assert.True(tabela.AutoScroll, "a lista precisa rolar para mostrar as linhas criadas");
        });

    // Cada linha nova precisa ser clicavel e filtravel como as do Designer: entrar na selecao, no
    // filtro de busca e ganhar o marcador lateral. Sem isso ela aparece mas nao opera.
    [Fact]
    public void Lista_LinhasNovasEntramNaSelecaoNoFiltroENoMarcador()
        => ExecutarEmSta(() =>
        {
            using CadastroUsuarioForm form = new();
            GarantirLinhas(form, 20);

            var selecoes = Campo<System.Collections.IList>(form, "_userRowSelections");
            var busca = Campo<System.Collections.IList>(form, "_profileSearchRows");
            var marcadores = Campo<System.Collections.IDictionary>(form, "_rowSelectionMarkers");

            Assert.True(selecoes.Count >= 20, $"selecao cobre {selecoes.Count} linhas");
            Assert.True(busca.Count >= 20, $"filtro cobre {busca.Count} linhas");
            Assert.True(marcadores.Count >= 20, $"marcador cobre {marcadores.Count} linhas");
        });

    // GATE 128A — GarantirLinhasDeUsuario CLONA a primeira linha, entao toda linha criada em runtime
    // tem de sair com a MESMA tipografia e altura do modelo. Vale para a fonte atual e valeria para
    // uma fonte ampliada, se um dia esta tela receber a escala (hoje nao recebe: ver
    // Usuario_RecebeLogoMasNaoAEscalaCompartilhada). Sem isso a lista ficaria com as quatro
    // primeiras linhas num tamanho e as demais noutro — o que nao quebra nada, so fica feio.
    [Fact]
    public void Lista_LinhasNovasNascemComATipografiaDaTela()
        => ExecutarEmSta(() =>
        {
            using CadastroUsuarioForm form = new();

            Label modeloLogin = Campo<Label>(form, "profileRow1LoginLabel");
            Panel modeloLinha = Campo<Panel>(form, "profileRow1Panel");
            float corpoModelo = modeloLogin.Font.Size;
            int alturaModelo = modeloLinha.Height;

            GarantirLinhas(form, 12);

            var linhas = Campo<System.Collections.IList>(form, "_linhasUsuario");
            object ultima = linhas[linhas.Count - 1]!;

            // Tupla nomeada: os nomes existem so em tempo de compilacao; por reflexao sao Item1..ItemN.
            Panel linhaNova = (Panel)ultima.GetType().GetField("Item1")!.GetValue(ultima)!;
            Label loginNovo = (Label)ultima.GetType().GetField("Item2")!.GetValue(ultima)!;

            Assert.Equal(corpoModelo, loginNovo.Font.Size, 2);
            Assert.Equal(alturaModelo, linhaNova.Height);
        });

    // As linhas sao empilhadas sem sobreposicao nem buraco: cada uma comeca onde a anterior termina.
    [Fact]
    public void Lista_EmpilhaLinhasSemSobreposicao()
        => ExecutarEmSta(() =>
        {
            using CadastroUsuarioForm form = new();
            GarantirLinhas(form, 10);

            Panel primeira = Campo<Panel>(form, "profileRow1Panel");
            Panel tabela = Campo<Panel>(form, "profilesTablePanel");

            List<Panel> linhas = tabela.Controls.OfType<Panel>()
                .Where(p => p.Name.StartsWith("profileRow", StringComparison.Ordinal)
                            && p.Name.EndsWith("Panel", StringComparison.Ordinal))
                .OrderBy(p => p.Top)
                .ToList();

            Assert.True(linhas.Count >= 10, $"esperado ao menos 10 paineis de linha; havia {linhas.Count}");

            for (int i = 1; i < linhas.Count; i++)
            {
                int esperado = linhas[i - 1].Top + primeira.Height;
                Assert.Equal(esperado, linhas[i].Top);
            }
        });
}
