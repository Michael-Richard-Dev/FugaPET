using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Grava o log de liberações gerenciais de estouro de tolerância NA PRODUÇÃO (incremento 071).
/// <para>
/// Só INSERT e SELECT: o papel da aplicação não tem DELETE nesta tabela, de propósito. Liberação
/// gravada não se reescreve nem se apaga — é o registro que responde "quem autorizou aquilo".
/// </para>
/// <para>
/// NÃO há parâmetro de senha em lugar nenhum desta classe, e não deve haver. Ver o cabeçalho do
/// incremento 070.
/// </para>
/// </summary>
public class LiberacaoToleranciaProcessoRepositorio : RepositorioBase
{
    public LiberacaoToleranciaProcessoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    private const string SqlInsercao = """
        INSERT INTO liberacao_tolerancia_processo
               (processo, numero_ordem, operacao, material,
                quantidade_prevista, quantidade_apurada, limite_aplicado, percentual_tolerancia,
                tipo_estouro, resultado, motivo_recusa,
                codigo_usuario_autorizador, login_autorizador,
                codigo_usuario_operador, login_operador, terminal)
        VALUES (@processo, @ordem, @operacao, @material,
                @qtd_prevista, @qtd_apurada, @limite, @percentual,
                @tipo, @resultado, @motivo,
                @cod_autorizador, @login_autorizador,
                @cod_operador, @login_operador, @terminal)
        RETURNING codigo_liberacao;
        """;

    /// <summary>
    /// Registra VÁRIAS liberações numa ÚNICA transação.
    /// <para>
    /// Uma transação só, e não N chamadas, por causa do fail-closed: se a terceira gravação falhasse
    /// depois de duas gravadas, o banco ficaria com duas linhas AUTORIZADA de uma liberação que NÃO
    /// valeu. Ou tudo entra, ou nada entra, e só então a liberação existe.
    /// </para>
    /// <para>
    /// Propaga exceção de propósito: é lançar que faz a liberação não valer.
    /// </para>
    /// </summary>
    public virtual async Task RegistrarLoteAsync(
        IReadOnlyList<LiberacaoToleranciaProcesso> liberacoes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liberacoes);
        if (liberacoes.Count == 0)
        {
            return;
        }

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);

        foreach (LiberacaoToleranciaProcesso liberacao in liberacoes)
        {
            await InserirAsync(conexao, transacao, liberacao, cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
    }

    private static async Task InserirAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction? transacao,
        LiberacaoToleranciaProcesso liberacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(liberacao);
        ContextoEstouroProducao c = liberacao.Contexto;

        await using NpgsqlCommand comando = new(SqlInsercao, conexao, transacao);
        comando.Parameters.AddWithValue("@processo", c.Processo);
        comando.Parameters.AddWithValue("@ordem", c.NumeroOrdem);
        comando.Parameters.AddWithValue("@operacao", (object?)c.Operacao ?? DBNull.Value);
        comando.Parameters.AddWithValue("@material", c.Material);
        comando.Parameters.AddWithValue("@qtd_prevista", (object?)c.QuantidadePrevista ?? DBNull.Value);
        comando.Parameters.AddWithValue("@qtd_apurada", (object?)c.QuantidadeApurada ?? DBNull.Value);
        comando.Parameters.AddWithValue("@limite", (object?)c.LimiteAplicado ?? DBNull.Value);
        comando.Parameters.AddWithValue("@percentual", (object?)c.PercentualTolerancia ?? DBNull.Value);
        comando.Parameters.AddWithValue("@tipo", c.TipoEstouro);
        comando.Parameters.AddWithValue("@resultado", liberacao.Resultado);
        comando.Parameters.AddWithValue("@motivo", (object?)liberacao.MotivoRecusa ?? DBNull.Value);
        comando.Parameters.AddWithValue("@cod_autorizador", (object?)liberacao.CodigoUsuarioAutorizador ?? DBNull.Value);
        comando.Parameters.AddWithValue("@login_autorizador", liberacao.LoginAutorizador);
        comando.Parameters.AddWithValue("@cod_operador", (object?)c.CodigoUsuarioOperador ?? DBNull.Value);
        comando.Parameters.AddWithValue("@login_operador", (object?)c.LoginOperador ?? DBNull.Value);
        comando.Parameters.AddWithValue("@terminal", (object?)c.Terminal ?? DBNull.Value);

        await comando.ExecuteScalarAsync(cancellationToken);
    }
}
