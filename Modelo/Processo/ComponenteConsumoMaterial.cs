namespace FugaPET_Dev.Modelo.Processo;

public sealed class ComponenteConsumoMaterial
{
    public const string StatusPendente = "PENDENTE";
    public const string StatusConsumido = "CONSUMIDO";

    public string CodigoMaterial { get; set; } = string.Empty;
    public string DescricaoMaterial { get; set; } = string.Empty;
    public string Centro { get; set; } = string.Empty;
    public string DepositoConsumo { get; set; } = string.Empty;
    public string NumeroReserva { get; set; } = string.Empty;
    public string ItemReserva { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;

    public decimal QuantidadePrevista { get; set; }          // RequiredQuantity
    public decimal QuantidadeConsumida { get; set; }         // WithdrawnQuantity
    public decimal QuantidadePendente { get; set; }          // max(0, Required - Withdrawn)

    /// <summary>Saldo SAP BRUTO (Required - Withdrawn), pode ser negativo. Apenas diagnostico/tooltip.</summary>
    public decimal QuantidadePendenteSapOriginal { get; set; }
    public decimal QuantidadeDisponivelConfirmada { get; set; }
    public string UnidadeMedida { get; set; } = string.Empty;

    /// <summary>Tipo de movimento esperado para o consumo (261 por padrao). Modelo de tela; sem POST.</summary>
    public string TipoMovimento { get; set; } = string.Empty;

    public string ItemBOM { get; set; } = string.Empty;
    public string CategoriaItemBOM { get; set; } = string.Empty;
    public bool ReservaFinalizada { get; set; }
    public bool MarcadoParaEliminacao { get; set; }
    public bool MaterialGranel { get; set; }
    public bool BackflushSap { get; set; }
    public string TipoSplitLote { get; set; } = string.Empty;

    /// <summary>Operacao do componente no SAP (ManufacturingOrderOperation). Usada no preview de Confirmacao.</summary>
    public string Operacao { get; set; } = string.Empty;

    /// <summary>ID interno real da operacao no SAP. Obrigatorio para envio real por Confirmacao.</summary>
    public string OrderOperationInternalId { get; set; } = string.Empty;

    /// <summary>Sequencia do componente no SAP (ManufacturingOrderSequence), quando existir.</summary>
    public string SequenciaOperacao { get; set; } = string.Empty;

    public bool ElegivelMaterialDocument261Direto { get; set; }
    public string MotivoInelegibilidadeMaterialDocument261 { get; set; } = string.Empty;

    /// <summary>
    /// Caminho de envio do consumo ao SAP: 261 direto, Confirmacao de Producao (Backflush) ou bloqueado.
    /// Apenas classificacao/diagnostico — nao executa POST.
    /// </summary>
    public ClassificacaoEnvioConsumo261 ClassificacaoEnvio { get; set; } = ClassificacaoEnvioConsumo261.Bloqueado;

    /// <summary>Status do componente (<see cref="StatusPendente"/> / <see cref="StatusConsumido"/>).</summary>
    public string Status { get; set; } = StatusPendente;

    /// <summary>True quando o componente ainda pode ser pesado (pendente e ordem liberada).</summary>
    public bool PesagemLiberada { get; set; }

    /// <summary>Motivo (sanitizado) do bloqueio de pesagem quando <see cref="PesagemLiberada"/> for false.</summary>
    public string MotivoBloqueioPesagem { get; set; } = string.Empty;
}
