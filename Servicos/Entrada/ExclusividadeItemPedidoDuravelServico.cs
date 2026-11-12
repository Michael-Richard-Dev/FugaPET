using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Entrada;
using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// P5/056: exclusividade DURAVEL por pedido+item, apoiada na chave primaria de
/// <c>homologacao.entrada_mp_p5_exclusividade_item_pedido</c>.
/// <para>
/// A garantia vem do banco, nao do processo: o INSERT concorrente de um segundo lancamento
/// para o mesmo pedido+item viola a chave primaria e perde. Isso vale entre lancamentos,
/// entre instancias e entre reinicializacoes — que e exatamente o que a reserva por
/// lancamento nao garantia.
/// </para>
/// <para>
/// A marca NAO e removida apos o envio. Uma falha DEPOIS do POST deixa a linha gravada, de
/// modo que um reenvio cego encontra <see cref="CenarioExclusividadeItemPedido.EmUsoPorOutroLancamento"/>
/// em vez de disparar um segundo movimento 101.
/// </para>
/// FAIL-CLOSED: qualquer erro, ausencia da tabela ou chave malformada resolve como
/// <see cref="CenarioExclusividadeItemPedido.Indisponivel"/>, que BLOQUEIA. Nunca "obtida" por omissao.
/// </summary>
public sealed class ExclusividadeItemPedidoDuravelServico : IExclusividadeItemPedidoServico
{
    public const string Tabela = "homologacao.entrada_mp_p5_exclusividade_item_pedido";

    private readonly IFabricaConexaoBanco _fabrica;
    private readonly Action<string>? _registrarDiagnostico;
    private readonly bool _disponivel;

    public ExclusividadeItemPedidoDuravelServico(
        IFabricaConexaoBanco fabricaConexaoBanco,
        bool bancoHabilitado,
        Action<string>? registrarDiagnostico = null)
    {
        _fabrica = fabricaConexaoBanco ?? throw new ArgumentNullException(nameof(fabricaConexaoBanco));
        _registrarDiagnostico = registrarDiagnostico;
        _disponivel = bancoHabilitado;
    }

    /// <summary>
    /// Declara disponibilidade apenas quando o banco esta habilitado. A existencia fisica da
    /// tabela e verificada na propria tentativa: ausente ⇒ Indisponivel, nunca "obtida".
    /// </summary>
    public bool Disponivel => _disponivel;

    public async Task<ResultadoExclusividadeItemPedido> TentarObterAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        if (!_disponivel)
        {
            return ResultadoExclusividadeItemPedido.Indisponivel(
                "banco desabilitado neste ambiente");
        }

        if (!TentarNormalizar(numeroPedido, numeroItemPedido, codigoLancamento,
                out string pedido, out string item, out string motivoChave))
        {
            return ResultadoExclusividadeItemPedido.Indisponivel(motivoChave);
        }

        try
        {
            await using NpgsqlConnection conexao = await _fabrica.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);

            // O INSERT e a propria trava: ON CONFLICT DO NOTHING nao devolve linha quando
            // outro lancamento ja detem o par pedido+item.
            long? titular;
            await using (var comando = new NpgsqlCommand(
                $"""
                 WITH tentativa AS (
                     INSERT INTO {Tabela} (numero_pedido, numero_item, codigo_lancamento)
                     VALUES (@pedido, @item, @lancamento)
                     ON CONFLICT (numero_pedido, numero_item) DO NOTHING
                     RETURNING codigo_lancamento
                 )
                 SELECT codigo_lancamento FROM tentativa
                 UNION ALL
                 SELECT e.codigo_lancamento FROM {Tabela} e
                  WHERE e.numero_pedido = @pedido AND e.numero_item = @item
                  LIMIT 1;
                 """, conexao, transacao) { CommandTimeout = 10 })
            {
                comando.Parameters.AddWithValue("pedido", pedido);
                comando.Parameters.AddWithValue("item", item);
                comando.Parameters.AddWithValue("lancamento", codigoLancamento);
                object? bruto = await comando.ExecuteScalarAsync(cancellationToken);
                titular = bruto is long valor ? valor : null;
            }

            await transacao.CommitAsync(cancellationToken);

            if (titular is null)
            {
                // Nem inseriu nem encontrou titular: estado nao explicado, portanto bloqueia.
                Registrar($"P5/056: exclusividade sem titular identificado para {pedido}/{item}.");
                return ResultadoExclusividadeItemPedido.Indisponivel(
                    "nao foi possivel determinar o titular da exclusividade deste item");
            }

            if (titular.Value == codigoLancamento)
            {
                Registrar($"P5/056: exclusividade de {pedido}/{item} com o lancamento {codigoLancamento}.");
                return ResultadoExclusividadeItemPedido.Obtida();
            }

            Registrar($"P5/056: {pedido}/{item} ja pertence ao lancamento {titular.Value}.");
            return ResultadoExclusividadeItemPedido.EmUso(
                $"o lancamento {titular.Value} ja detem a exclusividade deste item do pedido");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            Registrar("P5/056: tabela de exclusividade ausente no banco; incremento 056 nao aplicado.");
            return ResultadoExclusividadeItemPedido.Indisponivel(
                "estrutura duravel de exclusividade ausente (incremento 056 nao aplicado)");
        }
        catch (Exception ex)
        {
            // Mensagem tecnica fica no diagnostico; o operador recebe apenas o bloqueio.
            Registrar("P5/056: falha ao obter exclusividade: " + ex.GetType().Name);
            return ResultadoExclusividadeItemPedido.Indisponivel(
                "falha ao consultar ou registrar a exclusividade duravel deste item");
        }
    }

    /// <summary>
    /// Normaliza a chave do mesmo jeito que a consulta local D2 (item em 5 digitos). Chave
    /// fora do formato nao vira tentativa: viraria uma trava sobre uma identidade errada.
    /// </summary>
    internal static bool TentarNormalizar(
        string? numeroPedido,
        string? numeroItemPedido,
        long codigoLancamento,
        out string pedido,
        out string item,
        out string motivo)
    {
        pedido = (numeroPedido ?? string.Empty).Trim();
        item = (numeroItemPedido ?? string.Empty).Trim();
        motivo = string.Empty;

        if (codigoLancamento <= 0)
        {
            motivo = "codigo de lancamento invalido para registrar exclusividade";
            return false;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(pedido, "^[0-9]{1,10}$"))
        {
            motivo = "numero de pedido fora do formato esperado";
            return false;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(item, "^[0-9]{1,5}$"))
        {
            motivo = "numero de item fora do formato esperado";
            return false;
        }

        item = item.TrimStart('0');
        item = (item.Length == 0 ? "0" : item).PadLeft(5, '0');
        return true;
    }

    private void Registrar(string mensagem) => _registrarDiagnostico?.Invoke(mensagem);
}
