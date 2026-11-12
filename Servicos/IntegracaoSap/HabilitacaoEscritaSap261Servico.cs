using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 101E — cerimônia FOCAL de HABILITAÇÃO da escrita SAP 261 (Consumo de Matéria-Prima), BOUND a um
/// <c>codigo_lancamento</c>: usuário autenticado + policy PRÓPRIA do consumo (ProcessoProducao/ControleApontamentos/
/// HABILITAR_ESCRITA_SAP) + confirmação humana explícita (na UI, antes de <see cref="HabilitarAsync"/>) + auditoria
/// durável FAIL-CLOSED → arma a capability 261 para AQUELE lançamento. Nunca registra segredo. NÃO reutiliza
/// <see cref="HabilitacaoEscritaSapServico"/> (101) nem AutorizacaoEntradaProdutoServico; NÃO cria permissão/schema.
/// </summary>
public sealed class HabilitacaoEscritaSap261Servico
{
    private const string Tela = "ProcessoConsumoMaterialForm";

    private readonly IRuntimeSapWriteCapability261Service _capability;
    private readonly Func<bool> _usuarioAutorizado;
    private readonly Func<string, string, string, CancellationToken, Task> _auditarDuravelAsync;
    private readonly Func<string, string, string, CancellationToken, Task> _auditarBestEffortAsync;

    public HabilitacaoEscritaSap261Servico()
        : this(RuntimeSapWriteCapability261.Instancia, usuarioAutorizado: null, auditarDuravelAsync: null, auditarBestEffortAsync: null)
    {
    }

    internal HabilitacaoEscritaSap261Servico(
        IRuntimeSapWriteCapability261Service capability,
        Func<bool>? usuarioAutorizado,
        Func<string, string, string, CancellationToken, Task>? auditarDuravelAsync,
        Func<string, string, string, CancellationToken, Task>? auditarBestEffortAsync)
    {
        _capability = capability ?? RuntimeSapWriteCapability261.Instancia;
        _usuarioAutorizado = usuarioAutorizado ?? PossuiPermissaoHabilitar;

        Lazy<AuditoriaServico> auditoria = new(CriarAuditoriaServico);
        _auditarDuravelAsync = auditarDuravelAsync
            ?? ((acao, resultado, mensagem, ct) =>
                auditoria.Value.RegistrarAutorizacaoEscritaSapDuravelAsync(acao, resultado, mensagem, Tela, ct));
        _auditarBestEffortAsync = auditarBestEffortAsync
            ?? ((acao, resultado, mensagem, ct) =>
                auditoria.Value.RegistrarEventoOperacionalAsync(acao, resultado, mensagem, Tela, ct));
    }

    public SnapshotCapabilitySap261 Estado => _capability.ObterEstado();

    public bool UsuarioAutenticado => EstadoSessaoUsuarioAtual.SessaoAtual is not null;

    /// <summary>
    /// Entrada operacional do envio 261 bound ao <paramref name="codigoLancamento"/>. Preserva o one-shot: um
    /// one-shot ANTERIOR já consumido (do MESMO ou de outro lançamento) é apenas limpo para permitir uma NOVA
    /// habilitação EXPLÍCITA (não é auto-rearm/auto-retry — exige nova confirmação humana). RECONCILIACAO_REQUERIDA
    /// permanece BLOQUEADA (acione o suporte). ARMADA para outro lançamento não é reaproveitada silenciosamente.
    /// </summary>
    public async Task<ResultadoOperacao> HabilitarParaEnvioAsync(long codigoLancamento, CancellationToken cancellationToken = default)
    {
        if (codigoLancamento <= 0)
        {
            return ResultadoOperacao.Falha("Lançamento inválido para habilitar a escrita SAP 261.");
        }

        SnapshotCapabilitySap261 snapshot = _capability.ObterEstado();
        if (snapshot.Estado == EstadoCapabilitySap261.ReconciliacaoRequerida)
        {
            return ResultadoOperacao.Falha(
                "Envio bloqueado: reconciliação SAP 261 pendente. Acione o suporte.");
        }

        if (snapshot.Estado == EstadoCapabilitySap261.Armada261 && snapshot.CodigoLancamento == codigoLancamento)
        {
            return ResultadoOperacao.Ok("Escrita SAP 261 já habilitada para este lançamento.");
        }

        if (snapshot.Estado is EstadoCapabilitySap261.Consumida261 or EstadoCapabilitySap261.Armada261)
        {
            // One-shot anterior (consumido) OU armado para OUTRO lançamento: limpa para uma NOVA habilitação explícita.
            _capability.Desabilitar();
        }

        return await HabilitarAsync(codigoLancamento, cancellationToken);
    }

    public async Task<ResultadoOperacao> HabilitarAsync(long codigoLancamento, CancellationToken cancellationToken = default)
    {
        if (codigoLancamento <= 0)
        {
            return ResultadoOperacao.Falha("Lançamento inválido para habilitar a escrita SAP 261.");
        }

        if (!UsuarioAutenticado)
        {
            return ResultadoOperacao.Falha("Sessão de usuário ausente para habilitar a escrita SAP 261.");
        }

        if (!_usuarioAutorizado())
        {
            await SuprimirAsync(() => _auditarBestEffortAsync(
                AcoesCapabilitySap.Denied,
                "NEGADO",
                $"Tentativa de habilitar escrita SAP 261 sem permissão HABILITAR_ESCRITA_SAP. scope={EscopoCapabilitySap261.Scope}; codigo_lancamento={codigoLancamento}.",
                cancellationToken));
            return ResultadoOperacao.Falha("Usuário sem permissão para habilitar a escrita SAP 261 (HABILITAR_ESCRITA_SAP).");
        }

        // Só arma se a auditoria durável (ENABLE_REQUESTED + ENABLED) persistir. Falha ⇒ NÃO arma.
        return await _capability.SolicitarHabilitacao261Async(
            codigoLancamento,
            autorizado: true,
            auditarHabilitacaoDuravelAsync: async token =>
            {
                await _auditarDuravelAsync(
                    AcoesCapabilitySap.EnableRequested,
                    "SUCESSO",
                    $"Solicitação de habilitação de escrita SAP 261 (ambiente Q, Consumo). scope={EscopoCapabilitySap261.Scope}; codigo_lancamento={codigoLancamento}.",
                    token);
                await _auditarDuravelAsync(
                    AcoesCapabilitySap.Enabled,
                    "SUCESSO",
                    $"Escrita SAP 261 habilitada em runtime (ambiente Q, Consumo, one-shot). scope={EscopoCapabilitySap261.Scope}; codigo_lancamento={codigoLancamento}.",
                    token);
            },
            cancellationToken);
    }

    // Policy PRÓPRIA do consumo/apontamentos — NÃO usa AutorizacaoEntradaProdutoServico (101/Entrada).
    private static bool PossuiPermissaoHabilitar()
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.ControleApontamentos,
            PermissoesSistema.Acoes.HabilitarEscritaSap);

    private static AuditoriaServico CriarAuditoriaServico()
    {
        FabricaConexaoPostgreSql fabricaConexao = new(LeitorConfiguracaoBancoPostgreSql.Carregar());
        AuditoriaAcaoUsuarioRepositorio repositorio = new(fabricaConexao);
        return new AuditoriaServico(new AuditoriaAcaoUsuarioServico(repositorio));
    }

    private static async Task SuprimirAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch
        {
            // best-effort: DENIED não pode mascarar o resultado.
        }
    }
}
