using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Resultado da validação/agregação LOCAL do palete (sem SAP, sem banco, sem mutação).</summary>
public sealed record ResultadoPaleteLocal(
    bool Sucesso,
    string Mensagem,
    int QuantidadeCaixas,
    decimal PesoBrutoKg,
    decimal PesoLiquidoKg,
    decimal TaraKg)
{
    public static ResultadoPaleteLocal Falha(string mensagem) => new(false, mensagem, 0, 0m, 0m, 0m);
}

/// <summary>
/// Regras LOCAIS do palete (independentes do SAP): elegibilidade das caixas + agregação determinística de
/// pesos/quantidade. PURO e testável — NÃO muta caixas (a vinculação/CodigoPaleteLocal só ocorre no
/// Controller após sucesso). Preserva decimais (nunca deriva peso de texto de UI).
/// </summary>
public static class ProdutoAcabadoPaleteLocalValidador
{
    public static ResultadoPaleteLocal ValidarEAgrupar(
        string numeroOrdem,
        IReadOnlyList<ProdutoAcabadoCaixa> selecionadas,
        IReadOnlyList<ProdutoAcabadoPalete> paletesExistentes,
        bool exigirMesmaOp = true)
    {
        ArgumentNullException.ThrowIfNull(selecionadas);
        ArgumentNullException.ThrowIfNull(paletesExistentes);

        if (selecionadas.Count == 0)
        {
            return ResultadoPaleteLocal.Falha("Nenhuma caixa selecionada para o palete.");
        }

        // Sem caixa duplicada na seleção (por código) e sem NumeroCaixa duplicado.
        if (selecionadas.Select(c => c.CodigoProdutoAcabadoCaixa).Distinct().Count() != selecionadas.Count
            || selecionadas.Select(c => c.NumeroCaixa).Distinct().Count() != selecionadas.Count)
        {
            return ResultadoPaleteLocal.Falha("Caixa duplicada na seleção do palete.");
        }

        // Contexto OP compatível. Fluxos antigos continuam exigindo mesma OP por default; a Paletização 047
        // libera esse ponto de forma explícita sem relaxar as demais regras de elegibilidade.
        if (exigirMesmaOp && selecionadas.Any(c => !string.Equals((c.NumeroOrdemProducao ?? string.Empty).Trim(),
                (numeroOrdem ?? string.Empty).Trim(), StringComparison.Ordinal)))
        {
            return ResultadoPaleteLocal.Falha("Caixa de OP diferente não pode entrar neste palete.");
        }

        // Somente CONFIRMADA_SAP.
        if (selecionadas.Any(c => c.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap))
        {
            return ResultadoPaleteLocal.Falha("Todas as caixas do palete precisam estar CONFIRMADA_SAP.");
        }

        // HU SAP individual obrigatória (sem fallback).
        if (selecionadas.Any(c => string.IsNullOrWhiteSpace(c.HandlingUnitExternalId)))
        {
            return ResultadoPaleteLocal.Falha("Caixa sem HU SAP individual não pode entrar no palete.");
        }

        // Não pode estar já paletizada localmente.
        if (selecionadas.Any(c => !string.IsNullOrWhiteSpace(c.CodigoPaleteLocal)))
        {
            return ResultadoPaleteLocal.Falha("Caixa já vinculada a um palete.");
        }

        // Mesma caixa não pode estar em outro palete existente.
        HashSet<long> jaEmPalete = paletesExistentes
            .SelectMany(p => p.Caixas)
            .Where(c => c.CodigoProdutoAcabadoCaixa is not null)
            .Select(c => c.CodigoProdutoAcabadoCaixa!.Value)
            .ToHashSet();
        if (selecionadas.Any(c => c.CodigoProdutoAcabadoCaixa is long id && jaEmPalete.Contains(id)))
        {
            return ResultadoPaleteLocal.Falha("Caixa já pertence a outro palete.");
        }

        // Agregação determinística (decimais preservados).
        return new ResultadoPaleteLocal(
            true,
            "Palete local válido.",
            selecionadas.Count,
            selecionadas.Sum(c => c.PesoBrutoKg),
            selecionadas.Sum(c => c.PesoLiquidoKg),
            selecionadas.Sum(c => c.TaraKg));
    }
}


