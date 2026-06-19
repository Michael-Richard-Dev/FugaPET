namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Servico unico responsavel por gerar e verificar hashes de senha (BCrypt).
/// Nenhuma camada deve chamar BCrypt diretamente; tudo passa por aqui.
/// </summary>
public sealed class SenhaServico
{
    // Custo de trabalho do BCrypt. 12 e um bom equilibrio seguranca/performance.
    private const int WorkFactor = 12;

    /// <summary>
    /// Gera o hash BCrypt da senha em texto puro. Lanca se a senha for vazia.
    /// </summary>
    public string GerarHash(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new ArgumentException("Senha nao pode ser vazia.", nameof(senha));
        }

        return BCrypt.Net.BCrypt.HashPassword(senha, workFactor: WorkFactor);
    }

    /// <summary>
    /// Verifica a senha digitada contra o hash armazenado. Retorna false em qualquer
    /// inconsistencia (vazio, hash invalido, formato nao-BCrypt) sem lancar excecao.
    /// </summary>
    public bool Verificar(string senhaDigitada, string senhaHash)
    {
        if (string.IsNullOrWhiteSpace(senhaDigitada) || string.IsNullOrWhiteSpace(senhaHash))
        {
            return false;
        }

        string hash = senhaHash.Trim();
        if (!EhHashBCrypt(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(senhaDigitada, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Indica se o valor armazenado ja esta no formato de hash BCrypt
    /// (prefixos $2a$, $2b$ ou $2y$).
    /// </summary>
    public static bool EhHashBCrypt(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        string v = valor.Trim();
        return v.StartsWith("$2a$", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("$2b$", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("$2y$", StringComparison.OrdinalIgnoreCase);
    }
}
