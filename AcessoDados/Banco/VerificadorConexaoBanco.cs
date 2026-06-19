namespace FugaPET_Dev.AcessoDados.Banco;

public static class VerificadorConexaoBanco
{
    public static ResultadoConexaoBanco Verificar()
    {
        ContextoBancoOperacao contexto = new();
        return Verificar(contexto);
    }

    public static ResultadoConexaoBanco Verificar(ContextoBancoOperacao contexto)
    {
        if (!contexto.Configuracao.Habilitado)
        {
            return ResultadoConexaoBanco.Desabilitado(contexto.Configuracao);
        }

        try
        {
            using var conexao = contexto.FabricaConexao.CriarConexao();
            conexao.Open();
            return ResultadoConexaoBanco.Sucesso(contexto.Configuracao);
        }
        catch (Exception ex)
        {
            return ResultadoConexaoBanco.Falha(
                contexto.Configuracao,
                ErroBancoTratado.ObterMensagemAmigavel(ex));
        }
    }

    public static bool ConexaoDisponivel(out string mensagem)
    {
        ResultadoConexaoBanco resultado = Verificar();
        mensagem = resultado.Conectado ? string.Empty : resultado.Mensagem;
        return resultado.Conectado;
    }
}
