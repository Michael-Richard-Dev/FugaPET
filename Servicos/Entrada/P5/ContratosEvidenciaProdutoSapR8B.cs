using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed record CamposProdutoSapR8B(string Product, string ProductType,
    string? LastChangeDateTimeOriginal, string? OffsetLastChangeDateTimeOriginal,
    string? LastChangeDateOriginal, string? LastChangedByUser)
{
    public override string ToString() => "P5_R8B_CAMPOS_SAP_REDACTED";
}

internal sealed record ContextoBackendProdutoSapR8B(string Ambiente, string Mandante, string OrigemTecnica,
    string IdentidadeBackend, Guid CorrelacaoColetaBackend,
    DateTimeOffset ConsultaIniciadaEm, DateTimeOffset ConsultaConcluidaEm)
{
    public override string ToString() => "P5_R8B_CONTEXTO_BACKEND_REDACTED";
}

internal sealed record EvidenciaProdutoSapR8B(CamposProdutoSapR8B Campos,
    ContextoBackendProdutoSapR8B Contexto, string? ETagSap = null)
{
    public override string ToString() => "P5_R8B_CANDIDATA_SEM_AUTORIDADE";
}

internal sealed record IdentidadeItemProdutoSapR8B(EscopoP5 Escopo, long CodigoItemLocal,
    long CodigoSapPedidoCompraItem)
{
    internal bool Valida => Escopo.Valido && CodigoItemLocal > 0 && CodigoSapPedidoCompraItem > 0;
    public override string ToString() => "P5_R8B_IDENTIDADE_REDACTED";
}

internal sealed record ReciboEvidenciaProdutoSapR8B(EvidenciaProdutoSapR8B Evidencia,
    string ReferenciaEvidencia, long VersaoEvidencia, DateTimeOffset? RegistradaEmBanco,
    DateTimeOffset? RevogadaEmBanco)
{
    public override string ToString() => "P5_R8B_RECIBO_NAO_ATESTADO";
}

internal sealed record VinculoEvidenciaTentativaP5R8B(IdentidadeItemProdutoSapR8B Identidade,
    ClaimP5 Tentativa, string ReferenciaEvidencia, long VersaoEvidencia, string ProductType,
    Guid CorrelacaoColetaBackend, DateTimeOffset? VinculadaEmBanco)
{
    public override string ToString() => "P5_R8B_VINCULO_REDACTED";
}

internal enum ResultadoConsistenciaProdutoSapR8B
{
    NaoPersistida,
    MetadadosInvalidos,
    IdentidadeDivergente,
    ClassificacaoNaoComprovada,
    Revogada,
    SemVinculo,
    TentativaDivergente,
    EvidenciaDivergente,
    ClassificacaoAlterada,
    CoerenteSemAutoridade
}

internal interface IColetorBackendProdutoSapR8B
{
    Task<EvidenciaProdutoSapR8B?> ColetarAsync(string material, CancellationToken cancellationToken);
}

internal interface IContratoEvidenciaProdutoSapGaiaR8B
{
    Task<ReciboEvidenciaProdutoSapR8B> RegistrarEvidenciaClassificacaoSapV1Async(
        NpgsqlConnection conexao, NpgsqlTransaction transacao, EvidenciaProdutoSapR8B evidencia,
        CancellationToken cancellationToken);
    Task<ReciboEvidenciaProdutoSapR8B?> ResolverClassificacaoPersistidaP5V1Async(
        NpgsqlConnection conexao, NpgsqlTransaction transacao, EscopoP5 escopo,
        CancellationToken cancellationToken);
    Task<VinculoEvidenciaTentativaP5R8B> VincularEvidenciaTentativaAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao, IdentidadeItemProdutoSapR8B identidade,
        ClaimP5 tentativa, ReciboEvidenciaProdutoSapR8B evidencia, CancellationToken cancellationToken);
}
