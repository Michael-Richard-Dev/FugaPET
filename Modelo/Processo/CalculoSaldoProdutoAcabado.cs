namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Saldo pendente exibido: base SAP menos quantidade de produção das caixas locais elegíveis,
/// somente quando as unidades coincidem. Caixas confirmadas no SAP não são abatidas novamente.
/// </summary>
public static class CalculoSaldoProdutoAcabado
{
    /// <summary>
    /// A CAIXA AINDA ESTA FORA DO SALDO DO SAP? Só estas abatem — as demais o SAP já contou (ou não
    /// contam nunca, no caso das canceladas).
    /// <para>
    /// MEDIDO em 27/11/2026, na OP 1000452 do mandante 110: as duas caixas CONFIRMADA_SAP postaram
    /// entrada de mercadoria de verdade (101 nos documentos 5000001277 e 5000001278, 8 UN cada,
    /// movimento 101 na OP), e o saldo que o SAP devolve — 40 recebidas de 56 — JA inclui as duas.
    /// Abater de novo seria contar a mesma produção duas vezes.
    /// </para>
    /// </summary>
    public static bool AindaNaoContadaNoSap(ProdutoAcabadoCaixa caixa)
    {
        ArgumentNullException.ThrowIfNull(caixa);

        return caixa.StatusIntegracao is StatusIntegracaoCaixa.FinalizadaLocal
            or StatusIntegracaoCaixa.PreviewHuGerado
            or StatusIntegracaoCaixa.AguardandoAutorizacaoSap
            or StatusIntegracaoCaixa.ProntaParaEnvio;
    }

    /// <summary>
    /// Quantidade de PRODUTO das caixas que o SAP ainda não contou, na unidade de produção da OP.
    /// Caixa com unidade diferente da OP não entra — comparar unidades diferentes é o erro que ja
    /// barrou producao legitima nesta tela.
    /// </summary>
    public static decimal ProdutosAindaNaoContadosNoSap(
        IEnumerable<ProdutoAcabadoCaixa> caixas,
        string? unidadeProducao)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        string unidade = (unidadeProducao ?? string.Empty).Trim().ToUpperInvariant();

        decimal total = 0m;
        foreach (ProdutoAcabadoCaixa caixa in caixas)
        {
            if (!AindaNaoContadaNoSap(caixa) || caixa.QuantidadeProdutos <= 0 || unidade.Length == 0)
            {
                continue;
            }

            if ((caixa.UnidadeQuantidade ?? string.Empty).Trim().ToUpperInvariant() == unidade)
            {
                total += caixa.QuantidadeProdutos;
            }
        }

        return total;
    }

    /// <summary>Peso liquido das caixas que o SAP ainda não contou, para a OP medida em peso.</summary>
    public static decimal PesoLiquidoAindaNaoContadoNoSap(IEnumerable<ProdutoAcabadoCaixa> caixas)
    {
        ArgumentNullException.ThrowIfNull(caixas);

        decimal total = 0m;
        foreach (ProdutoAcabadoCaixa caixa in caixas)
        {
            if (AindaNaoContadaNoSap(caixa))
            {
                total += caixa.PesoLiquidoKg;
            }
        }

        return total;
    }

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
