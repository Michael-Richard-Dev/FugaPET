using System.Data.Common;
using FugaPET_Dev.Servicos.Entrada.P5;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal static class LeitorResultadosEvidenciaProdutoSapR8C
{
    internal static long? CodigoRegistro(object? resultado) => resultado is long codigo && codigo > 0 ? codigo : null;
    internal static RespostaVinculoSqlR8C VinculoTentativa(object? resultado)
        => resultado is "CLASSIFICACAO_VINCULADA_SEM_AUTORIZAR_POST"
            ? RespostaVinculoSqlR8C.TextoSemReciboDuravel : RespostaVinculoSqlR8C.Invalida;
    internal static ProjecaoClassificacaoSqlR8C? Classificacao(DbDataReader reader,
        IdentidadeItemProdutoSapR8B identidade, DateTimeOffset agora)
    {
        try
        {
            if (!identidade.Valida || reader.FieldCount != 7 || !reader.Read()
                || reader["codigo_item_local"] is not long local || reader["codigo_sap_item"] is not long sap
                || reader["producttype"] is not string tipo || reader["evidencia_id"] is not long evidencia
                || reader["vinculo_id"] is not long vinculo || reader["versao_vinculo"] is not long versao)
                return null;
            DateTimeOffset? validade = reader["validade_ate"] switch
            {
                DateTime data when data.Kind == DateTimeKind.Utc => new DateTimeOffset(data),
                DateTimeOffset instante when instante.Offset == TimeSpan.Zero => instante,
                _ => null
            };
            if (validade is null || validade <= agora || reader.Read()) return null;
            ProjecaoClassificacaoSqlR8C projecao = new(identidade.Escopo, local, sap, tipo, evidencia, vinculo, versao, validade.Value);
            return projecao.Corresponde(identidade) ? projecao : null;
        }
        catch (Exception) { return null; }
    }

    internal static ProjecaoVinculoClassificacaoSqlR8C? VinculoClassificacao(DbDataReader reader,
        long versaoAnterior, string productTypeEsperado)
    {
        try
        {
            if (versaoAnterior < 0 || versaoAnterior == long.MaxValue || productTypeEsperado is not ("ROH" or "HIBE")
                || reader.FieldCount != 3 || !reader.Read() || reader["vinculo_id"] is not long vinculo || vinculo <= 0
                || reader["versao"] is not long versao || versao != versaoAnterior + 1
                || reader["producttype"] is not string tipo || tipo != productTypeEsperado || reader.Read())
                return null;
            return new(vinculo, versao, tipo);
        }
        catch (Exception) { return null; }
    }
}
