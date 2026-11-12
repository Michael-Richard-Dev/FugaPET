using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;
using Npgsql;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Serviço local do Resultado do Apontamento. Não chama SAP, balança, consumo ou impressão.
/// A definição inicial fica centralizada aqui até existir configuração persistida por Gaia Dados.
/// </summary>
public sealed class ResultadoApontamentoServico
{
    internal const string MensagemPersistenciaNaoConfigurada =
        "A estrutura de persistência do Resultado do Apontamento ainda não foi configurada. Solicite a aplicação do pacote Gaia Dados antes de gravar resultados.";
    internal const string MensagemNenhumResultadoAInformar =
        "Nenhum resultado a informar para esta operação.";
    // GATE 050 (§10): erro Postgres que NÃO é ausência de estrutura não pode ser mascarado como "não configurada".
    internal const string MensagemFalhaTecnicaPersistencia =
        "Falha técnica ao gravar o resultado do apontamento. Tente novamente; se persistir, acione o suporte.";

    private readonly IResultadoApontamentoRepositorio? _repositorio;

    public ResultadoApontamentoServico()
        : this(new ResultadoApontamentoRepositorio(new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())))
    {
    }

    internal ResultadoApontamentoServico(IResultadoApontamentoRepositorio? repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
        ContextoApontamentoProcesso contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.CodigoPerfilResultado is not long codigoPerfilResultado)
        {
            System.Diagnostics.Trace.TraceWarning("[ResultadoApontamento][PERFIL_NAO_RESOLVIDO] ListarDefinicoes");
            return Array.Empty<ResultadoApontamentoItem>();
        }

        if (_repositorio is null)
        {
            throw new InvalidOperationException(MensagemPersistenciaNaoConfigurada);
        }

        try
        {
            IReadOnlyList<ResultadoApontamentoItem> configurados =
                await _repositorio.ListarDefinicoesAsync(codigoPerfilResultado, cancellationToken);
            return configurados.OrderBy(item => item.OrdemExibicao).ToArray();
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento][DEPENDENCIA_BANCO] ListarDefinicoes: {ex.GetType().Name}");
            throw;
        }
    }

    public async Task<ResultadoOperacao> RegistrarAsync(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ResultadoApontamentoItem> itens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(itens);

        bool perfilResolvido = contexto.CodigoPerfilResultado is long;
        string? erro = ValidarItens(itens, perfilResolvido);
        if (erro is not null)
        {
            return ResultadoOperacao.Falha(erro);
        }

        if (_repositorio is null)
        {
            return ResultadoOperacao.Falha(MensagemPersistenciaNaoConfigurada);
        }

        RegistroResultadoApontamento registro = new()
        {
            CodigoApontamento = contexto.CodigoApontamento,
            NumeroOrdem = contexto.NumeroOrdem,
            Sequencia = contexto.Sequencia,
            Operacao = contexto.Operacao,
            Suboperacao = contexto.Suboperacao,
            Itens = itens.OrderBy(item => item.OrdemExibicao).ToArray(),
            Usuario = contexto.Usuario,
            Estacao = contexto.Estacao,
            RegistradoEm = DateTime.Now,
            MensagemResumo = itens.Count == 0
                ? MensagemNenhumResultadoAInformar
                : MontarMensagemSucesso(contexto)
        };

        try
        {
            long id = await _repositorio.InserirAsync(registro, cancellationToken);
            return ResultadoOperacao.Ok(registro.MensagemResumo, id);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            // GATE 050 (§10): só ausência REAL de estrutura vira "não configurada"; qualquer outro erro
            // Postgres é falha técnica sanitizada — a causa (tipo/SQLSTATE) fica no trace, sem segredo.
            if (EhEstruturaAusente(ex))
            {
                System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento][ESTRUTURA_AUSENTE] Registrar: {DiagnosticoSanitizado(ex)}");
                return ResultadoOperacao.Falha(MensagemPersistenciaNaoConfigurada);
            }

            System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento][FALHA_TECNICA_BANCO] Registrar: {DiagnosticoSanitizado(ex)}");
            return ResultadoOperacao.Falha(MensagemFalhaTecnicaPersistencia);
        }
    }

    public async Task<ResultadoApontamentoPersistidoRecovery> ObterResultadoPersistidoDoApontamentoAsync(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ResultadoApontamentoItem> definicoes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(definicoes);

        if (!string.Equals(contexto.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal))
        {
            return ResultadoApontamentoPersistidoRecovery.Falha(
                "Recovery local permitido somente para Resultado do Apontamento.");
        }

        if (contexto.CodigoPerfilResultado is not long)
        {
            return ResultadoApontamentoPersistidoRecovery.Falha(
                "Perfil de resultado não resolvido para recovery local.");
        }

        if (_repositorio is null)
        {
            return ResultadoApontamentoPersistidoRecovery.Falha(MensagemPersistenciaNaoConfigurada);
        }

        try
        {
            IReadOnlyList<ResultadoPersistidoApontamento> resultados =
                await _repositorio.ListarResultadosPersistidosDoApontamentoAsync(
                    contexto.CodigoApontamento,
                    cancellationToken);

            if (resultados.Count == 0)
            {
                return ResultadoApontamentoPersistidoRecovery.Ausente();
            }

            if (resultados.Count > 1)
            {
                return ResultadoApontamentoPersistidoRecovery.Falha(
                    "Mais de um resultado persistido para o apontamento; recovery local bloqueado.");
            }

            ResultadoPersistidoApontamento resultado = resultados[0];
            string? erro = ValidarResultadoPersistido(contexto, definicoes, resultado);
            return erro is null
                ? ResultadoApontamentoPersistidoRecovery.Recuperado(resultado)
                : ResultadoApontamentoPersistidoRecovery.Falha(erro);
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            if (EhEstruturaAusente(ex))
            {
                System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento][ESTRUTURA_AUSENTE] Recovery: {DiagnosticoSanitizado(ex)}");
                return ResultadoApontamentoPersistidoRecovery.Falha(MensagemPersistenciaNaoConfigurada);
            }

            System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento][FALHA_TECNICA_BANCO] Recovery: {DiagnosticoSanitizado(ex)}");
            return ResultadoApontamentoPersistidoRecovery.Falha(MensagemFalhaTecnicaPersistencia);
        }
    }

    internal static string? ValidarResultadoPersistido(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ResultadoApontamentoItem> definicoes,
        ResultadoPersistidoApontamento resultado)
    {
        if (resultado.CodigoResultado <= 0)
        {
            return "Resultado persistido inválido para recovery local.";
        }

        if (resultado.CodigoApontamento != contexto.CodigoApontamento)
        {
            return "Resultado persistido não pertence ao apontamento atual.";
        }

        if (definicoes.Count == 0)
        {
            return resultado.Itens.Count == 0
                ? null
                : "Resultado persistido contém itens sem definição ativa.";
        }

        IGrouping<long, ResultadoApontamentoItem>[] gruposDefinicoes = definicoes
            .GroupBy(item => item.CodigoDefinicao)
            .ToArray();

        if (gruposDefinicoes.Any(grupo => grupo.Key <= 0 || grupo.Count() != 1))
        {
            return "Definições de resultado ambíguas para recovery local.";
        }

        Dictionary<long, ResultadoApontamentoItem> definicoesPorCodigo = gruposDefinicoes
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Single());

        HashSet<long> vistos = [];
        foreach (ResultadoApontamentoItem item in resultado.Itens)
        {
            if (!vistos.Add(item.CodigoDefinicao))
            {
                return "Resultado persistido possui definição duplicada.";
            }

            if (!definicoesPorCodigo.TryGetValue(item.CodigoDefinicao, out ResultadoApontamentoItem? definicao))
            {
                return "Resultado persistido contém definição estranha ao perfil atual.";
            }

            if (!string.Equals(item.CodigoItem, definicao.CodigoItem, StringComparison.Ordinal)
                || !string.Equals(item.Tipo, definicao.Tipo, StringComparison.Ordinal)
                || !string.Equals(item.Medida, definicao.Medida, StringComparison.Ordinal)
                || item.Obrigatorio != definicao.Obrigatorio)
            {
                return "Resultado persistido diverge da definição atual do perfil.";
            }
        }

        foreach (ResultadoApontamentoItem obrigatorio in definicoes.Where(item => item.Obrigatorio))
        {
            if (!resultado.Itens.Any(item => item.CodigoDefinicao == obrigatorio.CodigoDefinicao && item.Resultado is not null))
            {
                return $"Resultado persistido sem valor obrigatório para {obrigatorio.Tipo} / {obrigatorio.Medida}.";
            }
        }

        return ValidarItens(resultado.Itens, perfilResolvido: true);
    }

    // GATE 050 (§10): ausência REAL de estrutura/capability = tabela/coluna/esquema inexistente no Postgres.
    // 42P01 undefined_table · 42703 undefined_column · 3F000 invalid_schema_name · 3D000 invalid_catalog_name.
    internal static bool EhEstruturaAusente(Exception ex)
        => ex is PostgresException pg
           && pg.SqlState is "42P01" or "42703" or "3F000" or "3D000";

    // Diagnóstico controlado: tipo + SQLSTATE (não secretos). NUNCA Message/ToString/connection string/segredo.
    internal static string DiagnosticoSanitizado(Exception ex)
        => ex is PostgresException pg
            ? $"PostgresException SQLSTATE={pg.SqlState}"
            : ex.GetType().Name;


    public string ObterIdentificacaoBanco()
    {
        try
        {
            ConfiguracaoBancoPostgreSql configuracao = LeitorConfiguracaoBancoPostgreSql.Carregar();
            return string.IsNullOrWhiteSpace(configuracao.NomeBanco) ? "-" : configuracao.NomeBanco.Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[ResultadoApontamento] Falha ao identificar banco: {ex.GetType().Name}");
            return "-";
        }
    }

    internal static string? ValidarItens(IReadOnlyList<ResultadoApontamentoItem> itens, bool perfilResolvido = false)
    {
        if (itens.Count == 0 && !perfilResolvido)
        {
            return "Não há itens de resultado configurados para esta operação.";
        }

        foreach (ResultadoApontamentoItem item in itens.OrderBy(i => i.OrdemExibicao))
        {
            if (item.Obrigatorio && item.Resultado is null)
            {
                return $"Informe o resultado para {item.Tipo} / {item.Medida}.";
            }

            if (item.Resultado < 0m)
            {
                return $"Resultado de {item.Tipo} / {item.Medida} não pode ser negativo.";
            }
        }

        return null;
    }

    internal static string MontarMensagemSucesso(ContextoApontamentoProcesso contexto)
        => $"Resultado da operação {contexto.Operacao} registrado com sucesso.";
}

public enum ResultadoApontamentoPersistidoRecoveryEstado
{
    Ausente,
    Recuperado,
    Falha
}

public sealed class ResultadoApontamentoPersistidoRecovery
{
    private ResultadoApontamentoPersistidoRecovery(
        ResultadoApontamentoPersistidoRecoveryEstado estado,
        ResultadoPersistidoApontamento? resultado,
        string mensagem)
    {
        Estado = estado;
        Resultado = resultado;
        Mensagem = mensagem;
    }

    public ResultadoApontamentoPersistidoRecoveryEstado Estado { get; }
    public ResultadoPersistidoApontamento? Resultado { get; }
    public string Mensagem { get; }

    public static ResultadoApontamentoPersistidoRecovery Ausente()
        => new(ResultadoApontamentoPersistidoRecoveryEstado.Ausente, null, string.Empty);

    public static ResultadoApontamentoPersistidoRecovery Recuperado(ResultadoPersistidoApontamento resultado)
        => new(ResultadoApontamentoPersistidoRecoveryEstado.Recuperado, resultado, "Resultado já registrado.");

    public static ResultadoApontamentoPersistidoRecovery Falha(string mensagem)
        => new(ResultadoApontamentoPersistidoRecoveryEstado.Falha, null, mensagem);
}
