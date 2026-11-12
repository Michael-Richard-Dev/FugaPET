namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Saldo pendente exibido: base SAP menos quantidade de produção das caixas locais elegíveis,
/// somente quando as unidades coincidem. Caixas confirmadas no SAP não são abatidas novamente.
/// </summary>
public static class CalculoSaldoProdutoAcabado
{
    public static decimal SaldoPendenteExibido(
        decimal quantidadePendenteOp,
        IEnumerable<ProdutoAcabadoCaixa> caixas,
        string? unidadeProducao)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        string unidade = (unidadeProducao ?? string.Empty).Trim().ToUpperInvariant();
        decimal quantidadeLocal = 0m;
        foreach (ProdutoAcabadoCaixa caixa in caixas)
        {
            if (caixa.StatusIntegracao is not (StatusIntegracaoCaixa.FinalizadaLocal
                or StatusIntegracaoCaixa.PreviewHuGerado
                or StatusIntegracaoCaixa.AguardandoAutorizacaoSap
                or StatusIntegracaoCaixa.ProntaParaEnvio))
            {
                continue;
            }

            string unidadeCaixa = (caixa.UnidadeQuantidade ?? string.Empty).Trim().ToUpperInvariant();
            if (caixa.QuantidadeProdutos <= 0 || unidade.Length == 0 || unidadeCaixa != unidade)
            {
                System.Diagnostics.Trace.TraceWarning("PA121B: caixa local sem quantidade/unidade de producao compativel; abatimento nao aplicado.");
                continue;
            }

            quantidadeLocal += caixa.QuantidadeProdutos;
        }

        return Math.Max(quantidadePendenteOp - quantidadeLocal, 0m);
    }
}
