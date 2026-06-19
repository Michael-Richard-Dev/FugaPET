using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.Seguranca;

public static class PermissaoUsuarioAtual
{
    public static bool PossuiPermissao(string modulo, string rotina, string acao)
        => AutorizacaoServico.PossuiPermissao(modulo, rotina, acao);

    public static bool PodeAcessarModulo(string modulo)
        => AutorizacaoServico.PodeAcessar(modulo);

    public static bool PodeGerenciarCadastro(string rotina)
        => AutorizacaoServico.PodeGerenciarCadastro(rotina);

    public static bool UsuarioLogado(out long codigoUsuario)
    {
        long? codigo = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        codigoUsuario = codigo.GetValueOrDefault();
        return codigo.HasValue;
    }

    public static string ObterLogin()
        => EstadoSessaoUsuarioAtual.SessaoAtual?.Login ?? string.Empty;

    public static bool BancoHabilitado()
        => EstadoIntegracaoBanco.Habilitado;
}
