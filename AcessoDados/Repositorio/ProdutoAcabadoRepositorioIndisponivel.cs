using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Fallback fail-closed de persistência de Produto Acabado: NÃO finge banco (não usa memória/arquivo).
/// Toda operação lança <see cref="PersistenciaProdutoAcabadoIndisponivelException"/>. Usado quando o banco
/// está desabilitado/indisponível; o repositório produtivo real é <see cref="ProdutoAcabadoRepositorio"/>.
/// </summary>
public sealed class ProdutoAcabadoRepositorioIndisponivel : IProdutoAcabadoRepositorio
{
    private const string Motivo =
        "Persistência de Produto Acabado indisponível: banco não configurado/indisponível nesta execução.";

    private static PersistenciaProdutoAcabadoIndisponivelException Falha() => new(Motivo);

    public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa pesagem, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> FinalizarLocalAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> SalvarPreviewAsync(long codigo, string requestJsonSanitizado, string endpointSanitizado, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> AguardarAutorizacaoAsync(long codigo, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> RegistrarSucessoAsync(
        long codigo, int numeroTentativa, Guid claimToken, string handlingUnitExternalId, string? warehouse,
        int httpStatus, string? responseJsonSanitizado, string? sapMessagesJson, string? etag,
        string? createdByUserSap, DateTimeOffset? creationDatetimeSap, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> RegistrarErroAsync(
        long codigo, int numeroTentativa, Guid claimToken, int? httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, ResultadoErroHu resultado, bool podeReprocessar,
        CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> RegistrarTimeoutAsync(
        long codigo, int numeroTentativa, Guid claimToken, string? sapMessagesJson, string erroSanitizado,
        CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> ConfirmarReconciliacaoAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string? etag, string? createdByUserSap, DateTimeOffset? creationDatetimeSap,
        bool comparacaoAprovada, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(
        long codigo, string handlingUnitExternalId, string? warehouse, int httpStatus, string? responseJsonSanitizado,
        string? sapMessagesJson, string erroSanitizado, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> BloquearConfiguracaoAsync(
        long codigo, long usuario, string terminal, string erroSanitizado, string? sapMessagesJson,
        CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> CancelarAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<bool> LiberarReprocessamentoAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(string numeroOrdemProducao, string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(IReadOnlyList<string> husExternais, CancellationToken cancellationToken = default)
        => throw Falha();

    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(string huInicial, string huFinal, string? material, CancellationToken cancellationToken = default)
        => throw Falha();
}

/// <summary>Persistência de Produto Acabado indisponível (banco não configurado). Não é erro técnico da operação.</summary>
public sealed class PersistenciaProdutoAcabadoIndisponivelException(string mensagem) : Exception(mensagem);
