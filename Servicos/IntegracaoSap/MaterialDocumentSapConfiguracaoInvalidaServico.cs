using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Estado de erro de implantacao: arquivo de configuracao SAP existente porem malformado. Bloqueia o
/// envio sem cair em mock nem em "nao configurado silencioso". Nunca expoe caminho/conteudo.
/// </summary>
internal sealed class MaterialDocumentSapConfiguracaoInvalidaServico : IMaterialDocumentSapServico
{
    public bool EhSimulado => false;

    public bool MaterialDocumentConfigurado => false;

    public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoMaterialDocumentSap.Falha(
            null, ConfiguracaoSap.MensagemConfiguracaoInvalida));
}
