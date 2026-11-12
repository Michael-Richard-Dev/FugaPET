using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Entrada.P5;
using System.Data;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal sealed class TransacaoRohR8F(IFabricaConexaoBanco fabrica, IContratoSqlRohR8F contrato,
    PapelP5R8 papel = PapelP5R8.Provider) : IExecutorRohR8F
{
    public bool Disponivel => false;
    public PapelP5R8 Papel => papel;
    public async Task<T> ExecutarAsync<T>(Func<ISessaoRohR8F, CancellationToken, Task<T>> acao, CancellationToken ct)
    {
        if (!Disponivel || !BloqueioP5R7.MutacoesHomologadas)
            throw new InvalidOperationException("R8F_BACKEND_SQL_NAO_HOMOLOGADO");
        await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync(ct);
        await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        T resultado;
        try
        {
            resultado = await acao(new Sessao(conexao, tx, contrato), ct);
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception)
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (Exception) { throw new CommitRohIndeterminadoR8FException(); }
            throw new InvalidOperationException("R8F_OPERACAO_REVERTIDA_SEM_DISPATCH");
        }
        try { await tx.CommitAsync(ct); }
        catch (Exception) { throw new CommitRohIndeterminadoR8FException(); }
        return resultado;
    }

    private sealed class Sessao : ISessaoRohR8F
    {
        private readonly NpgsqlConnection _conexao;
        private readonly NpgsqlTransaction _tx;
        private readonly IContratoSqlRohR8F _contrato;

        internal Sessao(NpgsqlConnection conexao, NpgsqlTransaction tx, IContratoSqlRohR8F contrato)
        {
            if (conexao.State != ConnectionState.Open || !ReferenceEquals(tx.Connection, conexao))
                throw new InvalidOperationException("R8F_TRANSACAO_FISICA_DIVERGENTE");
            _conexao = conexao;
            _tx = tx;
            _contrato = contrato;
        }

        public Task<ReciboReservaRohR8F> ReservarIntegralAsync(PedidoReservaRohR8F pedido, CancellationToken ct)
            => _contrato.ReservarIntegralAsync(_conexao, _tx, pedido, ct);
        public Task<ReciboEstadoRohR8F> MarcarEnvioAsync(TitularRohR8F titular, CancellationToken ct)
            => _contrato.MarcarEnvioAsync(_conexao, _tx, titular, ct);
        public Task<ReciboEstadoRohR8F> SelarFronteiraAsync(TitularRohR8F titular, CancellationToken ct)
            => _contrato.SelarFronteiraAsync(_conexao, _tx, titular, ct);
        public Task<ReciboEstadoRohR8F> ConsultarTentativaAsync(TitularRohR8F titular, CancellationToken ct)
            => _contrato.ConsultarTentativaAsync(_conexao, _tx, titular, ct);
        public Task<ReciboResultadoRohR8F> RegistrarResultadoIntegralAsync(PedidoResultadoRohR8F pedido, CancellationToken ct)
            => _contrato.RegistrarResultadoIntegralAsync(_conexao, _tx, pedido, ct);
        public Task<ReciboCompensacaoRohR8F> CompensarPreHttpIntegralAsync(PedidoCompensacaoRohR8F pedido, CancellationToken ct)
            => _contrato.CompensarPreHttpIntegralAsync(_conexao, _tx, pedido, ct);
        public Task<ReciboRecoveryRohR8F> ReconciliarSemReenvioAsync(PedidoRecoveryRohR8F pedido, CancellationToken ct)
            => _contrato.ReconciliarSemReenvioAsync(_conexao, _tx, pedido, ct);
    }
}
