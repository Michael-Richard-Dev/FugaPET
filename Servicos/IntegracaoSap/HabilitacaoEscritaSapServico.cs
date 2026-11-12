using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Coordena a cerimônia de HABILITAÇÃO da escrita SAP 101 (12E-E-B):
/// usuário autenticado + permissão HABILITAR_ESCRITA_SAP + auditoria durável FAIL-CLOSED → arma a capability.
/// A confirmação explícita ocorre na UI (antes de chamar <see cref="HabilitarAsync"/>).
/// Nunca registra segredo. Escopo ÚNICO: Material Document 101.
/// </summary>
public sealed class HabilitacaoEscritaSapServico
{
    private const string TelaPadrao = "ProcessoEntradaProdutoForm";

    private readonly IRuntimeSapWriteCapabilityService _capability;
    private readonly Func<bool> _usuarioAutorizado;
    // (acao, resultado, mensagem, ct) — durável (propaga exceção) e best-effort (suprime).
    private readonly Func<string, string, string, CancellationToken, Task> _auditarDuravelAsync;
    private readonly Func<string, string, string, CancellationToken, Task> _auditarBestEffortAsync;
    // GATE 104C: rótulo da tela para a auditoria. Default preserva EXATAMENTE o comportamento da Entrada;
    // outras telas (ex.: Semi-Acabado) informam o próprio contexto sem alterar a policy HABILITAR_ESCRITA_SAP.
    private readonly string _tela;

    public HabilitacaoEscritaSapServico(string tela = TelaPadrao)
        : this(RuntimeSapWriteCapability.Instancia, usuarioAutorizado: null, auditarDuravelAsync: null, auditarBestEffortAsync: null, tela: tela)
    {
    }

    internal HabilitacaoEscritaSapServico(
        IRuntimeSapWriteCapabilityService capability,
        Func<bool>? usuarioAutorizado,
        Func<string, string, string, CancellationToken, Task>? auditarDuravelAsync,
        Func<string, string, string, CancellationToken, Task>? auditarBestEffortAsync,
        string tela = TelaPadrao)
    {
        _capability = capability ?? RuntimeSapWriteCapability.Instancia;
        _usuarioAutorizado = usuarioAutorizado ?? PossuiPermissaoHabilitar;
        _tela = string.IsNullOrWhiteSpace(tela) ? TelaPadrao : tela;

        Lazy<AuditoriaServico> auditoria = new(CriarAuditoriaServico);
        _auditarDuravelAsync = auditarDuravelAsync
            ?? ((acao, resultado, mensagem, ct) =>
                auditoria.Value.RegistrarAutorizacaoEscritaSapDuravelAsync(acao, resultado, mensagem, _tela, ct));
        _auditarBestEffortAsync = auditarBestEffortAsync
            ?? ((acao, resultado, mensagem, ct) =>
                auditoria.Value.RegistrarEventoOperacionalAsync(acao, resultado, mensagem, _tela, ct));
    }

    public SnapshotCapabilitySap Estado => _capability.ObterEstado();

    public bool UsuarioAutenticado => EstadoSessaoUsuarioAtual.SessaoAtual is not null;

    /// <summary>
    /// Entrada OPERACIONAL do envio (12G-B): habilita a escrita 101 como detalhe interno do botão original
    /// de envio. Preserva o one-shot certificado (o writer consome ARMADA→CONSUMIDA). Um one-shot ANTERIOR já
    /// consumido é apenas limpo (CONSUMIDA→DESABILITADA) para permitir um NOVO envio EXPLÍCITO — isto NÃO é
    /// auto-rearm/auto-retry (exige nova confirmação humana). RECONCILIACAO_REQUERIDA permanece BLOQUEADA
    /// (acione o suporte). A cerimônia de armar continua exigindo permissão + auditoria durável fail-closed.
    /// </summary>
    public async Task<ResultadoOperacao> HabilitarParaEnvioAsync(CancellationToken cancellationToken = default)
    {
        EstadoCapabilitySap estado = _capability.ObterEstado().Estado;
        if (estado == EstadoCapabilitySap.ReconciliacaoRequerida)
        {
            return ResultadoOperacao.Falha(
                "Envio bloqueado: reconciliação SAP pendente para esta capability. Acione o suporte.");
        }

        if (estado == EstadoCapabilitySap.Armada101)
        {
            return ResultadoOperacao.Ok("Escrita SAP 101 já habilitada.");
        }

        if (estado == EstadoCapabilitySap.Consumida)
        {
            // One-shot anterior consumido: limpa para permitir uma NOVA habilitação explícita deste envio.
            _capability.Desabilitar();
        }

        return await HabilitarAsync(cancellationToken);
    }

    public async Task<ResultadoOperacao> HabilitarAsync(CancellationToken cancellationToken = default)
    {
        if (!UsuarioAutenticado)
        {
            return ResultadoOperacao.Falha("Sessão de usuário ausente para habilitar a escrita SAP.");
        }

        if (!_usuarioAutorizado())
        {
            // DENIED é best-effort (não bloqueia o retorno de falha).
            await SuprimirAsync(() => _auditarBestEffortAsync(
                AcoesCapabilitySap.Denied,
                "NEGADO",
                "Tentativa de habilitar escrita SAP 101 sem permissão HABILITAR_ESCRITA_SAP.",
                cancellationToken));
            return ResultadoOperacao.Falha("Usuário sem permissão para habilitar a escrita SAP (HABILITAR_ESCRITA_SAP).");
        }

        // Só arma se a auditoria durável (ENABLE_REQUESTED + ENABLED) persistir. Falha ⇒ NÃO arma.
        return await _capability.SolicitarHabilitacao101Async(
            autorizado: true,
            auditarHabilitacaoDuravelAsync: async token =>
            {
                await _auditarDuravelAsync(
                    AcoesCapabilitySap.EnableRequested,
                    "SUCESSO",
                    "Solicitação de habilitação de escrita SAP 101 (ambiente Q, Material Document).",
                    token);
                await _auditarDuravelAsync(
                    AcoesCapabilitySap.Enabled,
                    "SUCESSO",
                    "Escrita SAP 101 habilitada em runtime (ambiente Q, Material Document, one-shot).",
                    token);
            },
            cancellationToken);
    }

    private static bool PossuiPermissaoHabilitar()
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.EntradaProduto,
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
