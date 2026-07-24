using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public interface ISemiAcabadoRepositorio
{
    Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default);

    Task<long> SalvarLancamentoLocalAsync(
        LancamentoSemiAcabado lancamento,
        string payloadPreviewJson,
        CancellationToken cancellationToken = default);

    Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, CancellationToken cancellationToken = default);

    Task<LancamentoSemiAcabado?> ObterLancamentoCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default);

    Task MarcarConfirmadoSapAsync(
        long codigoLancamento,
        string materialDocument,
        string materialDocumentYear,
        CancellationToken cancellationToken = default);

    Task MarcarErroSapAsync(long codigoLancamento, string mensagemErro, CancellationToken cancellationToken = default);

    /// <summary>HTTP 2xx sem MaterialDocument/Year: estado terminal DIVERGENCIA_SAP (nÃ£o reenviÃ¡vel).</summary>
    Task MarcarDivergenciaSapAsync(long codigoLancamento, string mensagem, CancellationToken cancellationToken = default);
    Task<bool> CancelarLancamentoLocalAsync(
        long codigoLancamento,
        string motivo,
        string usuario,
        CancellationToken cancellationToken = default);

    /// <summary>Pesagens persistidas de um lanÃ§amento (histÃ³rico/reimpressÃ£o), ordenadas por sequÃªncia.</summary>
    Task<IReadOnlyList<PesagemSemiAcabado>> ListarPesagensPorLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default);

    /// <summary>CabeÃ§alho persistido do lanÃ§amento mais recente de uma OP/item (para reabrir/reimprimir).</summary>
    Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// LanÃ§amento ABERTO (bloqueante) mais recente de uma OP/item: status em
    /// FINALIZADO_LOCAL / ENVIANDO_SAP / ERRO_SAP / DIVERGENCIA_SAP. CONFIRMADO_SAP NÃƒO Ã© aberto
    /// (a OP pode ter produÃ§Ãµes parciais posteriores). Null quando nÃ£o hÃ¡ lanÃ§amento aberto.
    /// </summary>
    Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoAbertoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os lanÃ§amentos de uma OP/item (mais recente primeiro), para consulta de histÃ³rico/reimpressÃ£o.</summary>
    Task<IReadOnlyList<LancamentoSemiAcabadoPersistido>> ListarLancamentosPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default);
}

