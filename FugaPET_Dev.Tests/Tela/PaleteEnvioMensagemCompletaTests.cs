using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 126K: a falha do envio de palete passou a ser mostrada em DIALOGO, porque a barra de status
/// corta o texto. O dialogo so serve para alguma coisa se a mensagem apresentada carregar a CAUSA
/// inteira — e por isso que o teste vive aqui, e nao no desenho da janela.
/// <para>
/// Caso real do DEV110 em 16/11/2026: o CPI devolveu HTTP 500 embrulhando um erro de aplicacao do
/// SAP (ZPP_CREATEHU/003, "Dados embalagem nao encontrado!"). Com a mensagem truncada, a unica
/// forma de ler isso era consultando hu_palete.erro_sanitizado no banco.
/// </para>
/// </summary>
public sealed class PaleteEnvioMensagemCompletaTests
{
    private const string RespostaRealDoCpi =
        "INT012 retornou HTTP 500. Resposta: {\"@odata.context\":\"../../$metadata#com.sap.gateway."
        + "srvd_a2x.zapi_pp_createhu_o4.v0001.ZDD_PP_CreateHUOutput\",\"UC_gerada\":\"\",\"Status\":\"E\","
        + "\"_Mensagens\":[{\"Id\":\"ZPP_CREATEHU\",\"UC_gerada\":\"\",\"Id_Msg\":\"003\",\"Tipo\":\"E\","
        + "\"Texto\":\"Dados embalagem nao encontrado!\"}]}";

    [Fact]
    public void MensagemApresentada_CarregaACausaDoSapPorInteiro()
    {
        ApresentacaoEnvioPaleteInt012 apresentacao = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "4", ResultadoPaleteInt012.ErroHttp(RespostaRealDoCpi, 500));

        Assert.False(apresentacao.Sucesso);
        Assert.Contains("ZPP_CREATEHU", apresentacao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("003", apresentacao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Dados embalagem nao encontrado!", apresentacao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("500", apresentacao.Mensagem, StringComparison.Ordinal);

        // O palete precisa ser identificavel: sem isso o operador nao sabe qual falhou.
        Assert.Contains("Palete 4", apresentacao.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// A mensagem do caso real tem mais de 300 caracteres. Se o presenter truncasse, o dialogo
    /// herdaria o mesmo problema da barra de status.
    /// </summary>
    [Fact]
    public void MensagemApresentada_NaoEhTruncadaPeloPresenter()
    {
        ApresentacaoEnvioPaleteInt012 apresentacao = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "4", ResultadoPaleteInt012.ErroHttp(RespostaRealDoCpi, 500));

        Assert.True(apresentacao.Mensagem.Length >= RespostaRealDoCpi.Length,
            $"mensagem ficou com {apresentacao.Mensagem.Length} caracteres, menos que a causa recebida");
        Assert.DoesNotContain("truncado", apresentacao.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Indeterminado tem tratamento proprio no dialogo (aviso, nao erro) porque o POST pode ter
    /// chegado. A orientacao de NAO reenviar precisa estar no texto que o operador le.
    /// </summary>
    [Fact]
    public void Indeterminado_OrientaNaoReenviarEPreservaAUc()
    {
        ApresentacaoEnvioPaleteInt012 apresentacao = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "4", ResultadoPaleteInt012.Indeterminado("Timeout no POST do INT012.") with { UcGerada = "UC-123" });

        Assert.False(apresentacao.Sucesso);
        Assert.Contains("INDETERMINADO", apresentacao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("NÃO reenviar", apresentacao.Mensagem, StringComparison.Ordinal);
        Assert.Contains("UC-123", apresentacao.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirmado nao abre dialogo; a mensagem segue sendo de sucesso, com a UC. O resultado e
    /// montado pelo PARSER real — a confirmacao nao tem fabrica propria de proposito: ela so pode
    /// nascer de uma resposta que o contrato aprovou.
    /// </summary>
    [Fact]
    public void Confirmado_ContinuaSendoSucessoComUc()
    {
        ResultadoPaleteInt012 confirmado = PaleteInt012ResponseParser.Parsear(
            """{"UC_gerada":"UC-999","Status":"S","_Mensagens":[]}""", 200);

        Assert.Equal(EstadoPaleteInt012.Confirmado, confirmado.Estado);

        ApresentacaoEnvioPaleteInt012 apresentacao =
            ProdutoAcabadoPaleteEnvioPresenter.Construir("4", confirmado);

        Assert.True(apresentacao.Sucesso);
        Assert.Contains("UC-999", apresentacao.Mensagem, StringComparison.Ordinal);
    }
}
