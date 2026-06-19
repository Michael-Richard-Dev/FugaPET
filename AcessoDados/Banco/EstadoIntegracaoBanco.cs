namespace FugaPET_Dev.AcessoDados.Banco;

public static class EstadoIntegracaoBanco
{
    private static readonly Lazy<ConfiguracaoBancoPostgreSql> Configuracao =
        new(LeitorConfiguracaoBancoPostgreSql.Carregar);

    public static bool Habilitado => Configuracao.Value.Habilitado;

    /// <summary>
    /// True apenas quando explicitamente em modo demonstracao (prototipo visual).
    /// Usado para liberar autorizacao/login com banco desabilitado E sinalizar com faixa.
    /// </summary>
    public static bool ModoDemonstracao => Configuracao.Value.ModoDemonstracao;
}
