using Npgsql;

namespace FugaPET_Dev.AcessoDados.Banco;

public static class ErroBancoTratado
{
    public static string ObterMensagemAmigavel(Exception ex)
    {
        return ex switch
        {
            // PostgresException = o servidor FOI alcancado e RECUSOU o comando (erro de SQL).
            // Nunca deve virar "nao foi possivel conectar": a conexao existe.
            PostgresException postgresEx => ObterMensagemErroServidor(postgresEx),

            TimeoutException => "Tempo limite excedido ao acessar o banco. Tente novamente.",

            // NpgsqlException que NAO e PostgresException = falha de conexao/transporte real.
            NpgsqlException npgsqlEx => ObterMensagemConexao(npgsqlEx),

            _ => "Não foi possível concluir a operação. Acione o suporte."
        };
    }

    /// <summary>
    /// Erros retornados pelo servidor PostgreSQL (com SQLSTATE). A conexao funcionou;
    /// o comando e que foi recusado. Mensagens amigaveis por classe de erro, sem detalhe tecnico.
    /// </summary>
    private static string ObterMensagemErroServidor(PostgresException ex)
    {
        return ex.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => "Já existe um registro com estes dados.",
            PostgresErrorCodes.ForeignKeyViolation => "Operação bloqueada: há vínculo com outro registro.",
            PostgresErrorCodes.NotNullViolation => "Preencha todos os campos obrigatórios.",
            PostgresErrorCodes.CheckViolation => "Dados inválidos para esta operação.",
            PostgresErrorCodes.StringDataRightTruncation => "Algum campo excede o tamanho permitido.",
            PostgresErrorCodes.InsufficientPrivilege =>
                "Sem permissão no banco para concluir esta operação. Acione o suporte.",
            _ => "Não foi possível concluir a operação. Acione o suporte."
        };
    }

    /// <summary>
    /// Falhas de conexao/transporte (servidor inacessivel, credenciais, banco inexistente, timeout).
    /// </summary>
    private static string ObterMensagemConexao(NpgsqlException ex)
    {
        if (ex.Message.Contains("password authentication failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Usuário ou senha do banco inválidos.";
        }

        if (ex.Message.Contains("database", StringComparison.OrdinalIgnoreCase)
            && ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "Banco de dados configurado não existe.";
        }

        if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Tempo limite excedido ao tentar conectar ao banco.";
        }

        return "Não foi possível conectar ao banco configurado. Acione o suporte.";
    }
}
