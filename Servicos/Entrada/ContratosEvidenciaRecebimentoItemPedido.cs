using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// GATE R5: fonte LOCAL de evidencia de recebimento do mesmo pedido+item (PostgreSQL). Somente LEITURA.
/// Nunca lanca para o chamador: qualquer problema vira <see cref="CenarioRecebimentoLocal.Indisponivel"/>,
/// que BLOQUEIA a jusante.
/// </summary>
public interface IEvidenciaRecebimentoLocalServico
{
    Task<ResultadoRecebimentoLocal> ConsultarAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamentoAtual,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// GATE R5: fonte REMOTA de evidencia — historico de documentos de material do pedido+item no SAP
/// (API_MATERIAL_DOCUMENT_SRV / A_MaterialDocumentItem), SOMENTE LEITURA e PAGINADA.
/// <para>
/// A implementacao concreta deve: normalizar o item (00010 x 10), aplicar $filter por
/// PurchaseOrder + PurchaseOrderItem, consumir TODAS as paginas e devolver
/// <c>PaginacaoCompleta=false</c> se qualquer pagina falhar. <c>AbrangenciaComprovada</c> so pode ser
/// true quando o filtro usado comprovadamente cobre todo o historico relevante.
/// </para>
/// </summary>
public interface IHistoricoMaterialDocumentSapServico
{
    /// <summary>True quando ha contrato/credenciais/URL para consultar o historico neste ambiente.</summary>
    bool Disponivel { get; }

    Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
        string numeroPedido,
        string numeroItemPedido,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// GATE R5 / ETAPA B: exclusividade DURAVEL por pedido+item durante preparacao, POST e persistencia do
/// resultado. NAO e um SELECT seguido de POST: exige registro duravel da tentativa, de modo que uma
/// falha APOS o POST nao permita reenvio cego.
/// </summary>
public interface IExclusividadeItemPedidoServico
{
    /// <summary>True quando o mecanismo duravel existe neste ambiente.</summary>
    bool Disponivel { get; }

    Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamento,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// GATE R5: implementacao FAIL-CLOSED da fonte local, usada enquanto a consulta aprovada nao existir.
/// Devolve sempre Indisponivel — nunca "sem registro" por omissao.
/// </summary>
public sealed class EvidenciaRecebimentoLocalIndisponivelServico : IEvidenciaRecebimentoLocalServico
{
    public const string Motivo =
        "consulta local de recebimentos por pedido+item nao disponivel neste ambiente "
        + "(nenhuma fonte aprovada conectada)";

    public Task<ResultadoRecebimentoLocal> ConsultarAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamentoAtual,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoRecebimentoLocal.Indisponivel(Motivo));
}

/// <summary>
/// GATE R5: implementacao FAIL-CLOSED do historico SAP. Usada enquanto o contrato de leitura de
/// A_MaterialDocumentItem nao estiver CONFIRMADO (entidade, campos e filtro) e autorizado.
/// Nenhuma chamada HTTP e feita aqui.
/// </summary>
public sealed class HistoricoMaterialDocumentSapIndisponivelServico : IHistoricoMaterialDocumentSapServico
{
    public const string Motivo =
        "leitura do historico de documentos de material (A_MaterialDocumentItem) nao implementada/"
        + "autorizada: contrato de entidade, campos e filtro pendente de confirmacao";

    public bool Disponivel => false;

    public Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
        string numeroPedido,
        string numeroItemPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoHistoricoMaterialSap.Indisponivel(Motivo));
}

/// <summary>
/// GATE R5: implementacao FAIL-CLOSED da exclusividade duravel. Usada enquanto a estrutura duravel por
/// pedido+item nao for aprovada (ver delta minimo de DDL no retorno do gate). Devolve Indisponivel, o
/// que BLOQUEIA o envio — e NAO um "obtida" falso.
/// </summary>
public sealed class ExclusividadeItemPedidoIndisponivelServico : IExclusividadeItemPedidoServico
{
    public const string Motivo =
        "exclusividade duravel por pedido+item indisponivel: a reserva atual e por LANCAMENTO e nao "
        + "garante unicidade por item do pedido";

    public bool Disponivel => false;

    public Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoExclusividadeItemPedido.Indisponivel(Motivo));
}
