using System.Text.RegularExpressions;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia da rastreabilidade da Entrada de Produto (entrada_produto_lancamento /
/// _item / _pesagem). Cada lancamento e independente e preserva todas as pesagens.
/// </summary>
public sealed class EntradaProdutoRepositorio : RepositorioBase
{
    private const string ConstraintCorrelationIdLote = "uq_entrada_lote_correlation_id";

    public EntradaProdutoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Grava um lancamento completo (cabecalho + itens + pesagens) em uma unica transacao auditavel.
    /// Retorna o codigo do lancamento criado.
    /// </summary>
    public async Task<long> SalvarLancamentoAsync(EntradaProdutoLancamento lancamento, CancellationToken cancellationToken = default)
    {
        long? usuario = ObterCodigoUsuarioSessao();

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long codigoLancamento = await InserirLancamentoAsync(conexao, transacao, lancamento, usuario, cancellationToken);

            foreach (EntradaProdutoItem item in lancamento.Itens)
            {
                long codigoItem = await InserirItemAsync(conexao, transacao, codigoLancamento, item, usuario, cancellationToken);

                IReadOnlyList<EntradaProdutoPesagem> pesagens =
                    EntradaProdutoPesagemCalculos.ValidarSequencias(item.Pesagens);
                foreach (EntradaProdutoPesagem pesagem in pesagens)
                {
                    await InserirPesagemAsync(
                        conexao,
                        transacao,
                        codigoItem,
                        pesagem,
                        usuario,
                        cancellationToken);
                }

                await AtualizarTotalRecebidoAsync(
                    conexao,
                    transacao,
                    codigoItem,
                    usuario,
                    cancellationToken);
            }

            return codigoLancamento;
        }, cancellationToken);
    }


    public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia entrada,
        ContextoAuditoriaEntradaLotes contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        EntradaProdutoLancamentoComLotesPersistencia snapshot = EntradaProdutoArvoreLotesSnapshot.Criar(entrada);
        ValidadorEntradaProdutoArvoreLotes.Validar(snapshot, contexto.DataReferencia);
        long? usuario = contexto.CodigoUsuario;
        string loginUsuario = contexto.LoginUsuario;

        return await ExecutarEmTransacaoAuditavelAsync(contexto.CodigoUsuario, async (conexao, transacao) =>
        {
            Dictionary<string, long> codigosItens = new(StringComparer.Ordinal);
            Dictionary<Guid, long> codigosLotes = [];
            Dictionary<Guid, long> codigosPesagens = [];
            Dictionary<Guid, decimal> pesosConsolidados = [];

            long codigoLancamento = await InserirLancamentoAsync(
                conexao,
                transacao,
                snapshot.Lancamento,
                usuario,
                cancellationToken);

            foreach (EntradaProdutoItemComLotesPersistencia itemPersistencia in snapshot.Itens)
            {
                string numeroItemSap = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(itemPersistencia.NumeroItemSap);
                long codigoItem = await InserirItemAsync(
                    conexao,
                    transacao,
                    codigoLancamento,
                    itemPersistencia.Item with { NumeroItem = numeroItemSap },
                    usuario,
                    cancellationToken);
                codigosItens.Add(numeroItemSap, codigoItem);

                foreach (EntradaProdutoLoteComPesagensPersistencia lote in itemPersistencia.Lotes)
                {
                    long codigoLote = await InserirLoteAsync(
                        conexao,
                        transacao,
                        codigoItem,
                        lote,
                        loginUsuario,
                        cancellationToken);
                    codigosLotes.Add(lote.CodigoLocal, codigoLote);

                    foreach (EntradaProdutoPesagemComCodigoLocalPersistencia pesagem in lote.Pesagens)
                    {
                        long codigoPesagem = await InserirPesagemComLoteAsync(
                            conexao,
                            transacao,
                            codigoItem,
                            codigoLote,
                            lote,
                            pesagem.Pesagem,
                            usuario,
                            cancellationToken);
                        codigosPesagens.Add(pesagem.CodigoLocalPesagem, codigoPesagem);
                    }

                    decimal pesoBanco = await ConsultarPesoConsolidadoLoteAsync(
                        conexao,
                        transacao,
                        codigoLote,
                        cancellationToken);
                    decimal pesoAplicacao = lote.Pesagens
                        .Where(p => string.Equals(
                            p.Pesagem.StatusPesagem?.Trim(),
                            EntradaProdutoPesagemCalculos.StatusValida,
                            StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Pesagem.PesoLiquidoKg);
                    ValidadorEntradaProdutoArvoreLotes.ConferirPesoConsolidado(
                        lote.CodigoLocal,
                        pesoAplicacao,
                        pesoBanco);
                    pesosConsolidados.Add(lote.CodigoLocal, pesoBanco);
                }

                await AtualizarTotalRecebidoAsync(
                    conexao,
                    transacao,
                    codigoItem,
                    usuario,
                    cancellationToken);
            }

            return ResultadoPersistenciaEntradaComLotes.Criar(
                codigoLancamento,
                codigosItens,
                codigosLotes,
                codigosPesagens,
                pesosConsolidados);
        }, cancellationToken);
    }

    public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia entrada,
        ContextoAuditoriaEntradaLotes contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        EntradaProdutoLancamentoComLotesPersistencia snapshot = EntradaProdutoArvoreLotesSnapshot.Criar(entrada);
        ValidadorEntradaProdutoArvoreLotes.Validar(snapshot, contexto.DataReferencia);
        IReadOnlyList<Guid> correlations = ColetarCorrelationIds(snapshot);

        // ETAPA A: localizar o(s) lançamento(s) das correlações e classificar de forma pura.
        ResultadoClassificacaoCorrelacoesEntradaLotes classificacao =
            await ClassificarCorrelacoesComLancamentoAsync(correlations, cancellationToken);

        return classificacao.Classificacao switch
        {
            ClassificacaoCorrelacoesEntradaLotes.NenhumaExistente =>
                await RegistrarOuRecuperarAposCorridaAsync(snapshot, contexto, correlations, cancellationToken),
            ClassificacaoCorrelacoesEntradaLotes.TodasExistentes =>
                await RecuperarLancamentoComLotesAsync(snapshot, RequererLancamentoUnico(classificacao), cancellationToken),
            ClassificacaoCorrelacoesEntradaLotes.LancamentosDivergentes => throw ConflitoLancamentosDivergentes(classificacao),
            _ => throw ConflitoPersistenciaParcial(classificacao)
        };
    }

    /// <summary>Wrapper de conveniência: delega ao overload PURO testável.</summary>
    internal static bool EhConflitoCorrelationId(PostgresException ex)
        => EhConflitoCorrelationId(ex.SqlState, ex.ConstraintName);

    /// <summary>
    /// Overload PURO (testável sem banco): true somente quando SqlState é 23505 E a constraint violada é a
    /// unicidade da correlation_id do lote. Valores nulos ou constraint diferente resultam em false.
    /// </summary>
    internal static bool EhConflitoCorrelationId(string? sqlState, string? constraintName)
        => string.Equals(sqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
           && string.Equals(constraintName, ConstraintCorrelationIdLote, StringComparison.Ordinal);

    private async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarAposCorridaAsync(
        EntradaProdutoLancamentoComLotesPersistencia snapshot,
        ContextoAuditoriaEntradaLotes contexto,
        IReadOnlyList<Guid> correlations,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RegistrarLancamentoComLotesAsync(snapshot, contexto, cancellationToken);
        }
        catch (PostgresException ex) when (EhConflitoCorrelationId(ex))
        {
            // §4: depois do rollback do insert-only, RECLASSIFICAR (nunca recuperar direto).
            ResultadoClassificacaoCorrelacoesEntradaLotes reclassificacao =
                await ClassificarCorrelacoesComLancamentoAsync(correlations, cancellationToken);

            switch (reclassificacao.Classificacao)
            {
                case ClassificacaoCorrelacoesEntradaLotes.TodasExistentes:
                    return await RecuperarLancamentoComLotesAsync(
                        snapshot, RequererLancamentoUnico(reclassificacao), cancellationToken);
                case ClassificacaoCorrelacoesEntradaLotes.ParcialmenteExistente:
                    throw ConflitoPersistenciaParcial(reclassificacao);
                case ClassificacaoCorrelacoesEntradaLotes.LancamentosDivergentes:
                    throw ConflitoLancamentosDivergentes(reclassificacao);
                default:
                    // §4.8: nenhuma existe -> o conflito NÃO pôde ser confirmado; propaga a exceção original.
                    throw;
            }
        }
    }

    private async Task<ResultadoPersistenciaEntradaComLotes> RecuperarLancamentoComLotesAsync(
        EntradaProdutoLancamentoComLotesPersistencia snapshot,
        long codigoLancamento,
        CancellationToken cancellationToken)
    {
        // ETAPA B: carrega a ÁRVORE COMPLETA do lançamento (todos os itens/lotes/pesagens), NÃO só os das
        // correlações. Assim o comparador recebe item adicional persistido e pode rejeitar a repetição parcial.
        EntradaProdutoLancamentoPersistidoComLotes persistido =
            await ConsultarArvoreCompletaPorLancamentoAsync(codigoLancamento, cancellationToken);

        ResultadoValidacaoRecuperacaoEntradaLotes validacao =
            ValidadorRecuperacaoPersistenciaEntradaLotes.Validar(snapshot, persistido);
        if (!validacao.Sucesso || validacao.ResultadoRecuperado is null)
        {
            throw ConflitoArvoreDivergente(validacao.Divergencia);
        }

        return validacao.ResultadoRecuperado;
    }

    private static IReadOnlyList<Guid> ColetarCorrelationIds(EntradaProdutoLancamentoComLotesPersistencia snapshot)
    {
        List<Guid> correlations = snapshot.Itens
            .SelectMany(item => item.Lotes)
            .Select(lote => lote.CorrelationId)
            .ToList();

        if (correlations.Count == 0)
        {
            throw new ConflitoPersistenciaEntradaLotesException(
                "Nenhuma correlation_id informada para a persistência por lotes.",
                "Árvore de entrada por lotes sem nenhuma correlation_id.");
        }

        if (correlations.Any(correlation => correlation == Guid.Empty))
        {
            throw new ConflitoPersistenciaEntradaLotesException(
                "Correlation_id vazia não é permitida na persistência por lotes.",
                "Árvore de entrada por lotes com correlation_id vazia (Guid.Empty).",
                Guid.Empty);
        }

        if (correlations.Distinct().Count() != correlations.Count)
        {
            Guid duplicada = correlations
                .GroupBy(correlation => correlation)
                .First(grupo => grupo.Count() > 1)
                .Key;
            throw new ConflitoPersistenciaEntradaLotesException(
                "Correlation_id duplicada na árvore de entrada por lotes.",
                $"Correlation_id duplicada na árvore de entrada: {duplicada}.",
                duplicada);
        }

        return correlations;
    }

    /// <summary>
    /// ETAPA A — localiza o lançamento das correlações e classifica de forma PURA. A consulta relaciona
    /// entrada_produto_lote → entrada_produto_item para obter o codigo_entrada_produto_lancamento de cada
    /// correlation_id encontrada. Não infere o lançamento pelo primeiro registro.
    /// </summary>
    private async Task<ResultadoClassificacaoCorrelacoesEntradaLotes> ClassificarCorrelacoesComLancamentoAsync(
        IReadOnlyList<Guid> correlations,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT lote.correlation_id,
                   item.codigo_entrada_produto_lancamento
              FROM entrada_produto_lote lote
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_item = lote.codigo_entrada_produto_item
             WHERE lote.correlation_id = ANY(@correlations);
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(new NpgsqlParameter("@correlations", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = correlations.ToArray()
        });

        List<(Guid CorrelationId, long CodigoLancamento)> encontradas = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            encontradas.Add((leitor.GetGuid(0), leitor.GetInt64(1)));
        }

        return ClassificarCorrelacoes(correlations, encontradas);
    }

    /// <summary>
    /// Classificador PURO (testável sem banco). Recebe as correlações SOLICITADAS e os pares
    /// correlation_id/codigo_lancamento ENCONTRADOS. Um resultado duplicado (mesmo par repetido) não altera
    /// a contagem lógica. Todas encontradas em um único lançamento ⇒ TodasExistentes; todas encontradas em
    /// lançamentos diferentes ⇒ LancamentosDivergentes; parte encontrada ⇒ ParcialmenteExistente.
    /// </summary>
    internal static ResultadoClassificacaoCorrelacoesEntradaLotes ClassificarCorrelacoes(
        IReadOnlyCollection<Guid> solicitadas,
        IReadOnlyCollection<(Guid CorrelationId, long CodigoLancamento)> encontradas)
    {
        HashSet<Guid> solicitadasUnicas = [.. solicitadas];

        // Só consideram-se as correlações realmente pedidas; duplicatas de linha são colapsadas.
        Dictionary<Guid, long> lancamentoPorCorrelacao = [];
        foreach ((Guid correlationId, long codigoLancamento) in encontradas)
        {
            if (solicitadasUnicas.Contains(correlationId))
            {
                lancamentoPorCorrelacao[correlationId] = codigoLancamento;
            }
        }

        int quantidadeSolicitada = solicitadasUnicas.Count;
        int quantidadeEncontrada = lancamentoPorCorrelacao.Count;
        List<long> lancamentosDistintos = lancamentoPorCorrelacao.Values.Distinct().OrderBy(codigo => codigo).ToList();

        ClassificacaoCorrelacoesEntradaLotes classificacao;
        long? lancamentoUnico = null;

        if (quantidadeEncontrada == 0)
        {
            classificacao = ClassificacaoCorrelacoesEntradaLotes.NenhumaExistente;
        }
        else if (quantidadeEncontrada < quantidadeSolicitada)
        {
            classificacao = ClassificacaoCorrelacoesEntradaLotes.ParcialmenteExistente;
        }
        else if (lancamentosDistintos.Count == 1)
        {
            classificacao = ClassificacaoCorrelacoesEntradaLotes.TodasExistentes;
            lancamentoUnico = lancamentosDistintos[0];
        }
        else
        {
            classificacao = ClassificacaoCorrelacoesEntradaLotes.LancamentosDivergentes;
        }

        return new ResultadoClassificacaoCorrelacoesEntradaLotes(
            classificacao,
            quantidadeSolicitada,
            quantidadeEncontrada,
            lancamentosDistintos,
            lancamentoUnico);
    }

    private static long RequererLancamentoUnico(ResultadoClassificacaoCorrelacoesEntradaLotes classificacao)
        => classificacao.CodigoLancamentoUnico
           ?? throw new ConflitoPersistenciaEntradaLotesException(
               "A correlation_id informada já pertence a uma árvore de entrada diferente.",
               "TodasExistentes sem código de lançamento único resolvido.");

    private static ConflitoPersistenciaEntradaLotesException ConflitoPersistenciaParcial(
        ResultadoClassificacaoCorrelacoesEntradaLotes classificacao)
        => new(
            "A persistência por lotes está inconsistente: apenas parte das correlações já existe no banco.",
            $"Persistência parcial: {classificacao.QuantidadeEncontrada} de {classificacao.QuantidadeSolicitada} "
            + "correlações já existem no banco.");

    private static ConflitoPersistenciaEntradaLotesException ConflitoLancamentosDivergentes(
        ResultadoClassificacaoCorrelacoesEntradaLotes classificacao)
        => new(
            "A correlation_id informada já pertence a uma árvore de entrada diferente.",
            $"Correlações pertencem a {classificacao.CodigosLancamentoDistintos.Count} lançamentos distintos: "
            + string.Join(", ", classificacao.CodigosLancamentoDistintos) + ".");

    // §11: usa a divergência do comparador como diagnóstico técnico; a mensagem pública continua genérica.
    private static ConflitoPersistenciaEntradaLotesException ConflitoArvoreDivergente(string? divergencia)
    {
        string diagnostico = string.IsNullOrWhiteSpace(divergencia)
            ? "Árvore persistida difere da árvore informada."
            : divergencia;

        (Guid? correlationId, string? numeroItem) = ExtrairDetalheDivergencia(diagnostico);
        return new ConflitoPersistenciaEntradaLotesException(
            "A correlation_id informada já pertence a uma árvore de entrada diferente.",
            diagnostico,
            correlationId,
            numeroItem);
    }

    // Best-effort: extrai a primeira correlation_id (Guid) ou o número de item citado no diagnóstico do
    // comparador, quando presentes. Não falha se não houver — apenas devolve null.
    private static (Guid? CorrelationId, string? NumeroItemSap) ExtrairDetalheDivergencia(string diagnostico)
    {
        Match guid = Regex.Match(diagnostico, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        Guid? correlationId = guid.Success && Guid.TryParse(guid.Value, out Guid parsed) ? parsed : null;

        string? numeroItem = null;
        Match item = Regex.Match(diagnostico, @"item[^(]*\((?<numero>[^)]+)\)", RegexOptions.IgnoreCase);
        if (item.Success)
        {
            numeroItem = item.Groups["numero"].Value.Trim();
        }

        return (correlationId, numeroItem);
    }

    /// <summary>
    /// ETAPA B — carrega a ÁRVORE COMPLETA de um lançamento por codigo_entrada_produto_lancamento: o
    /// lançamento, TODOS os itens (JOIN), todos os lotes (LEFT JOIN) e todas as pesagens (LEFT JOIN). Um
    /// item sem lote chega ao comparador como item com coleção vazia. Colunas explícitas, sem SELECT *,
    /// parametrizada.
    /// </summary>
    private async Task<EntradaProdutoLancamentoPersistidoComLotes> ConsultarArvoreCompletaPorLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT lancamento.codigo_entrada_produto_lancamento,
                   lancamento.numero_pedido,
                   lancamento.fornecedor,
                   lancamento.codigo_setor,
                   lancamento.terminal,
                   lancamento.status_lancamento,
                   item.codigo_entrada_produto_item,
                   item.codigo_sap_pedido_compra_item,
                   item.numero_item,
                   item.material,
                   item.centro,
                   item.deposito,
                   item.unidade,
                   item.quantidade_prevista,
                   lote.codigo_entrada_produto_lote,
                   lote.correlation_id,
                   lote.numero_lote,
                   lote.data_fabricacao,
                   lote.data_vencimento,
                   lote.status_lote,
                   lote.peso_liquido_total_kg,
                   pesagem.codigo_entrada_produto_pesagem,
                   pesagem.sequencia,
                   pesagem.peso_bruto_kg,
                   pesagem.peso_tara_kg,
                   pesagem.peso_liquido_kg,
                   pesagem.codigo_tara,
                   pesagem.codigo_balanca,
                   pesagem.origem,
                   pesagem.status_pesagem,
                   pesagem.leitura_original,
                   pesagem.payload_balanca::text,
                   pesagem.pesado_em,
                   pesagem.numero_lote_snapshot,
                   pesagem.data_fabricacao_snapshot,
                   pesagem.data_vencimento_snapshot
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento = lancamento.codigo_entrada_produto_lancamento
              LEFT JOIN entrada_produto_lote lote
                ON lote.codigo_entrada_produto_item = item.codigo_entrada_produto_item
              LEFT JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_lote = lote.codigo_entrada_produto_lote
               AND pesagem.situacao_entrada_produto_pesagem = true
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
             ORDER BY item.codigo_entrada_produto_item,
                      lote.codigo_entrada_produto_lote NULLS LAST,
                      pesagem.sequencia NULLS LAST,
                      pesagem.codigo_entrada_produto_pesagem NULLS LAST;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));

        Dictionary<long, ItemBuilder> itens = [];
        Dictionary<long, LoteBuilder> lotes = [];
        LancamentoBuilder? lancamento = null;

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            lancamento ??= new LancamentoBuilder(
                leitor.GetInt64(0),
                leitor.GetString(1),
                leitor.IsDBNull(2) ? null : leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetInt64(3),
                leitor.IsDBNull(4) ? null : leitor.GetString(4),
                leitor.GetString(5));

            long codigoItem = leitor.GetInt64(6);
            if (!itens.TryGetValue(codigoItem, out ItemBuilder? item))
            {
                item = new ItemBuilder(
                    codigoItem,
                    leitor.IsDBNull(7) ? null : leitor.GetInt64(7),
                    leitor.GetString(8),
                    leitor.IsDBNull(9) ? null : leitor.GetString(9),
                    leitor.IsDBNull(10) ? null : leitor.GetString(10),
                    leitor.IsDBNull(11) ? null : leitor.GetString(11),
                    leitor.IsDBNull(12) ? null : leitor.GetString(12),
                    leitor.IsDBNull(13) ? null : leitor.GetDecimal(13));
                itens.Add(codigoItem, item);
                lancamento.Itens.Add(item);
            }

            // Item sem lote (LEFT JOIN): entra no comparador com coleção de lotes vazia.
            if (leitor.IsDBNull(14))
            {
                continue;
            }

            long codigoLote = leitor.GetInt64(14);
            if (!lotes.TryGetValue(codigoLote, out LoteBuilder? lote))
            {
                lote = new LoteBuilder(
                    codigoLote,
                    leitor.GetGuid(15),
                    leitor.GetString(16),
                    leitor.GetDateTime(17),
                    leitor.GetDateTime(18),
                    leitor.GetString(19),
                    leitor.GetDecimal(20));
                lotes.Add(codigoLote, lote);
                item.Lotes.Add(lote);
            }

            if (!leitor.IsDBNull(21))
            {
                lote.Pesagens.Add(new EntradaProdutoPesagemPersistidaLote
                {
                    CodigoPesagem = leitor.GetInt64(21),
                    Sequencia = leitor.GetInt32(22),
                    PesoBrutoKg = leitor.GetDecimal(23),
                    PesoTaraKg = leitor.GetDecimal(24),
                    PesoLiquidoKg = leitor.GetDecimal(25),
                    CodigoTara = leitor.IsDBNull(26) ? null : leitor.GetInt64(26),
                    CodigoBalanca = leitor.IsDBNull(27) ? null : leitor.GetInt64(27),
                    Origem = leitor.GetString(28),
                    StatusPesagem = leitor.GetString(29),
                    LeituraOriginal = leitor.IsDBNull(30) ? null : leitor.GetString(30),
                    PayloadBalanca = leitor.IsDBNull(31) ? null : leitor.GetString(31),
                    PesadoEm = leitor.GetFieldValue<DateTimeOffset>(32),
                    NumeroLoteSnapshot = leitor.IsDBNull(33) ? null : leitor.GetString(33),
                    DataFabricacaoSnapshot = leitor.IsDBNull(34) ? null : leitor.GetDateTime(34),
                    DataVencimentoSnapshot = leitor.IsDBNull(35) ? null : leitor.GetDateTime(35)
                });
            }
        }

        if (lancamento is null)
        {
            throw new ConflitoPersistenciaEntradaLotesException(
                "A correlation_id informada já pertence a uma árvore de entrada diferente.",
                $"Árvore não encontrada para o lançamento {codigoLancamento} depois do conflito confirmado.");
        }

        return lancamento.Criar();
    }

    internal enum ClassificacaoCorrelacoesEntradaLotes
    {
        NenhumaExistente,
        TodasExistentes,
        ParcialmenteExistente,

        /// <summary>Todas as correlações existem, mas em lançamentos DIFERENTES (árvore divergente).</summary>
        LancamentosDivergentes
    }

    /// <summary>
    /// Resultado PURO da classificação das correlações (sem banco). <see cref="CodigoLancamentoUnico"/> só é
    /// preenchido quando <see cref="Classificacao"/> == TodasExistentes.
    /// </summary>
    internal sealed record ResultadoClassificacaoCorrelacoesEntradaLotes(
        ClassificacaoCorrelacoesEntradaLotes Classificacao,
        int QuantidadeSolicitada,
        int QuantidadeEncontrada,
        IReadOnlyList<long> CodigosLancamentoDistintos,
        long? CodigoLancamentoUnico);

    private sealed class LancamentoBuilder(
        long codigoLancamento,
        string numeroPedido,
        string? fornecedor,
        long? codigoSetor,
        string? terminal,
        string statusLancamento)
    {
        public long CodigoLancamento { get; } = codigoLancamento;
        public List<ItemBuilder> Itens { get; } = [];

        public EntradaProdutoLancamentoPersistidoComLotes Criar()
            => new()
            {
                CodigoLancamento = CodigoLancamento,
                NumeroPedido = numeroPedido,
                Fornecedor = fornecedor,
                CodigoSetor = codigoSetor,
                Terminal = terminal,
                StatusLancamento = statusLancamento,
                Itens = Itens.Select(item => item.Criar()).ToList()
            };
    }

    private sealed class ItemBuilder(
        long codigoItem,
        long? codigoSapPedidoCompraItem,
        string numeroItemSap,
        string? material,
        string? centro,
        string? deposito,
        string? unidade,
        decimal? quantidadePrevista)
    {
        public List<LoteBuilder> Lotes { get; } = [];

        public EntradaProdutoItemPersistidoComLotes Criar()
            => new()
            {
                CodigoItem = codigoItem,
                CodigoSapPedidoCompraItem = codigoSapPedidoCompraItem,
                NumeroItemSap = numeroItemSap,
                Material = material,
                Centro = centro,
                Deposito = deposito,
                Unidade = unidade,
                QuantidadePrevista = quantidadePrevista,
                Lotes = Lotes.Select(lote => lote.Criar()).ToList()
            };
    }

    private sealed class LoteBuilder(
        long codigoLote,
        Guid correlationId,
        string numeroLote,
        DateTime dataFabricacao,
        DateTime dataVencimento,
        string statusLote,
        decimal pesoLiquidoTotalKg)
    {
        public List<EntradaProdutoPesagemPersistidaLote> Pesagens { get; } = [];

        public EntradaProdutoLotePersistidoComPesagens Criar()
            => new()
            {
                CodigoLote = codigoLote,
                CorrelationId = correlationId,
                NumeroLote = numeroLote,
                DataFabricacao = dataFabricacao,
                DataVencimento = dataVencimento,
                StatusLote = statusLote,
                PesoLiquidoTotalKg = pesoLiquidoTotalKg,
                Pesagens = Pesagens.ToList()
            };
    }

    private async Task<long> InserirLancamentoAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        EntradaProdutoLancamento lancamento, long? usuario, CancellationToken cancellationToken)
    {
        // status FINALIZADO_LOCAL: o lancamento e gravado completo (todas as pesagens) ao parar a producao,
        // ainda nao enviado ao SAP. Coerente com a CHECK ck_entrada_lancamento_finalizado_tem_data.
        const string sql = """
            INSERT INTO entrada_produto_lancamento
                (numero_pedido, fornecedor, codigo_setor, terminal, codigo_usuario,
                 iniciado_em, finalizado_em, status_lancamento, entrada_produto_lancamento_criado_por)
            VALUES
                (@numero_pedido, @fornecedor, @codigo_setor, @terminal, @usuario,
                 now(), now(), 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_lancamento;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", lancamento.NumeroPedido));
        comando.Parameters.Add(ParametroTextoNulo("@fornecedor", lancamento.Fornecedor));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_setor", lancamento.CodigoSetor));
        comando.Parameters.Add(ParametroTextoNulo("@terminal", lancamento.Terminal));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o lancamento de entrada.");
    }

    private async Task<long> InserirItemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoLancamento, EntradaProdutoItem item, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_item
                (codigo_entrada_produto_lancamento, codigo_sap_pedido_compra_item, numero_item, material,
                 centro, deposito, unidade, quantidade_prevista, quantidade_recebida, status_item, entrada_produto_item_criado_por)
            VALUES
                (@lancamento, @sap_item, @numero_item, @material,
                 @centro, @deposito, @unidade, @qtd_prevista, NULL, 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongoNulo("@sap_item", item.CodigoSapPedidoCompraItem));
        comando.Parameters.Add(ParametroTexto("@numero_item", item.NumeroItem));
        comando.Parameters.Add(ParametroTextoNulo("@material", item.Material));
        comando.Parameters.Add(ParametroTextoNulo("@centro", item.Centro));
        comando.Parameters.Add(ParametroTextoNulo("@deposito", item.Deposito));
        comando.Parameters.Add(ParametroTextoNulo("@unidade", item.Unidade));
        comando.Parameters.Add(ParametroNumericoNulo("@qtd_prevista", item.QuantidadePrevista));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o item do lancamento.");
    }

    private async Task InserirPesagemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoItem, EntradaProdutoPesagem pesagem, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_pesagem
                (codigo_entrada_produto_item, sequencia, peso_bruto_kg, peso_tara_kg, peso_liquido_kg,
                 codigo_tara, codigo_balanca, origem, status_pesagem, leitura_original,
                 payload_balanca, codigo_usuario, pesado_em, entrada_produto_pesagem_criado_por)
            VALUES
                (@item, @sequencia, @bruto, @tara, @liquido,
                 @codigo_tara, @codigo_balanca, @origem, @status, @leitura_original,
                 @payload_balanca, @usuario, @pesado_em, @usuario);
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        comando.Parameters.Add(ParametroInteiro("@sequencia", pesagem.Sequencia));
        comando.Parameters.Add(new NpgsqlParameter("@bruto", NpgsqlDbType.Numeric) { Value = pesagem.PesoBrutoKg });
        comando.Parameters.Add(new NpgsqlParameter("@tara", NpgsqlDbType.Numeric) { Value = pesagem.PesoTaraKg });
        comando.Parameters.Add(new NpgsqlParameter("@liquido", NpgsqlDbType.Numeric) { Value = pesagem.PesoLiquidoKg });
        comando.Parameters.Add(ParametroLongoNulo("@codigo_tara", pesagem.CodigoTara));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_balanca", pesagem.CodigoBalanca));
        comando.Parameters.Add(ParametroTexto("@origem", string.IsNullOrWhiteSpace(pesagem.Origem) ? "BALANCA" : pesagem.Origem));
        comando.Parameters.Add(ParametroTexto("@status", string.IsNullOrWhiteSpace(pesagem.StatusPesagem) ? "VALIDA" : pesagem.StatusPesagem));
        comando.Parameters.Add(ParametroTextoNulo("@leitura_original", pesagem.LeituraOriginal));
        comando.Parameters.Add(new NpgsqlParameter("@payload_balanca", NpgsqlDbType.Jsonb)
        {
            Value = string.IsNullOrWhiteSpace(pesagem.PayloadBalanca)
                ? DBNull.Value
                : pesagem.PayloadBalanca
        });
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        comando.Parameters.Add(new NpgsqlParameter("@pesado_em", NpgsqlDbType.TimestampTz)
        {
            Value = pesagem.PesadoEm.ToUniversalTime()
        });
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }


    private async Task<long> InserirLoteAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoItem,
        EntradaProdutoLoteComPesagensPersistencia lote,
        string loginUsuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_lote
                (codigo_entrada_produto_item, numero_lote, data_fabricacao, data_vencimento,
                 status_lote, correlation_id, criado_por, confirmado_em)
            VALUES
                (@codigo_item, @numero_lote, @data_fabricacao, @data_vencimento,
                 'FINALIZADO_LOCAL', @correlation_id, @criado_por, now())
            RETURNING codigo_entrada_produto_lote;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_item", codigoItem));
        comando.Parameters.Add(ParametroTexto("@numero_lote", lote.Dados.NumeroLote));
        comando.Parameters.Add(new NpgsqlParameter("@data_fabricacao", NpgsqlDbType.Date) { Value = lote.Dados.DataFabricacao.Date });
        comando.Parameters.Add(new NpgsqlParameter("@data_vencimento", NpgsqlDbType.Date) { Value = lote.Dados.DataVencimento.Date });
        comando.Parameters.Add(new NpgsqlParameter("@correlation_id", NpgsqlDbType.Uuid) { Value = lote.CorrelationId });
        comando.Parameters.Add(ParametroTexto("@criado_por", loginUsuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Não foi possível criar o lote da entrada.");
    }

    private async Task<long> InserirPesagemComLoteAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoItem,
        long codigoLote,
        EntradaProdutoLoteComPesagensPersistencia lote,
        EntradaProdutoPesagem pesagem,
        long? usuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_pesagem
                (codigo_entrada_produto_item, codigo_entrada_produto_lote, sequencia,
                 peso_bruto_kg, peso_tara_kg, peso_liquido_kg,
                 codigo_tara, codigo_balanca, origem, status_pesagem, leitura_original,
                 payload_balanca, codigo_usuario, pesado_em,
                 numero_lote_snapshot, data_fabricacao_snapshot, data_vencimento_snapshot,
                 entrada_produto_pesagem_criado_por)
            VALUES
                (@item, @lote, @sequencia,
                 @bruto, @tara, @liquido,
                 @codigo_tara, @codigo_balanca, @origem, @status, @leitura_original,
                 @payload_balanca, @usuario, @pesado_em,
                 @numero_lote_snapshot, @data_fabricacao_snapshot, @data_vencimento_snapshot,
                 @usuario)
            RETURNING codigo_entrada_produto_pesagem;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        comando.Parameters.Add(ParametroLongo("@lote", codigoLote));
        comando.Parameters.Add(ParametroInteiro("@sequencia", pesagem.Sequencia));
        comando.Parameters.Add(new NpgsqlParameter("@bruto", NpgsqlDbType.Numeric) { Value = pesagem.PesoBrutoKg });
        comando.Parameters.Add(new NpgsqlParameter("@tara", NpgsqlDbType.Numeric) { Value = pesagem.PesoTaraKg });
        comando.Parameters.Add(new NpgsqlParameter("@liquido", NpgsqlDbType.Numeric) { Value = pesagem.PesoLiquidoKg });
        comando.Parameters.Add(ParametroLongoNulo("@codigo_tara", pesagem.CodigoTara));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_balanca", pesagem.CodigoBalanca));
        comando.Parameters.Add(ParametroTexto("@origem", pesagem.Origem));
        comando.Parameters.Add(ParametroTexto("@status", pesagem.StatusPesagem));
        comando.Parameters.Add(ParametroTextoNulo("@leitura_original", pesagem.LeituraOriginal));
        comando.Parameters.Add(new NpgsqlParameter("@payload_balanca", NpgsqlDbType.Jsonb)
        {
            Value = string.IsNullOrWhiteSpace(pesagem.PayloadBalanca)
                ? DBNull.Value
                : pesagem.PayloadBalanca
        });
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        comando.Parameters.Add(new NpgsqlParameter("@pesado_em", NpgsqlDbType.TimestampTz)
        {
            Value = pesagem.PesadoEm.ToUniversalTime()
        });
        comando.Parameters.Add(ParametroTexto("@numero_lote_snapshot", lote.Dados.NumeroLote));
        comando.Parameters.Add(new NpgsqlParameter("@data_fabricacao_snapshot", NpgsqlDbType.Date) { Value = lote.Dados.DataFabricacao.Date });
        comando.Parameters.Add(new NpgsqlParameter("@data_vencimento_snapshot", NpgsqlDbType.Date) { Value = lote.Dados.DataVencimento.Date });
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Não foi possível criar a pesagem do lote da entrada.");
    }

    private static async Task<decimal> ConsultarPesoConsolidadoLoteAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoLote,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT peso_liquido_total_kg
              FROM entrada_produto_lote
             WHERE codigo_entrada_produto_lote = @codigo_lote;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_lote", codigoLote));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is decimal peso ? peso : throw new InvalidOperationException("Não foi possível consultar o peso consolidado do lote da entrada.");
    }
    private static async Task AtualizarTotalRecebidoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoItem,
        long? usuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE entrada_produto_item item
               SET quantidade_recebida = (
                       SELECT COALESCE(SUM(pesagem.peso_liquido_kg), 0)
                         FROM entrada_produto_pesagem pesagem
                        WHERE pesagem.codigo_entrada_produto_item = item.codigo_entrada_produto_item
                          AND pesagem.situacao_entrada_produto_pesagem = true
                          AND pesagem.status_pesagem = 'VALIDA'
                   ),
                   entrada_produto_item_atualizado_por = @usuario
             WHERE item.codigo_entrada_produto_item = @codigo_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_item", codigoItem));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EntradaProdutoItemPersistido?> ObterItemPersistidoAsync(
        long codigoLancamento,
        long codigoSapPedidoCompraItem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT lancamento.codigo_entrada_produto_lancamento,
                   item.codigo_sap_pedido_compra_item,
                   lancamento.numero_pedido,
                   item.numero_item,
                   COALESCE(item.material, ''),
                   COALESCE(item_sap.descricao_produto, ''),
                   COALESCE(lancamento.fornecedor, fornecedor.codigo_fornecedor, ''),
                   pedido_sap.data_pedido,
                   COALESCE(lancamento.terminal, ''),
                   COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3)
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento =
                   lancamento.codigo_entrada_produto_lancamento
              LEFT JOIN sap_pedido_compra_item item_sap
                ON item_sap.codigo_sap_pedido_compra_item =
                   item.codigo_sap_pedido_compra_item
              LEFT JOIN sap_pedido_compra pedido_sap
                ON pedido_sap.codigo_sap_pedido_compra =
                   item_sap.codigo_sap_pedido_compra
              LEFT JOIN sap_fornecedor fornecedor
                ON fornecedor.codigo_sap_fornecedor =
                   pedido_sap.codigo_sap_fornecedor
              LEFT JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_item =
                   item.codigo_entrada_produto_item
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND item.codigo_sap_pedido_compra_item = @codigo_sap_item
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
             GROUP BY
                   lancamento.codigo_entrada_produto_lancamento,
                   item.codigo_sap_pedido_compra_item,
                   lancamento.numero_pedido,
                   item.numero_item,
                   item.material,
                   item_sap.descricao_produto,
                   lancamento.fornecedor,
                   fornecedor.codigo_fornecedor,
                   pedido_sap.data_pedido,
                   lancamento.terminal
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongo("@codigo_sap_item", codigoSapPedidoCompraItem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new EntradaProdutoItemPersistido
        {
            CodigoLancamento = leitor.GetInt64(0),
            CodigoSapPedidoCompraItem = leitor.GetInt64(1),
            NumeroPedido = leitor.GetString(2),
            NumeroItem = leitor.GetString(3),
            Material = leitor.GetString(4),
            DescricaoMaterial = leitor.GetString(5),
            Fornecedor = leitor.GetString(6),
            DataPedido = leitor.IsDBNull(7) ? null : leitor.GetFieldValue<DateOnly>(7),
            Terminal = leitor.GetString(8),
            PesoLiquidoTotalKg = leitor.GetDecimal(9)
        };
    }

    /// <summary>
    /// Lista CADA pesagem persistida de um item (não usa SUM): usada para o detalhe e a reimpressão por
    /// pesagem individual. Ordenada por sequência e pesado_em. Parametrizada.
    /// </summary>
    public async Task<IReadOnlyList<EntradaProdutoPesagem>> ListarPesagensPersistidasAsync(
        long codigoLancamento,
        long codigoSapPedidoCompraItem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT pesagem.codigo_entrada_produto_pesagem,
                   pesagem.sequencia,
                   pesagem.peso_bruto_kg,
                   pesagem.peso_tara_kg,
                   pesagem.peso_liquido_kg,
                   pesagem.codigo_tara,
                   pesagem.codigo_balanca,
                   COALESCE(pesagem.origem, 'BALANCA'),
                   COALESCE(pesagem.status_pesagem, 'VALIDA'),
                   pesagem.leitura_original,
                   pesagem.pesado_em
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento =
                   lancamento.codigo_entrada_produto_lancamento
              JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_item =
                   item.codigo_entrada_produto_item
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND item.codigo_sap_pedido_compra_item = @codigo_sap_item
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
               AND pesagem.situacao_entrada_produto_pesagem = true
             ORDER BY pesagem.sequencia, pesagem.pesado_em;
            """;

        List<EntradaProdutoPesagem> pesagens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongo("@codigo_sap_item", codigoSapPedidoCompraItem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            pesagens.Add(new EntradaProdutoPesagem
            {
                CodigoEntradaProdutoPesagem = leitor.GetInt64(0),
                Sequencia = leitor.GetInt32(1),
                PesoBrutoKg = leitor.GetDecimal(2),
                PesoTaraKg = leitor.GetDecimal(3),
                PesoLiquidoKg = leitor.GetDecimal(4),
                CodigoTara = leitor.IsDBNull(5) ? null : leitor.GetInt64(5),
                CodigoBalanca = leitor.IsDBNull(6) ? null : leitor.GetInt64(6),
                Origem = leitor.GetString(7),
                StatusPesagem = leitor.GetString(8),
                LeituraOriginal = leitor.IsDBNull(9) ? null : leitor.GetString(9),
                PesadoEm = leitor.GetFieldValue<DateTimeOffset>(10)
            });
        }

        return pesagens;
    }

    /// <summary>
    /// Itens de um lancamento ja persistido, com pesos consolidados das pesagens VALIDAS, para o
    /// envio CONTROLADO de peso ao SAP. Retorna apenas itens com peso liquido positivo.
    /// </summary>
    public async Task<IReadOnlyList<EntradaProdutoItemEnvioSap>> ListarItensParaEnvioSapAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        // Material/centro/deposito/unidade vem do proprio lancamento local (entrada_produto_item),
        // gravados na finalizacao a partir do item do pedido/cache. numero_lote/data_fabricacao/
        // data_vencimento vem de entrada_produto_lote (pacote 043). O agrupamento e POR LOTE
        // (pesagem.codigo_entrada_produto_lote): cada linha gera UMA posicao SAP (Batch + datas
        // proprios). Lotes diferentes do mesmo item NUNCA sao somados numa unica posicao.
        const string sql = """
            SELECT lancamento.numero_pedido,
                   item.numero_item,
                   COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3) AS peso_liquido,
                   COALESCE(SUM(pesagem.peso_bruto_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3) AS peso_bruto,
                   item.material,
                   item.centro,
                   item.deposito,
                   item.unidade,
                   lote.numero_lote,
                   lote.data_fabricacao,
                   lote.data_vencimento,
                   pesagem.codigo_entrada_produto_lote
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento =
                   lancamento.codigo_entrada_produto_lancamento
              JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_item =
                   item.codigo_entrada_produto_item
              LEFT JOIN entrada_produto_lote lote
                ON lote.codigo_entrada_produto_lote =
                   pesagem.codigo_entrada_produto_lote
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
               -- Bloqueia reenvio/duplicacao: so itens nao confirmados/cancelados entram no payload 101.
               AND lancamento.status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')
               AND item.status_item IN ('FINALIZADO_LOCAL', 'ERRO_SAP')
             GROUP BY lancamento.numero_pedido, item.numero_item,
                      item.material, item.centro, item.deposito, item.unidade,
                      pesagem.codigo_entrada_produto_lote,
                      lote.numero_lote, lote.data_fabricacao, lote.data_vencimento
            HAVING COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0) > 0
             ORDER BY item.numero_item, lote.numero_lote NULLS LAST;
            """;

        List<EntradaProdutoItemEnvioSap> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new EntradaProdutoItemEnvioSap
            {
                NumeroPedido = leitor.GetString(0),
                NumeroItem = leitor.GetString(1),
                PesoLiquidoKg = leitor.GetDecimal(2),
                PesoBrutoKg = leitor.GetDecimal(3),
                Material = leitor.IsDBNull(4) ? null : leitor.GetString(4),
                Centro = leitor.IsDBNull(5) ? null : leitor.GetString(5),
                Deposito = leitor.IsDBNull(6) ? null : leitor.GetString(6),
                Unidade = leitor.IsDBNull(7) ? null : leitor.GetString(7),
                NumeroLote = leitor.IsDBNull(8) ? null : leitor.GetString(8),
                DataFabricacao = leitor.IsDBNull(9) ? null : leitor.GetDateTime(9),
                DataValidade = leitor.IsDBNull(10) ? null : leitor.GetDateTime(10),
                CodigoEntradaProdutoLote = leitor.IsDBNull(11) ? 0L : leitor.GetInt64(11)
            });
        }

        return itens;
    }

    /// <summary>
    /// Status atual do lancamento ativo (status_lancamento). Usado como defesa de reenvio antes de
    /// montar o documento de material. Retorna null quando o lancamento nao existe/ativo.
    /// </summary>
    public async Task<string?> ObterStatusLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT status_lancamento
              FROM entrada_produto_lancamento
             WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
               AND situacao_entrada_produto_lancamento = true
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno as string;
    }

    /// <summary>
    /// Reserva/claim ATOMICO do lancamento para envio SAP: transiciona FINALIZADO_LOCAL/ERRO_SAP ->
    /// ENVIADO_SAP em um unico UPDATE condicional. Retorna true se reservou (1 linha afetada); false
    /// se outro envio ja reservou ou o status nao permite (concorrencia/idempotencia). Nao cria
    /// documento de material — apenas marca a intencao de envio antes do POST.
    /// </summary>
    public async Task<bool> TentarReservarLancamentoParaEnvioSapAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        long? usuario = ObterCodigoUsuarioSessao();

        const string sql = """
            UPDATE entrada_produto_lancamento
               SET status_lancamento = 'ENVIADO_SAP',
                   entrada_produto_lancamento_atualizado_por = @usuario
             WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
               AND situacao_entrada_produto_lancamento = true
               AND status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP');
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        int afetadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        return afetadas == 1;
    }

    public async Task AtualizarStatusAposEnvioSapAsync(
        long codigoLancamento,
        IReadOnlyList<ResultadoItemEnvioSap> resultados,
        CenarioEnvioSapEntrada cenario,
        RastreabilidadeDocumentoMaterialSap? rastreabilidade = null,
        CancellationToken cancellationToken = default)
    {
        if (codigoLancamento <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codigoLancamento));
        }

        if (resultados.Count == 0)
        {
            throw new ArgumentException("Informe os resultados dos itens enviados.", nameof(resultados));
        }

        if (cenario is not CenarioEnvioSapEntrada.Enviado
            and not CenarioEnvioSapEntrada.Parcial
            and not CenarioEnvioSapEntrada.Falha)
        {
            throw new ArgumentException("Cenário de envio SAP inválido para atualização local.", nameof(cenario));
        }

        // Grava a rastreabilidade do documento material SOMENTE no sucesso (Enviado) e quando o SAP
        // devolveu o numero do documento. Vai na MESMA transacao que marca CONFIRMADO_SAP.
        bool gravarRastreio =
            cenario == CenarioEnvioSapEntrada.Enviado
            && !string.IsNullOrWhiteSpace(rastreabilidade?.Documento)
            && !string.IsNullOrWhiteSpace(rastreabilidade?.Exercicio);

        long? usuario = ObterCodigoUsuarioSessao();
        await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            for (int indice = 0; indice < resultados.Count; indice++)
            {
                ResultadoItemEnvioSap resultado = resultados[indice];

                // Item do documento material por ordem (best-effort), so quando ha sucesso/rastreio.
                string? documentoItem = gravarRastreio
                    && rastreabilidade!.ItensDocumento.Count > indice
                        ? rastreabilidade.ItensDocumento[indice]
                        : null;

                string sqlItem = gravarRastreio
                    ? """
                        UPDATE entrada_produto_item
                           SET status_item = @status_item,
                               documento_material_item = @documento_material_item,
                               entrada_produto_item_atualizado_por = @usuario
                         WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                           AND numero_item = @numero_item
                           AND situacao_entrada_produto_item = true;
                        """
                    : """
                        UPDATE entrada_produto_item
                           SET status_item = @status_item,
                               entrada_produto_item_atualizado_por = @usuario
                         WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                           AND numero_item = @numero_item
                           AND situacao_entrada_produto_item = true;
                        """;

                await using NpgsqlCommand comandoItem = new(sqlItem, conexao, transacao);
                comandoItem.Parameters.Add(ParametroTexto(
                    "@status_item",
                    resultado.Sucesso ? "CONFIRMADO_SAP" : "ERRO_SAP"));
                if (gravarRastreio)
                {
                    comandoItem.Parameters.Add(ParametroTextoNulo("@documento_material_item", documentoItem));
                }

                comandoItem.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
                comandoItem.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                comandoItem.Parameters.Add(ParametroTexto("@numero_item", resultado.NumeroItem));
                int itensAtualizados = await comandoItem.ExecuteNonQueryAsync(cancellationToken);
                if (itensAtualizados != 1)
                {
                    throw new InvalidOperationException(
                        $"Não foi possível atualizar o status local do item {resultado.NumeroItem}.");
                }
            }

            if (cenario is CenarioEnvioSapEntrada.Enviado or CenarioEnvioSapEntrada.Falha)
            {
                string sqlLancamento = gravarRastreio
                    ? """
                        UPDATE entrada_produto_lancamento
                           SET status_lancamento = 'CONFIRMADO_SAP',
                               documento_material_sap = @documento_material_sap,
                               exercicio_documento_material_sap = @exercicio_documento_material_sap,
                               enviado_sap_em = now(),
                               entrada_produto_lancamento_atualizado_por = @usuario
                         WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                           AND situacao_entrada_produto_lancamento = true;
                        """
                    : """
                        UPDATE entrada_produto_lancamento
                           SET status_lancamento = @status_lancamento,
                               entrada_produto_lancamento_atualizado_por = @usuario
                         WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                           AND situacao_entrada_produto_lancamento = true;
                        """;

                await using NpgsqlCommand comandoLancamento = new(sqlLancamento, conexao, transacao);
                if (gravarRastreio)
                {
                    comandoLancamento.Parameters.Add(
                        ParametroTextoNulo("@documento_material_sap", rastreabilidade!.Documento));
                    comandoLancamento.Parameters.Add(
                        ParametroTextoNulo("@exercicio_documento_material_sap", rastreabilidade.Exercicio));
                }
                else
                {
                    comandoLancamento.Parameters.Add(ParametroTexto(
                        "@status_lancamento",
                        cenario == CenarioEnvioSapEntrada.Enviado ? "CONFIRMADO_SAP" : "ERRO_SAP"));
                }

                comandoLancamento.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
                comandoLancamento.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                int lancamentosAtualizados =
                    await comandoLancamento.ExecuteNonQueryAsync(cancellationToken);
                if (lancamentosAtualizados != 1)
                {
                    throw new InvalidOperationException(
                        "Não foi possível atualizar o status local do lançamento.");
                }
            }

            return true;
        }, cancellationToken);
    }

    private static NpgsqlParameter ParametroTextoNulo(string nome, string? valor)
        => new(nome, NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim() };

    private static NpgsqlParameter ParametroNumericoNulo(string nome, decimal? valor)
        => new(nome, NpgsqlDbType.Numeric) { Value = valor.HasValue ? valor.Value : DBNull.Value };

    private static NpgsqlParameter ParametroUsuarioObrigatorio(string nome, long? usuario)
        => new(nome, NpgsqlDbType.Bigint)
        {
            Value = usuario ?? throw new InvalidOperationException("Usuario da sessao obrigatorio para gravar a entrada.")
        };
}



