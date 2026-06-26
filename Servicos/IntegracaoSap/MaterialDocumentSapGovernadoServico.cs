using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Aplica a governanca central (ambiente, integracao ativa, configuracao, permissao ENVIAR_SAP) antes
/// de delegar a criacao do documento de material ao servico real. Espelha
/// <see cref="PedidoCompraSapGovernadoServico"/> para a operacao de Escrita.
/// </summary>
internal sealed class MaterialDocumentSapGovernadoServico : IMaterialDocumentSapServico
{
    private readonly IMaterialDocumentSapServico _servicoInterno;
    private readonly EstadoIntegracaoSapServico _estadoIntegracaoSapServico;

    public MaterialDocumentSapGovernadoServico(
        IMaterialDocumentSapServico servicoInterno,
        EstadoIntegracaoSapServico estadoIntegracaoSapServico)
    {
        _servicoInterno = servicoInterno;
        _estadoIntegracaoSapServico = estadoIntegracaoSapServico;
    }

    public bool EhSimulado => false;

    public bool MaterialDocumentConfigurado => _servicoInterno.MaterialDocumentConfigurado;

    public async Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Escrita,
            cancellationToken);
        return validacao.Sucesso
            ? await _servicoInterno.CriarDocumentoMaterial101Async(requisicao, chaveNegocio, cancellationToken)
            : ResultadoMaterialDocumentSap.Falha(null, validacao.Mensagem);
    }
}
