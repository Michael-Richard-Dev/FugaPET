namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Caminho de envio do consumo de um componente ao SAP. Define se o componente vai pelo movimento
/// 261 direto (API_MATERIAL_DOCUMENT_SRV), se exige Confirmacao de Producao (Backflush) ou se esta
/// bloqueado por outro motivo. Apenas classificacao/diagnostico — nao executa POST.
/// </summary>
public enum ClassificacaoEnvioConsumo261
{
    /// <summary>Elegivel ao movimento 261 direto (API_MATERIAL_DOCUMENT_SRV).</summary>
    MaterialDocument261Direto,

    /// <summary>Backflush: consumo deve ocorrer via Confirmacao de Producao (nao 261 direto).</summary>
    RequerConfirmacaoProducao,

    /// <summary>Inelegivel por outro motivo (reserva finalizada, granel, unidade, lote, etc.).</summary>
    Bloqueado
}

/// <summary>Codigos textuais estaveis da classificacao (para tela/log/testes).</summary>
public static class ClassificacaoEnvioConsumo261Extensoes
{
    public const string CodigoMaterialDocument261Direto = "MATERIAL_DOCUMENT_261_DIRETO";
    public const string CodigoRequerConfirmacaoProducao = "REQUER_CONFIRMACAO_PRODUCAO";
    public const string CodigoBloqueado = "BLOQUEADO";

    public static string Codigo(this ClassificacaoEnvioConsumo261 classificacao)
        => classificacao switch
        {
            ClassificacaoEnvioConsumo261.MaterialDocument261Direto => CodigoMaterialDocument261Direto,
            ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao => CodigoRequerConfirmacaoProducao,
            _ => CodigoBloqueado
        };
}
