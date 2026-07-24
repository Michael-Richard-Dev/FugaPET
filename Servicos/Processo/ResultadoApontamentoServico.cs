using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Serviço local do Resultado do Apontamento. Não chama SAP, balança, consumo ou impressão.
/// A definição inicial fica centralizada aqui até existir configuração persistida por Gaia Dados.
/// </summary>
public sealed class ResultadoApontamentoServico
{
    internal const string MensagemPersistenciaNaoConfigurada =
        "A estrutura de persistência do Resultado do Apontamento ainda não foi configurada. Solicite a aplicação do pacote Gaia Dados antes de gravar resultados.";

    private readonly IResultadoApontamentoRepositorio? _repositorio;

    public ResultadoApontamentoServico()
        : this(null)
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

        if (_repositorio is not null)
        {
            IReadOnlyList<ResultadoApontamentoItem> configurados =
                await _repositorio.ListarDefinicoesAsync(contexto, cancellationToken);
            return configurados.OrderBy(item => item.OrdemExibicao).ToArray();
        }

        return CriarDefinicoesPadrao();
    }

    public async Task<ResultadoOperacao> RegistrarAsync(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ResultadoApontamentoItem> itens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(itens);

        string? erro = ValidarItens(itens);
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
            MensagemResumo = MontarMensagemSucesso(contexto)
        };

        long id = await _repositorio.InserirAsync(registro, cancellationToken);
        return ResultadoOperacao.Ok(registro.MensagemResumo, id);
    }


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
    internal static IReadOnlyList<ResultadoApontamentoItem> CriarDefinicoesPadrao()
        => new[]
        {
            new ResultadoApontamentoItem
            {
                CodigoItem = "TEMPO_MINUTO_PADRAO",
                Tipo = "TEMPO",
                Medida = "MINUTO",
                Referencia = 5m,
                OrdemExibicao = 1,
                Obrigatorio = true
            }
        };

    internal static string? ValidarItens(IReadOnlyList<ResultadoApontamentoItem> itens)
    {
        if (itens.Count == 0)
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
