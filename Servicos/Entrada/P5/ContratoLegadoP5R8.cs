using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal enum OperacaoLegadaP5R8 { Reservar, CompensarPreHttp, ConfirmarResultado }

internal sealed class PedidoLegadoP5R8(OperacaoLegadaP5R8 operacao, ClaimP5 claim, long codigoUsuario,
    string? statusAnterior = null, DocumentoP5? documento = null)
{
    internal OperacaoLegadaP5R8 Operacao { get; } = operacao;
    internal ClaimP5 Claim { get; } = claim;
    internal long CodigoUsuario { get; } = codigoUsuario;
    internal string? StatusAnterior { get; } = statusAnterior;
    internal DocumentoP5? Documento { get; } = documento;
    internal bool Valido => Claim.Valido && Claim.VersaoGuarda > 0 && Claim.GeracaoRecovery >= 0 && CodigoUsuario > 0
        && (Operacao == OperacaoLegadaP5R8.ConfirmarResultado
            ? Documento?.Valido == true : StatusAnterior is "FINALIZADO_LOCAL" or "ERRO_SAP");
    public override string ToString() => "P5_R8_PEDIDO_LEGADO_REDACTED";
}

internal sealed record ReciboLegadoP5R8(long VersaoGuarda, int ItensAssociados, int ItensCobertos,
    DocumentoP5? Documento = null)
{
    internal bool CobreUnicoItem => VersaoGuarda > 0 && ItensAssociados == 1 && ItensCobertos == 1;
}

internal interface IContratoLegadoP5R8
{
    Task<ClassificacaoDuravelP5R8?> ValidarEscopoPersistidoAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao,
        EscopoP5 escopo, CancellationToken cancellationToken);
    Task<ReciboLegadoP5R8> ExecutarNaTransacaoAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao,
        PedidoLegadoP5R8 pedido, CancellationToken cancellationToken);
}

internal sealed record ClassificacaoDuravelP5R8(EscopoP5 Escopo, long CodigoSapPedidoCompraItem,
    string ProductType, string ReferenciaAutoritativa)
{
    internal bool CorrespondeRoh(EscopoP5 esperado) => Escopo == esperado && CodigoSapPedidoCompraItem > 0
        && ProductType == "ROH" && !string.IsNullOrWhiteSpace(ReferenciaAutoritativa);
}
