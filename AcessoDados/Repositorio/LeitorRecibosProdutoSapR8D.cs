using System.Data.Common;
using FugaPET_Dev.Servicos.Entrada.P5;
using System.Text.RegularExpressions;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal static class LeitorRecibosProdutoSapR8D
{
    private const string EvidenciaLayout = "evidencia_id bigint,numero_pedido text,numero_item text,codigo_lancamento bigint,codigo_item_local bigint,codigo_sap_item bigint,material text,product text,producttype text,origem_servico text,sap_sistema text,sap_mandante text,last_change_datetime_raw text,last_change_datetime_utc timestamptz,offset_comprovado boolean,last_change_date date,last_changed_by_user text,referencia_coleta_backend text,correlation_id_coleta_backend uuid,registrado_no_postgresql_em timestamptz,validade_ate timestamptz,versao_politica bigint,fingerprint_sha256 text,coletor_login name,elegivel_agora boolean";
    private const string VinculoLayout = "vinculo_id bigint,evidencia_id bigint,versao_vinculo bigint,estado_vinculo text,numero_pedido text,numero_item text,codigo_lancamento bigint,codigo_item_local bigint,codigo_sap_item bigint,material text,product text,producttype text,referencia_coleta_backend text,correlation_id_coleta_backend uuid,vinculado_em timestamptz,revogado_em timestamptz,validade_ate timestamptz,versao_politica bigint";
    private const string ResolucaoLayout = "numero_pedido text,numero_item text,codigo_lancamento bigint,codigo_item_local bigint,codigo_sap_item bigint,material text,product text,producttype text,evidencia_id bigint,vinculo_id bigint,versao_vinculo bigint,estado_vinculo text,referencia_coleta_backend text,correlation_id_coleta_backend uuid,sap_sistema text,sap_mandante text,origem_servico text,last_change_datetime_raw text,last_change_datetime_utc timestamptz,offset_comprovado boolean,last_change_date date,last_changed_by_user text,registrado_no_postgresql_em timestamptz,vinculado_em timestamptz,validade_ate timestamptz,versao_politica bigint";
    private const string TentativaLayout = "numero_pedido text,numero_item text,codigo_lancamento bigint,codigo_item_local bigint,codigo_sap_item bigint,tentativa_id bigint,evidencia_id bigint,vinculo_id bigint,versao_vinculo bigint,versao_guarda bigint,correlation_id uuid,estado_tentativa text,tentativa_iniciada_em timestamptz,recovery_generation bigint";
    private static readonly HashSet<string> Opcionais = ["last_change_datetime_raw", "last_change_datetime_utc", "last_change_date", "last_changed_by_user", "revogado_em"];

    internal static ReciboEvidenciaSqlR8D? Evidencia(DbDataReader reader, IdentidadeItemProdutoSapR8B identidade,
        long evidencia, ContextoBackendProdutoSapR8B origem, DateTimeOffset agora)
    {
        Dictionary<string, object?>? linha = Linha(reader, EvidenciaLayout);
        if (linha is null) return null;
        DadosEvidenciaSqlR8D? dados = Dados(linha, identidade, origem, agora);
        if (dados is null || evidencia <= 0 || dados.EvidenciaId != evidencia || !Valor<bool>(linha, "elegivel_agora")
            || !Regex.IsMatch(Valor<string>(linha, "fingerprint_sha256"), "^[0-9a-f]{64}$")
            || string.IsNullOrWhiteSpace(Valor<string>(linha, "coletor_login"))) return null;
        return new(dados, Valor<string>(linha, "fingerprint_sha256"), Valor<string>(linha, "coletor_login"), Valor<bool>(linha, "elegivel_agora"));
    }

    internal static ReciboVinculoSqlR8D? Vinculo(DbDataReader reader, IdentidadeItemProdutoSapR8B identidade,
        ReciboEvidenciaSqlR8D evidencia, long vinculo, long versao, DateTimeOffset agora)
    {
        Dictionary<string, object?>? linha = Linha(reader, VinculoLayout);
        if (linha is null || evidencia is null) return null;
        IdentidadeReciboProdutoSapR8D retornada = Identidade(linha);
        if (!retornada.Corresponde(identidade) || !evidencia.Dados.Identidade.Corresponde(identidade)
            || vinculo <= 0 || versao <= 0 || Valor<long>(linha, "vinculo_id") != vinculo
            || Valor<long>(linha, "evidencia_id") != evidencia.Dados.EvidenciaId
            || Valor<long>(linha, "versao_vinculo") != versao || Valor<string>(linha, "estado_vinculo") != "VALIDADA"
            || linha["revogado_em"] is not null || Valor<string>(linha, "product") != evidencia.Dados.Product
            || Valor<string>(linha, "producttype") != evidencia.Dados.ProductType
            || Valor<string>(linha, "referencia_coleta_backend") != evidencia.Dados.ReferenciaColetaBackend
            || Valor<Guid>(linha, "correlation_id_coleta_backend") != evidencia.Dados.CorrelacaoColetaBackend
            || Valor<DateTimeOffset>(linha, "validade_ate") <= agora
            || Valor<DateTimeOffset>(linha, "validade_ate") != evidencia.Dados.ValidadeAte
            || Valor<long>(linha, "versao_politica") != evidencia.Dados.VersaoPolitica) return null;
        return new(Valor<long>(linha, "vinculo_id"), Valor<long>(linha, "evidencia_id"), Valor<long>(linha, "versao_vinculo"), Valor<string>(linha, "estado_vinculo"),
            retornada, Valor<string>(linha, "product"), Valor<string>(linha, "producttype"),
            Valor<string>(linha, "referencia_coleta_backend"), Valor<Guid>(linha, "correlation_id_coleta_backend"),
            Valor<DateTimeOffset>(linha, "vinculado_em"), InstanteOpcional(linha, "revogado_em"),
            Valor<DateTimeOffset>(linha, "validade_ate"), Valor<long>(linha, "versao_politica"));
    }

    internal static ReciboResolucaoSqlR8D? Resolucao(DbDataReader reader, IdentidadeItemProdutoSapR8B identidade,
        ContextoBackendProdutoSapR8B origem, DateTimeOffset agora)
    {
        Dictionary<string, object?>? linha = Linha(reader, ResolucaoLayout);
        if (linha is null) return null;
        DadosEvidenciaSqlR8D? dados = Dados(linha, identidade, origem, agora);
        if (dados is null || Valor<long>(linha, "vinculo_id") <= 0 || Valor<long>(linha, "versao_vinculo") <= 0
            || Valor<string>(linha, "estado_vinculo") != "VALIDADA") return null;
        return new(dados, Valor<long>(linha, "vinculo_id"), Valor<long>(linha, "versao_vinculo"),
            Valor<string>(linha, "estado_vinculo"), Valor<DateTimeOffset>(linha, "vinculado_em"));
    }

    internal static ReciboTentativaSqlR8D? Tentativa(DbDataReader reader, ClaimP5 tentativa,
        ReciboResolucaoSqlR8D resolucao)
    {
        Dictionary<string, object?>? linha = Linha(reader, TentativaLayout);
        if (linha is null || resolucao is null || !tentativa.Valido || tentativa.VersaoGuarda <= 0
            || tentativa.GeracaoRecovery < 0 || resolucao.Dados.ProductType != "ROH"
            || resolucao.EstadoVinculo != "VALIDADA") return null;
        IdentidadeReciboProdutoSapR8D identidade = resolucao.Dados.Identidade;
        if (Valor<string>(linha, "numero_pedido") != tentativa.Escopo.Chave.Pedido
            || Valor<string>(linha, "numero_item") != tentativa.Escopo.Chave.Item
            || Valor<long>(linha, "codigo_lancamento") != tentativa.Escopo.Lancamento
            || identidade.NumeroPedido != tentativa.Escopo.Chave.Pedido || identidade.NumeroItem != tentativa.Escopo.Chave.Item
            || identidade.CodigoLancamento != tentativa.Escopo.Lancamento || identidade.Material != tentativa.Escopo.Material
            || Valor<long>(linha, "codigo_item_local") != identidade.CodigoItemLocal
            || Valor<long>(linha, "codigo_sap_item") != identidade.CodigoSapItem
            || Valor<long>(linha, "tentativa_id") != tentativa.Tentativa
            || Valor<long>(linha, "evidencia_id") != resolucao.Dados.EvidenciaId
            || Valor<long>(linha, "vinculo_id") != resolucao.VinculoId
            || Valor<long>(linha, "versao_vinculo") != resolucao.VersaoVinculo
            || Valor<long>(linha, "versao_guarda") != tentativa.VersaoGuarda
            || Valor<Guid>(linha, "correlation_id") != tentativa.Correlacao
            || Valor<string>(linha, "estado_tentativa") != "RESERVADO"
            || Valor<long>(linha, "recovery_generation") != tentativa.GeracaoRecovery) return null;
        return new(Valor<string>(linha, "numero_pedido"), Valor<string>(linha, "numero_item"),
            Valor<long>(linha, "codigo_lancamento"), Valor<long>(linha, "codigo_item_local"), Valor<long>(linha, "codigo_sap_item"),
            Valor<long>(linha, "tentativa_id"), Valor<long>(linha, "evidencia_id"), Valor<long>(linha, "vinculo_id"),
            Valor<long>(linha, "versao_vinculo"), Valor<long>(linha, "versao_guarda"), Valor<Guid>(linha, "correlation_id"),
            Valor<string>(linha, "estado_tentativa"), Valor<DateTimeOffset>(linha, "tentativa_iniciada_em"),
            Valor<long>(linha, "recovery_generation"));
    }

    private static DadosEvidenciaSqlR8D? Dados(Dictionary<string, object?> linha, IdentidadeItemProdutoSapR8B esperado,
        ContextoBackendProdutoSapR8B origem, DateTimeOffset agora)
    {
        IdentidadeReciboProdutoSapR8D identidade = Identidade(linha);
        if (!identidade.Corresponde(esperado) || Valor<long>(linha, "evidencia_id") <= 0
            || Valor<string>(linha, "product") != identidade.Material || Valor<string>(linha, "producttype") is not ("ROH" or "HIBE")
            || string.IsNullOrWhiteSpace(origem.Ambiente) || string.IsNullOrWhiteSpace(origem.Mandante)
            || origem.OrigemTecnica != "API_PRODUCT_SRV/A_Product"
            || Valor<string>(linha, "sap_sistema") != origem.Ambiente || Valor<string>(linha, "sap_mandante") != origem.Mandante
            || Valor<string>(linha, "origem_servico") != origem.OrigemTecnica
            || string.IsNullOrWhiteSpace(Valor<string>(linha, "referencia_coleta_backend"))
            || Valor<Guid>(linha, "correlation_id_coleta_backend") == Guid.Empty
            || Valor<long>(linha, "versao_politica") <= 0 || Valor<DateTimeOffset>(linha, "validade_ate") <= agora) return null;
        string? raw = linha["last_change_datetime_raw"] as string;
        DateTime? utc = InstanteOpcional(linha, "last_change_datetime_utc")?.UtcDateTime;
        bool offset = Valor<bool>(linha, "offset_comprovado");
        if (!offset && utc is not null) return null;
        if (offset)
        {
            string? marcador = raw is null ? null : Regex.Match(raw, @"(?<offset>Z|[+-][0-9]{2}:[0-9]{2})$").Groups["offset"].Value;
            DatasProdutoSqlR8C? datas = MapeadorDatasProdutoSapR8C.Mapear(
                new(Valor<string>(linha, "product"), Valor<string>(linha, "producttype"), raw, marcador, null, null), true);
            if (utc is null || datas?.LastChangeDateTimeUtc != utc) return null;
        }
        return new(Valor<long>(linha, "evidencia_id"), identidade, Valor<string>(linha, "product"),
            Valor<string>(linha, "producttype"), Valor<string>(linha, "origem_servico"), Valor<string>(linha, "sap_sistema"),
            Valor<string>(linha, "sap_mandante"), raw, utc, offset, linha["last_change_date"] is DateOnly data ? data : null,
            linha["last_changed_by_user"] as string, Valor<string>(linha, "referencia_coleta_backend"),
            Valor<Guid>(linha, "correlation_id_coleta_backend"), Valor<DateTimeOffset>(linha, "registrado_no_postgresql_em"),
            Valor<DateTimeOffset>(linha, "validade_ate"), Valor<long>(linha, "versao_politica"));
    }

    private static IdentidadeReciboProdutoSapR8D Identidade(Dictionary<string, object?> linha) => new(
        Valor<string>(linha, "numero_pedido"), Valor<string>(linha, "numero_item"), Valor<long>(linha, "codigo_lancamento"),
        Valor<long>(linha, "codigo_item_local"), Valor<long>(linha, "codigo_sap_item"), Valor<string>(linha, "material"));

    private static T Valor<T>(Dictionary<string, object?> linha, string nome) => (T)linha[nome]!;
    private static DateTimeOffset? InstanteOpcional(Dictionary<string, object?> linha, string nome)
        => linha[nome] is DateTimeOffset instante ? instante : null;

    private static Dictionary<string, object?>? Linha(DbDataReader reader, string layout)
    {
        try
        {
            string[] colunas = layout.Split(',');
            if (reader.FieldCount != colunas.Length || !reader.Read()) return null;
            Dictionary<string, object?> linha = new(StringComparer.Ordinal);
            for (int indice = 0; indice < colunas.Length; indice++)
            {
                string[] campo = colunas[indice].Split(' ');
                if (reader.GetName(indice) != campo[0] || !TipoCompativel(reader.GetFieldType(indice), campo[1])) return null;
                if (reader.IsDBNull(indice))
                {
                    if (!Opcionais.Contains(campo[0])) return null;
                    linha.Add(campo[0], null);
                    continue;
                }
                object valor = reader.GetValue(indice);
                if (campo[1] == "timestamptz")
                {
                    valor = valor switch
                    {
                        DateTime data when data.Kind == DateTimeKind.Utc => new DateTimeOffset(data),
                        DateTimeOffset instante when instante.Offset == TimeSpan.Zero => instante,
                        _ => throw new InvalidOperationException("P5_R8D_INSTANTE_SQL_AMBIGUO")
                    };
                }
                if (campo[1] == "date")
                {
                    valor = valor switch
                    {
                        DateOnly civil => civil,
                        DateTime civil when civil.Kind == DateTimeKind.Unspecified && civil.TimeOfDay == TimeSpan.Zero => DateOnly.FromDateTime(civil),
                        _ => throw new InvalidOperationException("P5_R8D_DATA_SQL_AMBIGUA")
                    };
                }
                linha.Add(campo[0], valor);
            }
            return reader.Read() ? null : linha;
        }
        catch (Exception) { return null; }
    }

    private static bool TipoCompativel(Type tipo, string sql) => sql switch
    {
        "text" or "name" => tipo == typeof(string),
        "bigint" => tipo == typeof(long),
        "uuid" => tipo == typeof(Guid),
        "boolean" => tipo == typeof(bool),
        "timestamptz" => tipo == typeof(DateTime) || tipo == typeof(DateTimeOffset),
        "date" => tipo == typeof(DateOnly) || tipo == typeof(DateTime),
        _ => false
    };
}
