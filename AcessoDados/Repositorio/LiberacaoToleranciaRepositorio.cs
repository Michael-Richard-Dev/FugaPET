using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Grava o log de liberacoes gerenciais de estouro de tolerancia (incremento 070).
/// <para>
/// So INSERT e SELECT: o papel da aplicacao nao tem DELETE nesta tabela, de proposito. Liberacao
/// gravada nao se reescreve nem se apaga — e o registro que responde "quem autorizou aquilo".
/// </para>
/// <para>
/// NAO ha parametro de senha em lugar nenhum desta classe, e nao deve haver. Ver o cabecalho do
/// incremento 070.
/// </para>
/// </summary>
public class LiberacaoToleranciaRepositorio : RepositorioBase
{
    public LiberacaoToleranciaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Registra a tentativa — autorizada ou recusada — e devolve o codigo gravado.
    /// <para>
    /// Propaga excecao de proposito. Quem chama decide o que fazer: para a tentativa AUTORIZADA a
    /// falha de gravacao tem de BLOQUEAR, senao existiria liberacao sem registro de quem a deu.
    /// </para>
    /// </summary>
    public virtual async Task<long> RegistrarAsync(
        LiberacaoToleranciaRecebimento liberacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liberacao);

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        return await InserirAsync(conexao, null, liberacao, cancellationToken);
    }

    /// <summary>
    /// Registra VARIAS liberacoes numa UNICA transacao — o caso da finalizacao, em que mais de um
    /// item pode estar abaixo do piso e o gerente autoriza o recebimento inteiro de uma vez.
    /// <para>
    /// Uma transacao so, e nao N chamadas, por causa do fail-closed: se a terceira gravacao falhasse
    /// depois de duas gravadas, o banco ficaria com duas linhas AUTORIZADA de uma liberacao que NAO
    /// valeu. Ou tudo entra, ou nada entra, e so entao a liberacao existe.
    /// </para>
    /// </summary>
    public virtual async Task RegistrarLoteAsync(
        IReadOnlyList<LiberacaoToleranciaRecebimento> liberacoes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liberacoes);
        if (liberacoes.Count == 0)
        {
            return;
        }

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);

        foreach (LiberacaoToleranciaRecebimento liberacao in liberacoes)
        {
            await InserirAsync(conexao, transacao, liberacao, cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
    }

    private static async Task<long> InserirAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction? transacao,
        LiberacaoToleranciaRecebimento liberacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(liberacao);
        ContextoEstouroTolerancia c = liberacao.Contexto;

        const string sql = """
            INSERT INTO liberacao_tolerancia_recebimento
                   (numero_pedido, numero_item, material,
                    quantidade_pedida, quantidade_apurada, limite_aplicado, percentual_tolerancia,
                    tipo_estouro, resultado, motivo_recusa,
                    codigo_usuario_autorizador, login_autorizador,
                    codigo_usuario_operador, login_operador, terminal)
            VALUES (@pedido, @item, @material,
                    @qtd_pedida, @qtd_apurada, @limite, @percentual,
                    @tipo, @resultado, @motivo,
                    @cod_autorizador, @login_autorizador,
                    @cod_operador, @login_operador, @terminal)
            RETURNING codigo_liberacao;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.AddWithValue("@pedido", c.NumeroPedido);
        comando.Parameters.AddWithValue("@item", c.NumeroItem);
        comando.Parameters.AddWithValue("@material", c.Material);
        comando.Parameters.AddWithValue("@qtd_pedida", (object?)c.QuantidadePedida ?? DBNull.Value);
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

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : 0L;
    }
}
