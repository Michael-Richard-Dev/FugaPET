using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Banco;
using Npgsql;

namespace FugaPET_Dev.Servicos.Banco;

public sealed class StatusBancoServico
{
    private readonly ConfiguracaoBancoPostgreSql _configuracao;
    private readonly IFabricaConexaoBanco _fabricaConexaoBanco;

    public StatusBancoServico()
        : this(LeitorConfiguracaoBancoPostgreSql.Carregar())
    {
    }

    public StatusBancoServico(ConfiguracaoBancoPostgreSql configuracao)
        : this(configuracao, new FabricaConexaoPostgreSql(configuracao))
    {
    }

    public StatusBancoServico(
        ConfiguracaoBancoPostgreSql configuracao,
        IFabricaConexaoBanco fabricaConexaoBanco)
    {
        _configuracao = configuracao;
        _fabricaConexaoBanco = fabricaConexaoBanco;
    }

    public async Task<StatusBanco> ObterStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuracao.Habilitado)
        {
            return StatusBanco.Desabilitado(
                _configuracao.Servidor,
                _configuracao.Porta,
                _configuracao.NomeBanco);
        }

        try
        {
            await using NpgsqlConnection conexao =
                await _fabricaConexaoBanco.CriarConexaoAbertaAsync(cancellationToken);

            await using NpgsqlCommand comando = new(
                "select current_database(), current_schema(), now()",
                conexao);

            await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
            if (await leitor.ReadAsync(cancellationToken))
            {
                string nomeBanco = leitor.GetString(0);
                string schemaAtual = leitor.GetString(1);
                DateTime dataHoraServidor = leitor.GetDateTime(2);

                return StatusBanco.Online(
                    _configuracao.Servidor,
                    _configuracao.Porta,
                    nomeBanco,
                    schemaAtual,
                    dataHoraServidor);
            }

            return StatusBanco.Offline(
                _configuracao.Servidor,
                _configuracao.Porta,
                _configuracao.NomeBanco,
                "Nao foi possivel obter retorno do banco configurado.");
        }
        catch (Exception ex)
        {
            // Detalhe tecnico vai para o log de diagnostico; o operador ve mensagem generica.
            System.Diagnostics.Trace.TraceError($"StatusBancoServico: falha ao conectar ao banco. {ex}");
            return StatusBanco.Offline(
                _configuracao.Servidor,
                _configuracao.Porta,
                _configuracao.NomeBanco,
                "Nao foi possivel conectar ao banco configurado. Acione o suporte.");
        }
    }
}
