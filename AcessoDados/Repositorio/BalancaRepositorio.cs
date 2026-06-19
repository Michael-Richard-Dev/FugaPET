using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class BalancaRepositorio : RepositorioBase
{
    public BalancaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<BalancaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_balanca, codigo_setor, nome_balanca, identificacao_local, endereco_ip::text,
                   porta_tcp, porta_serial, tipo_conexao, baud_rate, data_bits, paridade, stop_bits,
                   flow_control, protocolo, parametros_tecnicos::text, observacao,
                   situacao_balanca, balanca_criado_em
            FROM balanca
            ORDER BY nome_balanca;
            """;

        List<BalancaCadastro> balancas = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            balancas.Add(MapearBalanca(leitor));
        }

        return balancas;
    }

    public async Task<BalancaCadastro?> ObterPorIdAsync(long codigoBalanca, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_balanca, codigo_setor, nome_balanca, identificacao_local, endereco_ip::text,
                   porta_tcp, porta_serial, tipo_conexao, baud_rate, data_bits, paridade, stop_bits,
                   flow_control, protocolo, parametros_tecnicos::text, observacao,
                   situacao_balanca, balanca_criado_em
            FROM balanca
            WHERE codigo_balanca = @codigo_balanca;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_balanca", codigoBalanca));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearBalanca(leitor);
    }

    public async Task<bool> ExisteNomeNoSetorAsync(string nome, long codigoSetor, long? ignorarCodigo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM balanca
                WHERE codigo_setor = @codigo_setor
                  AND upper(trim(nome_balanca)) = upper(trim(@nome_balanca))
                  AND situacao_balanca = true
                  AND (@ignorar_codigo IS NULL OR codigo_balanca <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
        comando.Parameters.Add(ParametroTexto("@nome_balanca", nome));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO balanca
            (codigo_setor, nome_balanca, identificacao_local, endereco_ip, porta_tcp, porta_serial,
             tipo_conexao, baud_rate, data_bits, paridade, stop_bits, flow_control,
             protocolo, parametros_tecnicos, observacao, situacao_balanca, balanca_criado_por)
            VALUES
            (@codigo_setor, @nome_balanca, @identificacao_local, CAST(NULLIF(@endereco_ip, '') AS inet), @porta_tcp, @porta_serial,
             @tipo_conexao, @baud_rate, @data_bits, @paridade, @stop_bits, @flow_control,
             @protocolo, CAST(NULLIF(@parametros_tecnicos, '') AS jsonb), @observacao, @situacao_balanca, @balanca_criado_por)
            RETURNING codigo_balanca;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            PreencherParametros(comando, balanca);
            comando.Parameters.Add(ParametroLongoNulo("@balanca_criado_por", balanca.BalancaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0;
        }, cancellationToken);
    }

    public async Task<int> AtualizarAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE balanca
            SET codigo_setor = @codigo_setor,
                nome_balanca = @nome_balanca,
                identificacao_local = @identificacao_local,
                endereco_ip = CAST(NULLIF(@endereco_ip, '') AS inet),
                porta_tcp = @porta_tcp,
                porta_serial = @porta_serial,
                tipo_conexao = @tipo_conexao,
                baud_rate = @baud_rate,
                data_bits = @data_bits,
                paridade = @paridade,
                stop_bits = @stop_bits,
                flow_control = @flow_control,
                protocolo = @protocolo,
                parametros_tecnicos = CAST(NULLIF(@parametros_tecnicos, '') AS jsonb),
                observacao = @observacao,
                situacao_balanca = @situacao_balanca,
                balanca_atualizado_por = @balanca_atualizado_por
            WHERE codigo_balanca = @codigo_balanca;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_balanca", balanca.CodigoBalanca));
            PreencherParametros(comando, balanca);
            comando.Parameters.Add(ParametroLongoNulo("@balanca_atualizado_por", balanca.BalancaAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long codigoBalanca, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE balanca
               SET situacao_balanca = false,
                   balanca_atualizado_por = @balanca_atualizado_por
             WHERE codigo_balanca = @codigo_balanca
               AND situacao_balanca = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_balanca", codigoBalanca));
            comando.Parameters.Add(ParametroLongoNulo("@balanca_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long codigoBalanca, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE balanca
               SET situacao_balanca = true,
                   balanca_atualizado_por = @balanca_atualizado_por
             WHERE codigo_balanca = @codigo_balanca
               AND situacao_balanca = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_balanca", codigoBalanca));
            comando.Parameters.Add(ParametroLongoNulo("@balanca_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametros(NpgsqlCommand comando, BalancaCadastro balanca)
    {
        comando.Parameters.Add(ParametroLongo("@codigo_setor", balanca.CodigoSetor));
        comando.Parameters.Add(ParametroTexto("@nome_balanca", balanca.NomeBalanca));
        comando.Parameters.Add(ParametroTexto("@identificacao_local", balanca.IdentificacaoLocal));
        comando.Parameters.Add(ParametroTexto("@endereco_ip", balanca.EnderecoIp));
        comando.Parameters.Add(ParametroInteiroNulo("@porta_tcp", balanca.PortaTcp));
        comando.Parameters.Add(ParametroTexto("@porta_serial", balanca.PortaSerial));
        comando.Parameters.Add(ParametroTexto("@tipo_conexao", string.IsNullOrWhiteSpace(balanca.TipoConexao) ? "SERIAL" : balanca.TipoConexao));
        comando.Parameters.Add(ParametroInteiroNulo("@baud_rate", balanca.BaudRate));
        comando.Parameters.Add(ParametroInteiroNulo("@data_bits", balanca.DataBits));
        comando.Parameters.Add(ParametroTexto("@paridade", balanca.Paridade));
        comando.Parameters.Add(ParametroDecimalNulo("@stop_bits", balanca.StopBits));
        comando.Parameters.Add(ParametroTexto("@flow_control", balanca.FlowControl));
        comando.Parameters.Add(ParametroTexto("@protocolo", balanca.Protocolo));
        comando.Parameters.Add(ParametroTexto("@parametros_tecnicos", balanca.ParametrosTecnicos));
        comando.Parameters.Add(ParametroTexto("@observacao", balanca.Observacao));
        comando.Parameters.Add(ParametroBooleano("@situacao_balanca", balanca.SituacaoBalanca));
    }

    private static BalancaCadastro MapearBalanca(NpgsqlDataReader leitor)
        => new()
        {
            CodigoBalanca = leitor.GetInt64(0),
            CodigoSetor = leitor.GetInt64(1),
            NomeBalanca = leitor.GetString(2),
            IdentificacaoLocal = leitor.IsDBNull(3) ? string.Empty : leitor.GetString(3),
            EnderecoIp = leitor.IsDBNull(4) ? string.Empty : leitor.GetString(4),
            PortaTcp = leitor.IsDBNull(5) ? null : leitor.GetInt32(5),
            PortaSerial = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
            TipoConexao = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
            BaudRate = leitor.IsDBNull(8) ? null : leitor.GetInt32(8),
            DataBits = leitor.IsDBNull(9) ? null : leitor.GetInt32(9),
            Paridade = leitor.IsDBNull(10) ? string.Empty : leitor.GetString(10),
            StopBits = leitor.IsDBNull(11) ? null : leitor.GetDecimal(11),
            FlowControl = leitor.IsDBNull(12) ? string.Empty : leitor.GetString(12),
            Protocolo = leitor.IsDBNull(13) ? string.Empty : leitor.GetString(13),
            ParametrosTecnicos = leitor.IsDBNull(14) ? string.Empty : leitor.GetString(14),
            Observacao = leitor.IsDBNull(15) ? string.Empty : leitor.GetString(15),
            SituacaoBalanca = leitor.GetBoolean(16),
            BalancaCriadoEm = leitor.IsDBNull(17) ? null : leitor.GetDateTime(17).ToLocalTime()
        };
}
