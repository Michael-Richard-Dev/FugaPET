namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Snapshot C# do pipeline de integração SAP por CAIXA de Produto Acabado: estágios 261 → 101 → HU.
/// Fonte da verdade para RETOMAR do próximo passo permitido (261 confirmado ⇒ segue 101; 261+101 confirmados
/// ⇒ segue HU) e para a UI compacta. Persistência definitiva = DEPENDENCIA_GAIA (aqui é modelo em memória;
/// a HU permanece na sua máquina de estados homologada <see cref="StatusIntegracaoCaixa"/>, referida em
/// <see cref="EstadoHu"/>/<see cref="HandlingUnitSap"/>, sem ser reimplementada).
/// </summary>
public sealed class ProdutoAcabadoPipelineSnapshot
{
    public long? CodigoProdutoAcabadoCaixa { get; init; }
    public string CodigoCaixaLocal { get; init; } = string.Empty;

    // 261
    public EstadoMovimentoSap Estado261 { get; set; } = EstadoMovimentoSap.Pendente;
    public int NumeroTentativa261 { get; set; }
    public string ClaimToken261 { get; set; } = string.Empty;
    public string CorrelationId261 { get; set; } = string.Empty;
    public DateTime? IniciadoEm261 { get; set; }
    public DateTime? FinalizadoEm261 { get; set; }
    public string? MaterialDocument261 { get; set; }
    public string? MaterialDocumentYear261 { get; set; }
    public int? HttpStatus261 { get; set; }
    public string Mensagem261Sanitizada { get; set; } = string.Empty;

    // 101
    public EstadoMovimentoSap Estado101 { get; set; } = EstadoMovimentoSap.Pendente;
    public int NumeroTentativa101 { get; set; }
    public string ClaimToken101 { get; set; } = string.Empty;
    public string CorrelationId101 { get; set; } = string.Empty;
    public DateTime? IniciadoEm101 { get; set; }
    public DateTime? FinalizadoEm101 { get; set; }
    public string? MaterialDocument101 { get; set; }
    public string? MaterialDocumentYear101 { get; set; }
    public int? HttpStatus101 { get; set; }
    public string Mensagem101Sanitizada { get; set; } = string.Empty;

    // HU (estado/HU SAP existentes — NÃO reimplementados)
    public StatusIntegracaoCaixa EstadoHu { get; set; } = StatusIntegracaoCaixa.EmPesagem;
    public string? HandlingUnitSap { get; set; }

    /// <summary>Próxima etapa permitida do pipeline a partir do snapshot atual.</summary>
    public EtapaPipelineProdutoAcabado ProximaEtapa()
    {
        if (Estado261 != EstadoMovimentoSap.Confirmado)
        {
            return Estado261 is EstadoMovimentoSap.Processando or EstadoMovimentoSap.IndeterminadoTimeout or EstadoMovimentoSap.Erro
                ? EtapaPipelineProdutoAcabado.Bloqueada
                : EtapaPipelineProdutoAcabado.Movimento261;
        }

        if (Estado101 != EstadoMovimentoSap.Confirmado)
        {
            return Estado101 is EstadoMovimentoSap.Processando or EstadoMovimentoSap.IndeterminadoTimeout or EstadoMovimentoSap.Erro
                ? EtapaPipelineProdutoAcabado.Bloqueada
                : EtapaPipelineProdutoAcabado.Movimento101;
        }

        return EstadoHu == StatusIntegracaoCaixa.ConfirmadaSap
            ? EtapaPipelineProdutoAcabado.Concluido
            : EtapaPipelineProdutoAcabado.HandlingUnit;
    }
}

/// <summary>Etapa a executar/continuar no pipeline (retomada a partir do snapshot).</summary>
public enum EtapaPipelineProdutoAcabado
{
    Movimento261,
    Movimento101,
    HandlingUnit,
    Concluido,
    Bloqueada
}
