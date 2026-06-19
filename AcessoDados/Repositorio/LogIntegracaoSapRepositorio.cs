using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.IntegracaoSap;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class LogIntegracaoSapRepositorio : RepositorioBase
{
    public LogIntegracaoSapRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    public async Task InserirAsync(
        RegistroLogIntegracaoSap registro,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO log_integracao_sap
                (tipo_integracao, operacao, entidade, chave_negocio,
                 codigo_usuario_fugapet, status_http, duracao_ms,
                 correlation_id, situacao, tentativa,
                 mensagem_tecnica_sanitizada, registrado_em_utc,
                 log_integracao_sap_criado_por)
            VALUES
                (@tipo_integracao, @operacao, @entidade, @chave_negocio,
                 @codigo_usuario, @status_http, @duracao_ms,
                 @correlation_id, @situacao, @tentativa,
                 @mensagem, @registrado_em_utc, @codigo_usuario);
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@tipo_integracao", registro.TipoIntegracao));
        comando.Parameters.Add(ParametroTexto("@operacao", registro.Operacao));
        comando.Parameters.Add(ParametroTexto("@entidade", registro.Entidade));
        comando.Parameters.Add(ParametroTexto("@chave_negocio", registro.ChaveNegocio));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_usuario", registro.CodigoUsuarioFugaPet));
        comando.Parameters.Add(ParametroInteiroNulo("@status_http", registro.StatusHttp));
        comando.Parameters.Add(ParametroLongo("@duracao_ms", Math.Max(0, registro.DuracaoMs)));
        comando.Parameters.Add(new NpgsqlParameter("@correlation_id", NpgsqlDbType.Uuid)
        {
            Value = registro.CorrelationId
        });
        comando.Parameters.Add(ParametroTexto("@situacao", registro.Situacao));
        comando.Parameters.Add(ParametroInteiro("@tentativa", Math.Max(1, registro.Tentativa)));
        comando.Parameters.Add(ParametroTexto("@mensagem", registro.MensagemTecnicaSanitizada));
        comando.Parameters.Add(new NpgsqlParameter("@registrado_em_utc", registro.RegistradoEmUtc));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}
