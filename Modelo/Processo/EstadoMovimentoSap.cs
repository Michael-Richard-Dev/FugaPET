namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Estado C# dos movimentos SAP 261 e 101 do Produto Acabado (independente da máquina de estados HU
/// homologada <see cref="StatusIntegracaoCaixa"/>, que NÃO é alterada). Confirmado exige documento SAP
/// completo (MaterialDocument + Year); IndeterminadoTimeout bloqueia a etapa seguinte (zero retry cego).
/// </summary>
public enum EstadoMovimentoSap
{
    Pendente,
    Processando,
    Confirmado,
    Erro,
    IndeterminadoTimeout
}
