namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Lancada quando o arquivo configuracao.sap.json EXISTE mas esta malformado (erro de implantacao).
/// NAO deve ser tratada como "SAP nao configurado". A mensagem operacional e segura: nao contem o
/// caminho do arquivo nem o conteudo. O detalhe tecnico (parse) pode vir como InnerException, sem
/// expor o conteudo do arquivo.
/// </summary>
public sealed class ConfiguracaoSapInvalidaException : Exception
{
    public ConfiguracaoSapInvalidaException(Exception? innerException = null)
        : base(ConfiguracaoSap.MensagemConfiguracaoInvalida, innerException)
    {
    }
}
