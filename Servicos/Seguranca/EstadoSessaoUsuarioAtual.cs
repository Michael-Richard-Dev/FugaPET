namespace FugaPET_Dev.Servicos.Seguranca;

public static class EstadoSessaoUsuarioAtual
{
    public static SessaoUsuarioAplicacao? SessaoAtual { get; private set; }

    public static void Definir(SessaoUsuarioAplicacao sessao)
    {
        SessaoAtual = sessao;
    }

    public static void Limpar()
    {
        SessaoAtual = null;
    }

    public static void MarcarSenhaAlterada()
    {
        if (SessaoAtual is null)
        {
            return;
        }

        SessaoAtual = new SessaoUsuarioAplicacao
        {
            IdUsuario = SessaoAtual.IdUsuario,
            Login = SessaoAtual.Login,
            Nome = SessaoAtual.Nome,
            IdSetorPadrao = SessaoAtual.IdSetorPadrao,
            PerfisCodigo = SessaoAtual.PerfisCodigo,
            Permissoes = SessaoAtual.Permissoes,
            IntegracaoBancoHabilitada = SessaoAtual.IntegracaoBancoHabilitada,
            DeveTrocarSenha = false
        };
    }
}
