using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE FUGAPET-Q-CODE-CANDIDATE-REPAIR-AND-PACKAGING-05:
/// (A) prova de contrato dos 6 membros reparados (baseline preexistente);
/// (E–J) gate Packaging Q por alvo Process/User/Machine;
/// (D/M) bloqueio ANTES de qualquer HTTP quando desabilitado;
/// (K/L/N) isolamento/startup independentes.
/// </summary>
public sealed class PackagingGateEContratosQTests
{
    // ---------- (A) contratos reparados ----------

    [Fact]
    public void Contratos_Reparados_Existem()
    {
        // CodigoPerfilResultado (long?) + TipoProcessoOperacao.ResultadoApontamento (string const)
        var ctx = new ContextoApontamentoProcesso
        {
            TipoProcesso = TipoProcessoOperacao.ResultadoApontamento,
            CodigoPerfilResultado = 7L
        };
        Assert.Equal("RESULTADO_APONTAMENTO", ctx.TipoProcesso);
        Assert.Equal(7L, ctx.CodigoPerfilResultado);

        // OperacaoOrdemProducaoSap.WorkCenterInternalId / WorkCenterTypeCode (string)
        var op = new OperacaoOrdemProducaoSap { WorkCenterInternalId = "WC-1", WorkCenterTypeCode = "1" };
        Assert.Equal("WC-1", op.WorkCenterInternalId);
        Assert.Equal("1", op.WorkCenterTypeCode);

        // ConfiguracaoSap.WorkCenterBaseUrlEfetiva / WorkCenterConfigurado
        var cfg = new ConfiguracaoSap
        {
            MaterialDocumentBaseUrl = "https://host:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            Usuario = "u",
            Senha = "p",
            HostsPermitidos = ["host"]
        };
        Assert.Contains("API_WORKCENTER", cfg.WorkCenterBaseUrlEfetiva, StringComparison.Ordinal);
        Assert.True(cfg.WorkCenterConfigurado);
    }

    [Fact]
    public void WorkCenter_SemBaseUrl_FailClosed()
    {
        var cfg = new ConfiguracaoSap { Usuario = "u", Senha = "p", HostsPermitidos = ["host"] };
        Assert.Equal(string.Empty, cfg.WorkCenterBaseUrlEfetiva);
        Assert.False(cfg.WorkCenterConfigurado);
    }

    // ---------- (E–J) gate Packaging por alvo ----------

    private static Func<string, EnvironmentVariableTarget, string?> Alvos(string? process, string? user, string? machine)
        => (nome, alvo) => alvo switch
        {
            EnvironmentVariableTarget.Process => process,
            EnvironmentVariableTarget.User => user,
            EnvironmentVariableTarget.Machine => machine,
            _ => null
        };

    [Fact] // I: Process=true, User/Machine ausentes → habilita
    public void Gate_ProcessTrueSomente_Habilita()
        => Assert.True(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("true", null, null)));

    [Fact] // ausentes → false
    public void Gate_TodosAusentes_False()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos(null, null, null)));

    [Fact] // Process=false → false
    public void Gate_ProcessFalse_False()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("false", null, null)));

    [Fact] // Process inválido → fail-closed
    public void Gate_ProcessInvalido_False()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("sim", null, null)));

    [Fact] // E: User=true (Process ausente) → false
    public void Gate_UserTrue_NaoHabilita()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos(null, "true", null)));

    [Fact] // F: Machine=true (Process ausente) → false
    public void Gate_MachineTrue_NaoHabilita()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos(null, null, "true")));

    [Fact] // G: Process=true + User=true → false
    public void Gate_ProcessTrueMaisUserTrue_NaoHabilita()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("true", "true", null)));

    [Fact] // H: Process=true + Machine=true → false
    public void Gate_ProcessTrueMaisMachineTrue_NaoHabilita()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("true", null, "true")));

    [Fact] // Process=true + User presente (mesmo que não "true") → false (fail-closed)
    public void Gate_ProcessTrueMaisUserQualquer_NaoHabilita()
        => Assert.False(LeitorConfiguracaoSap.ResolverPackagingHabilitado(Alvos("true", "false", null)));

    // ---------- (C) JSON não habilita ----------

    [Fact]
    public void Gate_JsonPackagingEnabledTrue_NaoHabilitaSemProcessEnv()
    {
        string arquivo = Path.GetTempFileName();
        try
        {
            File.WriteAllText(arquivo, "{ \"sap\": { \"packaging_enabled\": true, \"base_url\": \"https://h/API_MATERIAL_DOCUMENT_SRV/\" } }");
            // Sem leitor por alvo (nenhuma env Process) ⇒ capability permanece false, mesmo com JSON true.
            ConfiguracaoSap cfg = LeitorConfiguracaoSap.Carregar(arquivo, _ => null, Alvos(null, null, null));
            Assert.False(cfg.PackagingHabilitado);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    // ---------- (D/M) bloqueio antes de HTTP ----------

    [Fact] // D/M: desabilitado + config técnica legada completa → zero HTTP, handler fail-if-called não alcançado
    public async Task Norma_Desabilitada_NaoChamaHttp()
    {
        int chamadasHttp = 0;
        var cfg = new ConfiguracaoSap
        {
            PackagingHabilitado = false,
            PackagingBaseUrl = "https://cpi.example/http/pesagem/handling_unit/ZC",
            PackagingUsuario = "u",
            PackagingSenha = "p",
            PackagingHostsPermitidos = ["cpi.example"]
        };
        var servico = new ProdutoAcabadoNormaEmbalagemSapServico(
            cfg,
            (_, _) => { chamadasHttp++; throw new InvalidOperationException("HTTP não deveria ser chamado"); });

        ResultadoConsultaNormaEmbalagemSap r = await servico.ObterNormaAsync("MAT-1");

        Assert.Equal(0, chamadasHttp);
        Assert.Equal(CenarioConsultaNormaEmbalagem.NaoConfigurada, r.Cenario);
    }

    [Fact] // habilitado + config técnica presente ⇒ o gate NÃO bloqueia (o envio override é alcançado)
    public async Task Norma_Habilitada_AlcancaEnvio()
    {
        int chamadasHttp = 0;
        var cfg = new ConfiguracaoSap
        {
            PackagingHabilitado = true,
            PackagingBaseUrl = "https://cpi.example/http/pesagem/handling_unit/ZC",
            PackagingUsuario = "u",
            PackagingSenha = "p",
            PackagingHostsPermitidos = ["cpi.example"]
        };
        var servico = new ProdutoAcabadoNormaEmbalagemSapServico(
            cfg,
            (_, _) => { chamadasHttp++; return Task.FromResult(new RespostaHttpNorma(200, "{}")); });

        _ = await servico.ObterNormaAsync("MAT-1");

        Assert.Equal(1, chamadasHttp); // gate liberou; envio alcançado
    }

    // ---------- (K/L/N) startup / isolamento independentes ----------

    [Fact] // K/L: Packaging false não bloqueia SAP Standard Q (ValidarSap independe de Packaging)
    public void Startup_PackagingFalse_SapStandardIndependente()
    {
        ConfiguracaoSap cfg = SapQValido();
        Assert.True(FugaPET_Dev.Servicos.Ambiente.ValidadorAmbienteQ.ValidarSap(cfg).Valido);
        Assert.False(cfg.PackagingHabilitado); // capability desabilitada não afeta o Standard
    }

    private static ConfiguracaoSap SapQValido()
    {
        const string host = "vhfufqs4ci.sap.fugacouros.com.br";
        static string Url(string h, string s) => $"https://{h}:44300/sap/opu/odata/sap/{s}/";
        return new ConfiguracaoSap
        {
            BaseUrl = Url(host, "API_MATERIAL_DOCUMENT_SRV"),
            MaterialDocumentBaseUrl = Url(host, "API_MATERIAL_DOCUMENT_SRV"),
            ProductionOrderBaseUrl = Url(host, "API_PRODUCTION_ORDER_2_SRV"),
            ProductionOrderConfirmationBaseUrl = Url(host, "API_PROD_ORDER_CONFIRMATION_2_SRV"),
            ProductBaseUrl = Url(host, "API_PRODUCT_SRV"),
            HandlingUnitBaseUrl = Url(host, "API_HANDLINGUNIT"),
            SapClient = "123",
            HostsPermitidos = [host]
        };
    }
}
