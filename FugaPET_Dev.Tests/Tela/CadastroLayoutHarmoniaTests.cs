using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A HARMONIA DAS TELAS DE CADASTRO EM 1920x1080 A 100% (pedidos do usuario em 25/11/2026).
/// <para>
/// "A tela nao esta harmonizada, a quebras de informacoes, linhas sobrescrevendo, botoes
/// quebrados... para a resolucao 1920 x 1080 por 100% de escala."
/// </para>
/// <para>
/// CAUSA, medida e nao suposta: o GATE 127C grava <c>MinimumSize.Height</c> com a altura que o
/// texto precisa NA LARGURA DAQUELE MOMENTO — e nas telas de cadastro isso acontece com o cartao
/// ainda no tamanho do Designer. Depois o cartao e esticado e a fonte e reajustada para baixo, mas
/// o minimo velho continua prendendo a caixa. "Resumo do Setor" ficava numa caixa de 62px calculada
/// para TRES linhas a 18pt em 200px de largura, quando o real era uma linha a 12pt em 285px; e
/// "Situacao", centralizado numa caixa de 84px calculada para QUATRO linhas, descia ~25px e parecia
/// outra linha da tabela.
/// </para>
/// <para>
/// Os testes MONTAM a tela em 1920x1080 e medem os Bounds. Foi assim que a causa apareceu — pelos
/// pixels eu estava inferindo errado, e a primeira correcao que escrevi piorou a tela.
/// </para>
/// </summary>
public sealed class CadastroLayoutHarmoniaTests
{
    /// <summary>
    /// OS DOIS CABECALHOS DA LISTA NA MESMA LINHA. Era a "linha sobrescrevendo" do relato: mesma
    /// posicao Y, mas caixas de alturas diferentes (33 contra 84) e texto centralizado.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OsCabecalhosDaLista_FicamNaMesmaLinha(string tela)
        => ComATela(tela, c =>
        {
            Label setor = (Label)c["profilesHeaderProfileLabel"];
            Label situacao = (Label)c["profilesHeaderStatusLabel"];

            Assert.Equal(setor.Top, situacao.Top);
            Assert.Equal(setor.Height, situacao.Height);
        });

    /// <summary>
    /// NENHUM ROTULO FICA COM CAIXA INFLADA: a altura nao pode passar muito do que o texto precisa
    /// na largura que o rotulo REALMENTE tem. Era exatamente o estrago do minimo obsoleto, e e por
    /// isso que a medicao usa a largura final, nao a do Designer.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void NenhumRotulo_FicaComCaixaInflada(string tela)
        => ComATela(tela, c =>
        {
            foreach (string nome in new[]
            {
                "summaryTitleLabel", "summaryPerfilCaptionLabel", "summaryPerfilValueLabel",
                "tipTextLabel", "profilesHeaderProfileLabel", "profilesHeaderStatusLabel",
            })
            {
                Label rotulo = (Label)c[nome];
                int necessario = TextRenderer.MeasureText(
                    rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    rotulo.Height <= necessario + 24,
                    $"'{nome}' tem {rotulo.Height}px para um texto que precisa de {necessario}px "
                    + $"em {rotulo.Width}px de largura — caixa inflada por minimo obsoleto");
            }
        });

    /// <summary>
    /// E NADA FICA CORTADO: inflar era o defeito, mas encolher demais seria trocar um por outro.
    /// Esta guarda e o contrapeso da anterior.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void NenhumRotulo_FicaCortado(string tela)
        => ComATela(tela, c =>
        {
            foreach (string nome in new[]
            {
                "summaryTitleLabel", "summaryPerfilCaptionLabel", "summaryPerfilValueLabel",
                "profilesHeaderProfileLabel", "profilesHeaderStatusLabel",
            })
            {
                Label rotulo = (Label)c[nome];
                int necessario = TextRenderer.MeasureText(
                    rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    rotulo.Height >= necessario,
                    $"'{nome}' tem {rotulo.Height}px e o texto precisa de {necessario}px — cortaria");
            }
        });

    /// <summary>
    /// Os divisores do cartao de resumo NAO SE SOBREPOEM aos blocos de texto. Linha passando por
    /// cima de texto foi uma das quebras que o usuario viu.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OsDivisores_NaoCruzamOsTextos(string tela)
        => ComATela(tela, c =>
        {
            Control[] divisores =
            [
                c["summaryDividerLabel"], c["summaryDividerLabel2"],
                c["summaryDividerLabel3"], c["summaryDividerLabel4"],
            ];

            Control[] textos =
            [
                c["summaryTitleLabel"], c["summaryPerfilCaptionLabel"],
                c["summaryPerfilValueLabel"], c["tipTextLabel"],
            ];

            foreach (Control divisor in divisores)
            {
                foreach (Control texto in textos)
                {
                    bool cruza = divisor.Top > texto.Top && divisor.Top < texto.Bottom;
                    Assert.False(
                        cruza,
                        $"um divisor em y={divisor.Top} cruza '{texto.Text}' ({texto.Top}..{texto.Bottom})");
                }
            }
        });

    /// <summary>
    /// A limpeza do minimo obsoleto acontece ANTES de posicionar, e a protecao contra texto cortado
    /// e REAPLICADA depois — com a largura e a fonte reais. Sem a reaplicacao, a tela ficaria sem a
    /// guarda do GATE 127C.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OMinimoELimpoAntes_EAProtecaoReaplicadaDepois(string tela)
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Cadastro", tela + ".cs"));

        Assert.Equal(3, Ocorrencias(fonte, "LimparAlturaMinimaDosRotulos("));
        Assert.Contains("ReaplicarProtecaoDeAltura(", fonte, StringComparison.Ordinal);
        Assert.Contains("AdaptarAlturasParaFonte(cartao)", fonte, StringComparison.Ordinal);
    }

    // ------------------------------------------------- apoio

    // ------------------------------------------------- Balanca (desenho proprio)

    /// <summary>
    /// A tela de Balanca nao tem os mesmos controles das outras duas, entao tem guardas proprias.
    /// Aqui: OS TRES CABECALHOS DA LISTA no mesmo topo e com a mesma altura. "Setor / Conexao"
    /// ocupa duas linhas de verdade; os outros dois precisam acompanhar, senao cada titulo aparece
    /// numa altura e o da Situacao fica cortado, como no print do usuario.
    /// </summary>
    [Fact]
    public void Balanca_OsTresCabecalhos_FicamAlinhados()
        => ComATela("BalancaForm", c =>
        {
            Label[] cabecalhos =
            [
                (Label)c["listaNomeHeaderLabel"],
                (Label)c["listaDetalhesHeaderLabel"],
                (Label)c["listaSituacaoHeaderLabel"],
            ];

            Assert.Single(cabecalhos.Select(h => h.Top).Distinct());
            Assert.Single(cabecalhos.Select(h => h.Height).Distinct());
            Assert.Single(cabecalhos.Select(h => h.TextAlign.ToString()[..3]).Distinct());
        });

    /// <summary>
    /// E A FAIXA QUE OS HOSPEDA CABE NELES. Dar altura ao rotulo nao adianta se o painel continua
    /// com os 28px do Designer: foi assim que "Identificacao" e "Situacao" sumiram mesmo ja
    /// alinhados.
    /// </summary>
    [Fact]
    public void Balanca_AFaixaDoCabecalho_CabeOsRotulos()
        => ComATela("BalancaForm", c =>
        {
            Control faixa = c["listaHeaderPanel"];

            foreach (string nome in new[]
                     { "listaNomeHeaderLabel", "listaDetalhesHeaderLabel", "listaSituacaoHeaderLabel" })
            {
                Label cabecalho = (Label)c[nome];
                Assert.True(
                    cabecalho.Bottom <= faixa.Height,
                    $"'{cabecalho.Text}' termina em {cabecalho.Bottom} e a faixa tem {faixa.Height}px");
            }
        });

    /// <summary>
    /// NO RESUMO, A LEGENDA NAO INVADE O VALOR. O Designer poe a legenda em Y com 18px e o valor em
    /// Y+20; quando a adaptacao de fonte cresce a legenda para 36px, ela passa POR CIMA do valor —
    /// 16px de sobreposicao em cada uma das quatro linhas.
    /// </summary>
    [Fact]
    public void Balanca_NoResumo_ALegendaNaoInvadeOValor()
        => ComATela("BalancaForm", c =>
        {
            (string Legenda, string Valor)[] linhas =
            [
                ("resumoNomeLabel", "resumoNomeValueLabel"),
                ("resumoSetorLabel", "resumoSetorValueLabel"),
                ("resumoConexaoLabel", "resumoConexaoValueLabel"),
                ("resumoSituacaoLabel", "resumoSituacaoValueLabel"),
            ];

            foreach ((string legenda, string valor) in linhas)
            {
                Label l = (Label)c[legenda];
                Label v = (Label)c[valor];

                Assert.True(
                    l.Bottom <= v.Top,
                    $"'{l.Text}' termina em {l.Bottom} e o valor comeca em {v.Top} — sobrepostos");
            }
        });

    /// <summary>
    /// Os divisores do resumo atravessam o cartao. Vinham com a largura do Designer e paravam no
    /// meio da linha quando o cartao estica.
    /// </summary>
    [Fact]
    public void Balanca_OsDivisores_AtravessamOCartao()
        => ComATela("BalancaForm", c =>
        {
            Control cartao = c["resumoCard"];

            foreach (string nome in new[]
                     { "resumoDivisorTitulo", "resumoDivisorNome", "resumoDivisorSetor", "resumoDivisorConexao" })
            {
                Control divisor = c[nome];
                Assert.True(
                    divisor.Width >= cartao.ClientSize.Width - (divisor.Left * 2) - 2,
                    $"{nome} tem {divisor.Width}px num cartao de {cartao.ClientSize.Width}px");
            }
        });

    /// <summary>
    /// A FONTE DO BALANCA ACOMPANHA A DAS OUTRAS DUAS TELAS (relatado em 25/11/2026: "ver sobre a
    /// fonte, nao esta padrao das outras duas telas").
    /// <para>
    /// O Balanca mirava em <c>base * Escala</c>, com Escala = 2,0 — e como o segundo fator nunca
    /// passa de 1, o resultado era SEMPRE o dobro: 17pt onde Setor e Cargo mostram 12,1pt, e 18pt
    /// na busca contra 12pt. Era por isso que "Balancas Cadastradas" quebrava em duas linhas.
    /// </para>
    /// <para>
    /// A tolerancia de 1,5pt existe porque as telas chegam ao tamanho por caminhos diferentes
    /// (Clamp por controle contra escala do cartao); o que nao pode e a diferenca de 5pt que o
    /// usuario viu.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("balancasTitleLabel", "profilesTitleLabel")]
    [InlineData("searchTextBox", "searchTextBox")]
    [InlineData("listaNomeHeaderLabel", "profilesHeaderProfileLabel")]
    [InlineData("resumoTitleLabel", "summaryTitleLabel")]
    public void Balanca_TemAMesmaFonteDasOutrasTelas(string noBalanca, string noSetor)
    {
        float tamanhoBalanca = 0;
        float tamanhoSetor = 0;

        ComATela("BalancaForm", c => tamanhoBalanca = c[noBalanca].Font.Size);
        ComATela("SetorForm", c => tamanhoSetor = c[noSetor].Font.Size);

        Assert.True(
            Math.Abs(tamanhoBalanca - tamanhoSetor) <= 1.5f,
            $"'{noBalanca}' do Balanca esta em {tamanhoBalanca:0.00}pt e o equivalente do Setor "
            + $"('{noSetor}') em {tamanhoSetor:0.00}pt");
    }

    private static int Ocorrencias(string texto, string agulha)
        => texto.Split(agulha).Length - 1;

    private static void ComATela(string nomeDaTela, Action<Dictionary<string, Control>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro." + nomeDaTela)
                    ?? throw new InvalidOperationException("tela " + nomeDaTela + " nao encontrada");
                // Os construtores recebem dependencias OPCIONAIS, e cada tela tem um numero
                // diferente delas (Balanca tem tres, Setor e Cargo tem duas). CreateInstance sem
                // argumentos falha por "no parameterless constructor", e um numero fixo de nulos
                // falha na tela que tem outro numero — por isso a contagem sai do PROPRIO
                // construtor. Nulo faz a tela montar as proprias dependencias, como em producao.
                ConstructorInfo construtor = tipo.GetConstructors().Single();
                object?[] nulos = [.. construtor.GetParameters().Select(_ => (object?)null)];

                using Form tela = (Form)construtor.Invoke(nulos);
                tela.StartPosition = FormStartPosition.Manual;
                tela.Location = new Point(-5000, -5000);
                tela.Size = new Size(1920, 1080);
                tela.ShowInTaskbar = false;
                tela.Show();
                tela.Refresh();

                Dictionary<string, Control> controles = [];
                foreach (FieldInfo campo in tela.GetType()
                             .GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (campo.GetValue(tela) is Control c)
                    {
                        controles[campo.Name] = c;
                    }
                }

                verificar(controles);
                tela.Hide();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        if (erro is not null)
        {
            throw erro;
        }
    }

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
