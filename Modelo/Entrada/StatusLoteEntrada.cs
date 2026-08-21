namespace FugaPET_Dev.Modelo.Entrada;

public static class StatusLoteEntrada
{
    public const string AguardandoDados = "AGUARDANDO_DADOS";
    public const string LoteConfirmado = "LOTE_CONFIRMADO";
    public const string Pesando = "PESANDO";
    public const string FinalizadoLocal = "FINALIZADO_LOCAL";
    public const string EnviandoSap = "ENVIANDO_SAP";
    public const string ConfirmadoSap = "CONFIRMADO_SAP";
    public const string ErroSap = "ERRO_SAP";
    public const string Cancelado = "CANCELADO";
}
