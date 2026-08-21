namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Caixa individual de Produto Acabado, PERSISTÍVEL. Uma caixa por vez: identidade estável
/// (<see cref="CorrelationId"/> gerado uma única vez), estado de integração via
/// <see cref="StatusIntegracaoCaixa"/> e campos preparados para a Handling Unit (HU) SAP.
/// NÃO contém MaterialDocument/MaterialDocumentYear nesta fase (Produto Acabado deixou o movimento 101).
/// Payloads sanitizados (nunca senha/Authorization/cookie/token).
/// </summary>
public sealed class ProdutoAcabadoCaixa
{
    // Identidade / persistência
    public long? CodigoProdutoAcabadoCaixa { get; set; }
    public int NumeroCaixa { get; set; }
    public string CodigoCaixaLocal { get; set; } = string.Empty;

    // Origem (Ordem de Produção)
    public string NumeroOrdemProducao { get; init; } = string.Empty;
    public string ItemOrdemProducao { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string Deposito { get; init; } = string.Empty;

    // Embalagem da caixa (§16): NUNCA reutiliza PALLET01; origem sempre explícita.
    public string MaterialEmbalagem { get; set; } = string.Empty;
    public OrigemMaterialEmbalagemCaixa OrigemMaterialEmbalagem { get; set; } = OrigemMaterialEmbalagemCaixa.NaoInformada;

    // Pesagem (sufixo Kg mantido por compatibilidade com o restante do processo)
    public decimal PesoBrutoKg { get; init; }
    public decimal TaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public string UnidadePeso { get; init; } = "KG";
    public int QuantidadeProdutos { get; init; }
    public string UnidadeQuantidade { get; init; } = "UN";
    public string OrigemPesagem { get; init; } = string.Empty;

    /// <summary>Balança usada na pesagem (coluna hu_caixa.codigo_balanca). Nula em pesagem MANUAL.</summary>
    public long? CodigoBalanca { get; init; }

    // Integração / Handling Unit
    public Guid CorrelationId { get; init; }
    public StatusIntegracaoCaixa StatusIntegracao { get; set; } = StatusIntegracaoCaixa.EmPesagem;
    public string? HandlingUnitExternalId { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErroSanitizado { get; set; }
    public int? HttpStatus { get; set; }

    /// <summary>Número da tentativa corrente (coluna <c>tentativas</c>). Congelado no claim; usado na finalização.</summary>
    public int Tentativas { get; set; }

    /// <summary>Token do claim atômico (coluna <c>claim_token</c>). Exigido para finalizar a tentativa corrente.</summary>
    public Guid? ClaimToken { get; set; }

    // Auditoria
    public DateTimeOffset CriadoEm { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? AtualizadoEm { get; set; }
    public DateTimeOffset? EnviadoSapEm { get; set; }
    public DateTimeOffset? ConfirmadoSapEm { get; set; }
    public long? CodigoUsuario { get; init; }
    public string Terminal { get; init; } = string.Empty;

    // Palete (fase futura; preservado para o fluxo de palete existente — oculto nesta entrega).
    public string CodigoPaleteLocal { get; set; } = string.Empty;

    /// <summary>Código de HU da caixa usado pelo builder de palete (fase futura). Não é o retorno HU desta fase.</summary>
    public string HandlingUnitCaixa { get; set; } = string.Empty;

    /// <summary>Tenta transitar o status; lança <see cref="TransicaoStatusInvalidaException"/> se inválido.</summary>
    public void TransicionarPara(StatusIntegracaoCaixa destino)
        => StatusIntegracao = TransicaoStatusIntegracaoCaixa.Transitar(StatusIntegracao, destino);
}
