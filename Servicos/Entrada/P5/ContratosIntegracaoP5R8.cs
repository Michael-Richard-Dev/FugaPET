namespace FugaPET_Dev.Servicos.Entrada.P5;

internal enum PapelP5R8 { Provider, Recovery }

internal sealed record DependenciasP5R8(
    bool ProjecaoFkHomologada = false,
    bool BootstrapHomologado = false,
    bool FencingLegadoHomologado = false,
    bool TransicaoLoteHomologada = false,
    bool IdentidadesBancoHomologadas = false)
{
    internal bool Completa => ProjecaoFkHomologada && BootstrapHomologado && FencingLegadoHomologado
        && TransicaoLoteHomologada && IdentidadesBancoHomologadas;
}
