namespace FugaPET_Dev.AcessoDados.Banco;

public sealed class ResultadoConexaoBanco
{
    public bool Habilitado { get; init; }
    public bool Conectado { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string Servidor { get; init; } = string.Empty;
    public int Porta { get; init; }
    public string NomeBanco { get; init; } = string.Empty;

    public static ResultadoConexaoBanco Desabilitado(ConfiguracaoBancoPostgreSql configuracao)
        => new()
        {
            Habilitado = false,
            Conectado = true,
            Servidor = configuracao.Servidor,
            Porta = configuracao.Porta,
            NomeBanco = configuracao.NomeBanco,
            Mensagem = "Integração com banco desabilitada."
        };

    public static ResultadoConexaoBanco Sucesso(ConfiguracaoBancoPostgreSql configuracao)
        => new()
        {
            Habilitado = true,
            Conectado = true,
            Servidor = configuracao.Servidor,
            Porta = configuracao.Porta,
            NomeBanco = configuracao.NomeBanco,
            Mensagem = "Conexão com banco disponível."
        };

    public static ResultadoConexaoBanco Falha(ConfiguracaoBancoPostgreSql configuracao, string mensagem)
        => new()
        {
            Habilitado = true,
            Conectado = false,
            Servidor = configuracao.Servidor,
            Porta = configuracao.Porta,
            NomeBanco = configuracao.NomeBanco,
            Mensagem = mensagem
        };
}
