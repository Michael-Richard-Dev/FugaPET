using System.Data;
using System.Data.Common;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal interface ISessaoP5R8
{
    Task<T> LerUnicoAsync<T>(NpgsqlCommand comando, Func<DbDataReader, T> ler, CancellationToken cancellationToken);
    Task<ReciboLegadoP5R8> AplicarLegadoAsync(PedidoLegadoP5R8 pedido, CancellationToken cancellationToken);
}

internal interface IExecutorTransacionalP5R8
{
    PapelP5R8 Papel { get; }
    bool FencingAtomicoDisponivel => false;
    long? CodigoUsuario => null;
    Task<T> ExecutarAsync<T>(Func<ISessaoP5R8, CancellationToken, Task<T>> acao, CancellationToken cancellationToken);
}

internal sealed class TransacaoEntradaMpP5R8(IFabricaConexaoBanco fabrica, PapelP5R8 papel, long? codigoUsuario = null,
    IContratoLegadoP5R8? contratoLegado = null)
    : IExecutorTransacionalP5R8
{
    public PapelP5R8 Papel => papel;
    public bool FencingAtomicoDisponivel => false;
    public long? CodigoUsuario => codigoUsuario;

    public async Task<T> ExecutarAsync<T>(Func<ISessaoP5R8, CancellationToken, Task<T>> acao,
        CancellationToken cancellationToken)
    {
        if (!BloqueioP5R7.MutacoesHomologadas || !FencingAtomicoDisponivel)
            throw new InvalidOperationException("P5_R8_EXECUCAO_REAL_DESABILITADA");
        try
        {
            await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            if (codigoUsuario is not > 0) throw new InvalidOperationException("P5_R8_USUARIO_AUDITORIA_AUSENTE");
            await using (NpgsqlCommand auditoria = new("SELECT pg_catalog.set_config('app.usuario_id',$1,true)", conexao, tx))
            {
                auditoria.Parameters.AddWithValue(codigoUsuario.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await auditoria.ExecuteScalarAsync(cancellationToken);
            }
            T retorno = await acao(new Sessao(conexao, tx, contratoLegado), cancellationToken);
            try { await tx.CommitAsync(cancellationToken); }
            catch (Exception) when (retorno is ClaimP5 claim) { throw new CommitP5IndeterminadoException(claim); }
            return retorno;
        }
        catch (CommitP5IndeterminadoException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new InvalidOperationException("P5_R8_TRANSACAO_NAO_COMPROVADA"); }
    }

    private sealed class Sessao(NpgsqlConnection conexao, NpgsqlTransaction tx, IContratoLegadoP5R8? contratoLegado) : ISessaoP5R8
    {
        public async Task<ReciboLegadoP5R8> AplicarLegadoAsync(PedidoLegadoP5R8 pedido, CancellationToken cancellationToken)
        {
            if (!pedido.Valido || contratoLegado is null)
                throw new InvalidOperationException("P5_R8_FUNCAO_LEGADA_PRIVILEGIADA_AUSENTE");
            ClassificacaoDuravelP5R8? classificacao = await contratoLegado.ValidarEscopoPersistidoAsync(
                conexao, tx, pedido.Claim.Escopo, cancellationToken);
            if (classificacao?.CorrespondeRoh(pedido.Claim.Escopo) != true)
                throw new InvalidOperationException("P5_R8_CLASSIFICACAO_DURAVEL_NAO_COMPROVADA");
            return await contratoLegado.ExecutarNaTransacaoAsync(conexao, tx, pedido, cancellationToken);
        }
        public async Task<T> LerUnicoAsync<T>(NpgsqlCommand comando, Func<DbDataReader, T> ler, CancellationToken cancellationToken)
        {
            comando.Connection = conexao;
            comando.Transaction = tx;
            await using NpgsqlDataReader reader = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("P5_R8_RETORNO_AUSENTE");
            T retorno = ler(reader);
            if (await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("P5_R8_RETORNO_AMBIGUO");
            return retorno;
        }

    }
}
