using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class ConsumoMaterialConsultaServico
{
    public const int LimitePadrao = 200;
    public const int LimiteMaximo = 1000;
    public const string MensagemStatusInvalido = "Status de consumo inválido para consulta.";
    public const string MensagemPreviewSomentePendente = "Preview SAP 261 disponível apenas para lançamentos pendentes.";

    private static readonly HashSet<string> StatusConhecidos = new(StringComparer.Ordinal)
    {
        "PENDENTE_SAP",
        "ENVIANDO_SAP",
        "FALHA_SAP",
        "CONFIRMADO_SAP",
        "CANCELADO_LOCAL"
    };

    private readonly Func<IConsumoMaterialRepositorio> _criarRepositorio;
    private readonly ConsumoMaterialServico _consumoMaterialServico;

    public ConsumoMaterialConsultaServico()
        : this(
            () => new ConsumoMaterialRepositorio(
                new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())),
            new ConsumoMaterialServico())
    {
    }

    internal ConsumoMaterialConsultaServico(
        Func<IConsumoMaterialRepositorio> criarRepositorio,
        ConsumoMaterialServico consumoMaterialServico)
    {
        _criarRepositorio = criarRepositorio ?? throw new ArgumentNullException(nameof(criarRepositorio));
        _consumoMaterialServico = consumoMaterialServico ?? throw new ArgumentNullException(nameof(consumoMaterialServico));
    }

    public static ConsultaConsumoMaterialFiltro NormalizarFiltro(ConsultaConsumoMaterialFiltro? filtro)
    {
        filtro ??= new ConsultaConsumoMaterialFiltro();
        string? status = string.IsNullOrWhiteSpace(filtro.StatusLancamento) || filtro.StatusLancamento == "Todos"
            ? null
            : filtro.StatusLancamento.Trim();

        if (status is not null && !StatusConhecidos.Contains(status))
        {
            throw new ArgumentException(MensagemStatusInvalido, nameof(filtro));
        }

        int limite = filtro.Limite <= 0 ? LimitePadrao : Math.Min(filtro.Limite, LimiteMaximo);

        return new ConsultaConsumoMaterialFiltro
        {
            NumeroOrdem = string.IsNullOrWhiteSpace(filtro.NumeroOrdem) ? null : filtro.NumeroOrdem.Trim(),
            StatusLancamento = status,
            CriadoDeUtc = NormalizarUtc(filtro.CriadoDeUtc),
            CriadoAteUtc = NormalizarUtc(filtro.CriadoAteUtc),
            Limite = limite
        };
    }

    public async Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
        ConsultaConsumoMaterialFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        ConsultaConsumoMaterialFiltro filtroNormalizado = NormalizarFiltro(filtro);
        return await _criarRepositorio().ConsultarLancamentosAsync(filtroNormalizado, cancellationToken);
    }

    public async Task<DetalheConsumoMaterialLancamento?> ObterDetalheCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento =
            await _criarRepositorio().ObterDetalheCompletoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return null;
        }

        IReadOnlyList<ConsumoMaterialPesagem> pesagens = lancamento.Itens
            .SelectMany(item => item.Pesagens)
            .ToList();

        return new DetalheConsumoMaterialLancamento
        {
            Lancamento = lancamento,
            Itens = lancamento.Itens,
            Pesagens = pesagens
        };
    }

    public async Task<ResultadoPreviewConsumoSap261> GerarPreviewSap261Async(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento =
            await _criarRepositorio().ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return ResultadoPreviewConsumoSap261.Falha(ConsumoMaterialServico.MensagemConsumoNaoEncontrado);
        }

        if (!string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal))
        {
            return ResultadoPreviewConsumoSap261.Falha(MensagemPreviewSomentePendente);
        }

        return _consumoMaterialServico.GerarPreviewSap261(lancamento, DateTime.UtcNow);
    }

    private static DateTime? NormalizarUtc(DateTime? valor)
        => valor?.Kind switch
        {
            null => null,
            DateTimeKind.Utc => valor.Value,
            DateTimeKind.Local => valor.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(valor.Value, DateTimeKind.Utc)
        };
}
