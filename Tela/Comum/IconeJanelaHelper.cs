namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// Aplica o icone padrao do sistema (o mesmo do menu/PainelInicialForm) na barra de titulo e na
/// barra de tarefas do Windows. Centraliza o caminho do icone para todas as telas usarem o mesmo.
/// </summary>
public static class IconeJanelaHelper
{
    private const string CaminhoIcone = "Servicos\\icone\\fuga.ico";

    public static void AplicarIconePadrao(Form janela)
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, CaminhoIcone);
        if (File.Exists(caminho))
        {
            janela.Icon = new Icon(caminho);
        }
    }
}
