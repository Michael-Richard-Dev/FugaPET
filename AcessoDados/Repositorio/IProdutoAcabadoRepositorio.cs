using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Contrato de persistência da caixa individual de Produto Acabado (Handling Unit) sobre o incremental
/// de banco 044 (REV11). Uma caixa por vez por terminal; identidade (numero_caixa/codigo_caixa_local/
/// hu_caixa) é gerada ATOMICAMENTE pelo banco (trigger + advisory lock por OP) — a aplicação NUNCA a
/// fornece. Toda transição de estado passa por FUNÇÃO de banco (nunca UPDATE direto). A finalização de
/// tentativa exige <c>numero_tentativa</c> + <c>claim_token</c>: nenhuma resposta atrasada pode alterar
/// uma tentativa diferente. Timeout após possível POST vira INDETERMINADO_TIMEOUT (sem retry/2º POST).
/// </summary>
public interface IProdutoAcabadoRepositorio
{
    /// <summary>
    /// INSERT inicial da caixa (status EM_PESAGEM) usando apenas as colunas concedidas ao app; o banco
    /// gera numero_caixa/codigo_caixa_local/hu_caixa e preserva o correlation_id. Retorna o snapshot
    /// efetivamente persistido (RETURNING).
    /// </summary>
    Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default);

    /// <summary>Persiste UMA pesagem em hu_caixa_pesagem (superfície autorizada). Retorna o código gerado.</summary>
    Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa pesagem, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_finalizar_local: EM_PESAGEM → FINALIZADA_LOCAL. Exige usuário+terminal donos da caixa.</summary>
    Task<bool> FinalizarLocalAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_salvar_preview: FINALIZADA_LOCAL → PREVIEW_HU_GERADO (grava request/endpoint sanitizados).</summary>
    Task<bool> SalvarPreviewAsync(long codigo, string requestJsonSanitizado, string endpointSanitizado, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_aguardar_autorizacao: PREVIEW_HU_GERADO → AGUARDANDO_AUTORIZACAO_SAP.</summary>
    Task<bool> AguardarAutorizacaoAsync(long codigo, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_autorizar_envio: AGUARDANDO_AUTORIZACAO_SAP → PRONTA_PARA_ENVIO (usuário ativo + terminal).</summary>
    Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// fn_hu_caixa_claim_envio: claim ATÔMICO PRONTA_PARA_ENVIO → ENVIANDO_SAP. Retorna o SNAPSHOT oficial
    /// (com Tentativas + ClaimToken congelados) quando reservou, ou <c>null</c> quando nenhuma linha voltou
    /// (não reservou/estado divergente ⇒ NÃO enviar). O envio deve usar EXCLUSIVAMENTE este snapshot.
    /// </summary>
    Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_registrar_sucesso: ENVIANDO_SAP → CONFIRMADA_SAP (HTTP 201). Exige tentativa+claim.</summary>
    Task<bool> RegistrarSucessoAsync(
        long codigo, int numeroTentativa, Guid claimToken, string handlingUnitExternalId, string? warehouse,
        int httpStatus, string? responseJsonSanitizado, string? sapMessagesJson, string? etag,
        string? createdByUserSap, DateTimeOffset? creationDatetimeSap, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_registrar_erro: ENVIANDO_SAP → ERRO_SAP. 401/403 ⇒ NAO_AUTORIZADO (não reprocessável).</summary>
    Task<bool> RegistrarErroAsync(
        long codigo, int numeroTentativa, Guid claimToken, int? httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, ResultadoErroHu resultado, bool podeReprocessar,
        CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_registrar_timeout: ENVIANDO_SAP → INDETERMINADO_TIMEOUT. Sem retry/2º POST.</summary>
    Task<bool> RegistrarTimeoutAsync(
        long codigo, int numeroTentativa, Guid claimToken, string? sapMessagesJson, string erroSanitizado,
        CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_confirmar_reconciliacao: INDETERMINADO_TIMEOUT → CONFIRMADA_SAP (HTTP 200 + comparação aprovada).</summary>
    Task<bool> ConfirmarReconciliacaoAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string? etag, string? createdByUserSap, DateTimeOffset? creationDatetimeSap,
        bool comparacaoAprovada, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_registrar_reconciliacao_nao_encontrada: HTTP 404 (permanece INDETERMINADO_TIMEOUT; sem novo POST automático).</summary>
    Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_bloquear_configuracao: estados locais pré-envio → BLOQUEADA.</summary>
    Task<bool> BloquearConfiguracaoAsync(
        long codigo, long usuario, string terminal, string erroSanitizado, string? sapMessagesJson,
        CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_cancelar: cancela por usuário/terminal/motivo (nunca UPDATE direto).</summary>
    Task<bool> CancelarAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default);

    /// <summary>fn_hu_caixa_liberar_reprocessamento: ERRO_SAP reprocessável → PRONTA_PARA_ENVIO (limpa tentativa).</summary>
    Task<bool> LiberarReprocessamentoAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default);

    /// <summary>SELECT do snapshot por código (ou null).</summary>
    Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken cancellationToken = default);

    /// <summary>Caixa ATIVA (não CONFIRMADA_SAP nem CANCELADA) do terminal, ou null. Garante "uma caixa por vez".</summary>
    Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista (somente leitura) TODAS as caixas persistidas da OP+terminal, em qualquer estado (inclusive
    /// CONFIRMADA_SAP/CANCELADA), ordenadas por numero_caixa. Base da recuperação da grid após reabrir a tela.
    /// </summary>
    Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(
        string numeroOrdemProducao, string terminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// REV4-§12 / REV5-§3: recuperação por CONTEXTO INEQUÍVOCO (OP + item + material + lote + terminal),
    /// FAIL-CLOSED. Todos os campos são igualdade OBRIGATÓRIA — não há filtro opcional que amplie o contexto.
    /// Se qualquer campo obrigatório vier vazio, retorna lista VAZIA (nunca amplia para OP+terminal).
    /// Ordenadas por numero_caixa.
    /// </summary>
    Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(
        string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// INC-047: leitura (somente) das caixas persistidas por Handling Unit externo (HU SAP da caixa). Usada pela
    /// Paletização por SELEÇÃO MANUAL (uma ou mais HUs). Lista vazia se nenhuma HU informada. Sem POST/SAP.
    /// </summary>
    Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(
        IReadOnlyList<string> husExternais, CancellationToken cancellationToken = default);

    /// <summary>
    /// INC-047: leitura (somente) das caixas por INTERVALO de Handling Unit externo (huInicial..huFinal),
    /// opcionalmente filtrando por material. Usada pela Paletização POR SEQUÊNCIA DE HU. Fail-closed em intervalo
    /// vazio. Sem POST/SAP.
    /// </summary>
    Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(
        string huInicial, string huFinal, string? material, CancellationToken cancellationToken = default);
}
