using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Quando uma caixa impede seguir: pesar a próxima, trocar a ordem, fechar a tela.
/// <para>
/// Existe como regra separada porque ela MUDA com o processo de pesagem, e a versão antiga — "qualquer
/// caixa que não esteja confirmada ou cancelada" — juntava duas coisas que só pareciam uma enquanto
/// existia um processo só:
/// </para>
/// <list type="number">
/// <item><b>Uma caixa por vez na balança.</b> Física, vale nos dois processos.</item>
/// <item><b>Não deixe caixa finalizada sem integrar.</b> Do caixa a caixa, onde cada caixa vai ao SAP
/// antes da próxima.</item>
/// </list>
/// <para>
/// O envio único existe para acumular caixas finalizadas e sem integrar. Sob a regra antiga a SEGUNDA
/// caixa já era barrada, e o processo ficava impossível de operar — foi o que o usuário encontrou em
/// 19/11/2026, com 25 caixas pesadas e a pesagem travada.
/// </para>
/// </summary>
public static class RegraBloqueioPesagemProdutoAcabado
{
    /// <summary>
    /// Estados que exigem DECISÃO HUMANA e por isso impedem seguir em qualquer processo.
    /// <para>
    /// Empilhar caixas sobre um destes esconderia o problema: um envio em curso ou um resultado
    /// indeterminado precisa ser resolvido antes de a operação continuar, não depois de mais dez caixas.
    /// </para>
    /// </summary>
    public static bool ExigeDecisaoHumana(StatusIntegracaoCaixa status)
        => status is StatusIntegracaoCaixa.EmPesagem
            or StatusIntegracaoCaixa.EnviandoSap
            or StatusIntegracaoCaixa.IndeterminadoTimeout
            or StatusIntegracaoCaixa.Bloqueada
            or StatusIntegracaoCaixa.ErroSap;

    /// <summary>
    /// A caixa neste estado impede seguir, dado o processo? <c>null</c> em processo = produto sem
    /// vínculo, que segue a regra histórica.
    /// </summary>
    public static bool Impede(ProcessoPesagemProduto? processo, StatusIntegracaoCaixa status)
        => processo == ProcessoPesagemProduto.EnvioUnico
            ? ExigeDecisaoHumana(status)
            // Caixa a caixa: a regra de sempre — tudo que não está confirmado nem cancelado segura.
            : status is not StatusIntegracaoCaixa.ConfirmadaSap and not StatusIntegracaoCaixa.Cancelada;

    /// <summary>Alguma das caixas impede seguir?</summary>
    public static bool ImpedeSeguir(ProcessoPesagemProduto? processo, IEnumerable<ProdutoAcabadoCaixa> caixas)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        return caixas.Any(c => Impede(processo, c.StatusIntegracao));
    }

    /// <summary>
    /// Os status que impedem, escritos como o BANCO os guarda, para a consulta por terminal.
    /// <para>
    /// Existe para a consulta perguntar "existe caixa que me impede?" em vez de "qual a última caixa
    /// não confirmada?". A diferença aparece no envio único: ali a última pendente é inofensiva, e uma
    /// caixa em ERRO_SAP de uma ordem anterior, no mesmo terminal, passaria despercebida.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> StatusQueImpedemNoBanco(ProcessoPesagemProduto? processo)
        => [.. Enum.GetValues<StatusIntegracaoCaixa>()
            .Where(status => Impede(processo, status))
            .Select(MapeadorStatusHuCaixa.ParaTextoBanco)];

    /// <summary>A primeira caixa que impede seguir, para a mensagem poder nomeá-la.</summary>
    public static ProdutoAcabadoCaixa? PrimeiraQueImpede(
        ProcessoPesagemProduto? processo, IEnumerable<ProdutoAcabadoCaixa> caixas)
    {
        ArgumentNullException.ThrowIfNull(caixas);
        return caixas.FirstOrDefault(c => Impede(processo, c.StatusIntegracao));
    }
}
