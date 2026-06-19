using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class CargoRepositorio : RepositorioBase
{
    public CargoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<CargoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_cargo, nome_cargo, descricao_cargo, situacao_cargo, cargo_criado_em
            FROM cargo
            ORDER BY nome_cargo;
            """;

        List<CargoCadastro> cargos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            cargos.Add(new CargoCadastro
            {
                IdCargo = leitor.GetInt64(0),
                NomeCargo = leitor.GetString(1),
                DescricaoCargo = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
                SituacaoCargo = leitor.GetBoolean(3),
                CargoCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
            });
        }

        return cargos;
    }

    public async Task<CargoCadastro?> ObterPorIdAsync(long codigoCargo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_cargo, nome_cargo, descricao_cargo, situacao_cargo, cargo_criado_em
            FROM cargo
            WHERE codigo_cargo = @codigo_cargo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_cargo", codigoCargo));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;

        return new CargoCadastro
        {
            IdCargo = leitor.GetInt64(0),
            NomeCargo = leitor.GetString(1),
            DescricaoCargo = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
            SituacaoCargo = leitor.GetBoolean(3),
            CargoCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
        };
    }

    public async Task<long> InserirAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO cargo (nome_cargo, descricao_cargo, situacao_cargo, cargo_criado_por)
            VALUES (@nome_cargo, @descricao_cargo, @situacao_cargo, @cargo_criado_por)
            RETURNING codigo_cargo;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@nome_cargo", cargo.NomeCargo));
            comando.Parameters.Add(ParametroTexto("@descricao_cargo", cargo.DescricaoCargo));
            comando.Parameters.Add(ParametroBooleano("@situacao_cargo", cargo.SituacaoCargo));
            comando.Parameters.Add(ParametroLongoNulo("@cargo_criado_por", cargo.CargoCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<bool> ExisteNomeAsync(string nomeCargo, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM cargo
                WHERE upper(trim(nome_cargo)) = upper(trim(@nome_cargo))
                  AND situacao_cargo = true
                  AND (@ignorar_codigo IS NULL OR codigo_cargo <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_cargo", nomeCargo));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<int> AtualizarAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE cargo
            SET nome_cargo = @nome_cargo,
                descricao_cargo = @descricao_cargo,
                situacao_cargo = @situacao_cargo,
                cargo_atualizado_por = @cargo_atualizado_por
            WHERE codigo_cargo = @codigo_cargo;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_cargo", cargo.IdCargo));
            comando.Parameters.Add(ParametroTexto("@nome_cargo", cargo.NomeCargo));
            comando.Parameters.Add(ParametroTexto("@descricao_cargo", cargo.DescricaoCargo));
            comando.Parameters.Add(ParametroBooleano("@situacao_cargo", cargo.SituacaoCargo));
            comando.Parameters.Add(ParametroLongoNulo("@cargo_atualizado_por", cargo.CargoAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE cargo
               SET situacao_cargo = false,
                   cargo_atualizado_por = @cargo_atualizado_por
             WHERE codigo_cargo = @codigo_cargo
               AND situacao_cargo = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_cargo", id));
            comando.Parameters.Add(ParametroLongoNulo("@cargo_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE cargo
               SET situacao_cargo = true,
                   cargo_atualizado_por = @cargo_atualizado_por
             WHERE codigo_cargo = @codigo_cargo
               AND situacao_cargo = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_cargo", id));
            comando.Parameters.Add(ParametroLongoNulo("@cargo_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }
}
