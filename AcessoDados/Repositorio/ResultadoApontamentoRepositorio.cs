using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistência local do Resultado do Apontamento. Usa nomes de tabela nus para respeitar o search_path
/// configurado no ambiente; se o pacote Gaia correspondente não existir, a camada superior bloqueia de forma
/// operacional sem alterar o apontamento.
/// </summary>
public sealed class ResultadoApontamentoRepositorio : RepositorioBase, IResultadoApontamentoRepositorio
{
    public ResultadoApontamentoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
        long codigoPerfilResultado,
        CancellationToken cancellationToken)
    {
        // GATE 050 (GAIA 041): carrega o ID REAL da definição (codigo_definicao) para propagar como snapshot
        // ao item persistido — nunca hardcodado.
        const string sql = """
            SELECT codigo_definicao,
                   codigo_item,
                   tipo,
                   medida,
                   referencia,
                   ordem_exibicao,
                   obrigatorio
              FROM operacao_resultado_definicao
             WHERE ativo = true
               AND codigo_perfil_resultado = @codigo_perfil_resultado
             ORDER BY ordem_exibicao, codigo_item;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_resultado", codigoPerfilResultado));

        List<ResultadoApontamentoItem> itens = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new ResultadoApontamentoItem
            {
                CodigoDefinicao = leitor.GetInt64(0),
                CodigoItem = leitor.GetString(1),
                Tipo = leitor.GetString(2),
                Medida = leitor.GetString(3),
                Referencia = leitor.GetDecimal(4),
                OrdemExibicao = leitor.GetInt32(5),
                Obrigatorio = leitor.GetBoolean(6)
            });
        }

        return itens;
    }

    public async Task<long> InserirAsync(
        RegistroResultadoApontamento registro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            // GATE 050 (GAIA 041): cabeçalho no contrato físico canônico — apenas as colunas do 041.
            // As colunas redundantes de ordem/sequência/operação e a de atualização não existem em 041.
            const string sqlRegistro = """
                INSERT INTO operacao_resultado_registro
                (
                    codigo_apontamento, usuario, estacao, registrado_em, mensagem_resumo, criado_em
                )
                VALUES
                (
                    @codigo_apontamento, @usuario, @estacao, @registrado_em, @mensagem_resumo, now()
                )
                RETURNING codigo_resultado;
                """;

            await using NpgsqlCommand comandoRegistro = new(sqlRegistro, conexao, transacao);
            comandoRegistro.Parameters.Add(ParametroLongo("@codigo_apontamento", registro.CodigoApontamento));
            comandoRegistro.Parameters.Add(ParametroTexto("@usuario", registro.Usuario));
            comandoRegistro.Parameters.Add(ParametroTexto("@estacao", registro.Estacao));
            comandoRegistro.Parameters.Add(new NpgsqlParameter("@registrado_em", registro.RegistradoEm));
            comandoRegistro.Parameters.Add(ParametroTexto("@mensagem_resumo", registro.MensagemResumo));

            long codigoResultado = Convert.ToInt64(await comandoRegistro.ExecuteScalarAsync(cancellationToken));

            // GATE 050 (GAIA 041): item com codigo_definicao (FK real da definição) e obrigatorio (snapshot
            // histórico). A coluna de atualização não faz parte do 041.
            const string sqlItem = """
                INSERT INTO operacao_resultado_registro_item
                (
                    codigo_resultado, codigo_definicao, codigo_item, tipo, medida, referencia,
                    resultado, ordem_exibicao, obrigatorio, criado_em
                )
                VALUES
                (
                    @codigo_resultado, @codigo_definicao, @codigo_item, @tipo, @medida, @referencia,
                    @resultado, @ordem_exibicao, @obrigatorio, now()
                );
                """;

            foreach (ResultadoApontamentoItem item in registro.Itens.OrderBy(i => i.OrdemExibicao))
            {
                await using NpgsqlCommand comandoItem = new(sqlItem, conexao, transacao);
                comandoItem.Parameters.Add(ParametroLongo("@codigo_resultado", codigoResultado));
                comandoItem.Parameters.Add(ParametroLongo("@codigo_definicao", item.CodigoDefinicao));
                comandoItem.Parameters.Add(ParametroTexto("@codigo_item", item.CodigoItem));
                comandoItem.Parameters.Add(ParametroTexto("@tipo", item.Tipo));
                comandoItem.Parameters.Add(ParametroTexto("@medida", item.Medida));
                comandoItem.Parameters.Add(new NpgsqlParameter("@referencia", item.Referencia));
                comandoItem.Parameters.Add(ParametroDecimalNulo("@resultado", item.Resultado));
                comandoItem.Parameters.Add(ParametroInteiro("@ordem_exibicao", item.OrdemExibicao));
                comandoItem.Parameters.Add(ParametroBooleano("@obrigatorio", item.Obrigatorio));
                await comandoItem.ExecuteNonQueryAsync(cancellationToken);
            }

            return codigoResultado;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
        long codigoApontamento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.codigo_resultado,
                   r.codigo_apontamento,
                   COALESCE(r.mensagem_resumo, '') AS mensagem_resumo,
                   i.codigo_definicao,
                   i.codigo_item,
                   i.tipo,
                   i.medida,
                   i.referencia,
                   i.resultado,
                   i.ordem_exibicao,
                   i.obrigatorio
              FROM operacao_resultado_registro r
              LEFT JOIN operacao_resultado_registro_item i
                ON i.codigo_resultado = r.codigo_resultado
             WHERE r.codigo_apontamento = @codigo_apontamento
             ORDER BY r.codigo_resultado, i.ordem_exibicao, i.codigo_definicao, i.codigo_item;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_apontamento", codigoApontamento));

        Dictionary<long, (long CodigoApontamento, string MensagemResumo, List<ResultadoApontamentoItem> Itens)> resultados = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            long codigoResultado = leitor.GetInt64(0);
            if (!resultados.TryGetValue(codigoResultado, out var acumulado))
            {
                acumulado = (leitor.GetInt64(1), leitor.GetString(2), []);
                resultados.Add(codigoResultado, acumulado);
            }

            if (leitor.IsDBNull(3))
            {
                continue;
            }

            acumulado.Itens.Add(new ResultadoApontamentoItem
            {
                CodigoDefinicao = leitor.GetInt64(3),
                CodigoItem = leitor.GetString(4),
                Tipo = leitor.GetString(5),
                Medida = leitor.GetString(6),
                Referencia = leitor.GetDecimal(7),
                Resultado = leitor.IsDBNull(8) ? null : leitor.GetDecimal(8),
                OrdemExibicao = leitor.GetInt32(9),
                Obrigatorio = leitor.GetBoolean(10)
            });
        }

        // ORDEM CRESCENTE DE codigo_resultado, e isso e CONTRATO, nao detalhe: desde o incremento
        // 072 o apontamento pode ter mais de um resultado (correcao), e quem consome toma o ULTIMO
        // como o valor corrente. O codigo e identity, entao cresce com o tempo de gravacao; ordenar
        // por aqui, e nao por registrado_em, evita empate entre duas gravacoes no mesmo instante.
        return resultados
            .OrderBy(par => par.Key)
            .Select(par => new ResultadoPersistidoApontamento
            {
                CodigoResultado = par.Key,
                CodigoApontamento = par.Value.CodigoApontamento,
                MensagemResumo = par.Value.MensagemResumo,
                Itens = par.Value.Itens.OrderBy(item => item.OrdemExibicao).ToArray()
            })
            .ToArray();
    }
}
