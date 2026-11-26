using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Controls;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O SELO DE SITUACAO DA LISTA DE USUARIOS SAI DO DADO, NAO DO DESENHO DO DESIGNER.
/// <para>
/// Dois defeitos vistos na foto do usuario em 26/11/2026, com 37 usuarios carregados no DEV110:
/// </para>
/// <para>
/// 1. Da QUINTA linha em diante nao havia pilula nenhuma, so o texto solto. As quatro primeiras sao
/// do Designer e as demais eram clonadas — mas o clone criava um <c>Panel</c> reto copiando o
/// <c>BackColor</c>, que e Transparent: a cor do selo mora no <c>FillColor</c> do
/// <see cref="RoundedPanel"/>, e ela nao era copiada.
/// </para>
/// <para>
/// 2. A terceira linha mostrava "Ativo" dentro de uma pilula LARANJA. A linha 3 do Designer e a de
/// exemplo "Inativo" e o codigo so trocava o TEXTO; a cor continuava a do desenho. Selo que diz Ativo
/// em laranja e pior que selo nenhum, porque contradiz o proprio texto.
/// </para>
/// </summary>
public sealed class CadastroUsuarioSeloSituacaoTests
{
    private const int LinhasParaCobrir = 8;

    /// <summary>
    /// TODA LINHA TEM SELO ARREDONDADO — inclusive as clonadas, que sao a maioria quando o cadastro
    /// tem dezenas de usuarios.
    /// </summary>
    [Fact]
    public void TodaLinhaClonada_TemSeloArredondado()
        => ComATela((tela, linhas) =>
        {
            Assert.True(linhas.Count >= LinhasParaCobrir, $"esperava ao menos {LinhasParaCobrir} linhas");

            for (int i = 0; i < linhas.Count; i++)
            {
                object linha = linhas[i]!;
                object? selo = PorPosicao(linha, 5);

                Assert.True(
                    selo is RoundedPanel,
                    $"a linha {i + 1} tem o selo como {selo?.GetType().Name ?? "nulo"}; como Panel reto "
                    + "a cor do FillColor nao existe e a linha sai sem pilula");
            }
        });

    /// <summary>
    /// ATIVO E VERDE E INATIVO E LARANJA, EM QUALQUER LINHA. Cobra as duas direcoes na MESMA linha
    /// do Designer que estava errada (a terceira) e tambem numa clonada.
    /// </summary>
    [Fact]
    public void OSelo_SegueASituacao_EmQualquerLinha()
        => ComATela((tela, linhas) =>
        {
            MethodInfo aplicar = tela.GetType().GetMethod(
                "AplicarSeloDeSituacao", BindingFlags.NonPublic | BindingFlags.Instance)!;

            (Color fundoAtivo, Color textoAtivo) = Paleta(tela, "_paletaAtiva");
            (Color fundoInativo, Color textoInativo) = Paleta(tela, "_paletaInativa");

            Assert.NotEqual(fundoAtivo, fundoInativo);

            foreach (int indice in new[] { 0, 2, LinhasParaCobrir - 1 })
            {
                object linha = linhas[indice]!;
                RoundedPanel selo = (RoundedPanel)PorPosicao(linha, 5)!;
                Label texto = (Label)PorPosicao(linha, 4)!;

                aplicar.Invoke(tela, [selo, texto, true]);
                Assert.Equal("Ativo", texto.Text);
                Assert.Equal(fundoAtivo, selo.FillColor);
                Assert.Equal(textoAtivo, texto.ForeColor);

                aplicar.Invoke(tela, [selo, texto, false]);
                Assert.Equal("Inativo", texto.Text);
                Assert.Equal(fundoInativo, selo.FillColor);
                Assert.Equal(textoInativo, texto.ForeColor);
            }
        });

    /// <summary>
    /// A PALETA NAO SE CONTAMINA.
    /// <para>
    /// A cor de referencia nao pode ser lida da linha 1 na hora de pintar: a linha 1 TAMBEM e
    /// repintada, entao bastaria o primeiro usuario da lista ser inativo para a "paleta de ativo"
    /// virar laranja e levar junto todas as linhas seguintes. Por isso ela e capturada no construtor,
    /// e este teste pinta a linha 1 de inativo antes de conferir as outras.
    /// </para>
    /// </summary>
    [Fact]
    public void APaletaDeAtivo_NaoSeContaminaPelaLinhaUm()
        => ComATela((tela, linhas) =>
        {
            MethodInfo aplicar = tela.GetType().GetMethod(
                "AplicarSeloDeSituacao", BindingFlags.NonPublic | BindingFlags.Instance)!;

            (Color fundoAtivo, _) = Paleta(tela, "_paletaAtiva");

            RoundedPanel seloUm = (RoundedPanel)PorPosicao(linhas[0]!, 5)!;
            Label textoUm = (Label)PorPosicao(linhas[0]!, 4)!;
            aplicar.Invoke(tela, [seloUm, textoUm, false]);

            RoundedPanel seloDois = (RoundedPanel)PorPosicao(linhas[1]!, 5)!;
            Label textoDois = (Label)PorPosicao(linhas[1]!, 4)!;
            aplicar.Invoke(tela, [seloDois, textoDois, true]);

            Assert.Equal(fundoAtivo, seloDois.FillColor);
        });

    private static (Color Fundo, Color Texto) Paleta(Form tela, string nome)
    {
        object paleta = tela.GetType()
            .GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(tela)!;

        // (Fundo, Borda, Texto)
        return ((Color)PorPosicao(paleta, 1)!, (Color)PorPosicao(paleta, 3)!);
    }

    /// <summary>
    /// O campo de uma tupla pela POSICAO. Em tempo de execucao uma ValueTuple nao tem os nomes dos
    /// elementos — eles vivem so num atributo da declaracao —, entao procurar por "StatusPanel"
    /// devolve nulo e o teste acusaria um defeito que nao existe. Aqui a linha e
    /// (RowPanel, LoginLabel, CargoLabel, StatusLabel, StatusPanel).
    /// </summary>
    private static object? PorPosicao(object tupla, int posicao)
        => tupla.GetType().GetField("Item" + posicao)!.GetValue(tupla);

    private static void ComATela(Action<Form, System.Collections.IList> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro.CadastroUsuarioForm")!;
                ConstructorInfo construtor = tipo.GetConstructors().Single();
                using Form tela = (Form)construtor.Invoke(
                    [.. construtor.GetParameters().Select(_ => (object?)null)]);
                tela.StartPosition = FormStartPosition.Manual;
                tela.WindowState = FormWindowState.Normal;
                tela.Location = new Point(-5000, -5000);
                tela.ShowInTaskbar = false;
                tela.Show();
                tela.Size = new Size(1920, 1080);
                tela.PerformLayout();

                // Forca a lista a crescer alem das quatro linhas do Designer, que e onde o defeito vivia.
                tipo.GetMethod("GarantirLinhasDeUsuario", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(tela, [LinhasParaCobrir]);

                System.Collections.IList linhas = (System.Collections.IList)tipo
                    .GetField("_linhasUsuario", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(tela)!;

                verificar(tela, linhas);
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
}
