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

    public static bool AmbienteDemonstrativo => Configuracao.Value.AmbienteDemonstrativo;

    public static bool PodeUsarDadosSimulados
        => CalcularPodeUsarDadosSimulados(
            Habilitado,
            ModoDemonstracao,
            AmbienteDemonstrativo);

    internal static bool CalcularPodeUsarDadosSimulados(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => !bancoHabilitado
           && modoDemonstracao
           && ambienteDemonstrativo;

    public static void GarantirDadosSimuladosPermitidos()
    {
        if (!PodeUsarDadosSimulados)
        {
            throw new InvalidOperationException(
                "Dados simulados so podem ser usados com banco desabilitado, modo demonstracao ativo e ambiente demonstrativo.");
        }
    }
}
