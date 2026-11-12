using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Seguranca;

public static class EstadoSessaoUsuarioAtual
{
    public static SessaoUsuarioAplicacao? SessaoAtual { get; private set; }

    public static void Definir(SessaoUsuarioAplicacao sessao)
    {
        // 12E-E-C / GATE 101E: toda troca de sessão (login) desarma as capabilities de escrita SAP 101 e 261
        // (in-memory, sem persistência). Impede que uma capability armada por um usuário sobreviva para outro no
        // MESMO processo. NÃO altera lançamentos ENVIANDO_SAP (o reset é só da autoridade em memória).
        RuntimeSapWriteCapability.Instancia.Desabilitar();
        RuntimeSapWriteCapability261.Instancia.Desabilitar();
        SessaoAtual = sessao;
    }

    public static void Limpar()
    {
        // 12E-E-C / GATE 101E: logout/fim de sessão desarma as capabilities de escrita SAP 101 e 261
        // (cross-user fail-closed). NÃO altera lançamentos ENVIANDO_SAP.
        RuntimeSapWriteCapability.Instancia.Desabilitar();
        RuntimeSapWriteCapability261.Instancia.Desabilitar();
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
