using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Quando avisar o operador para trocar a caixa, no processo ENVIO_UNICO.
/// <para>
/// E um aviso de ritmo, nao uma trava: a cada 24 caixas pesadas o operador confirma e segue. Nada
/// aqui bloqueia pesagem, envio ou qualquer outra coisa — quem usa esta regra so decide se mostra
/// uma mensagem.
/// </para>
/// </summary>
public static class AvisoTrocaDeCaixaEnvioUnico
{
    /// <summary>
    /// De quantas em quantas caixas o aviso aparece.
    /// <para>
    /// NUMERO DADO PELO USUARIO em 23/11/2026, nao derivado do sistema. A norma de embalagem traz
    /// <c>QuantidadeProdutosPorCaixa</c> — produtos DENTRO de uma caixa — e nada sobre quantas caixas
    /// cabem no recipiente que se troca. Enquanto nao houver essa origem, o valor fica aqui, visivel
    /// e com a procedencia escrita, em vez de disfarcado de calculo.
    /// </para>
    /// </summary>
    public const int CaixasPorTroca = 24;

    /// <summary>
    /// Caixas que CONTAM para o aviso: as que existem fisicamente na mao do operador.
    /// <para>
    /// Cancelada NAO conta. O aviso existe porque o recipiente encheu; uma caixa cancelada nao foi
    /// para dentro dele, e conta-la adiantaria o aviso para antes da hora.
    /// </para>
    /// </summary>
    public static bool Conta(ProdutoAcabadoCaixa caixa)
        => caixa is not null && caixa.StatusIntegracao != StatusIntegracaoCaixa.Cancelada;

    /// <summary>
    /// Deve avisar agora? Verdadeiro quando o processo e ENVIO_UNICO e a contagem acabou de completar
    /// um multiplo de <see cref="CaixasPorTroca"/>.
    /// <para>
    /// A decisao sai da CONTAGEM, nao de um contador proprio que precisasse ser zerado. Contador
    /// separado erra assim que uma caixa e cancelada ou a tela e reaberta com caixas recuperadas do
    /// banco; a contagem nao erra, porque e recalculada do estado real a cada pesagem.
    /// </para>
    /// </summary>
    public static bool DeveAvisar(ProcessoPesagemProduto? processo, IEnumerable<ProdutoAcabadoCaixa> caixas)
    {
        if (processo != ProcessoPesagemProduto.EnvioUnico || caixas is null)
        {
            return false;
        }

        int pesadas = caixas.Count(Conta);
        return pesadas > 0 && pesadas % CaixasPorTroca == 0;
    }

    /// <summary>Texto do aviso, nomeando quantas caixas ja foram.</summary>
    public static string Mensagem(int caixasPesadas)
        => $"{caixasPesadas} caixas pesadas. Troque a caixa para continuar.";
}
