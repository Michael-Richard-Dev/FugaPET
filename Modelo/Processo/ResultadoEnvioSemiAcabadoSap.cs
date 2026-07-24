namespace FugaPET_Dev.Modelo.Processo;

public sealed class ResultadoEnvioSemiAcabadoSap
{
    private ResultadoEnvioSemiAcabadoSap(
        bool sucesso,
        bool estruturaPendente,
        bool envioDuplicadoBloqueado,
        bool divergenciaSap,
        bool inconsistenciaLocal,
        string mensagem,
        string? materialDocument,
        string? materialDocumentYear,
        long? codigoLancamento)
    {
        Sucesso = sucesso;
        EstruturaPendente = estruturaPendente;
        EnvioDuplicadoBloqueado = envioDuplicadoBloqueado;
        DivergenciaSap = divergenciaSap;
        InconsistenciaLocal = inconsistenciaLocal;
        Mensagem = mensagem;
        MaterialDocument = materialDocument;
        MaterialDocumentYear = materialDocumentYear;
        CodigoLancamento = codigoLancamento;
    }

    public bool Sucesso { get; }
    public bool EstruturaPendente { get; }
    public bool EnvioDuplicadoBloqueado { get; }

    /// <summary>Resultado indeterminado (2xx sem documento OU rede interrompida durante o POST): bloqueado, NÃƒO reenviar.</summary>
    public bool DivergenciaSap { get; }

    /// <summary>Dados locais legados incompatíveis com a versão atual: não reenviar nem alterar.</summary>
    public bool InconsistenciaLocal { get; }

    public string Mensagem { get; }
    public string? MaterialDocument { get; }
    public string? MaterialDocumentYear { get; }

    /// <summary>
    /// CÃ³digo do lanÃ§amento persistido. Preenchido em TODOS os resultados produzidos DEPOIS do salvamento
    /// local (Confirmado, Falha SAP, DivergÃªncia, Bloqueio de claim), para que a tela reutilize a MESMA
    /// linha no reenvio (nÃ£o insere novo cabeÃ§alho nem reinsere pesagens). Null antes da persistÃªncia
    /// (EstruturaNaoAplicada e validaÃ§Ãµes de preview/requisiÃ§Ã£o).
    /// </summary>
    public long? CodigoLancamento { get; }

    public static ResultadoEnvioSemiAcabadoSap Confirmado(string materialDocument, string materialDocumentYear, long? codigoLancamento = null)
        => new(true, false, false, false, false, $"SAP 101 confirmado. Documento {materialDocument}/{materialDocumentYear}.", materialDocument, materialDocumentYear, codigoLancamento);

    public static ResultadoEnvioSemiAcabadoSap EstruturaNaoAplicada()
        => new(false, true, false, false, false, "Estrutura local do Produto Semi-Acabado nÃ£o encontrada. Solicite aplicaÃ§Ã£o do pacote Gaia 037 antes de enviar ao SAP.", null, null, null);

    public static ResultadoEnvioSemiAcabadoSap BloqueadoDuplicidade(string mensagem, long? codigoLancamento = null)
        => new(false, false, true, false, false, mensagem, null, null, codigoLancamento);

    public static ResultadoEnvioSemiAcabadoSap Divergencia(string mensagem, long? codigoLancamento = null)
        => new(false, false, false, true, false, mensagem, null, null, codigoLancamento);

    public static ResultadoEnvioSemiAcabadoSap FalhaInconsistenciaLocal(string mensagem, long? codigoLancamento = null)
        => new(false, false, false, false, true, mensagem, null, null, codigoLancamento);

    public static ResultadoEnvioSemiAcabadoSap Falha(string mensagem, long? codigoLancamento = null)
        => new(false, false, false, false, false, mensagem, null, null, codigoLancamento);
}


