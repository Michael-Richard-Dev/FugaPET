using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal static class ComandosRecibosProdutoSapR8D
{
    internal static NpgsqlCommand Evidencia(IdentidadeItemProdutoSapR8B identidade, long evidencia)
    {
        Exigir(identidade.Valida && evidencia > 0);
        return Criar("fn_p5_r8c_obter_evidencia_recibo", [("p_evidencia", NpgsqlDbType.Bigint, evidencia), .. Identidade(identidade)]);
    }

    internal static NpgsqlCommand Vinculo(IdentidadeItemProdutoSapR8B identidade, long vinculo, long evidencia, long versao)
    {
        Exigir(identidade.Valida && vinculo > 0 && evidencia > 0 && versao > 0);
        return Criar("fn_p5_r8c_obter_vinculo_recibo", [("p_vinculo", NpgsqlDbType.Bigint, vinculo),
            ("p_evidencia", NpgsqlDbType.Bigint, evidencia), ("p_versao", NpgsqlDbType.Bigint, versao), .. Identidade(identidade)]);
    }

    internal static NpgsqlCommand Resolver(EscopoP5 escopo)
    {
        Exigir(escopo.Valido);
        return Criar("fn_p5_r8c_resolver_recibo", [.. Escopo(escopo), ("p_material", NpgsqlDbType.Text, escopo.Material)]);
    }

    internal static NpgsqlCommand VincularTentativa(IdentidadeItemProdutoSapR8B identidade,
        ClaimP5 tentativa, ReciboResolucaoSqlR8D resolucao)
    {
        Exigir(identidade.Valida && tentativa.Valido && tentativa.Escopo == identidade.Escopo
            && tentativa.VersaoGuarda > 0 && tentativa.GeracaoRecovery >= 0
            && resolucao.Dados.Identidade.Corresponde(identidade) && resolucao.Dados.Product == identidade.Escopo.Material
            && resolucao.Dados.ProductType == "ROH" && resolucao.Dados.EvidenciaId > 0
            && resolucao.VinculoId > 0 && resolucao.VersaoVinculo > 0 && resolucao.EstadoVinculo == "VALIDADA");
        return Criar("fn_p5_r8c_vincular_tentativa_recibo", [.. Escopo(identidade.Escopo),
            ("p_tentativa", NpgsqlDbType.Bigint, tentativa.Tentativa), ("p_claim", NpgsqlDbType.Uuid, tentativa.Token),
            ("p_correlacao", NpgsqlDbType.Uuid, tentativa.Correlacao), ("p_vinculo_id", NpgsqlDbType.Bigint, resolucao.VinculoId),
            ("p_versao_vinculo", NpgsqlDbType.Bigint, resolucao.VersaoVinculo),
            ("p_evidencia_esperada", NpgsqlDbType.Bigint, resolucao.Dados.EvidenciaId),
            ("p_versao_guarda_esperada", NpgsqlDbType.Bigint, tentativa.VersaoGuarda)]);
    }

    private static (string Nome, NpgsqlDbType Tipo, object Valor)[] Escopo(EscopoP5 escopo) =>
        [("p_pedido", NpgsqlDbType.Text, escopo.Chave.Pedido), ("p_item", NpgsqlDbType.Text, escopo.Chave.Item),
         ("p_lancamento", NpgsqlDbType.Bigint, escopo.Lancamento)];

    private static (string Nome, NpgsqlDbType Tipo, object Valor)[] Identidade(IdentidadeItemProdutoSapR8B identidade) =>
        [.. Escopo(identidade.Escopo), ("p_item_local", NpgsqlDbType.Bigint, identidade.CodigoItemLocal),
         ("p_fk_sap_item", NpgsqlDbType.Bigint, identidade.CodigoSapPedidoCompraItem),
         ("p_material", NpgsqlDbType.Text, identidade.Escopo.Material)];

    private static NpgsqlCommand Criar(string funcao, (string Nome, NpgsqlDbType Tipo, object Valor)[] parametros)
    {
        string slots = string.Join(",", Enumerable.Range(1, parametros.Length).Select(indice => "$" + indice));
        NpgsqlCommand comando = new($"SELECT * FROM homologacao.{funcao}({slots})");
        foreach (var parametro in parametros)
            comando.Parameters.Add(new NpgsqlParameter { ParameterName = parametro.Nome, NpgsqlDbType = parametro.Tipo, Value = parametro.Valor });
        return comando;
    }

    private static void Exigir(bool valido)
    {
        if (!valido) throw new InvalidOperationException("P5_R8D_IDENTIDADE_OU_RECIBO_INVALIDO");
    }
}
