using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Entrada;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia da rastreabilidade da Entrada de Produto (entrada_produto_lancamento /
/// _item / _pesagem). Cada lancamento e independente e preserva todas as pesagens.
/// </summary>
public sealed class EntradaProdutoRepositorio : RepositorioBase
{
    public EntradaProdutoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Grava um lancamento completo (cabecalho + itens + pesagens) em uma unica transacao auditavel.
    /// Retorna o codigo do lancamento criado.
    /// </summary>
    public async Task<long> SalvarLancamentoAsync(EntradaProdutoLancamento lancamento, CancellationToken cancellationToken = default)
    {
        long? usuario = ObterCodigoUsuarioSessao();

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long codigoLancamento = await InserirLancamentoAsync(conexao, transacao, lancamento, usuario, cancellationToken);

            foreach (EntradaProdutoItem item in lancamento.Itens)
            {
                long codigoItem = await InserirItemAsync(conexao, transacao, codigoLancamento, item, usuario, cancellationToken);

                int sequencia = 0;
                foreach (EntradaProdutoPesagem pesagem in item.Pesagens)
                {
                    sequencia++;
                    await InserirPesagemAsync(conexao, transacao, codigoItem, sequencia, pesagem, usuario, cancellationToken);
                }
            }

            return codigoLancamento;
        }, cancellationToken);
    }

    private async Task<long> InserirLancamentoAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        EntradaProdutoLancamento lancamento, long? usuario, CancellationToken cancellationToken)
    {
        // status FINALIZADO_LOCAL: o lancamento e gravado completo (todas as pesagens) ao parar a producao,
        // ainda nao enviado ao SAP. Coerente com a CHECK ck_entrada_lancamento_finalizado_tem_data.
        const string sql = """
            INSERT INTO entrada_produto_lancamento
                (numero_pedido, fornecedor, codigo_setor, terminal, codigo_usuario,
                 iniciado_em, finalizado_em, status_lancamento, entrada_produto_lancamento_criado_por)
            VALUES
                (@numero_pedido, @fornecedor, @codigo_setor, @terminal, @usuario,
                 now(), now(), 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_lancamento;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", lancamento.NumeroPedido));
        comando.Parameters.Add(ParametroTextoNulo("@fornecedor", lancamento.Fornecedor));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_setor", lancamento.CodigoSetor));
        comando.Parameters.Add(ParametroTextoNulo("@terminal", lancamento.Terminal));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o lancamento de entrada.");
    }

    private async Task<long> InserirItemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoLancamento, EntradaProdutoItem item, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_item
                (codigo_entrada_produto_lancamento, codigo_sap_pedido_compra_item, numero_item, material,
                 centro, deposito, unidade, quantidade_prevista, quantidade_recebida, status_item, entrada_produto_item_criado_por)
            VALUES
                (@lancamento, @sap_item, @numero_item, @material,
                 @centro, @deposito, @unidade, @qtd_prevista, @qtd_recebida, 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongoNulo("@sap_item", item.CodigoSapPedidoCompraItem));
        comando.Parameters.Add(ParametroTexto("@numero_item", item.NumeroItem));
        comando.Parameters.Add(ParametroTextoNulo("@material", item.Material));
        comando.Parameters.Add(ParametroTextoNulo("@centro", item.Centro));
        comando.Parameters.Add(ParametroTextoNulo("@deposito", item.Deposito));
        comando.Parameters.Add(ParametroTextoNulo("@unidade", item.Unidade));
        comando.Parameters.Add(ParametroNumericoNulo("@qtd_prevista", item.QuantidadePrevista));
        comando.Parameters.Add(ParametroNumericoNulo("@qtd_recebida", item.QuantidadeRecebida));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o item do lancamento.");
    }

    private async Task InserirPesagemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoItem, int sequencia, EntradaProdutoPesagem pesagem, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_pesagem
                (codigo_entrada_produto_item, sequencia, peso_bruto_kg, peso_tara_kg, peso_liquido_kg,
                 codigo_tara, codigo_balanca, origem, codigo_usuario, entrada_produto_pesagem_criado_por)
            VALUES
                (@item, @sequencia, @bruto, @tara, @liquido,
                 @codigo_tara, @codigo_balanca, @origem, @usuario, @usuario);
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        comando.Parameters.Add(ParametroInteiro("@sequencia", sequencia));
        comando.Parameters.Add(new NpgsqlParameter("@bruto", NpgsqlDbType.Numeric) { Value = pesagem.PesoBrutoKg });
        comando.Parameters.Add(new NpgsqlParameter("@tara", NpgsqlDbType.Numeric) { Value = pesagem.PesoTaraKg });
        comando.Parameters.Add(new NpgsqlParameter("@liquido", NpgsqlDbType.Numeric) { Value = pesagem.PesoLiquidoKg });
        comando.Parameters.Add(ParametroLongoNulo("@codigo_tara", pesagem.CodigoTara));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_balanca", pesagem.CodigoBalanca));
        comando.Parameters.Add(ParametroTexto("@origem", string.IsNullOrWhiteSpace(pesagem.Origem) ? "BALANCA" : pesagem.Origem));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlParameter ParametroTextoNulo(string nome, string? valor)
        => new(nome, NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim() };

    private static NpgsqlParameter ParametroNumericoNulo(string nome, decimal? valor)
        => new(nome, NpgsqlDbType.Numeric) { Value = valor.HasValue ? valor.Value : DBNull.Value };

    private static NpgsqlParameter ParametroUsuarioObrigatorio(string nome, long? usuario)
        => new(nome, NpgsqlDbType.Bigint)
        {
            Value = usuario ?? throw new InvalidOperationException("Usuario da sessao obrigatorio para gravar a entrada.")
        };
}
