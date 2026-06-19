namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Lancada quando uma sincronizacao de permissoes removeria as permissoes administrativas
/// essenciais do ULTIMO perfil que as mantem (protecao contra lockout administrativo).
/// Permite que a camada de servico trate o caso por TIPO, sem depender de comparar ex.Message.
/// </summary>
public sealed class RemocaoPermissaoAdministrativaEssencialException : Exception
{
    public RemocaoPermissaoAdministrativaEssencialException()
        : base(PerfilPermissaoRepositorio.MensagemProtecaoPerfilAdministrador)
    {
    }
}
