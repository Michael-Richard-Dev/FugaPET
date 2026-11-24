using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// LANÇAMENTO MANUAL DO SEMI-ACABADO NO SAP (incremento 073, decisão do usuário em 24/11/2026).
/// <para>
/// Nas palavras dele: "quando chegar no processo de semi acabado fazer todo o processo normal, sair
/// a etiqueta bonitinha mas não fazer post no SAP, esse processo será feito manualmente dentro do
/// SAP". A tela continua pesando, imprimindo e persistindo; só não transmite.
/// </para>
/// </summary>
public sealed class SemiAcabadoLancamentoManualTests
{
    private static string Tela() => Fonte("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
    private static string Controlador() => Fonte("Controle", "Processo", "SemiAcabadoController.cs");
    private static string Leitor() => Fonte("Servicos", "IntegracaoSap", "LeitorConfiguracaoSap.cs");

    // ---------------------------------------------------------------- o padrão é não enviar

    /// <summary>
    /// O PADRÃO É NÃO ENVIAR. Uma configuração nova que já nasce ligada entregaria o oposto do
    /// pedido em qualquer ambiente onde ninguém a configurasse.
    /// </summary>
    [Fact]
    public void SemConfiguracaoNenhuma_OEnvioFicaDesligado()
        => Assert.False(new ConfiguracaoSap().SemiAcabadoEnvioSapHabilitado);

    /// <summary>
    /// O portão é ISOLADO: não depende de EscritaHabilitada nem dos gates de HU, palete ou PA.
    /// Amarrá-lo a outro faria religar um ligar o outro sem ninguém pedir.
    /// </summary>
    [Fact]
    public void OPortao_ETemChavePropria()
    {
        string leitor = Leitor();

        Assert.Contains("FUGAPET_SAP_SEMI_ACABADO_WRITE_ENABLED", leitor, StringComparison.Ordinal);
        Assert.Contains("semi_acabado_write_enabled", leitor, StringComparison.Ordinal);
        // Nem o perfil Q liga isso sozinho: a decisao e de negocio, nao de ambiente.
        int onde = leitor.IndexOf("SemiAcabadoEnvioSapHabilitado = ResolverGateOperacional", StringComparison.Ordinal);
        Assert.True(onde >= 0, "o gate nao e resolvido pelo leitor");
        int fim = leitor.IndexOf("),", onde, StringComparison.Ordinal);
        Assert.Contains("padraoPerfil: false", leitor[onde..fim], StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- o que a tela faz e não faz

    /// <summary>
    /// Com o portão fechado a tela PARA ANTES da cerimônia de habilitação: sem POST não há o que
    /// autorizar, e armar a capability gastaria uma habilitação one-shot sem uso, registrando na
    /// auditoria uma escrita que nunca foi tentada.
    /// </summary>
    [Fact]
    public void ComOPortaoFechado_NaoArmaCapability_NemEnvia()
    {
        string tela = Tela();

        int ondePara = tela.IndexOf(
            "if (preparo.Sucesso && !_controller.EnvioSemiAcabadoSapHabilitado)", StringComparison.Ordinal);
        Assert.True(ondePara >= 0, "o desvio do lancamento manual sumiu");

        int ondeCerimonia = tela.IndexOf("_habilitacaoEscritaSap.HabilitarParaEnvioAsync", StringComparison.Ordinal);
        int ondeEnvio = tela.IndexOf("_controller.EnviarPreparado101Async", StringComparison.Ordinal);

        Assert.True(ondeCerimonia > ondePara, "a cerimonia precisa ficar DEPOIS do desvio");
        Assert.True(ondeEnvio > ondePara, "o envio precisa ficar DEPOIS do desvio");
    }

    /// <summary>
    /// A PERSISTÊNCIA LOCAL CONTINUA: o lançamento é gravado antes do desvio. Sem ela, a produção
    /// pesada não existiria em lugar nenhum — e o pedido era manter todo o resto do processo.
    /// </summary>
    [Fact]
    public void APersistenciaLocal_AconteceAntesDoDesvio()
    {
        string tela = Tela();

        int ondePrepara = tela.IndexOf("_controller.PrepararEnvio101Async", StringComparison.Ordinal);
        int ondePara = tela.IndexOf(
            "if (preparo.Sucesso && !_controller.EnvioSemiAcabadoSapHabilitado)", StringComparison.Ordinal);

        Assert.True(ondePrepara >= 0 && ondePara > ondePrepara,
            "o lancamento precisa ser persistido ANTES de a tela parar");
    }

    /// <summary>
    /// Conclui como CONCLUIDO LOCALMENTE, nunca como confirmado no SAP. Dizer "confirmado" sem
    /// documento de material seria afirmar o que não aconteceu, e quem olhasse o apontamento depois
    /// não saberia que ainda falta lançar.
    /// </summary>
    [Fact]
    public void ConcluiLocalmente_NuncaComoConfirmadoNoSap()
    {
        string tela = Tela();

        int ondePara = tela.IndexOf(
            "if (preparo.Sucesso && !_controller.EnvioSemiAcabadoSapHabilitado)", StringComparison.Ordinal);
        int fim = tela.IndexOf("ResultadoEnvioSemiAcabadoSap resultado;", ondePara, StringComparison.Ordinal);
        string ramo = tela[ondePara..fim];

        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente", ramo, StringComparison.Ordinal);
        Assert.Contains("indicadorConfirmadoSap: false", ramo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmadoSap,", ramo, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tela NÃO promete o que não vai fazer: com o portão fechado, a confirmação diz que o 101 não
    /// será enviado. Prometer o envio faria o operador acreditar que lançou, e descobrir o contrário
    /// só no estoque.
    /// </summary>
    [Fact]
    public void AConfirmacao_DizAVerdadeSobreOEnvio()
    {
        string tela = Tela();

        Assert.Contains("bool enviaAoSap = _controller.EnvioSemiAcabadoSapHabilitado;", tela, StringComparison.Ordinal);
        Assert.Contains("O movimento 101 NÃO será enviado: lance-o manualmente no SAP.", tela, StringComparison.Ordinal);
    }

    /// <summary>
    /// O caminho de envio NÃO foi removido — continua inteiro, com claim, one-shot e tratamento de
    /// divergência. Religar é mudar a chave, não reescrever e revalidar tudo.
    /// </summary>
    [Fact]
    public void OCaminhoDeEnvio_ContinuaInteiroNoCodigo()
    {
        string tela = Tela();

        Assert.Contains("_controller.EnviarPreparado101Async", tela, StringComparison.Ordinal);
        Assert.Contains("resultado.DivergenciaSap", tela, StringComparison.Ordinal);
        Assert.Contains("resultado.EnvioDuplicadoBloqueado", tela, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConfirmadoSap", tela, StringComparison.Ordinal);
    }

    /// <summary>A consulta ao portão é feita a cada confirmação, não guardada ao abrir a tela.</summary>
    [Fact]
    public void OPortao_ELidoACadaConfirmacao()
    {
        string controlador = Controlador();

        Assert.Contains("public bool EnvioSemiAcabadoSapHabilitado", controlador, StringComparison.Ordinal);
        // Propriedade calculada, nao campo: ligar a chave vale na proxima confirmacao.
        Assert.Contains("=> Servicos.IntegracaoSap.LeitorConfiguracaoSap.Carregar().SemiAcabadoEnvioSapHabilitado;",
            controlador, StringComparison.Ordinal);
    }

    private static string Fonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
