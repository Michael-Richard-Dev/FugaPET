using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia das pesagens locais da Entrada de Produto (pesagem_entrada_item).
/// Dado capturado na balanca/manual, gravado de uma vez ao finalizar a leitura.
/// </summary>
public sealed class PesagemEntradaItemRepositorio : RepositorioBase
{
    public PesagemEntradaItemRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Situacao do item para validacao de pesagem: se existe, se o pedido e o item estao ativos
    /// e se ha codigo de material. <c>Existe=false</c> quando o item nao foi encontrado.
    /// </summary>
    public async Task<ValidacaoItemPesagem> ValidarItemAsync(long codigoItem, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT (p.situacao_sap_pedido_compra AND p.ativo_sap) AS pedido_ativo,
                   (i.situacao_sap_pedido_compra_item AND i.ativo_sap) AS item_ativo,
                   (length(trim(coalesce(i.codigo_produto, ''))) > 0) AS material_presente
              FROM sap_pedido_compra_item i
              JOIN sap_pedido_compra p
                ON p.codigo_sap_pedido_compra = i.codigo_sap_pedido_compra
             WHERE i.codigo_sap_pedido_compra_item = @codigo_item;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_item", codigoItem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken))
        {
            return new ValidacaoItemPesagem(false, false, false, false);
        }

        return new ValidacaoItemPesagem(
            Existe: true,
            PedidoAtivo: !leitor.IsDBNull(0) && leitor.GetBoolean(0),
            ItemAtivo: !leitor.IsDBNull(1) && leitor.GetBoolean(1),
            MaterialPresente: !leitor.IsDBNull(2) && leitor.GetBoolean(2));
    }

    /// <summary>Grava as pesagens informadas em uma unica transacao auditavel. Retorna a quantidade gravada.</summary>
    public async Task<int> SalvarPesagensAsync(IReadOnlyList<PesagemEntradaItem> pesagens, CancellationToken cancellationToken = default)
    {
        if (pesagens.Count == 0)
        {
            return 0;
        }

        long? usuario = ObterCodigoUsuarioSessao();

        // Uma pesagem por item: na primeira vez insere; nas seguintes atualiza a mesma linha.
        // pesagem_entrada_item_atualizado_em e preenchido pelo trigger fn_definir_atualizado_em.
        const string sql = """
            INSERT INTO pesagem_entrada_item
                (codigo_sap_pedido_compra_item, peso_kg, origem_peso, pesagem_entrada_item_criado_por)
            VALUES
                (@codigo_item, @peso_kg, @origem_peso, @usuario)
            ON CONFLICT (codigo_sap_pedido_compra_item) DO UPDATE
               SET peso_kg = EXCLUDED.peso_kg,
                   origem_peso = EXCLUDED.origem_peso,
                   situacao_pesagem_entrada_item = true,
                   pesagem_entrada_item_atualizado_por = @usuario;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            int gravados = 0;
            foreach (PesagemEntradaItem pesagem in pesagens)
            {
                await using NpgsqlCommand comando = new(sql, conexao, transacao);
                comando.Parameters.Add(ParametroLongo("@codigo_item", pesagem.CodigoSapPedidoCompraItem));
                comando.Parameters.Add(new NpgsqlParameter("@peso_kg", NpgsqlDbType.Numeric) { Value = pesagem.PesoKg });
                comando.Parameters.Add(ParametroTexto("@origem_peso", string.IsNullOrWhiteSpace(pesagem.OrigemPeso) ? "LIDO" : pesagem.OrigemPeso));
                comando.Parameters.Add(new NpgsqlParameter("@usuario", NpgsqlDbType.Bigint) { Value = usuario.HasValue ? usuario.Value : DBNull.Value });
                await comando.ExecuteNonQueryAsync(cancellationToken);
                gravados++;
            }

            return gravados;
        }, cancellationToken);
    }
}

/// <summary>Resultado da validacao de um item de pedido para pesagem.</summary>
public sealed record ValidacaoItemPesagem(bool Existe, bool PedidoAtivo, bool ItemAtivo, bool MaterialPresente);
