namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Estado de um registro/operacao de integracao SAP.
/// CONCEITUAL: contrato interno (mock) ate a integracao real existir.
/// </summary>
public enum SituacaoIntegracaoSap
{
    /// <summary>Aguardando envio ao SAP.</summary>
    Pendente,

    /// <summary>Enviado ao SAP, aguardando confirmacao.</summary>
    Enviado,

    /// <summary>Confirmado pelo SAP (retorno recebido).</summary>
    Confirmado,

    /// <summary>Concluido com sucesso.</summary>
    Sucesso,

    /// <summary>Falhou na integracao.</summary>
    Erro
}
