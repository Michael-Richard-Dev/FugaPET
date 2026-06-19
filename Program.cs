using FugaPET_Dev.Tela;

namespace FugaPET_Dev;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using LoginForm loginForm = new();
        if (loginForm.ShowDialog() == DialogResult.OK)
        {
            Application.Run(new PainelInicialForm());
        }
    }
}
