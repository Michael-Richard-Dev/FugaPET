namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Preview INTERNO da Handling Unit de uma caixa individual (API_HANDLINGUNIT / OP_HANDLINGUNIT_0001).
/// ATENÇÃO: este NÃO é o DTO definitivo de transporte da API SAP. Os nomes aqui são INTERNOS (português)
/// e não afirmam correspondência com o JSON externo da OP_HANDLINGUNIT_0001. <see cref="ContratoSapConfirmado"/>
/// permanece false até o Ares fornecer $metadata, payload válido, tipos, campos obrigatórios e formato exato.
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitCaixaPreview
{
    public string NumeroOrdemProducao { get; init; } = string.Empty;
    public string ItemOrdemProducao { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string Deposito { get; init; } = string.Empty;
    public string MaterialEmbalagem { get; init; } = string.Empty;
    public decimal PesoBruto { get; init; }
    public decimal PesoLiquido { get; init; }
    public decimal Tara { get; init; }
    public string UnidadePeso { get; init; } = "KG";
    public decimal Quantidade { get; init; }
    public string UnidadeQuantidade { get; init; } = "UN";
    public Guid CorrelationId { get; init; }
    public string CodigoCaixaLocal { get; init; } = string.Empty;

    /// <summary>Sempre false nesta fase: o contrato externo da OP_HANDLINGUNIT_0001 ainda não foi confirmado pelo Ares.</summary>
    public bool ContratoSapConfirmado { get; init; }
}

/// <summary>
/// Projeção mínima de uma caixa pronta para envio à HU (entrada do gateway). Somente dados funcionais
/// já validados; nunca carrega credenciais. Separa "o que enviar" do modelo persistente completo.
/// </summary>
public sealed class ProdutoAcabadoCaixaParaEnvio
{
    public long? CodigoProdutoAcabadoCaixa { get; init; }
    public string CodigoCaixaLocal { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }
    public string NumeroOrdemProducao { get; init; } = string.Empty;
    public string ItemOrdemProducao { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string Deposito { get; init; } = string.Empty;
    public string MaterialEmbalagem { get; init; } = string.Empty;
    public decimal PesoBruto { get; init; }
    public decimal PesoLiquido { get; init; }
    public decimal Tara { get; init; }
    public string UnidadePeso { get; init; } = "KG";
    public decimal Quantidade { get; init; }
    public string UnidadeQuantidade { get; init; } = "UN";
    public string RequestPayloadSanitizado { get; init; } = string.Empty;
}
