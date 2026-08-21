namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoNormaEmbalagem
{
    public string Material { get; init; } = string.Empty;
    public string PackagingInstruction { get; init; } = string.Empty;
    public IReadOnlyList<ProdutoAcabadoNormaItem> Itens { get; init; } = [];
    public int QuantidadeProdutosPorCaixa { get; init; }
    public string MaterialCaixa { get; init; } = string.Empty;
    public string Unidade { get; init; } = "UN";

    /// <summary>
    /// Rótulo de status para a tela conforme o CENÁRIO da consulta: "CONSULTADA SAP" (válida),
    /// "NAO CONFIGURADA", "ERRO DE AUTENTICACAO", "ACESSO NAO AUTORIZADO", "ERRO NA CONSULTA" ou
    /// "SEM NORMA CADASTRADA" (API respondeu e não há cadastro).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// True somente quando a norma é VÁLIDA (cenário Encontrada + interpretação bem-sucedida). Governa
    /// a habilitação do botão INICIAR LEITURA e o preview HU. Qualquer outro cenário ⇒ false.
    /// </summary>
    public bool NormaValida { get; init; }

    /// <summary>Diagnóstico SANITIZADO do cenário (tooltip/status). Nunca contém segredo.</summary>
    public string DiagnosticoSanitizado { get; init; } = string.Empty;
}

public sealed class ProdutoAcabadoNormaItem
{
    public string Material { get; init; } = string.Empty;
    public string TipoMaterial { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
}
