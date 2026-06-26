using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica da integracao de Movimentos de Material (API_MATERIAL_DOCUMENT_SRV) usada pela
/// Entrada de Produto para criar o documento de material (movimento 101). A escolha entre
/// implementacao real, demonstracao e configuracao invalida pertence a
/// <see cref="FabricaMaterialDocumentSapServico"/>.
/// </summary>
public interface IMaterialDocumentSapServico
{
    bool EhSimulado { get; }

    /// <summary>True quando ha URL de Material Document + credenciais + allowlist para o POST real.</summary>
    bool MaterialDocumentConfigurado { get; }

    /// <summary>
    /// Cria um documento de material (movimento 101) no SAP. Aplica as travas de config/escrita e
    /// registra o resultado sanitizado em <c>log_integracao_sap</c>. Nunca lanca por erro do SAP.
    /// </summary>
    Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default);
}
