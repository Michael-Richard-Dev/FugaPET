using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed record PedidoReservaRohR8F(IdentidadeItemProdutoSapR8B Identidade,
    ReciboResolucaoSqlR8D Classificacao, long VersaoGuardaEsperada, ProvaSapP5 Prova)
{
    public override string ToString() => "R8F_PEDIDO_SEM_AUTORIDADE_DESKTOP";
}

internal sealed record TitularRohR8F(IdentidadeItemProdutoSapR8B Identidade, ClaimP5 Claim,
    long Evidencia, long Vinculo, long VersaoVinculo)
{
    internal bool Valido => Identidade.Valida && Claim.Valido && Claim.Escopo == Identidade.Escopo
        && Claim.VersaoGuarda > 0 && Claim.GeracaoRecovery >= 0 && Evidencia > 0 && Vinculo > 0 && VersaoVinculo > 0;
    internal bool Corresponde(TitularRohR8F esperado) => Valido && esperado.Valido
        && Identidade == esperado.Identidade && Evidencia == esperado.Evidencia
        && Vinculo == esperado.Vinculo && VersaoVinculo == esperado.VersaoVinculo
        && Claim.Tentativa == esperado.Claim.Tentativa && Claim.Token == esperado.Claim.Token
        && Claim.Correlacao == esperado.Claim.Correlacao && Claim.GeracaoRecovery == esperado.Claim.GeracaoRecovery;
    public override string ToString() => "R8F_TITULAR_REDACTED";
}

internal sealed record ReciboReservaRohR8F(TitularRohR8F Titular, ReciboResolucaoSqlR8D Classificacao,
    long VersaoGuardaAnterior, EstadoP5 Estado, int ItensAssociados, int ItensCobertos, string StatusAnterior)
{
    public override string ToString() => "R8F_RESERVA_RESULTADO_FUNCAO";
}

internal sealed record ReciboEstadoRohR8F(TitularRohR8F Titular, long VersaoGuardaAnterior, EstadoP5 Estado,
    DateTimeOffset? EnvioIniciadoEm, DateTimeOffset? FronteiraConfirmadaEm, bool HttpTentado, bool CommitIncerto)
{
    public override string ToString() => "R8F_ESTADO_SEM_CAPABILITY_HTTP";
}

internal sealed record PedidoResultadoRohR8F(TitularRohR8F Titular, RetornoHttpP5 Retorno)
{
    public override string ToString() => "R8F_RESULTADO_REDACTED";
}

internal sealed record ReciboResultadoRohR8F(TitularRohR8F Titular, long VersaoGuardaAnterior,
    EstadoP5 Estado, string StatusLancamento, string StatusItem, DocumentoP5? Documento,
    int ItensAssociados, int ItensCobertos)
{
    public override string ToString() => "R8F_RESULTADO_SEM_PROVA_DE_COMMIT";
}

internal sealed record PedidoCompensacaoRohR8F(TitularRohR8F Titular, string StatusAnterior)
{
    public override string ToString() => "R8F_COMPENSACAO_REDACTED";
}

internal sealed record ReciboCompensacaoRohR8F(TitularRohR8F Titular, long VersaoGuardaAnterior,
    EstadoP5 Estado, string StatusLancamento, int ItensAssociados, int ItensCobertos)
{
    public override string ToString() => "R8F_COMPENSACAO_SEM_PROVA_DE_COMMIT";
}

internal sealed record PedidoRecoveryRohR8F(IdentidadeItemProdutoSapR8B Identidade, RecoveryP5 Recovery,
    ProvaSapP5 Prova)
{
    public override string ToString() => "R8F_RECOVERY_REDACTED";
}

internal sealed record ReciboRecoveryRohR8F(RecoveryP5 Recovery, EstadoP5 Estado,
    DocumentoP5? Documento, bool PermiteNovoPost, IdentidadeItemProdutoSapR8B Identidade,
    int ItensAssociados, int ItensCobertos)
{
    public override string ToString() => "R8F_RECOVERY_SEM_DISPATCH";
}

internal interface ISessaoRohR8F
{
    Task<ReciboReservaRohR8F> ReservarIntegralAsync(PedidoReservaRohR8F pedido, CancellationToken ct);
    Task<ReciboEstadoRohR8F> MarcarEnvioAsync(TitularRohR8F titular, CancellationToken ct);
    Task<ReciboEstadoRohR8F> SelarFronteiraAsync(TitularRohR8F titular, CancellationToken ct);
    Task<ReciboEstadoRohR8F> ConsultarTentativaAsync(TitularRohR8F titular, CancellationToken ct);
    Task<ReciboResultadoRohR8F> RegistrarResultadoIntegralAsync(PedidoResultadoRohR8F pedido, CancellationToken ct);
    Task<ReciboCompensacaoRohR8F> CompensarPreHttpIntegralAsync(PedidoCompensacaoRohR8F pedido, CancellationToken ct);
    Task<ReciboRecoveryRohR8F> ReconciliarSemReenvioAsync(PedidoRecoveryRohR8F pedido, CancellationToken ct);
}

internal interface IExecutorRohR8F
{
    bool Disponivel { get; }
    PapelP5R8 Papel { get; }
    Task<T> ExecutarAsync<T>(Func<ISessaoRohR8F, CancellationToken, Task<T>> acao, CancellationToken ct);
}

internal interface IContratoSqlRohR8F
{
    Task<ReciboReservaRohR8F> ReservarIntegralAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        PedidoReservaRohR8F pedido, CancellationToken ct) => throw Indisponivel();
    Task<ReciboEstadoRohR8F> MarcarEnvioAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        TitularRohR8F titular, CancellationToken ct) => throw Indisponivel();
    Task<ReciboEstadoRohR8F> SelarFronteiraAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        TitularRohR8F titular, CancellationToken ct) => throw Indisponivel();
    Task<ReciboEstadoRohR8F> ConsultarTentativaAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        TitularRohR8F titular, CancellationToken ct) => throw Indisponivel();
    Task<ReciboResultadoRohR8F> RegistrarResultadoIntegralAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        PedidoResultadoRohR8F pedido, CancellationToken ct) => throw Indisponivel();
    Task<ReciboCompensacaoRohR8F> CompensarPreHttpIntegralAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        PedidoCompensacaoRohR8F pedido, CancellationToken ct) => throw Indisponivel();
    Task<ReciboRecoveryRohR8F> ReconciliarSemReenvioAsync(NpgsqlConnection conexao, NpgsqlTransaction tx,
        PedidoRecoveryRohR8F pedido, CancellationToken ct) => throw Indisponivel();
    private static InvalidOperationException Indisponivel() => new("R8F_SQL_PRIVILEGIADO_NAO_IMPLEMENTADO");
}

internal sealed class ContratoSqlRohIndisponivelR8F : IContratoSqlRohR8F;

internal sealed class CommitRohIndeterminadoR8FException() : Exception("R8F_COMMIT_INDETERMINADO")
{
    public override string ToString() => Message;
}
