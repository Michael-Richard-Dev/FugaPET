namespace FugaPET_Dev.Modelo.Entrada;

public enum CenarioEnvioSapEntrada
{
    AmbienteNaoHomologacao,
    SemPermissao,
    SapNaoConfigurado,
    IntegracaoInativa,
    EscritaDesabilitada,
    LancamentoSemItens,
    Enviado,
    Parcial,
    Falha,
    FalhaPersistenciaLocal,

    /// <summary>URL do servico de Material Document nao configurada (bloqueio antes do POST).</summary>
    MaterialDocumentNaoConfigurado,

    /// <summary>Algum item elegivel esta com unidade diferente de KG (sem conversao implicita).</summary>
    UnidadeNaoSuportada,

    /// <summary>Falta material, centro, deposito, unidade ou item do pedido em algum item elegivel.</summary>
    DadosIncompletos,

    /// <summary>Lancamento ja CONFIRMADO_SAP: reenvio bloqueado (evita documento de material duplicado).</summary>
    LancamentoJaConfirmadoSap,

    /// <summary>Lancamento CANCELADO: envio bloqueado.</summary>
    LancamentoCancelado,

    /// <summary>Reserva/claim atomico nao obtido: outro envio ja reservou o lancamento (concorrencia).</summary>
    EnvioEmProcessamento
}

public sealed record ResultadoItemEnvioSap(
    string NumeroItem,
    bool Sucesso,
    string Mensagem);

/// <summary>
/// Rastreabilidade do documento de material SAP (movimento 101), para persistir na MESMA transacao
/// que marca o lancamento/itens como CONFIRMADO_SAP. <see cref="ItensDocumento"/> alinha por ordem
/// com os itens enviados (best-effort) para gravar <c>documento_material_item</c> quando houver.
/// </summary>
public sealed record RastreabilidadeDocumentoMaterialSap(
    string? Documento,
    string? Exercicio,
    IReadOnlyList<string> ItensDocumento);
