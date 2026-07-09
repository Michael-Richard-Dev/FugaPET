using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Tarefa Entrada 24.1: totais por classificação dos itens do pedido (diagnóstico/decisão).</summary>
public sealed record TotaisClassificacaoEntrada
{
    public int Total { get; init; }
    public int MateriaPrima { get; init; }
    public int Quimico { get; init; }
    public int OutroOuIndefinido { get; init; }
    public int DoModo { get; init; }
}

/// <summary>
/// Tarefa Entrada 24.1 (Ajustes 9/10/11/15): filtro dos itens do Pedido de Compra pelo modo da tela e
/// mensagens de bloqueio. Pedido misto NÃO é bloqueado inteiro (Ajuste 10) — só filtra; bloqueia apenas
/// quando NENHUM item do modo sobra (Ajuste 9) ou quando todos ficaram Indefinidos (Ajuste 15).
/// </summary>
public static class FiltroItensEntradaMaterial
{
    /// <summary>Ajuste 9/10/11: apenas os itens cuja classificação corresponde ao modo atual.</summary>
    public static IReadOnlyList<PedidoCompraSapItem> FiltrarItensPorModo(
        IEnumerable<PedidoCompraSapItem> itens,
        ModoEntradaMaterial modo)
        => (itens ?? []).Where(item =>
            ClassificadorItemEntradaMaterial.ItemPertenceAoModo(item.ClassificacaoEntrada, modo)).ToList();

    public static TotaisClassificacaoEntrada ContarClassificacoes(
        IReadOnlyList<PedidoCompraSapItem> itens,
        ModoEntradaMaterial modo)
    {
        int materiaPrima = itens.Count(i => i.ClassificacaoEntrada == ClassificacaoEntradaMaterial.MateriaPrima);
        int quimico = itens.Count(i => i.ClassificacaoEntrada == ClassificacaoEntradaMaterial.Quimico);
        return new TotaisClassificacaoEntrada
        {
            Total = itens.Count,
            MateriaPrima = materiaPrima,
            Quimico = quimico,
            OutroOuIndefinido = itens.Count - materiaPrima - quimico,
            DoModo = FiltrarItensPorModo(itens, modo).Count
        };
    }

    /// <summary>Ajuste 15: todos os itens ficaram Indefinidos (Product Master não classificou nenhum).</summary>
    public static bool TodosIndefinidos(IReadOnlyList<PedidoCompraSapItem> itens)
        => itens.Count > 0 && itens.All(i => i.ClassificacaoEntrada == ClassificacaoEntradaMaterial.Indefinido);

    public const string MensagemTodosIndefinidos =
        "Não foi possível classificar os itens do pedido pelo tipo de material SAP. Verifique a API_PRODUCT_SRV.";

    public const string MensagemItemForaDoModo =
        "Item não pertence ao módulo atual de Entrada.";

    /// <summary>Ajuste 9: mensagem por modo quando o pedido não possui item compatível.</summary>
    public static string MontarMensagemSemItensDoModo(
        ModoEntradaMaterial modo,
        string numeroPedido,
        int totalItens)
    {
        string pedido = string.IsNullOrWhiteSpace(numeroPedido) ? "(não informado)" : numeroPedido.Trim();
        bool quimico = modo == ModoEntradaMaterial.Quimico;
        string tipo = quimico ? "Entrada de Químicos" : "Entrada de Matéria-Prima";
        string telaAlternativa = quimico ? "Entrada de Matéria-Prima" : "Entrada de Químicos";
        string rotulo = quimico ? "Itens de químicos encontrados" : "Itens de matéria-prima encontrados";

        return $"Este Pedido de Compra não possui itens classificados para {tipo}.\r\n\r\n"
            + $"Pedido: {pedido}\r\n"
            + $"Itens encontrados: {totalItens}\r\n"
            + $"{rotulo}: 0\r\n\r\n"
            + $"Use a tela de {telaAlternativa} ou verifique o tipo do material no SAP.";
    }
}
