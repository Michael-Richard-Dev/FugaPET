namespace FugaPET_Dev.AcessoDados.Banco;

public sealed class ContextoBancoOperacao
{
    public ConfiguracaoBancoPostgreSql Configuracao { get; }
    public IFabricaConexaoBanco FabricaConexao { get; }

    public ContextoBancoOperacao()
        : this(LeitorConfiguracaoBancoPostgreSql.Carregar())
    {
    }

    public ContextoBancoOperacao(ConfiguracaoBancoPostgreSql configuracao)
        : this(configuracao, new FabricaConexaoPostgreSql(configuracao))
    {
    }

    public ContextoBancoOperacao(
        ConfiguracaoBancoPostgreSql configuracao,
        IFabricaConexaoBanco fabricaConexao)
    {
        Configuracao = configuracao;
        FabricaConexao = fabricaConexao;
    }
}
