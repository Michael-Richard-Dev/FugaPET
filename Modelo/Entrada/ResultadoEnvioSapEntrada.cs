namespace FugaPET_Dev.Modelo.Entrada;

public enum CenarioEnvioSapEntrada
{
    AmbienteNaoHomologacao,
    SemPermissao,
    SapNaoConfigurado,
    IntegracaoInativa,
    EscritaDesabilitada,
    LancamentoSemItens,
    Enviado,
    Parcial,
    Falha,
    FalhaPersistenciaLocal
}

public sealed record ResultadoItemEnvioSap(
    string NumeroItem,
    bool Sucesso,
    string Mensagem);
