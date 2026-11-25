using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O ICONE DOS TRES CARTOES CLONADOS (pedido do usuario em 25/11/2026, com print).
/// <para>
/// "Os tres ultimos cards estao com um icone incompativel com o que representa, coloque o mesmo
/// icone do setor nesses tres."
/// </para>
/// <para>
/// CAUSA: Vinculo de Produto e as duas Tolerancias sao montados clonando o cartao de ETIQUETA — e
/// dele herdam tamanho, margens e fonte. O simbolo veio junto, entao os tres exibiam o "ABC" de
/// etiqueta, que nada tem a ver com vinculo nem com tolerancia. Clonar o DESENHO de um cartao nao
/// obriga a clonar o SIMBOLO dele.
/// </para>
/// <para>
/// O teste compara os BYTES da imagem renderizada, nao o nome do campo de onde ela veio: e a
/// imagem que o usuario ve, e trocar a origem mantendo o pixel errado passaria numa auditoria de
/// fonte.
/// </para>
/// </summary>
public sealed class CadastroCartoesIconeTests
{
    private static readonly string[] CartoesClonados =
    [
        "Vínculo de Produto",
        "Tolerância de Recebimento",
        "Tolerância de Produção",
    ];

    /// <summary>Os tres cartoes clonados exibem a MESMA imagem do cartao de Setor.</summary>
    [Fact]
    public void OsTresClonados_UsamOIconeDoSetor()
        => ComOsIcones(icones =>
        {
            byte[] doSetor = Bytes(icones["Setor"]);

            foreach (string titulo in CartoesClonados)
            {
                Assert.True(icones.ContainsKey(titulo), $"cartao '{titulo}' nao encontrado na tela");
                Assert.True(
                    Bytes(icones[titulo]).SequenceEqual(doSetor),
                    $"'{titulo}' nao esta com o icone do Setor");
            }
        });

    /// <summary>
    /// E NAO estao mais com o icone de Etiqueta — o que o usuario viu no print. Sem esta metade, um
    /// icone em branco nos tres tambem passaria pela primeira guarda se o de Setor sumisse.
    /// </summary>
    [Fact]
    public void OsTresClonados_NaoUsamMaisOIconeDeEtiqueta()
        => ComOsIcones(icones =>
        {
            byte[] deEtiqueta = Bytes(icones["Etiqueta"]);
            byte[] doSetor = Bytes(icones["Setor"]);

            Assert.False(
                deEtiqueta.SequenceEqual(doSetor),
                "Setor e Etiqueta teriam de ter icones DIFERENTES para este teste significar algo");

            foreach (string titulo in CartoesClonados)
            {
                Assert.False(
                    Bytes(icones[titulo]).SequenceEqual(deEtiqueta),
                    $"'{titulo}' continua com o icone de Etiqueta");
            }
        });

    /// <summary>
    /// Os cartoes do Designer NAO foram tocados: cada um mantem o seu simbolo. O pedido era sobre
    /// os tres ultimos, e repintar a grade inteira com o icone de Setor apagaria a distincao que
    /// ainda funciona.
    /// </summary>
    [Fact]
    public void OsCartoesDoDesigner_MantemCadaUmOSeuIcone()
        => ComOsIcones(icones =>
        {
            string[] doDesigner =
                [.. icones.Keys.Where(k => !CartoesClonados.Contains(k, StringComparer.Ordinal))];

            Assert.True(doDesigner.Length >= 7, $"deveria haver ao menos 7 cartoes do Designer e ha {doDesigner.Length}");

            int distintos = doDesigner.Select(k => Convert.ToBase64String(Bytes(icones[k]))).Distinct().Count();

            Assert.True(distintos >= 5, $"os cartoes do Designer tem so {distintos} icones distintos — algo os uniformizou");
        });

    // ------------------------------------------------- apoio

    private static byte[] Bytes(Image? imagem)
    {
        if (imagem is null)
        {
            return [];
        }

        using MemoryStream memoria = new();
        imagem.Save(memoria, ImageFormat.Png);

        return memoria.ToArray();
    }

    private static void ComOsIcones(Action<Dictionary<string, Image?>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                using CadastroForm tela = new();

                FlowLayoutPanel grade = tela.Controls.Find("modulesFlowLayoutPanel", searchAllChildren: true)
                    .OfType<FlowLayoutPanel>().Single();

                Dictionary<string, Image?> icones = [];
                foreach (Control cartao in grade.Controls)
                {
                    FugaPET_Dev.Tela.Controls.RoundedPanel? ladrilho =
                        cartao.Controls.OfType<FugaPET_Dev.Tela.Controls.RoundedPanel>().FirstOrDefault();
                    Label[] rotulos = [.. cartao.Controls.OfType<Label>().OrderBy(r => r.Top)];

                    if (ladrilho is null || rotulos.Length < 3)
                    {
                        continue;
                    }

                    icones[rotulos[0].Text] = ladrilho.Controls.OfType<Label>().FirstOrDefault()?.Image;
                }

                verificar(icones);
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
