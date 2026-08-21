namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// REGRA 4 (fonte ÚNICA): saldo pendente EXIBIDO da OP de Produto Acabado. Parte do saldo pendente da OP
/// (origem SAP: <c>QuantidadePendente</c>) e abate SOMENTE a produção que EFETIVAMENTE concluiu o fluxo do
/// sistema — caixas CONFIRMADA_SAP. NÃO abate caixa cancelada, apenas pesada, aguardando autorização,
/// enviando, com erro, INDETERMINADO_TIMEOUT ou qualquer estado não confirmado. Regra pura e testável,
/// reutilizada pela Form no carregamento da OP e após cada confirmação (nunca duplicada no code-behind).
/// </summary>
public static class CalculoSaldoProdutoAcabado
{
    public static decimal SaldoPendenteExibido(
        decimal quantidadePendenteOp,
        IEnumerable<ProdutoAcabadoCaixa> caixas)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        decimal confirmado = caixas
            .Where(c => c.StatusIntegracao == StatusIntegracaoCaixa.ConfirmadaSap)
            .Sum(c => c.PesoLiquidoKg);
        return Math.Max(quantidadePendenteOp - confirmado, 0m);
    }
}
