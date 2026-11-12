using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal static class ComandosEvidenciaProdutoSapR8C
{
    internal static NpgsqlCommand Registrar(PedidoRegistroEvidenciaSqlR8C pedido)
    {
        ContextoBackendProdutoSapR8B contexto = pedido.Evidencia.Contexto;
        CamposProdutoSapR8B campos = pedido.Evidencia.Campos;
        Exigir(pedido.Identidade.Valida && campos.Product == pedido.Identidade.Escopo.Material
            && campos.ProductType is "ROH" or "HIBE" && !string.IsNullOrWhiteSpace(pedido.ReferenciaColetaBackend)
            && contexto.CorrelacaoColetaBackend != Guid.Empty && contexto.OrigemTecnica == "API_PRODUCT_SRV/A_Product"
            && !string.IsNullOrWhiteSpace(contexto.Ambiente) && !string.IsNullOrWhiteSpace(contexto.Mandante)
            && !string.IsNullOrWhiteSpace(contexto.IdentidadeBackend) && contexto.ConsultaIniciadaEm != default
            && contexto.ConsultaConcluidaEm >= contexto.ConsultaIniciadaEm);
        DatasProdutoSqlR8C datas = MapeadorDatasProdutoSapR8C.Mapear(campos, pedido.OffsetComprovadoPeloBackend)
            ?? throw new InvalidOperationException("P5_R8C_DATAS_SAP_NAO_MAPEAVEIS");
        return Criar("fn_p5_r8b_ingestar_producttype", [.. Identidade(pedido.Identidade),
            ("p_product", NpgsqlDbType.Text, campos.Product),
            ("p_producttype", NpgsqlDbType.Text, campos.ProductType),
            ("p_lastchange_datetime_raw", NpgsqlDbType.Text, datas.LastChangeDateTimeOriginal),
            ("p_lastchange_datetime_utc", NpgsqlDbType.TimestampTz, datas.LastChangeDateTimeUtc),
            ("p_offset_comprovado", NpgsqlDbType.Boolean, datas.OffsetComprovado),
            ("p_lastchange_date", NpgsqlDbType.Date, datas.LastChangeDate),
            ("p_lastchanged_by_user", NpgsqlDbType.Text, campos.LastChangedByUser),
            ("p_referencia_coleta", NpgsqlDbType.Text, pedido.ReferenciaColetaBackend),
            ("p_correlation_coleta", NpgsqlDbType.Uuid, contexto.CorrelacaoColetaBackend)]);
    }

    internal static NpgsqlCommand Resolver(EscopoP5 escopo)
    {
        Exigir(escopo.Valido);
        return Criar("fn_p5_r8b_resolver_classificacao", [.. Escopo(escopo), ("p_material", NpgsqlDbType.Text, escopo.Material)]);
    }

    internal static NpgsqlCommand VincularClassificacao(IdentidadeItemProdutoSapR8B identidade, long evidencia, long versaoAnterior)
    {
        Exigir(identidade.Valida && evidencia > 0 && versaoAnterior >= 0 && versaoAnterior < long.MaxValue);
        return Criar("fn_p5_r8b_vincular_classificacao", [.. Identidade(identidade),
            ("p_evidencia", NpgsqlDbType.Bigint, evidencia), ("p_versao_anterior", NpgsqlDbType.Bigint, versaoAnterior)]);
    }

    internal static NpgsqlCommand VincularTentativa(IdentidadeItemProdutoSapR8B identidade, ClaimP5 tentativa,
        ProjecaoClassificacaoSqlR8C classificacao)
        => throw new InvalidOperationException("P5_R8F_ENTRADA_DIRETA_R8B1_PROIBIDA");

    private static (string Nome, NpgsqlDbType Tipo, object? Valor)[] Escopo(EscopoP5 escopo) =>
        [("p_pedido", NpgsqlDbType.Text, escopo.Chave.Pedido), ("p_item", NpgsqlDbType.Text, escopo.Chave.Item),
         ("p_lancamento", NpgsqlDbType.Bigint, escopo.Lancamento)];

    private static (string Nome, NpgsqlDbType Tipo, object? Valor)[] Identidade(IdentidadeItemProdutoSapR8B identidade) =>
        [.. Escopo(identidade.Escopo), ("p_item_local", NpgsqlDbType.Bigint, identidade.CodigoItemLocal),
         ("p_fk_sap_item", NpgsqlDbType.Bigint, identidade.CodigoSapPedidoCompraItem),
         ("p_material", NpgsqlDbType.Text, identidade.Escopo.Material)];

    private static NpgsqlCommand Criar(string funcao, (string Nome, NpgsqlDbType Tipo, object? Valor)[] parametros)
    {
        string slots = string.Join(",", Enumerable.Range(1, parametros.Length).Select(indice => "$" + indice));
        NpgsqlCommand comando = new($"SELECT * FROM homologacao.{funcao}({slots})");
        foreach (var parametro in parametros)
            comando.Parameters.Add(new NpgsqlParameter
            {
                ParameterName = parametro.Nome, NpgsqlDbType = parametro.Tipo, Value = parametro.Valor ?? DBNull.Value
            });
        return comando;
    }

    private static void Exigir(bool valido)
    {
        if (!valido) throw new InvalidOperationException("P5_R8C_IDENTIDADE_OU_DADO_OBRIGATORIO_AUSENTE");
    }
}
