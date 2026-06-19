namespace FugaPET_Dev.Servicos.Auditoria;

/// <summary>
/// Fachada de alto nivel para registrar eventos em auditoria_acao_usuario.
/// Concentra os eventos relevantes do sistema (login, falhas, acesso negado, CRUD critico)
/// para que todos sigam o mesmo formato (acao, resultado, modulo, tela).
///
/// Os triggers do banco ja registram log_alteracao_cadastral automaticamente.
/// Este servico cobre apenas eventos de aplicacao (semantica de negocio).
/// </summary>
// Nao-sealed e com metodos virtuais para permitir spy/fake em testes (sem alterar assinaturas).
public class AuditoriaServico
{
    private const string ModuloSeguranca = "SEGURANCA";
    private const string TelaLogin = "LoginForm";
    private const string ModuloCadastro = "CADASTRO";

    private const string ResultadoSucesso = "SUCESSO";
    private const string ResultadoFalha = "FALHA";
    private const string ResultadoNegado = "NEGADO";
    private const string ResultadoErro = "ERRO";

    private readonly AuditoriaAcaoUsuarioServico _acaoUsuarioServico;

    public AuditoriaServico(AuditoriaAcaoUsuarioServico acaoUsuarioServico)
    {
        _acaoUsuarioServico = acaoUsuarioServico;
    }

    // ============================================================
    // SEGURANCA / LOGIN
    // ============================================================

    public virtual Task RegistrarLoginSucessoAsync(long codigoUsuario, string login, CancellationToken cancellationToken = default)
        => ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: "LOGIN",
            resultado: ResultadoSucesso,
            codigoUsuario: codigoUsuario,
            mensagem: $"Login realizado: {login}.",
            modulo: ModuloSeguranca,
            tela: TelaLogin,
            cancellationToken: cancellationToken));

    public virtual Task RegistrarLoginFalhaAsync(string loginTentado, long? codigoUsuario, string motivo, CancellationToken cancellationToken = default)
        => ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: "LOGIN",
            resultado: ResultadoFalha,
            codigoUsuario: codigoUsuario,
            mensagem: $"Falha de login para '{loginTentado}': {motivo}",
            modulo: ModuloSeguranca,
            tela: TelaLogin,
            cancellationToken: cancellationToken));

    public virtual Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default)
        => ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: "ACESSO_NEGADO",
            resultado: ResultadoNegado,
            codigoUsuario: codigoUsuario,
            mensagem: motivo,
            modulo: ModuloSeguranca,
            tela: tela,
            cancellationToken: cancellationToken));

    // ============================================================
    // CRUD CRITICO (cadastros sensiveis: usuario, perfil, permissao, etc.)
    // ============================================================

    public virtual Task RegistrarCadastroCriadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
        => RegistrarCrudAsync($"{entidade.ToUpperInvariant()}_CRIADO", codigoRegistro, descricao, tela, cancellationToken);

    public virtual Task RegistrarCadastroAtualizadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
        => RegistrarCrudAsync($"{entidade.ToUpperInvariant()}_ATUALIZADO", codigoRegistro, descricao, tela, cancellationToken);

    public virtual Task RegistrarCadastroExcluidoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
        => RegistrarCrudAsync($"{entidade.ToUpperInvariant()}_EXCLUIDO", codigoRegistro, descricao, tela, cancellationToken);

    public virtual Task RegistrarCadastroReativadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
        => RegistrarCrudAsync($"{entidade.ToUpperInvariant()}_REATIVADO", codigoRegistro, descricao, tela, cancellationToken);

    private Task RegistrarCrudAsync(string acao, long codigoRegistro, string? descricao, string? tela, CancellationToken cancellationToken)
    {
        long? operador = Seguranca.EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        string mensagem = $"Registro {codigoRegistro}" + (string.IsNullOrWhiteSpace(descricao) ? "." : $" - {descricao}.");

        return ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: acao,
            resultado: ResultadoSucesso,
            codigoUsuario: operador,
            mensagem: mensagem,
            modulo: ModuloCadastro,
            tela: tela,
            cancellationToken: cancellationToken));
    }

    // ============================================================
    // ERROS GERAIS
    // ============================================================

    public virtual Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default)
    {
        long? operador = Seguranca.EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        return ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: acao,
            resultado: ResultadoErro,
            codigoUsuario: operador,
            mensagem: mensagem,
            modulo: ModuloCadastro,
            tela: tela,
            cancellationToken: cancellationToken));
    }

    public virtual Task RegistrarBloqueioIntegracaoSapAsync(
        string acao,
        string mensagem,
        string? tela = null,
        CancellationToken cancellationToken = default)
    {
        long? operador = Seguranca.EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        return ExecutarSeguroAsync(() => _acaoUsuarioServico.RegistrarAsync(
            acao: acao,
            resultado: ResultadoNegado,
            codigoUsuario: operador,
            mensagem: mensagem,
            modulo: Seguranca.PermissoesSistema.Modulos.IntegracaoSap,
            tela: tela,
            cancellationToken: cancellationToken));
    }

    // Auditoria nunca pode quebrar o fluxo de negocio.
    private static async Task ExecutarSeguroAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch
        {
            // Suprimido de proposito.
        }
    }
}
