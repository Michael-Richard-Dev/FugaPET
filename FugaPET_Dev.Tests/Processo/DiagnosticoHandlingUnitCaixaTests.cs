using FugaPET_Dev.Modelo.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Por que a HU bipada na Paletizacao nao entrou (23/11/2026).
/// <para>
/// CONTEXTO QUE JUSTIFICA ISTO: o usuario relatou que "caixa pesada em outra maquina nao aparece".
/// A investigacao REFUTOU a premissa — nem a funcao de busca, nem a de intervalo, nem a view
/// `vw_hu_caixas_sem_palete` mencionam terminal, e no DEV110 caixas de OUTROS terminais sao
/// encontradas normalmente. A HU que nao aparecia (300016055) ja estava no palete 7.
/// </para>
/// <para>
/// O defeito real era a MENSAGEM: todos os casos diziam "HU nao encontrada", entao "ja paletizada",
/// "nao confirmada no SAP" e "nao existe" eram indistinguiveis, e o operador so podia bipar de novo.
/// </para>
/// </summary>
public sealed class DiagnosticoHandlingUnitCaixaTests
{
    private static DiagnosticoHandlingUnitCaixa Diag(
        MotivoHuIndisponivel motivo, long? palete = null, string status = "CONFIRMADA_SAP") => new()
        {
            HandlingUnit = "300016055",
            Motivo = motivo,
            StatusCaixa = status,
            CodigoPalete = palete,
            NumeroCaixa = 38,
            NumeroOrdemProducao = "1002762",
            Material = "4000548",
            Terminal = "TERMINAL_TI_TESTE",
        };

    // ---------------------------------------------------------------- ordem das regras

    /// <summary>
    /// A ordem espelha a da view: inativa e "nao confirmada" excluem ANTES do vinculo de palete.
    /// Invertida, uma caixa CANCELADA que esteve num palete seria relatada como "ja paletizada", e o
    /// operador iria procurar no palete em vez de olhar a caixa.
    /// </summary>
    [Fact]
    public void InativaVenceOPalete()
        => Assert.Equal(
            MotivoHuIndisponivel.Inativa,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: false, "CONFIRMADA_SAP", codigoPalete: 7));

    [Fact]
    public void NaoConfirmadaVenceOPalete()
        => Assert.Equal(
            MotivoHuIndisponivel.NaoConfirmadaNoSap,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: true, "EM_PESAGEM", codigoPalete: 7));

    [Fact]
    public void ConfirmadaEAtivaComPalete_EhJaPaletizada()
        => Assert.Equal(
            MotivoHuIndisponivel.JaPaletizada,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: true, "CONFIRMADA_SAP", codigoPalete: 7));

    /// <summary>Caso real do DEV110: a caixa 22, de OUTRO terminal, está disponível.</summary>
    [Fact]
    public void ConfirmadaEAtivaSemPalete_EstaDisponivel()
        => Assert.Equal(
            MotivoHuIndisponivel.Disponivel,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: true, "CONFIRMADA_SAP", codigoPalete: null));

    [Theory]
    [InlineData("confirmada_sap")]
    [InlineData("  CONFIRMADA_SAP  ")]
    public void StatusComparaSemCaixaAltaEComEspacos(string status)
        => Assert.Equal(
            MotivoHuIndisponivel.Disponivel,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: true, status, codigoPalete: null));

    [Fact]
    public void StatusNuloNaoEhConfirmado()
        => Assert.Equal(
            MotivoHuIndisponivel.NaoConfirmadaNoSap,
            DiagnosticoHandlingUnitCaixa.DecidirMotivo(ativa: true, null, codigoPalete: null));

    // ---------------------------------------------------------------- mensagens

    /// <summary>Caso que originou a tarefa: a mensagem precisa dizer QUAL palete.</summary>
    [Fact]
    public void JaPaletizada_DizQualPalete()
    {
        string msg = Diag(MotivoHuIndisponivel.JaPaletizada, palete: 7).Mensagem();

        Assert.Contains("palete 7", msg, StringComparison.Ordinal);
        Assert.Contains("300016055", msg, StringComparison.Ordinal);
        Assert.Contains("caixa 38", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void NaoConfirmada_DizOStatusEOQueFazer()
    {
        string msg = Diag(MotivoHuIndisponivel.NaoConfirmadaNoSap, status: "AGUARDANDO_AUTORIZACAO_SAP").Mensagem();

        Assert.Contains("AGUARDANDO_AUTORIZACAO_SAP", msg, StringComparison.Ordinal);
        Assert.Contains("Envie a caixa ao SAP", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Desconhecida_NaoAfirmaQueACaixaExiste()
    {
        string msg = Diag(MotivoHuIndisponivel.Desconhecida).Mensagem();

        Assert.Contains("não existe neste sistema", msg, StringComparison.Ordinal);
        // Nao pode citar caixa/OP: nao ha linha, e os campos do dublê seriam invencao.
        Assert.DoesNotContain("caixa 38", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("1002762", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Inativa_DizCancelada()
        => Assert.Contains("cancelada", Diag(MotivoHuIndisponivel.Inativa).Mensagem(), StringComparison.Ordinal);

    [Fact]
    public void Disponivel_NaoGeraMensagem()
        => Assert.Equal(string.Empty, Diag(MotivoHuIndisponivel.Disponivel).Mensagem());

    /// <summary>
    /// O TERMINAL nao pode aparecer como justificativa da recusa. Ele NAO entra na regra — verificado
    /// nas tres camadas — e cita-lo faria o operador concluir que a maquina importa, que foi
    /// exatamente a crenca errada que originou esta tarefa.
    /// </summary>
    [Theory]
    [InlineData(MotivoHuIndisponivel.JaPaletizada)]
    [InlineData(MotivoHuIndisponivel.NaoConfirmadaNoSap)]
    [InlineData(MotivoHuIndisponivel.Inativa)]
    [InlineData(MotivoHuIndisponivel.Desconhecida)]
    public void NenhumaMensagemCulpaOTerminal(MotivoHuIndisponivel motivo)
    {
        string msg = Diag(motivo, palete: 7).Mensagem();

        Assert.DoesNotContain("TERMINAL_TI_TESTE", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal", msg, StringComparison.OrdinalIgnoreCase);
    }
}
