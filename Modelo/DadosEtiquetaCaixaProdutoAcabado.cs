namespace FugaPET_Dev.Modelo;

/// <summary>
/// Contrato da etiqueta da CAIXA de Produto Acabado (Handling Unit individual). Todos os campos derivam de
/// uma <c>ProdutoAcabadoCaixa</c> PERSISTIDA (rastreabilidade pela caixa) + contexto da OP/terminal já
/// carregado na tela. Reaproveita o pipeline Zebra existente (envio RAW, gate, permissão, auditoria) — não
/// é o layout de produção do Semi-Acabado (aquele tem CAIXAS/PACOTES/SALDO/DATAS previstas, que não se
/// aplicam a uma caixa individual).
/// </summary>
public sealed class DadosEtiquetaCaixaProdutoAcabado
{
    public string OrdemProducao { get; init; } = string.Empty;
    public string ItemOrdem { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string DescricaoMaterial { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string NumeroCaixa { get; init; } = string.Empty;
    public string CodigoCaixaLocal { get; init; } = string.Empty;
    public string PesoBruto { get; init; } = string.Empty;
    public string Tara { get; init; } = string.Empty;
    public string PesoLiquido { get; init; } = string.Empty;
    public string Quantidade { get; init; } = string.Empty;
    /// <summary>Handling Unit SAP retornada (quando CONFIRMADA_SAP); vazio ⇒ "PENDENTE" na etiqueta.</summary>
    public string HandlingUnitSap { get; init; } = string.Empty;
    /// <summary>
    /// EAN da unidade CAIXA do material, lido de A_ProductUnitsOfMeasureEAN (aba "EANs adicionais" da MM02,
    /// linha CX). Vazio ⇒ a etiqueta mantém o aviso "EAN DO PRODUTO NAO CADASTRADO", nunca uma barra
    /// inventada ou de outra unidade. Pedido do usuário em 23/11/2026.
    /// </summary>
    public string EanCaixa { get; init; } = string.Empty;
    public string DataHora { get; init; } = string.Empty;
    public string Terminal { get; init; } = string.Empty;
}
