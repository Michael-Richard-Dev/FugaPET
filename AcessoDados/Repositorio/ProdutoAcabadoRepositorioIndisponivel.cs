using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistência produtiva de Produto Acabado BLOQUEADA_POR_SCHEMA_PRODUTO_ACABADO_PENDENTE: não existe
/// tabela aplicável no banco. Este repositório NÃO finge persistência (não usa memória/arquivo como
/// banco): toda operação de escrita/leitura lança <see cref="PersistenciaProdutoAcabadoIndisponivelException"/>.
/// É o default de produção até a Gaia Dados aplicar o schema. Os testes usam um fake em memória próprio.
/// </summary>
public sealed class ProdutoAcabadoRepositorioIndisponivel : IProdutoAcabadoRepositorio
{
    private const string Motivo =
        "Persistência de Produto Acabado indisponível: schema do banco pendente "
        + "(BLOQUEADA_POR_SCHEMA_PRODUTO_ACABADO_PENDENTE).";

    public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task<ProdutoAcabadoCaixa?> ObterCaixaAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task<ProdutoAcabadoCaixa?> ObterCaixaPorCodigoAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task AtualizarPreviewHuAsync(long codigoProdutoAcabadoCaixa, string requestPayloadSanitizado, StatusIntegracaoCaixa status, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task<ProdutoAcabadoCaixa?> TentarReservarEnvioSapAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task RegistrarSucessoHuAsync(long codigoProdutoAcabadoCaixa, string handlingUnitExternalId, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task RegistrarErroHuAsync(long codigoProdutoAcabadoCaixa, string erroSanitizado, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);

    public Task CancelarCaixaAsync(long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
        => throw new PersistenciaProdutoAcabadoIndisponivelException(Motivo);
}

/// <summary>Persistência de Produto Acabado indisponível por schema pendente (não é erro técnico da operação).</summary>
public sealed class PersistenciaProdutoAcabadoIndisponivelException(string mensagem) : Exception(mensagem);
