namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// GATE R5 (P5 operacional): desfechos da avaliacao de evidencia de recebimento anterior de UM item do
/// Pedido de Compra. Somente <see cref="SemRecebimentoComprovado"/> autoriza lancar a quantidade
/// INTEGRAL do pedido; todos os demais BLOQUEIAM.
/// </summary>
public enum CenarioEvidenciaRecebimento
{
    /// <summary>Comprovado que NAO houve recebimento impeditivo. Unico cenario que libera.</summary>
    SemRecebimentoComprovado = 0,

    /// <summary>Recebimento anterior impeditivo (local confirmado ou 101 liquido no SAP).</summary>
    RecebimentoImpeditivo = 1,

    /// <summary>Existe tentativa local ainda pendente/reservada para o mesmo pedido e item.</summary>
    TentativaLocalPendente = 2,

    /// <summary>Estado remoto nao conclusivo (ex.: 102 sem 101 correspondente, 101 revertido).</summary>
    ResultadoRemotoIndeterminado = 3,

    /// <summary>Falha de consulta, fonte indisponivel ou paginacao nao concluida.</summary>
    FalhaConsulta = 4,

    /// <summary>Historico sem registros, porem com ABRANGENCIA nao comprovada (R4C/NOT_PROVEN).</summary>
    AbrangenciaNaoComprovada = 5,

    /// <summary>Exclusividade duravel por pedido+item indisponivel: sem ela nao se autoriza o envio.</summary>
    ExclusividadeIndisponivel = 6
}

/// <summary>
/// GATE R5: resultado da avaliacao P5. O tri-state exposto a jusante preserva a semantica do seam do R2:
/// <c>false</c> = sem recebimento (libera), <c>true</c> = ja recebido (bloqueia),
/// <c>null</c> = DESCONHECIDO (bloqueia).
/// </summary>
public sealed record ResultadoEvidenciaRecebimento(CenarioEvidenciaRecebimento Cenario, string Motivo)
{
    public bool Libera => Cenario == CenarioEvidenciaRecebimento.SemRecebimentoComprovado;

    /// <summary>
    /// Conversao para o tri-state consumido pelo controller. Apenas o cenario comprovado devolve
    /// <c>false</c>; o impeditivo devolve <c>true</c>; TODO o resto devolve <c>null</c> (bloqueia).
    /// </summary>
    public bool? ComoTriState() => Cenario switch
    {
        CenarioEvidenciaRecebimento.SemRecebimentoComprovado => false,
        CenarioEvidenciaRecebimento.RecebimentoImpeditivo => true,
        _ => null
    };

    public static ResultadoEvidenciaRecebimento Liberado(string motivo)
        => new(CenarioEvidenciaRecebimento.SemRecebimentoComprovado, motivo);

    public static ResultadoEvidenciaRecebimento Bloqueado(CenarioEvidenciaRecebimento cenario, string motivo)
        => cenario == CenarioEvidenciaRecebimento.SemRecebimentoComprovado
            ? throw new ArgumentOutOfRangeException(nameof(cenario), "Cenario de bloqueio nao pode liberar.")
            : new(cenario, motivo);
}

// ===========================================================================================
// Evidencia LOCAL (PostgreSQL)
// ===========================================================================================

/// <summary>GATE R5: desfechos da consulta local de recebimentos do mesmo pedido+item.</summary>
public enum CenarioRecebimentoLocal
{
    /// <summary>Consulta concluida e NENHUM lancamento confirmado/pendente para o pedido+item.</summary>
    SemRegistro = 0,

    /// <summary>Existe lancamento CONFIRMADO_SAP para o pedido+item.</summary>
    Confirmado = 1,

    /// <summary>Existe lancamento reservado/ENVIADO_SAP ou tentativa duravel em aberto.</summary>
    Pendente = 2,

    /// <summary>Fonte local indisponivel (banco desabilitado, erro, consulta nao executada).</summary>
    Indisponivel = 3
}

/// <summary>GATE R5: resultado da consulta local. Nunca presume ausencia quando indisponivel.</summary>
public sealed record ResultadoRecebimentoLocal(CenarioRecebimentoLocal Cenario, string Motivo)
{
    public static ResultadoRecebimentoLocal SemRegistro()
        => new(CenarioRecebimentoLocal.SemRegistro, string.Empty);

    public static ResultadoRecebimentoLocal Confirmado(string motivo)
        => new(CenarioRecebimentoLocal.Confirmado, motivo);

    public static ResultadoRecebimentoLocal Pendente(string motivo)
        => new(CenarioRecebimentoLocal.Pendente, motivo);

    public static ResultadoRecebimentoLocal Indisponivel(string motivo)
        => new(CenarioRecebimentoLocal.Indisponivel, motivo);
}

// ===========================================================================================
// Evidencia REMOTA (historico de documentos de material no SAP)
// ===========================================================================================

/// <summary>GATE R5: desfechos da leitura do historico 101/102 do pedido+item no SAP.</summary>
public enum CenarioHistoricoMaterialSap
{
    /// <summary>Leitura concluida (ver <see cref="ResultadoHistoricoMaterialSap.PaginacaoCompleta"/>).</summary>
    Lido = 0,

    /// <summary>Falha tecnica, HTTP, timeout, parsing ou paginacao interrompida.</summary>
    FalhaConsulta = 1,

    /// <summary>Contrato/servico de leitura indisponivel neste ambiente.</summary>
    Indisponivel = 2
}

/// <summary>
/// GATE R5: historico de movimentos do pedido+item.
/// <para>
/// <see cref="PaginacaoCompleta"/> e <see cref="AbrangenciaComprovada"/> sao DISTINTOS de proposito:
/// a primeira afirma que TODAS as paginas do filtro foram consumidas; a segunda afirma que o filtro
/// COBRE todo o historico relevante. Sem a segunda, ausencia de registros NAO vira P5=FALSE (regra 15).
/// </para>
/// </summary>
public sealed record ResultadoHistoricoMaterialSap(
    CenarioHistoricoMaterialSap Cenario,
    int Documentos101,
    int Movimentos102,
    bool PaginacaoCompleta,
    bool AbrangenciaComprovada,
    string Motivo,
    // GATE R6: true quando ha documento sem identidade/tipo, ou estorno 102 SEM vinculo documental
    // comprovado. Correspondencia de estorno NUNCA e presumida ⇒ incerteza bloqueia.
    bool TemDocumentoIncerto = false)
{
    public static ResultadoHistoricoMaterialSap Lido(
        int documentos101,
        int movimentos102,
        bool paginacaoCompleta,
        bool abrangenciaComprovada,
        bool temDocumentoIncerto = false)
        => new(CenarioHistoricoMaterialSap.Lido, documentos101, movimentos102,
            paginacaoCompleta, abrangenciaComprovada, string.Empty, temDocumentoIncerto);

    public static ResultadoHistoricoMaterialSap Falha(string motivo)
        => new(CenarioHistoricoMaterialSap.FalhaConsulta, 0, 0, false, false, motivo);

    public static ResultadoHistoricoMaterialSap Indisponivel(string motivo)
        => new(CenarioHistoricoMaterialSap.Indisponivel, 0, 0, false, false, motivo);
}

// ===========================================================================================
// Exclusividade durável por pedido+item (ETAPA B)
// ===========================================================================================

/// <summary>GATE R5: desfechos da tentativa de obter exclusividade DURAVEL por pedido+item.</summary>
public enum CenarioExclusividadeItemPedido
{
    /// <summary>Exclusividade obtida e registrada de forma duravel para este lancamento.</summary>
    Obtida = 0,

    /// <summary>Outro lancamento detem a exclusividade (envio concorrente do mesmo pedido+item).</summary>
    EmUsoPorOutroLancamento = 1,

    /// <summary>Mecanismo duravel ausente/indisponivel: sem ele NAO se autoriza o envio.</summary>
    Indisponivel = 2
}

/// <summary>GATE R5: resultado da exclusividade. Indisponivel BLOQUEIA (nunca libera por omissao).</summary>
public sealed record ResultadoExclusividadeItemPedido(CenarioExclusividadeItemPedido Cenario, string Motivo)
{
    public static ResultadoExclusividadeItemPedido Obtida()
        => new(CenarioExclusividadeItemPedido.Obtida, string.Empty);

    public static ResultadoExclusividadeItemPedido EmUso(string motivo)
        => new(CenarioExclusividadeItemPedido.EmUsoPorOutroLancamento, motivo);

    public static ResultadoExclusividadeItemPedido Indisponivel(string motivo)
        => new(CenarioExclusividadeItemPedido.Indisponivel, motivo);
}
