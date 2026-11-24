using System.Windows.Forms;
using System.Reflection;
using FugaPET_Dev.Tela.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// A SENHA DIGITADA PRECISA SOBREVIVER AO FECHAMENTO DO DIÁLOGO.
/// <para>
/// Defeito real, relatado pelo usuário em 24/11/2026: com usuário e senha corretos, a liberação
/// respondia "Informe usuário e senha". O botão <c>Liberar</c> tem <c>DialogResult.OK</c>, então
/// clicar nele FECHA o diálogo; o fechamento disparava <c>OnFormClosed</c>, que apagava a senha por
/// higiene — tudo isso ANTES de <c>ShowDialog</c> retornar. Quem lia <c>Senha</c> depois recebia
/// sempre vazio. A liberação nunca funcionou, em nenhum ambiente.
/// </para>
/// <para>
/// O teste é COMPORTAMENTAL de propósito. A versão por texto ("existe a captura?") passaria em
/// qualquer ordem errada das duas operações, e a ordem é justamente o defeito.
/// </para>
/// </summary>
public sealed class LiberacaoToleranciaDialogSenhaTests
{
    /// <summary>WinForms exige STA; o corpo roda numa thread própria e a falha é repassada.</summary>
    private static void EmSta(Action corpo)
    {
        Exception? falha = null;
        Thread t = new(() => { try { corpo(); } catch (Exception e) { falha = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        if (falha is not null)
        {
            throw new Xunit.Sdk.XunitException(falha.ToString());
        }
    }

    private static void Digitar(LiberacaoToleranciaDialog dialogo, string campo, string valor)
    {
        FieldInfo info = typeof(LiberacaoToleranciaDialog)
            .GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"campo {campo} nao encontrado");

        ((TextBox)info.GetValue(dialogo)!).Text = valor;
    }

    /// <summary>
    /// Simula o fechamento que o clique em Liberar provoca — <b>as duas etapas</b>, Closing e Closed.
    /// <para>
    /// Chamar só uma delas invalidaria o teste: o código defeituoso limpava a senha em
    /// <c>OnFormClosed</c>, então um teste que parasse no <c>OnFormClosing</c> passaria TAMBÉM na
    /// versão quebrada e não provaria nada.
    /// </para>
    /// </summary>
    private static void Fechar(LiberacaoToleranciaDialog dialogo, DialogResult resultado)
    {
        dialogo.DialogResult = resultado;

        typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogo, [new FormClosingEventArgs(CloseReason.None, false)]);

        typeof(Form).GetMethod("OnFormClosed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogo, [new FormClosedEventArgs(CloseReason.None)]);
    }

    [Fact]
    public void SenhaDigitada_SobreviveAoFechamentoComOk()
    {
        EmSta(() =>
        {
            using LiberacaoToleranciaDialog dialogo = new("detalhe do estouro");
            Digitar(dialogo, "_usuario", "t.gerente");
            Digitar(dialogo, "_senha", "segredo-do-gerente");

            Fechar(dialogo, DialogResult.OK);

            // ESTE assert e o defeito: antes da correcao vinha string vazia.
            Assert.Equal("segredo-do-gerente", dialogo.Senha);

            // O usuario vai sem espacos. NAO se afirma caixa alta aqui: CharacterCasing age sobre
            // DIGITACAO, e este teste atribui Text por codigo. A busca compara com lower() dos dois
            // lados, entao a caixa nao muda o desfecho.
            Assert.Equal("t.gerente", dialogo.Usuario);
        });
    }

    /// <summary>Cancelar não entrega credencial nenhuma.</summary>
    [Fact]
    public void FechamentoCancelado_NaoEntregaSenha()
    {
        EmSta(() =>
        {
            using LiberacaoToleranciaDialog dialogo = new("detalhe");
            Digitar(dialogo, "_senha", "segredo-do-gerente");

            Fechar(dialogo, DialogResult.Cancel);

            Assert.Equal(string.Empty, dialogo.Senha);
        });
    }

    /// <summary>
    /// Depois de uma recusa o diálogo segue aberto para nova tentativa, e a senha anterior NÃO pode
    /// ficar pendurada — reenviá-la sozinha repetiria a recusa sem ninguém ter digitado nada.
    /// </summary>
    [Fact]
    public void AposRecusa_SenhaAnteriorNaoFicaPendurada()
    {
        EmSta(() =>
        {
            using LiberacaoToleranciaDialog dialogo = new("detalhe");
            Digitar(dialogo, "_senha", "errada");
            Fechar(dialogo, DialogResult.OK);
            Assert.Equal("errada", dialogo.Senha);

            dialogo.MostrarErro("Credencial inválida.");

            Assert.Equal(string.Empty, dialogo.Senha);
        });
    }

    /// <summary>O controle da senha fica limpo depois do fechamento; o que viaja é a cópia.</summary>
    [Fact]
    public void ControleDaSenhaFicaLimpoAposOFechamento()
    {
        EmSta(() =>
        {
            using LiberacaoToleranciaDialog dialogo = new("detalhe");
            Digitar(dialogo, "_senha", "segredo-do-gerente");
            Fechar(dialogo, DialogResult.OK);

            TextBox senha = (TextBox)typeof(LiberacaoToleranciaDialog)
                .GetField("_senha", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dialogo)!;

            Assert.Equal(string.Empty, senha.Text);
            Assert.Equal("segredo-do-gerente", dialogo.Senha);
        });
    }
}
