namespace FugaPET_Dev.AcessoDados.Banco;

/// <summary>
/// Diagnóstico do RUNTIME de banco da aplicação (044 §6). A superfície de privilégios do Produto Acabado
/// (INSERT por coluna, funções, sem DML no log) foi desenhada para a role <c>fugapet_dev_app</c>. Se a
/// configuração efetiva usar <c>postgres</c> (owner/superuser), o runtime é considerado NÃO APROVADO —
/// reportado como diagnóstico. NUNCA se resolve com <c>SET ROLE</c> em código produtivo (isso permanece
/// exclusivo dos testes funcionais). Este tipo apenas OBSERVA e reporta; não altera role/senha/grant.
/// </summary>
public static class DiagnosticoRuntimeBanco
{
    public const string RoleAplicacaoEsperada = "fugapet_dev_app";

    /// <summary>Usuário efetivo da configuração runtime da aplicação.</summary>
    public static string UsuarioEfetivo(ConfiguracaoBancoPostgreSql configuracao)
        => (configuracao?.Usuario ?? string.Empty).Trim();

    /// <summary>
    /// True somente quando o runtime usa a role da aplicação (<see cref="RoleAplicacaoEsperada"/>) e NÃO
    /// o owner/superuser <c>postgres</c>. Falso ⇒ RUNTIME_DB_ROLE_INCOMPATIVEL=fugapet_dev_app_nao_configurado.
    /// </summary>
    public static bool RuntimeAplicacaoAprovado(ConfiguracaoBancoPostgreSql configuracao)
    {
        string usuario = UsuarioEfetivo(configuracao);
        return !string.Equals(usuario, "postgres", StringComparison.OrdinalIgnoreCase)
            && string.Equals(usuario, RoleAplicacaoEsperada, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Classificação textual para relatório/telemetria (nunca contém segredo).</summary>
    public static string Classificar(ConfiguracaoBancoPostgreSql configuracao)
        => RuntimeAplicacaoAprovado(configuracao)
            ? "RUNTIME_FUGAPET_DEV_APP_VALIDADO"
            : $"RUNTIME_DB_ROLE_INCOMPATIVEL=fugapet_dev_app_nao_configurado (usuario_efetivo={UsuarioEfetivo(configuracao)})";

    /// <summary>Emite um aviso de diagnóstico (Trace) quando o runtime não usa a role da aplicação. Não bloqueia.</summary>
    public static void AvisarSeRuntimeNaoAprovado(ConfiguracaoBancoPostgreSql configuracao)
    {
        if (!RuntimeAplicacaoAprovado(configuracao))
        {
            System.Diagnostics.Trace.TraceWarning($"[Banco][044] {Classificar(configuracao)}");
        }
    }
}
