namespace FugaPET_Dev.Modelo.Entrada;

public sealed class ValidadorDadosLoteEntrada
{
    private readonly Func<DateTime> _obterDataAtual;

    public ValidadorDadosLoteEntrada(Func<DateTime>? obterDataAtual = null)
    {
        _obterDataAtual = obterDataAtual ?? (() => DateTime.Today);
    }

    public ResultadoValidacaoLoteEntrada Validar(
        string? numeroLote,
        DateTime? dataFabricacao,
        DateTime? dataVencimento,
        EntradaProdutoItemEmMemoria? item = null)
    {
        string numeroNormalizado = DadosLoteEntrada.NormalizarNumeroLote(numeroLote);
        if (string.IsNullOrWhiteSpace(numeroNormalizado))
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Informe o número do lote.");
        }

        if (numeroNormalizado.Length > 10)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Número do lote deve ter no máximo 10 caracteres.");
        }

        if (!dataFabricacao.HasValue)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Informe a data de fabricação do lote.");
        }

        if (!dataVencimento.HasValue)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Informe a data de vencimento do lote.");
        }

        DateTime fabricacao = dataFabricacao.Value.Date;
        DateTime vencimento = dataVencimento.Value.Date;
        DateTime hoje = _obterDataAtual().Date;

        if (fabricacao > hoje)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Data de fabricação não pode ser futura.");
        }

        if (vencimento < fabricacao)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Data de vencimento não pode ser anterior à fabricação.");
        }

        if (vencimento < hoje)
        {
            return ResultadoValidacaoLoteEntrada.Reprovado("Lote vencido. Informe um lote dentro da validade.");
        }

        DadosLoteEntrada dados = new(numeroNormalizado, fabricacao, vencimento);

        if (item is not null)
        {
            EntradaProdutoLoteEmMemoria? existente = item.LocalizarLote(numeroNormalizado, fabricacao, vencimento);
            if (existente is not null)
            {
                return ResultadoValidacaoLoteEntrada.Aprovado(
                    dados,
                    "Lote já existente para este item e datas. O agrupamento atual será reutilizado.",
                    existente);
            }

            if (item.PossuiLoteComMesmoNumeroEDatasDiferentes(numeroNormalizado, fabricacao, vencimento))
            {
                return ResultadoValidacaoLoteEntrada.Reprovado(
                    "Já existe lote com este número para o item, mas com datas diferentes. Confira fabricação e vencimento.");
            }
        }

        return ResultadoValidacaoLoteEntrada.Aprovado(dados);
    }
}
