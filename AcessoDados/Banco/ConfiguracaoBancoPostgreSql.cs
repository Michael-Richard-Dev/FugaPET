namespace FugaPET_Dev.AcessoDados.Banco;

public sealed class ConfiguracaoBancoPostgreSql
{
    public bool Habilitado { get; init; } = false;

    /// <summary>
    /// Modo demonstracao (prototipo visual): com o banco desabilitado, a autorizacao
    /// libera tudo e o login local offline e permitido — mas a UI exibe faixa fixa.
    /// Padrao FALSE: em homologacao/producao, banco desabilitado BLOQUEIA login/operacao
    /// (nunca operar silenciosamente sem seguranca).
    /// </summary>
    public bool ModoDemonstracao { get; init; } = false;

    /// <summary>
    /// Identifica explicitamente uma instalacao destinada a demonstracao.
    /// Nao inferir pelo nome do banco ou schema. Padrao false para falhar fechado.
    /// </summary>
    public bool AmbienteDemonstrativo { get; init; } = false;
    public string Servidor { get; init; } = "127.0.0.1";
    public int Porta { get; init; } = 5432;
    public string NomeBanco { get; init; } = "api_balanca";
    public string Schema { get; init; } = "desenvolvimento";
    public string Usuario { get; init; } = "postgres";
    public string Senha { get; init; } = string.Empty;
    public int TimeoutSegundos { get; init; } = 15;
    public bool Pooling { get; init; } = true;
    public string SslMode { get; init; } = "Prefer";
}
