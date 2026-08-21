using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// GATE 046-E §3/§4: resultado da criação ATÔMICA (uma transação) do palete local + vínculo de todas as caixas.
/// <see cref="Sucesso"/>=true ⇒ COMMIT efetuado com o palete e TODOS os vínculos. false ⇒ ROLLBACK integral
/// (nada persiste).
/// </summary>
public sealed record ResultadoCriacaoPaleteLocalAtomica(bool Sucesso, long? CodigoHuPalete, string Motivo)
{
    public static ResultadoCriacaoPaleteLocalAtomica Ok(long codigoHuPalete)
        => new(true, codigoHuPalete, "Palete criado localmente (RASCUNHO) com todas as caixas vinculadas.");

    public static ResultadoCriacaoPaleteLocalAtomica Bloqueado(string motivo)
        => new(false, null, motivo);
}

/// <summary>
/// SuperfÃ­cie de operaÃ§Ãµes do contrato 045 (REV5 CORRETIVA 2 FINAL) que o orquestrador PRODUTIVO consome. Toda
/// persistÃªncia autoritativa passa por aqui (funÃ§Ãµes fn_pa_045_*), NUNCA por snapshot em memÃ³ria. Implementada
/// pelo <see cref="ProdutoAcabadoPipelinePostgresStore"/> (real) e por fakes de teste. <c>guard_hu_pos_101</c>
/// NÃƒO faz parte da superfÃ­cie (Ã© trigger de banco).
/// </summary>
public interface IProdutoAcabadoPipeline045Operacoes
{
    bool SuportaPersistenciaDefinitiva { get; }

    // Etapa 261/101
    Task<bool> IniciarFluxoAsync(long codigo, long usuario, string terminal, string? origem = null, CancellationToken ct = default);
    Task<bool> PrepararEtapaAsync(long codigo, string etapa, string payloadJson, long usuario, string terminal, CancellationToken ct = default);
    Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarSucessoEtapaAsync(long codigo, string etapa, int http, string materialDocument, string materialDocumentYear, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarSucessoEtapaAsync(long codigo, string etapa, int tentativa, Guid claimToken, int http, string materialDocument, string materialDocumentYear, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarSucessoEtapaAsync(codigo, etapa, http, materialDocument, materialDocumentYear, responseJson, endpoint, usuario, terminal, ct);
    Task<bool> RegistrarErroEtapaAsync(long codigo, string etapa, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarErroEtapaAsync(long codigo, string etapa, int tentativa, Guid claimToken, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarErroEtapaAsync(codigo, etapa, http, responseJson, erro, endpoint, usuario, terminal, ct);
    Task<bool> RegistrarTimeoutEtapaAsync(long codigo, string etapa, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarTimeoutEtapaAsync(long codigo, string etapa, int tentativa, Guid claimToken, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarTimeoutEtapaAsync(codigo, etapa, erro, endpoint, usuario, terminal, ct);

    // Recovery de etapa
    Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default);
    Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarReconciliacaoEtapaAsync(long codigo, string etapa, string resultado, int? http, string? materialDocument, string? materialDocumentYear, string responseJson, string? erro, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> LiberarReprocessamentoEtapaAsync(long codigo, string etapa, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default);

    // Palete
    Task<long?> CriarPaleteAsync(
        string codigoPaleteLocal,
        string plant,
        string storageLocation,
        decimal pesoBrutoKg,
        decimal pesoLiquidoKg,
        decimal taraKg,
        long usuario,
        string terminal,
        CancellationToken ct = default);
    Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long codigoPalete, string requestJson, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default);
    Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarSucessoPaleteAsync(long codigoPalete, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarSucessoPaleteAsync(long codigoPalete, int tentativa, Guid claimToken, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarSucessoPaleteAsync(codigoPalete, http, ucGerada, responseJson, endpoint, usuario, terminal, ct);
    Task<bool> RegistrarErroPaleteAsync(long codigoPalete, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarErroPaleteAsync(long codigoPalete, int tentativa, Guid claimToken, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarErroPaleteAsync(codigoPalete, http, responseJson, erro, endpoint, usuario, terminal, ct);
    Task<bool> RegistrarTimeoutPaleteAsync(long codigoPalete, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default);
    Task<bool> RegistrarTimeoutPaleteAsync(long codigoPalete, int tentativa, Guid claimToken, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        => RegistrarTimeoutPaleteAsync(codigoPalete, erro, endpoint, usuario, terminal, ct);
    Task<bool> VincularCaixaPaleteAsync(long codigoPalete, long codigoCaixa, int sequencia, long usuario, string terminal, CancellationToken ct = default);

    // GATE 046-E §3/§4: criação ATÔMICA (uma conexão/uma transação) — palete + TODOS os vínculos, ou nada.
    // Default fail-closed: implementações sem suporte transacional não persistem parcialmente.
    Task<ResultadoCriacaoPaleteLocalAtomica> CriarPaleteComCaixasAsync(
        string materialEmbalagem, string plant, string storageLocation,
        decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg,
        IReadOnlyList<long> codigosCaixas, long usuario, string terminal, CancellationToken ct = default)
        => Task.FromResult(ResultadoCriacaoPaleteLocalAtomica.Bloqueado(
            "Criação atômica de palete indisponível nesta implementação (fail-closed)."));

    // GATE 046-E §7/§10: reload persistente da composição por OP (e terminal). Default: nada persistido.
    Task<IReadOnlyList<ProdutoAcabadoPalete>> LerPaletesLocaisPorOrdemAsync(
        string numeroOrdemProducao, string? terminal, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProdutoAcabadoPalete>>([]);

    // GATE 047-AB: reload persistente por TERMINAL (sem OP) — abertura da Paletização. Default: nada persistido.
    Task<IReadOnlyList<ProdutoAcabadoPalete>> LerPaletesLocaisPorTerminalAsync(
        string? terminal, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProdutoAcabadoPalete>>([]);

    // Snapshot por VIEWS runtime (sem tokens)
    Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long codigo, CancellationToken ct = default);
    Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long codigoPalete, CancellationToken ct = default);
}



