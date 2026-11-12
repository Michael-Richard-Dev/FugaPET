using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

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

    // ==========================================================================================
    // GATE 101J — RESOLUÇÃO READ-ONLY do consumo PERSISTIDO correspondente à ocorrência atual.
    // Sem POST, sem claim, sem save: apenas leitura (ConsultarLancamentosAsync + ObterDetalheCompletoAsync)
    // e classificação fail-closed. A autoridade do estado (PENDENTE/ENVIANDO) vem do banco → sobrevive a
    // restart/deploy, fechando o gap do 101I.
    // ==========================================================================================

    /// <summary>Movimento SAP de consumo elegível para recuperação/261 direto.</summary>
    public const string MovimentoConsumo261 = "261";

    /// <summary>
    /// Resolve, READ-ONLY, o estado persistido de consumo para a ocorrência descrita pelos
    /// <paramref name="componentes"/> já filtrados por operação/sequência do apontamento. Fail-closed:
    /// ENVIANDO_SAP correspondente ⇒ reconciliação (bloqueia novo consumo); >1 PENDENTE ⇒ ambíguo;
    /// exatamente 1 PENDENTE elegível ⇒ recupera esse PK; nada ⇒ fluxo novo. NUNCA cria/salva/envia.
    /// </summary>
    public async Task<ResultadoRecuperacaoConsumoContexto> ResolverRecuperacaoPendenteAsync(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ComponenteConsumoMaterial> componentes,
        CancellationToken cancellationToken = default)
    {
        if (contexto is null
            || componentes is null
            || componentes.Count == 0
            || string.IsNullOrWhiteSpace(contexto.NumeroOrdem)
            || !string.Equals(contexto.TipoProcesso, TipoProcessoOperacao.ConsumoMateriaPrima, StringComparison.Ordinal))
        {
            return ResultadoRecuperacaoConsumoContexto.Nenhum;
        }

        IConsumoMaterialRepositorio repositorio = _criarRepositorio();

        IReadOnlyList<ResumoConsumoMaterialLancamento> resumos =
            await repositorio.ConsultarLancamentosAsync(
                NormalizarFiltro(new ConsultaConsumoMaterialFiltro { NumeroOrdem = contexto.NumeroOrdem }),
                cancellationToken);

        var pendentesElegiveis = new List<long>();
        var enviandoCorrespondentes = new List<long>();

        foreach (ResumoConsumoMaterialLancamento resumo in resumos)
        {
            ConsumoMaterialLancamento? detalhe =
                await repositorio.ObterDetalheCompletoAsync(resumo.CodigoLancamento, cancellationToken);
            if (detalhe is null)
            {
                continue;
            }

            // Só entra na classificação o lançamento cujos itens casam 1:1 com os componentes da ocorrência.
            if (!ItensCasamComComponentes(detalhe.Itens, componentes))
            {
                continue;
            }

            if (string.Equals(detalhe.StatusLancamento, ConsumoMaterialLancamento.StatusEnviandoSap, StringComparison.Ordinal))
            {
                enviandoCorrespondentes.Add(detalhe.Codigo);
                continue;
            }

            if (string.Equals(detalhe.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(detalhe.DocumentoMaterialSap)
                && string.IsNullOrWhiteSpace(detalhe.ExercicioDocumentoMaterialSap))
            {
                pendentesElegiveis.Add(detalhe.Codigo);
            }

            // CONFIRMADO_SAP / CANCELADO_LOCAL / FALHA_SAP / PENDENTE com doc|ano / status desconhecido:
            // NÃO são candidatos para novo 261 (§9) e não são blocker de reconciliação.
        }

        // §10: ENVIANDO_SAP correspondente domina — reconciliação pendente, novo consumo bloqueado.
        if (enviandoCorrespondentes.Count > 0)
        {
            return ResultadoRecuperacaoConsumoContexto.EnviandoReconciliacao(enviandoCorrespondentes);
        }

        // §11: mais de um PENDENTE elegível para a mesma ocorrência → ambíguo, nenhuma autosseleção.
        if (pendentesElegiveis.Count > 1)
        {
            return ResultadoRecuperacaoConsumoContexto.AmbiguoPendente(pendentesElegiveis);
        }

        if (pendentesElegiveis.Count == 1)
        {
            return ResultadoRecuperacaoConsumoContexto.UmPendente(pendentesElegiveis[0]);
        }

        return ResultadoRecuperacaoConsumoContexto.Nenhum;
    }

    /// <summary>
    /// MATCH item-a-item PURO e fail-closed: cada item persistido corresponde inequivocamente (bijeção) a um
    /// componente distinto da OCORRÊNCIA pelos campos de identidade da RESERVA (material, reserva, item_reserva,
    /// depósito) com movimento 261. GATE 102B: o LOTE (batch) é um atributo do CONSUMO — escolhido só na pesagem
    /// — e NÃO existe no componente de reserva pré-consumo; portanto o lote é comparado SOMENTE quando o
    /// componente da ocorrência efetivamente carrega um lote (divergência real de batch continua rejeitada).
    /// Identidade de reserva ausente, movimento ≠ 261, item sem par, ambiguidade (um item casa com &gt;1
    /// componente ou dois itens disputam o mesmo componente) ⇒ false. Sem I/O.
    /// </summary>
    public static bool ItensCasamComComponentes(
        IReadOnlyList<ConsumoMaterialItem> itens,
        IReadOnlyList<ComponenteConsumoMaterial> componentes)
    {
        if (itens is null || componentes is null || itens.Count == 0 || componentes.Count == 0)
        {
            return false;
        }

        // Toda identidade obrigatória precisa estar presente dos DOIS lados (fail-closed).
        foreach (ConsumoMaterialItem item in itens)
        {
            if (IdentidadeItemIncompleta(item)
                || !string.Equals(NormalizarCampo(item.TipoMovimentoSap), MovimentoConsumo261, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (ComponenteConsumoMaterial componente in componentes)
        {
            if (IdentidadeComponenteIncompleta(componente))
            {
                return false;
            }
        }

        var componenteConsumido = new bool[componentes.Count];

        foreach (ConsumoMaterialItem item in itens)
        {
            int indiceCasado = -1;
            for (int j = 0; j < componentes.Count; j++)
            {
                if (!IdentidadesIguais(item, componentes[j]))
                {
                    continue;
                }

                if (indiceCasado != -1)
                {
                    // item casa com mais de um componente → ambíguo.
                    return false;
                }

                indiceCasado = j;
            }

            if (indiceCasado == -1)
            {
                // item sem componente correspondente (ex.: pertence a outra operação).
                return false;
            }

            if (componenteConsumido[indiceCasado])
            {
                // dois itens disputam o mesmo componente → ambíguo.
                return false;
            }

            componenteConsumido[indiceCasado] = true;
        }

        return true;
    }

    private static bool IdentidadesIguais(ConsumoMaterialItem item, ComponenteConsumoMaterial componente)
        => string.Equals(NormalizarCampo(item.CodigoMaterial), NormalizarCampo(componente.CodigoMaterial), StringComparison.Ordinal)
        && string.Equals(NormalizarCampo(item.NumeroReserva), NormalizarCampo(componente.NumeroReserva), StringComparison.Ordinal)
        && string.Equals(NormalizarCampo(item.ItemReserva), NormalizarCampo(componente.ItemReserva), StringComparison.Ordinal)
        && string.Equals(NormalizarCampo(item.DepositoConsumo), NormalizarCampo(componente.DepositoConsumo), StringComparison.Ordinal)
        && LoteCompativel(item, componente);

    // GATE 102B: o lote só discrimina quando o COMPONENTE da ocorrência tem batch (raro pré-consumo). Quando o
    // componente não tem lote (reserva antes da pesagem), o lote do item NÃO é usado para rejeitar — a identidade
    // da RESERVA (material/reserva/item/depósito) já vincula a ocorrência. Batch divergente comprovado ⇒ rejeita.
    private static bool LoteCompativel(ConsumoMaterialItem item, ComponenteConsumoMaterial componente)
    {
        string loteComponente = NormalizarCampo(componente.Lote);
        if (loteComponente.Length == 0)
        {
            return true;
        }

        return string.Equals(NormalizarCampo(item.Lote), loteComponente, StringComparison.Ordinal);
    }

    private static bool IdentidadeItemIncompleta(ConsumoMaterialItem item)
        => string.IsNullOrWhiteSpace(item.CodigoMaterial)
        || string.IsNullOrWhiteSpace(item.NumeroReserva)
        || string.IsNullOrWhiteSpace(item.ItemReserva)
        || string.IsNullOrWhiteSpace(item.DepositoConsumo);

    private static bool IdentidadeComponenteIncompleta(ComponenteConsumoMaterial componente)
        => string.IsNullOrWhiteSpace(componente.CodigoMaterial)
        || string.IsNullOrWhiteSpace(componente.NumeroReserva)
        || string.IsNullOrWhiteSpace(componente.ItemReserva)
        || string.IsNullOrWhiteSpace(componente.DepositoConsumo);

    private static string NormalizarCampo(string? valor) => valor?.Trim() ?? string.Empty;
}
