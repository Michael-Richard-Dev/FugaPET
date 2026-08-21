using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Apresentação controlada do resultado do envio de palete (INT012) para a View.</summary>
public sealed record ApresentacaoEnvioPaleteInt012(bool Sucesso, string Titulo, string Mensagem);

/// <summary>
/// GATE 046-AP: presenter PURO do resultado do envio de palete INT012. Garante que TODO desfecho — inclusive
/// falhas PRÉ-CLAIM (write gate off, orquestrador indisponível, snapshot/preview inválido, contrato pendente,
/// exceção) — vire uma mensagem CONTROLADA e não vazia para a View, para que nada "suma silenciosamente"
/// após a confirmação do operador. Não faz UI, não chama SAP/CPI.
/// </summary>
public static class ProdutoAcabadoPaleteEnvioPresenter
{
    public const string TituloPadrao = "Produto Acabado — Palete INT012";

    public static ApresentacaoEnvioPaleteInt012 Construir(string? codigoPaleteLocal, ResultadoPaleteInt012 resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        string codigo = string.IsNullOrWhiteSpace(codigoPaleteLocal) ? "(sem código)" : codigoPaleteLocal!;

        if (resultado.Estado == EstadoPaleteInt012.Confirmado)
        {
            return new ApresentacaoEnvioPaleteInt012(
                true, TituloPadrao, $"Palete {codigo} confirmado no SAP (UC {resultado.UcGerada}).");
        }

        // GATE 046-AQ-Z2-C: POST confirmado no SAP porém fechamento local não comprovado ⇒ INDETERMINADO
        // (não é sucesso integral, não é "não enviado"): preserva a UC e orienta reconciliação, sem reenvio.
        if (resultado.Estado == EstadoPaleteInt012.IndeterminadoTimeout)
        {
            string ucNota = string.IsNullOrWhiteSpace(resultado.UcGerada)
                ? string.Empty
                : $" UC {resultado.UcGerada} preservada para reconciliação.";
            return new ApresentacaoEnvioPaleteInt012(false, TituloPadrao,
                $"Palete {codigo} em estado INDETERMINADO após o envio: {DetalheOuPadrao(resultado.MensagemSanitizada)}{ucNota} NÃO reenviar.");
        }

        // Qualquer outro estado != Confirmado (inclui NaoEnviado pré-claim) ⇒ mensagem explícita e não vazia.
        return new ApresentacaoEnvioPaleteInt012(
            false, TituloPadrao,
            $"Palete {codigo} NÃO enviado ao SAP ({resultado.Estado}): {DetalheOuPadrao(resultado.MensagemSanitizada)}");
    }

    public static ApresentacaoEnvioPaleteInt012 ParaExcecao(string? codigoPaleteLocal)
    {
        string codigo = string.IsNullOrWhiteSpace(codigoPaleteLocal) ? "(sem código)" : codigoPaleteLocal!;
        return new ApresentacaoEnvioPaleteInt012(
            false, TituloPadrao,
            $"Palete {codigo}: falha inesperada ANTES do envio. Nada foi enviado ao SAP. "
            + "Verifique o estado do palete e tente novamente.");
    }

    private static string DetalheOuPadrao(string? mensagem)
        => string.IsNullOrWhiteSpace(mensagem)
            ? "bloqueado antes do claim/POST, sem detalhe do fluxo."
            : mensagem!;
}
