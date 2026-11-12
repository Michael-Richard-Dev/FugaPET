using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 104C — o envio 101 do Semi-Acabado passa pela MESMA cerimônia one-shot certificada da Entrada:
/// permissão HABILITAR_ESCRITA_SAP + auditoria durável fail-closed → ARMA a capability → o writer 101
/// (MaterialDocumentSapServico) consome one-shot ANTES do HTTP. Provas por unidade compondo
/// <see cref="HabilitacaoEscritaSapServico.HabilitarParaEnvioAsync"/> (o método que a tela chama) com o
/// writer REAL (dispatch contado, sem rede/DB/SAP). Fail-closed: sem armamento ⇒ ZERO dispatch.
/// </summary>
public sealed class ProcessoSemiAcabado101Capability104CTests : IDisposable
{
    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    // ---------- helpers (espelham o harness 12E-E-B, sem DB/SAP/rede) ----------

    private static RuntimeSapWriteCapabilityService NovaCapability()
        => new(TimeSpan.FromSeconds(120), () => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static ConfiguracaoSap ConfigWriter() => new()
    {
        BaseUrl = "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/",
        MaterialDocumentBaseUrl = "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
        HostsPermitidos = ["vhfufqs4ci.sap.fugacouros.com.br"],
        SapClient = "110",
        Usuario = "u",
        Senha = "p",
        EscritaHabilitada = false
    };

    private static MaterialDocumentSapServico WriterReal(IRuntimeSapWriteCapabilityService cap, Action aoDespachar)
        => new(
            ConfigWriter(),
            logIntegracaoSapServico: null,
            cliente: null,
            capability: cap,
            dispatchHttp: (_, _) =>
            {
                aoDespachar();
                return Task.FromResult(ResultadoMaterialDocumentSap.Falha(200, "stub-dispatch"));
            });

    private static Task<ResultadoMaterialDocumentSap> InvocarWriter(MaterialDocumentSapServico writer)
        => writer.CriarDocumentoMaterial101Async(new MaterialDocumentSapRequest(), "SEMI_ACABADO:OP:1");

    // Cerimônia com a tela do Semi-Acabado e fakes injetados (sem DB/SAP).
    private static HabilitacaoEscritaSapServico Cerimonia(
        IRuntimeSapWriteCapabilityService capability,
        bool autorizado,
        bool auditoriaFalha = false)
        => new(
            capability,
            usuarioAutorizado: () => autorizado,
            auditarDuravelAsync: (_, _, _, _) => auditoriaFalha
                ? throw new InvalidOperationException("42501 auditoria")
                : Task.CompletedTask,
            auditarBestEffortAsync: (_, _, _, _) => Task.CompletedTask,
            tela: "ProcessoSemiAcabadoForm");

    private static void DefinirSessao()
        => EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "op",
            Nome = "Operador",
            Permissoes = [],
            IntegracaoBancoHabilitada = true
        });

    // ---------- provas comportamentais (cerimônia + writer real) ----------

    // Sem habilitação (capability DESABILITADA) ⇒ ZERO HTTP.
    [Fact]
    public async Task SemHabilitacao_ZeroDispatch()
    {
        var cap = NovaCapability();
        int dispatches = 0;
        MaterialDocumentSapServico writer = WriterReal(cap, () => dispatches++);

        ResultadoMaterialDocumentSap r = await InvocarWriter(writer);

        Assert.Equal(0, dispatches);
        Assert.False(r.Sucesso);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
    }

    // Usuário sem permissão ⇒ não arma ⇒ ZERO HTTP (fail-closed).
    [Fact]
    public async Task SemPermissao_NaoArma_ZeroDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao();
        int dispatches = 0;
        MaterialDocumentSapServico writer = WriterReal(cap, () => dispatches++);

        ResultadoOperacao habil = await Cerimonia(cap, autorizado: false).HabilitarParaEnvioAsync();
        await InvocarWriter(writer);

        Assert.False(habil.Sucesso);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
        Assert.Equal(0, dispatches);
    }

    // Auditoria durável falha ⇒ não arma ⇒ ZERO HTTP (fail-closed).
    [Fact]
    public async Task AuditoriaFalha_NaoArma_ZeroDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao();
        int dispatches = 0;
        MaterialDocumentSapServico writer = WriterReal(cap, () => dispatches++);

        ResultadoOperacao habil = await Cerimonia(cap, autorizado: true, auditoriaFalha: true).HabilitarParaEnvioAsync();
        await InvocarWriter(writer);

        Assert.False(habil.Sucesso);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
        Assert.Equal(0, dispatches);
    }

    // Habilitação (autorizado + auditoria ok) ⇒ ARMADA ⇒ writer consome one-shot ⇒ EXATAMENTE 1 dispatch.
    [Fact]
    public async Task Habilitado_ConsomeOneShot_ExatamenteUmDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao();
        int dispatches = 0;
        MaterialDocumentSapServico writer = WriterReal(cap, () => dispatches++);

        ResultadoOperacao habil = await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        Assert.True(habil.Sucesso, habil.Mensagem);
        Assert.Equal(EstadoCapabilitySap.Armada101, cap.ObterEstado().Estado);

        await InvocarWriter(writer);
        Assert.Equal(1, dispatches);
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);
    }

    // Segunda invocação do writer SEM nova habilitação ⇒ NENHUM segundo dispatch (one-shot).
    [Fact]
    public async Task SegundaInvocacao_SemNovaHabilitacao_ZeroSegundoDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao();
        int dispatches = 0;
        MaterialDocumentSapServico writer = WriterReal(cap, () => dispatches++);

        await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        await InvocarWriter(writer);
        await InvocarWriter(writer);

        Assert.Equal(1, dispatches);
    }

    // ---------- contrato de fonte (ordenação e fail-closed na tela) ----------

    [Fact]
    public void ConfirmarSemiAcabado_PersisteIdentidadeAntesDeArmar_ArmaAntesDoEnvio_EFailClosed()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync");

        int prepara = metodo.IndexOf("PrepararEnvio101Async", StringComparison.Ordinal);
        int arma = metodo.IndexOf("_habilitacaoEscritaSap.HabilitarParaEnvioAsync", StringComparison.Ordinal);
        int envio = metodo.IndexOf("EnviarPreparado101Async", StringComparison.Ordinal);
        int failClosed = metodo.IndexOf("if (!habilitacaoEscrita.Sucesso)", StringComparison.Ordinal);

        // GATE 104C-D: identidade durável (Preparar) ANTES de armar; armar ANTES do envio; fail-closed entre eles.
        Assert.True(prepara >= 0, "preparo de identidade durável ausente.");
        Assert.True(arma > prepara, "a capability deve ser armada DEPOIS da identidade durável (Preparar).");
        Assert.True(envio > arma, "o envio deve ocorrer DEPOIS de armar.");
        Assert.True(failClosed > arma && failClosed < envio, "fail-closed deve interromper antes do envio.");
        // reuso do mesmo lançamento (ERRO_SAP/reenvio) preservado.
        Assert.Contains("lancamento.CodigoSemiAcabadoLancamento = _codigoLancamentoPersistido;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_UsaCerimoniaComTelaSemiAcabado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        Assert.Contains("HabilitacaoEscritaSapServico", form, StringComparison.Ordinal);
        Assert.Contains("new(\"ProcessoSemiAcabadoForm\")", form, StringComparison.Ordinal);
    }

    // A tela da Entrada permanece com o rótulo padrão (comportamento preservado).
    [Fact]
    public void Habilitacao_PadraoPreservaEntrada()
    {
        string svc = LerArquivoProjeto("Servicos", "IntegracaoSap", "HabilitacaoEscritaSapServico.cs");
        Assert.Contains("private const string TelaPadrao = \"ProcessoEntradaProdutoForm\";", svc, StringComparison.Ordinal);
        Assert.Contains("string tela = TelaPadrao", svc, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do método não encontrado: {assinatura}");

        return fonte[inicio..proximoMetodo];
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
