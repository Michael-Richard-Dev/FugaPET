namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed record IdentidadeReciboProdutoSapR8D(string NumeroPedido, string NumeroItem,
    long CodigoLancamento, long CodigoItemLocal, long CodigoSapItem, string Material)
{
    internal bool Corresponde(IdentidadeItemProdutoSapR8B esperado) => esperado.Valida
        && NumeroPedido == esperado.Escopo.Chave.Pedido && NumeroItem == esperado.Escopo.Chave.Item
        && CodigoLancamento == esperado.Escopo.Lancamento && CodigoItemLocal == esperado.CodigoItemLocal
        && CodigoSapItem == esperado.CodigoSapPedidoCompraItem && Material == esperado.Escopo.Material;
    public override string ToString() => "P5_R8D_IDENTIDADE_SQL_REDACTED";
}

internal sealed record DadosEvidenciaSqlR8D(long EvidenciaId, IdentidadeReciboProdutoSapR8D Identidade,
    string Product, string ProductType, string OrigemServico, string SapSistema, string SapMandante,
    string? LastChangeDateTimeRaw, DateTime? LastChangeDateTimeUtc, bool OffsetComprovado,
    DateOnly? LastChangeDate, string? LastChangedByUser, string ReferenciaColetaBackend,
    Guid CorrelacaoColetaBackend, DateTimeOffset RegistradoNoPostgresqlEm,
    DateTimeOffset ValidadeAte, long VersaoPolitica)
{
    public override string ToString() => "P5_R8D_DADOS_SQL_SEM_ATESTACAO_SAP";
}

internal sealed record ReciboEvidenciaSqlR8D(DadosEvidenciaSqlR8D Dados, string FingerprintSha256,
    string ColetorLogin, bool ElegivelAgora)
{
    public override string ToString() => "P5_R8D_EVIDENCIA_RESULTADO_FUNCAO";
}

internal sealed record ReciboResolucaoSqlR8D(DadosEvidenciaSqlR8D Dados, long VinculoId,
    long VersaoVinculo, string EstadoVinculo, DateTimeOffset VinculadoEm)
{
    public override string ToString() => "P5_R8D_RESOLUCAO_RESULTADO_FUNCAO";
}

internal sealed record ReciboVinculoSqlR8D(long VinculoId, long EvidenciaId, long VersaoVinculo,
    string EstadoVinculo, IdentidadeReciboProdutoSapR8D Identidade, string Product, string ProductType,
    string ReferenciaColetaBackend, Guid CorrelacaoColetaBackend, DateTimeOffset VinculadoEm,
    DateTimeOffset? RevogadoEm, DateTimeOffset ValidadeAte, long VersaoPolitica)
{
    public override string ToString() => "P5_R8D_VINCULO_RESULTADO_FUNCAO";
}

internal sealed record ReciboTentativaSqlR8D(string NumeroPedido, string NumeroItem, long CodigoLancamento,
    long CodigoItemLocal, long CodigoSapItem, long TentativaId, long EvidenciaId, long VinculoId,
    long VersaoVinculo, long VersaoGuarda, Guid CorrelationId, string EstadoTentativa,
    DateTimeOffset TentativaIniciadaEm, long RecoveryGeneration)
{
    public override string ToString() => "P5_R8D_TENTATIVA_RESULTADO_FUNCAO_SEM_TOKEN";
}

internal enum EstadoCommitProdutoSapR8D { ResultadoDaFuncao, CommitIndeterminado, CommitConfirmado }
