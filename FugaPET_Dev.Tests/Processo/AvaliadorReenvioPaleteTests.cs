using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 126L: quem decide se um palete recusado pelo SAP pode ser enviado de novo.
/// <para>
/// A regra de seguranca e "prova de ausencia, nunca ausencia de prova". Os testes negativos valem
/// mais que os positivos aqui: liberar um palete que o SAP criou duplica estoque, e e um erro que
/// so aparece depois, no inventario.
/// </para>
/// </summary>
public sealed class AvaliadorReenvioPaleteTests
{
    // Resposta REAL do CPI no DEV110 em 16/11/2026: recusa de aplicacao, sem UC.
    private const string RecusaReal =
        """{"UC_gerada":"","Status":"E","_Mensagens":[{"Id":"ZPP_CREATEHU","UC_gerada":"","Id_Msg":"003","Tipo":"E","Texto":"Dados embalagem nao encontrado!"}]}""";

    [Fact]
    public void RecusaComprovadaDoSap_PodeLiberar()
    {
        AvaliacaoReenvioPalete a = AvaliadorReenvioPalete.Avaliar(
            "ERRO_SAP", podeReprocessar: false, handlingUnitExterna: null, huPalete: null,
            statusHttp: 500, respostaSanitizada: RecusaReal);

        Assert.Equal(SituacaoReenvioPalete.PodeLiberar, a.Situacao);
    }

    [Fact]
    public void JaLiberado_PodeEnviarDireto()
        => Assert.Equal(SituacaoReenvioPalete.PodeEnviar, AvaliadorReenvioPalete.Avaliar(
            "ERRO_SAP", podeReprocessar: true, null, null, 500, RecusaReal).Situacao);

    [Fact]
    public void Rascunho_PodeEnviar()
        => Assert.Equal(SituacaoReenvioPalete.PodeEnviar, AvaliadorReenvioPalete.Avaliar(
            "RASCUNHO", false, null, null, null, null).Situacao);

    /// <summary>
    /// O caso que nao pode falhar: HU gravada significa que o SAP CRIOU o palete. Nem mesmo com
    /// reprocessamento liberado ou com uma resposta de recusa no corpo isso pode virar envio —
    /// a verificacao de HU vem ANTES de qualquer outra.
    /// </summary>
    [Theory]
    [InlineData("ERRO_SAP", false)]
    [InlineData("ERRO_SAP", true)]
    [InlineData("RASCUNHO", true)]
    [InlineData("CONFIRMADO_SAP", false)]
    public void ComHuCriadaNoSap_SempreBloqueado(string status, bool podeReprocessar)
    {
        Assert.Equal(SituacaoReenvioPalete.Bloqueado, AvaliadorReenvioPalete.Avaliar(
            status, podeReprocessar, handlingUnitExterna: "300099999", huPalete: null,
            statusHttp: 500, respostaSanitizada: RecusaReal).Situacao);

        Assert.Equal(SituacaoReenvioPalete.Bloqueado, AvaliadorReenvioPalete.Avaliar(
            status, podeReprocessar, handlingUnitExterna: null, huPalete: "UC-123",
            statusHttp: 500, respostaSanitizada: RecusaReal).Situacao);
    }

    /// <summary>
    /// Corpo vazio, timeout ou corpo inconclusivo NAO provam ausencia. Era o caso do palete 3 no
    /// DEV110: HTTP 500 com resposta vazia, que poderia ter criado a HU.
    /// </summary>
    [Theory]
    [InlineData(500, null)]
    [InlineData(500, "")]
    [InlineData(500, "   ")]
    [InlineData(500, "INT012 retornou HTTP 500.")]
    [InlineData(null, "Timeout no POST do INT012.")]
    [InlineData(500, """{"Status":"E"}""")]
    public void SemProvaDeAusencia_Bloqueado(int? http, string? resposta)
        => Assert.Equal(SituacaoReenvioPalete.Bloqueado, AvaliadorReenvioPalete.Avaliar(
            "ERRO_SAP", false, null, null, http, resposta).Situacao);

    /// <summary>Status "E" com UC PREENCHIDA e recusa parcial com objeto criado: jamais libera.</summary>
    [Fact]
    public void StatusErroComUcPreenchida_Bloqueado()
        => Assert.Equal(SituacaoReenvioPalete.Bloqueado, AvaliadorReenvioPalete.Avaliar(
            "ERRO_SAP", false, null, null, 500,
            """{"UC_gerada":"300099999","Status":"E","_Mensagens":[]}""").Situacao);

    [Theory]
    [InlineData("ENVIADO_SAP")]
    [InlineData("INDETERMINADO")]
    [InlineData("INDETERMINADO_TIMEOUT")]
    [InlineData("CONFIRMADO_SAP")]
    [InlineData("CANCELADO")]
    [InlineData("")]
    [InlineData("ESTADO_QUE_NAO_EXISTE")]
    public void EstadosQueNaoAdmitemNovoEnvio(string status)
        => Assert.Equal(SituacaoReenvioPalete.Bloqueado, AvaliadorReenvioPalete.Avaliar(
            status, podeReprocessar: true, null, null, 500, RecusaReal).Situacao);

    [Fact]
    public void ProvaDeAusencia_ToleraEspacosECaixa()
    {
        Assert.True(AvaliadorReenvioPalete.AusenciaComprovada(500,
            """{ "status" : "e" , "uc_gerada" : "" }"""));
        Assert.True(AvaliadorReenvioPalete.AusenciaComprovada(500, RecusaReal));
        Assert.False(AvaliadorReenvioPalete.AusenciaComprovada(500, """{"Status":"S","UC_gerada":""}"""));
    }
}
