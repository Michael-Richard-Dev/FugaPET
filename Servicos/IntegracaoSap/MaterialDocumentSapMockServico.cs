using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Implementacao de DEMONSTRACAO: nunca cria documento real no SAP. Reporta-se como nao configurada
/// para que a tela bloqueie o envio.
/// </summary>
internal sealed class MaterialDocumentSapMockServico : IMaterialDocumentSapServico
{
    internal MaterialDocumentSapMockServico(bool usoAutorizado)
    {
        if (!usoAutorizado)
        {
            throw new InvalidOperationException(
                "Mock SAP proibido fora de ambiente demonstrativo com banco desabilitado.");
        }
    }

    public bool EhSimulado => true;

    public bool MaterialDocumentConfigurado => false;

    public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoMaterialDocumentSap.Falha(
            null, "Modo demonstracao: nenhum documento de material foi enviado ao SAP."));
}
