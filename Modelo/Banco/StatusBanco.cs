namespace FugaPET_Dev.Modelo.Banco;

public sealed class StatusBanco
{
    public bool IntegracaoHabilitada { get; init; }
    public bool Conectado { get; init; }
    public string Servidor { get; init; } = string.Empty;
    public int Porta { get; init; }
    public string NomeBanco { get; init; } = string.Empty;
    public string SchemaAtual { get; init; } = string.Empty;
    public DateTime? DataHoraServidor { get; init; }
    public string Mensagem { get; init; } = string.Empty;

    public static StatusBanco Desabilitado(string servidor, int porta, string nomeBanco)
        => new()
        {
            IntegracaoHabilitada = false,
            Conectado = false,
            Servidor = servidor,
            Porta = porta,
            NomeBanco = nomeBanco,
            Mensagem = "Integracao com banco desabilitada."
        };

    public static StatusBanco Online(
        string servidor,
        int porta,
        string nomeBanco,
        string schemaAtual,
        DateTime dataHoraServidor)
        => new()
        {
            IntegracaoHabilitada = true,
            Conectado = true,
            Servidor = servidor,
            Porta = porta,
            NomeBanco = nomeBanco,
            SchemaAtual = schemaAtual,
            DataHoraServidor = dataHoraServidor,
            Mensagem = "Conexao com banco disponivel."
        };

    public static StatusBanco Offline(string servidor, int porta, string nomeBanco, string mensagem)
        => new()
        {
            IntegracaoHabilitada = true,
            Conectado = false,
            Servidor = servidor,
            Porta = porta,
            NomeBanco = nomeBanco,
            Mensagem = mensagem
        };
}
