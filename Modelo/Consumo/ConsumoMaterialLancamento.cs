namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>
/// Cabecalho do consumo LOCAL por OP (tabela consumo_material_lancamento). Persistencia local,
/// status inicial PENDENTE_SAP. Nao envia SAP nesta etapa.
/// </summary>
public sealed class ConsumoMaterialLancamento
{
    public const string StatusPendenteSap = "PENDENTE_SAP";
    public const string StatusEnviandoSap = "ENVIANDO_SAP";
    public const string StatusConfirmadoSap = "CONFIRMADO_SAP";
    public const string StatusFalhaSap = "FALHA_SAP";

    public long Codigo { get; set; }
    public string NumeroOrdem { get; set; } = string.Empty;
    public string? Centro { get; set; }
    public string? MaterialProduzido { get; set; }
    public string? LoteOrdem { get; set; }
    public decimal? QuantidadePrevista { get; set; }
    public string? Unidade { get; set; }
    public string StatusLancamento { get; set; } = StatusPendenteSap;
    public string? Observacao { get; set; }
    public string? UsuarioCriacao { get; set; }

    /// <summary>Rastreabilidade SAP (preenchida somente apos CONFIRMADO_SAP; usada p/ bloquear reenvio).</summary>
    public string? DocumentoMaterialSap { get; set; }
    public string? ExercicioDocumentoMaterialSap { get; set; }

    public IReadOnlyList<ConsumoMaterialItem> Itens { get; set; } = [];
}
