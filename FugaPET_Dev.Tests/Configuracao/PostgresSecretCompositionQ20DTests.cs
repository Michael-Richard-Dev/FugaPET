using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE 20D — composição do secret PostgreSQL Q pelo caminho FUGAPET_Q_CONEXAO_POSTGRES:
/// a connection string preserva Host/Port/Database/Username/SearchPath; a senha, quando ausente/blank na
/// string, é complementada EXCLUSIVAMENTE por FUGAPET_Q_POSTGRES_SENHA. Sem fallback HML, sem hardcode,
/// ausência de secret permanece fail-closed.
/// </summary>
public sealed class PostgresSecretCompositionQ20DTests
{
    private const string ConexaoQSemSenha =
        "Host=192.168.3.226;Port=5432;Database=fuga_jales_local_homologacao_q_v1_2;Username=fugapet_q_app;SearchPath=homologacao";

    private static Func<string, string?> Amb(params (string Nome, string? Valor)[] pares)
    {
        Dictionary<string, string?> mapa = new(StringComparer.Ordinal);
        foreach ((string nome, string? valor) in pares)
        {
            mapa[nome] = valor;
        }

        return nome => mapa.TryGetValue(nome, out string? v) ? v : null;
    }

    [Fact] // A + D + contrato §5: conexão Q sem Password + secret Q → senha carregada, demais campos preservados.
    public void A_ConexaoQ_ComSecretQ_SenhaCarregada()
    {
        ConfiguracaoBancoPostgreSql cfg = LeitorConfiguracaoBancoPostgreSql.Carregar(
            Amb(("FUGAPET_Q_CONEXAO_POSTGRES", ConexaoQSemSenha), ("FUGAPET_Q_POSTGRES_SENHA", "s3cr3t-q")));

        Assert.Equal("192.168.3.226", cfg.Servidor);
        Assert.Equal(5432, cfg.Porta);
        Assert.Equal("fuga_jales_local_homologacao_q_v1_2", cfg.NomeBanco);
        Assert.Equal("fugapet_q_app", cfg.Usuario);
        Assert.Equal("homologacao", cfg.Schema);                 // D: SearchPath preservado
        Assert.False(string.IsNullOrWhiteSpace(cfg.Senha));      // §5 SenhaVazia = NAO
        Assert.Equal("s3cr3t-q", cfg.Senha);
    }

    [Fact] // B: conexão Q sem Password + secret Q AUSENTE → senha vazia (fail-closed).
    public void B_ConexaoQ_SemSecretQ_SenhaVaziaFailClosed()
    {
        ConfiguracaoBancoPostgreSql cfg = LeitorConfiguracaoBancoPostgreSql.Carregar(
            Amb(("FUGAPET_Q_CONEXAO_POSTGRES", ConexaoQSemSenha)));

        Assert.True(string.IsNullOrWhiteSpace(cfg.Senha));
    }

    [Fact] // Password materializado na própria connection string tem precedência sobre o env.
    public void Password_NaConexao_TemPrecedenciaSobreEnv()
    {
        ConfiguracaoBancoPostgreSql cfg = LeitorConfiguracaoBancoPostgreSql.Carregar(
            Amb(("FUGAPET_Q_CONEXAO_POSTGRES", ConexaoQSemSenha + ";Password=da-string"),
                ("FUGAPET_Q_POSTGRES_SENHA", "do-env")));

        Assert.Equal("da-string", cfg.Senha);
    }

    [Fact] // C: FUGAPET_HML_* disponível NÃO é usado como fallback Q — sem secret Q, senha continua vazia.
    public void C_HmlDisponivel_NaoEhFallbackQ()
    {
        ConfiguracaoBancoPostgreSql cfg = LeitorConfiguracaoBancoPostgreSql.Carregar(
            Amb(("FUGAPET_Q_CONEXAO_POSTGRES", ConexaoQSemSenha),
                ("FUGAPET_HML_POSTGRES_SENHA", "senha-hml"),
                ("FUGAPET_HML_CONEXAO_POSTGRES", "Host=10.0.0.9;Database=fuga_jales_local_homologacao_v1_2;Username=fugapet_hml_app;Password=hml")));

        Assert.True(string.IsNullOrWhiteSpace(cfg.Senha));                       // HML não complementa Q
        Assert.Equal("fuga_jales_local_homologacao_q_v1_2", cfg.NomeBanco);      // usou a conexão Q, não a HML
        Assert.Equal("fugapet_q_app", cfg.Usuario);
    }

    [Fact] // E: o secret vive apenas no objeto de configuração; o reader não o emite em log/console/exceção.
    public void E_SecretNaoExpostoPeloReader()
    {
        ConfiguracaoBancoPostgreSql cfg = LeitorConfiguracaoBancoPostgreSql.Carregar(
            Amb(("FUGAPET_Q_CONEXAO_POSTGRES", ConexaoQSemSenha), ("FUGAPET_Q_POSTGRES_SENHA", "SEGREDO-NAO-VAZAR")));

        Assert.Equal("SEGREDO-NAO-VAZAR", cfg.Senha);

        // Prova estrutural: o reader é puro — não referencia Trace/Console/Debug (não há superfície de log do secret).
        string fonte = LerFonte("AcessoDados", "Banco", "LeitorConfiguracaoBancoPostgreSql.cs");
        Assert.DoesNotContain("Trace.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug.", fonte, StringComparison.Ordinal);
    }

    private static string LerFonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && raiz is not null; i++)
        {
            foreach (string candidato in new[] { Path.Combine(raiz, Path.Combine(partes)), Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes)) })
            {
                if (File.Exists(candidato))
                {
                    return File.ReadAllText(candidato);
                }
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException($"Fonte não encontrada: {string.Join('/', partes)}");
    }
}
