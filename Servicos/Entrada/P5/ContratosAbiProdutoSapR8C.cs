using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed record PedidoRegistroEvidenciaSqlR8C(IdentidadeItemProdutoSapR8B Identidade,
    EvidenciaProdutoSapR8B Evidencia, string ReferenciaColetaBackend, bool OffsetComprovadoPeloBackend)
{
    public override string ToString() => "P5_R8C_REGISTRO_SEM_AUTORIDADE";
}

internal sealed record DatasProdutoSqlR8C(string? LastChangeDateTimeOriginal,
    DateTime? LastChangeDateTimeUtc, bool OffsetComprovado, DateOnly? LastChangeDate);

internal sealed record ProjecaoClassificacaoSqlR8C(EscopoP5 EscopoSolicitado,
    long CodigoItemLocal, long CodigoSapItem, string ProductType, long EvidenciaId,
    long VinculoId, long VersaoVinculo, DateTimeOffset ValidadeAte)
{
    internal bool Corresponde(IdentidadeItemProdutoSapR8B identidade) => identidade.Valida
        && EscopoSolicitado == identidade.Escopo && CodigoItemLocal == identidade.CodigoItemLocal
        && CodigoSapItem == identidade.CodigoSapPedidoCompraItem
        && ProductType is "ROH" or "HIBE" && EvidenciaId > 0 && VinculoId > 0 && VersaoVinculo > 0;
    public override string ToString() => "P5_R8C_PROJECAO_PARCIAL_SEM_RECIBO";
}

internal sealed record ProjecaoVinculoClassificacaoSqlR8C(long VinculoId, long Versao, string ProductType)
{
    public override string ToString() => "P5_R8C_VINCULO_CLASSIFICACAO_SEM_COMMIT_COMPROVADO";
}

internal enum RespostaVinculoSqlR8C { Invalida, TextoSemReciboDuravel }

internal interface ISessaoAbiProdutoSapR8C
{
    Task<T> LerNaTransacaoExistenteAsync<T>(NpgsqlConnection conexao, NpgsqlTransaction transacao,
        NpgsqlCommand comando, Func<System.Data.Common.DbDataReader, T> leitor,
        CancellationToken cancellationToken);
}
