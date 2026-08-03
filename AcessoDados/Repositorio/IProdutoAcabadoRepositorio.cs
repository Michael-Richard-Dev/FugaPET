using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Contrato de persistência da caixa individual de Produto Acabado (Handling Unit). Uma caixa por vez,
/// identidade preservada (correlation_id), claim ATÔMICO antes do envio. Nenhuma View acessa banco.
///
/// IMPORTANTE: nesta fase NÃO existe schema aplicável no banco de Produto Acabado — nenhuma tabela/coluna
/// foi criada nesta tarefa. A persistência produtiva está classificada como
/// BLOQUEADA_POR_SCHEMA_PRODUTO_ACABADO_PENDENTE. O contrato exato para a Gaia Dados está no relatório.
/// Apenas a interface (+ fakes de teste) existe; a implementação real depende do schema aprovado.
/// </summary>
public interface IProdutoAcabadoRepositorio
{
    /// <summary>Persiste uma nova caixa (identidade + correlation_id). Retorna a caixa com o código atribuído.</summary>
    /// <remarks>
    /// A caixa chega sem identidade sequencial definitiva. Na mesma transação, o Repository deve alocar
    /// atomicamente o próximo NumeroCaixa por NumeroOrdemProducao, formar CodigoCaixaLocal no padrão
    /// CX-&lt;OP&gt;-&lt;NUMERO_4_DIGITOS&gt;, persistir e retornar o snapshot completo. O contrato Gaia deve garantir
    /// UNIQUE(numero_ordem_producao, numero_caixa), UNIQUE(codigo_caixa_local) e UNIQUE(correlation_id).
    /// </remarks>
    Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default);

    /// <summary>Caixa ATIVA (não confirmada/não cancelada) do terminal, ou null. Garante "uma caixa por vez".</summary>
    Task<ProdutoAcabadoCaixa?> ObterCaixaAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default);

    Task<ProdutoAcabadoCaixa?> ObterCaixaPorCodigoAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default);

    /// <summary>Grava o request_payload sanitizado do preview HU e atualiza o status da integração.</summary>
    Task AtualizarPreviewHuAsync(
        long codigoProdutoAcabadoCaixa,
        string requestPayloadSanitizado,
        StatusIntegracaoCaixa status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim ATÔMICO PRONTA_PARA_ENVIO → ENVIANDO_SAP. Retorna o SNAPSHOT já atualizado
    /// (StatusIntegracao=EnviandoSap) quando a reserva ocorre, ou <c>null</c> quando não reservou
    /// (idempotência/duplo clique/estado não elegível). O Controller deve prosseguir o envio usando
    /// EXCLUSIVAMENTE o snapshot retornado — nunca o objeto carregado antes do claim, que num Repository
    /// real vem DESCONECTADO e poderia continuar em PRONTA_PARA_ENVIO, gerando transição inválida.
    /// </summary>
    Task<ProdutoAcabadoCaixa?> TentarReservarEnvioSapAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default);

    /// <summary>Sucesso: persiste HandlingUnitExternalId + response sanitizado + CONFIRMADA_SAP + confirmado_sap_em.</summary>
    Task RegistrarSucessoHuAsync(
        long codigoProdutoAcabadoCaixa,
        string handlingUnitExternalId,
        string responsePayloadSanitizado,
        int? httpStatus,
        CancellationToken cancellationToken = default);

    /// <summary>Erro: persiste erro sanitizado + response sanitizado + ERRO_SAP (correlation_id/caixa preservados).</summary>
    Task RegistrarErroHuAsync(
        long codigoProdutoAcabadoCaixa,
        string erroSanitizado,
        string responsePayloadSanitizado,
        int? httpStatus,
        CancellationToken cancellationToken = default);

    Task CancelarCaixaAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default);
}
