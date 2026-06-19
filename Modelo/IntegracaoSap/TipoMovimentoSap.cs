namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Natureza de um movimento de integracao com o SAP.
/// CONCEITUAL: usado pelo contrato interno (mock) enquanto a integracao real nao existe.
/// </summary>
public enum TipoMovimentoSap
{
    /// <summary>Apontamento enviado da aplicacao/balanca para o SAP.</summary>
    Envio,

    /// <summary>Confirmacao/retorno recebido do SAP.</summary>
    Retorno,

    /// <summary>Movimento que terminou em erro de integracao.</summary>
    Erro
}
