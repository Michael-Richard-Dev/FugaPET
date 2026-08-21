using System.Net;
using System.Text.Json;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Testes unitários do fluxo HU de caixa (incremental 044): enum/status, DTO SAP exato, gateway fail-closed
/// + mapeamento HTTP, e orquestração do <see cref="ProdutoAcabadoHuService"/> (claim/token/tentativa,
/// timeout→INDETERMINADO_TIMEOUT sem retry, reconciliação, erro 401/403, cancelamento, uma caixa por
/// terminal). Sem banco/rede real: repositório em memória + HttpMessageHandler fake. Nenhum POST real.
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitCaixaTests
{
    private const string Terminal = "TERM-01";
    private const long Usuario = 42;

    // ===================== §24.1 enum INDETERMINADO_TIMEOUT + transições =====================

    [Fact]
    public void Enum_TemIndeterminadoTimeout_ComMapeamentoDb()
    {
        Assert.Equal("INDETERMINADO_TIMEOUT", MapeadorStatusHuCaixa.ParaTextoBanco(StatusIntegracaoCaixa.IndeterminadoTimeout));
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, MapeadorStatusHuCaixa.DoTextoBanco("INDETERMINADO_TIMEOUT"));
    }

    [Fact]
    public void Transicoes_EspelhamContratoBanco()
    {
        Assert.True(TransicaoStatusIntegracaoCaixa.PodeTransitar(StatusIntegracaoCaixa.EnviandoSap, StatusIntegracaoCaixa.IndeterminadoTimeout));
        Assert.True(TransicaoStatusIntegracaoCaixa.PodeTransitar(StatusIntegracaoCaixa.IndeterminadoTimeout, StatusIntegracaoCaixa.ConfirmadaSap));
        // Sem retrocesso automático a partir de INDETERMINADO_TIMEOUT.
        Assert.False(TransicaoStatusIntegracaoCaixa.PodeTransitar(StatusIntegracaoCaixa.IndeterminadoTimeout, StatusIntegracaoCaixa.ProntaParaEnvio));
        Assert.False(TransicaoStatusIntegracaoCaixa.PodeTransitar(StatusIntegracaoCaixa.IndeterminadoTimeout, StatusIntegracaoCaixa.ErroSap));
        // PREVIEW não pula direto para PRONTA (contrato exige AGUARDANDO_AUTORIZACAO_SAP).
        Assert.False(TransicaoStatusIntegracaoCaixa.PodeTransitar(StatusIntegracaoCaixa.PreviewHuGerado, StatusIntegracaoCaixa.ProntaParaEnvio));
    }

    // ===================== §24.2/3/4 DTO SAP exato =====================

    [Fact]
    public void Dto_SerializaComNomesExatos_TaraEDadosLocaisAusentes()
    {
        ProdutoAcabadoCaixa caixa = CaixaValida();
        ResultadoRequestHandlingUnitCaixa r = new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(caixa);
        Assert.True(r.Sucesso);
        string json = r.JsonSanitizado;

        Assert.Contains("\"HandlingUnitExternalID\":\"$1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"WeightUnit\":\"KG\"", json, StringComparison.Ordinal);
        Assert.Contains("\"WeightUnitISOCode\":\"KGM\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Warehouse\":\"\"", json, StringComparison.Ordinal);
        Assert.Contains("\"HandlingUnitTypeOfContent\":\"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"_HandlingUnitItem\":[", json, StringComparison.Ordinal);
        Assert.Contains($"\"Plant\":\"{caixa.Centro}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"StorageLocation\":\"{caixa.Deposito}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"PackagingMaterial\":\"{caixa.MaterialEmbalagem}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"Material\":\"{caixa.Material}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"Batch\":\"{caixa.Lote}\"", json, StringComparison.Ordinal);

        // Decimais como NÚMERO JSON (sem aspas) e quantidade dinâmica (não hardcode de massa).
        Assert.Contains("\"GrossWeight\":10.5", json, StringComparison.Ordinal);
        Assert.Contains("\"HandlingUnitQuantity\":60", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"GrossWeight\":\"10.5\"", json, StringComparison.Ordinal);

        // §24.3/§24.4: tara, OP, correlation_id, usuário, terminal NUNCA vão ao SAP.
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("Tara", out _));
        Assert.False(doc.RootElement.TryGetProperty("PesoTara", out _));
        foreach (string proibido in new[] { caixa.NumeroOrdemProducao, caixa.CorrelationId.ToString(), Terminal })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===================== §24.23/24 gateway fail-closed + §24.9-11 status HTTP =====================

    [Fact]
    public async Task Gateway_Default_FailClosed_NaoEnvia()
    {
        IProdutoAcabadoHandlingUnitSapServico gateway = new ProdutoAcabadoHandlingUnitSapGateway();
        Assert.False(gateway.EnvioAutorizado);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(
            new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(CaixaValida()).Request!,
            ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo);
        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, r.Cenario);
    }

    [Theory]
    [InlineData(201, CenarioPostHandlingUnit.Confirmado)]
    [InlineData(401, CenarioPostHandlingUnit.NaoAutorizado)]
    [InlineData(403, CenarioPostHandlingUnit.NaoAutorizado)]
    [InlineData(500, CenarioPostHandlingUnit.ErroDefinitivo)]
    public async Task Gateway_ClassificaStatusHttp(int status, CenarioPostHandlingUnit esperado)
    {
        string corpo = status == 201 ? """{ "d": { "HandlingUnitExternalID": "HU123", "Warehouse": "" } }""" : "{}";
        ProdutoAcabadoHandlingUnitSapGateway gateway = GatewayComRespostas((HttpStatusCode)status, corpo);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(
            new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(CaixaValida()).Request!,
            ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo);
        Assert.Equal(esperado, r.Cenario);
        if (status == 201)
        {
            Assert.Equal("HU123", r.HandlingUnitExternalId);
        }
        if (status is 401 or 403)
        {
            Assert.False(r.PodeReprocessar);
        }
    }

    [Fact]
    public async Task Gateway_NaoVazaSegredoNoResultado()
    {
        ProdutoAcabadoHandlingUnitSapGateway gateway = GatewayComRespostas(HttpStatusCode.Unauthorized, "erro");
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(
            new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(CaixaValida()).Request!,
            ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo);
        foreach (string proibido in new[] { "Authorization", "Basic ", "Cookie", "X-CSRF-Token", "pkg-pass" })
        {
            Assert.DoesNotContain(proibido, r.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(proibido, r.ResponseJsonSanitizado ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===================== §24.5-8 claim/token/tentativa/resposta antiga =====================

    [Fact]
    public async Task Claim_RetornaSnapshotNovo_ComTokenETentativa()
    {
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico();
        long codigo = await PrepararAtePronta(service, repo);

        ProdutoAcabadoCaixa? snapshot = await repo.ClaimEnvioAsync(codigo, Usuario, Terminal);
        Assert.NotNull(snapshot);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, snapshot!.StatusIntegracao);
        Assert.Equal(1, snapshot.Tentativas);
        Assert.NotNull(snapshot.ClaimToken);
        Assert.NotEqual(Guid.Empty, snapshot.ClaimToken!.Value);
    }

    [Fact]
    public async Task Claim_Nulo_BloqueiaEnvio_SemPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await repo.ClaimEnvioAsync(codigo, Usuario, Terminal); // rouba o claim ⇒ EnviandoSap

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ClaimNaoObtido, r.Cenario);
        Assert.Equal(0, gateway.Chamadas);
    }

    [Fact]
    public async Task RespostaDeTentativaAntiga_Rejeitada()
    {
        RepoFake repo = new();
        long codigo = await PrepararAtePronta(new ProdutoAcabadoHuService(repo, new GatewayFake(CenarioPostHandlingUnit.NaoEnviado)), repo);
        ProdutoAcabadoCaixa snapshot = (await repo.ClaimEnvioAsync(codigo, Usuario, Terminal))!;
        int tentativa = snapshot.Tentativas;
        Guid token = snapshot.ClaimToken!.Value;

        Assert.True(await repo.RegistrarTimeoutAsync(codigo, tentativa, token, null, "timeout"));
        // Resposta ATRASADA da mesma tentativa (sucesso) NÃO altera (não está mais em EnviandoSap).
        Assert.False(await repo.RegistrarSucessoAsync(codigo, tentativa, token, "HU999", "", 201, null, null, null, null, null));
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // ===================== §24.9/12/13 sucesso e erro definitivo =====================

    [Fact]
    public async Task Envio_Confirmado_PersisteConfirmadaSap()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "HU-OK" };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Confirmado, r.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Envio_ErroDefinitivo_Reprocessavel_OuNao(bool podeReprocessar)
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.ErroDefinitivo) { EnvioAutorizado = true, PodeReprocessar = podeReprocessar, HttpStatus = 500 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Erro, r.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(podeReprocessar, repo.UltimoPodeReprocessar);
        Assert.Equal(ResultadoErroHu.ErroDefinitivo, repo.UltimoResultadoErro);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Envio_NaoAutorizado_ClassificaEnaoReprocessa(int status)
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.NaoAutorizado) { EnvioAutorizado = true, HttpStatus = status };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Erro, r.Cenario);
        Assert.Equal(ResultadoErroHu.NaoAutorizado, repo.UltimoResultadoErro);
        Assert.False(repo.UltimoPodeReprocessar);
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // ===================== §24.14/15 timeout → INDETERMINADO_TIMEOUT, sem retry =====================

    [Fact]
    public async Task Envio_Timeout_VaiParaIndeterminado_SemRetryOuSegundoPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Timeout, r.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(1, gateway.Chamadas);
    }

    // ===================== §24.16/17 reconciliação =====================

    [Fact]
    public async Task Reconciliacao_Confirmada_ConfirmaSap()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigo, Usuario, Terminal);

        gateway.ReconciliacaoCenario = CenarioReconciliacaoHandlingUnit.Confirmado;
        gateway.ReconciliacaoComparacaoAprovada = true;
        // §5: reconciliação SÓ com HU SAP explicitamente conhecida (nunca CodigoCaixaLocal).
        ResultadoReconciliacaoHu r = await service.ReconciliarAsync(codigo, "HU-REAL-123");
        Assert.Equal(CenarioReconciliacaoHu.Confirmada, r.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    [Fact]
    public async Task Reconciliacao_SemHuConhecida_Indeterminada_SemChamarGateway()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigo, Usuario, Terminal); // → INDETERMINADO_TIMEOUT
        int reconcAntes = gateway.ReconciliacaoChamadas;

        // Sem HU conhecida (caso DEV): NÃO reconcilia, NÃO chama gateway, reporta a incompatibilidade.
        ResultadoReconciliacaoHu r = await service.ReconciliarAsync(codigo, handlingUnitExternalIdConhecido: null);
        Assert.Equal(CenarioReconciliacaoHu.Indeterminada, r.Cenario);
        Assert.Contains("INCOMPATIBILIDADE_CONTRATO_SAP_RECONCILIACAO", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(reconcAntes, gateway.ReconciliacaoChamadas); // gateway não foi chamado
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    [Fact]
    public void Service_NuncaUsaCodigoCaixaLocalComoHuDeReconciliacao()
    {
        string service = File.ReadAllText(CaminhoProjeto("Servicos", "Operacao", "ProdutoAcabadoHuService.cs"));
        Assert.DoesNotContain("huEsperada = caixa.CodigoCaixaLocal", service, StringComparison.Ordinal);
        Assert.DoesNotContain("caixa.CodigoCaixaLocal", service, StringComparison.Ordinal); // não acessa a propriedade p/ reconciliar
        Assert.Contains("IncompatibilidadeReconciliacao", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconciliacao_404_MantemIndeterminado_SemNovoPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigo, Usuario, Terminal);
        int postsAntes = gateway.Chamadas;

        gateway.ReconciliacaoCenario = CenarioReconciliacaoHandlingUnit.NaoEncontrado;
        ResultadoReconciliacaoHu r = await service.ReconciliarAsync(codigo, "HU-REAL-123");
        Assert.Equal(CenarioReconciliacaoHu.NaoEncontrada, r.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(postsAntes, gateway.Chamadas);
    }

    // ===================== §24.18/19 cancelamento + uma caixa por terminal =====================

    [Fact]
    public async Task Cancelamento_Persistente()
    {
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico();
        long codigo = await PrepararAtePronta(service, repo);
        Assert.True(await service.CancelarAsync(codigo, Usuario, Terminal, "teste"));
        Assert.Equal(StatusIntegracaoCaixa.Cancelada, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    [Fact]
    public async Task UmaCaixaAtivaPorTerminal_BloqueiaSegunda()
    {
        (ProdutoAcabadoHuService service, RepoFake _, _) = MontarServico();
        await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        ResultadoFinalizacaoHu segunda = await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        Assert.False(segunda.Sucesso);
        Assert.Contains("caixa ativa", segunda.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== §24.20/21/22 Form/Repository sem banco/SAP/SQL direto =====================

    [Fact]
    public void Form_NaoAcessaBancoNemSapDireto()
    {
        string form = File.ReadAllText(CaminhoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        Assert.DoesNotContain("NpgsqlConnection", form, StringComparison.Ordinal);
        Assert.DoesNotContain("NpgsqlCommand", form, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", form, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO desenvolvimento", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_UsaFuncoesDeBanco_SemUpdateOuLogDireto()
    {
        string repo = File.ReadAllText(CaminhoProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs"));
        Assert.Contains("fn_hu_caixa_claim_envio", repo, StringComparison.Ordinal);
        Assert.Contains("fn_hu_caixa_registrar_timeout", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE desenvolvimento.hu_caixa", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("desenvolvimento.log_alteracao_cadastral", repo, StringComparison.Ordinal);
    }

    // ===================== §14-22/§26 MODO HML REPETITIVO CONTROLADO =====================

    // §26.1/2/19: gate HU false ⇒ nenhum POST e NENHUM claim consumido (caixa permanece PRONTA_PARA_ENVIO).
    [Fact]
    public async Task GateHuFalse_BloqueiaPost_SemConsumirClaim()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = false };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.NaoAutorizado, r.Cenario);
        Assert.Equal(0, gateway.Chamadas);                               // nenhum POST
        Assert.Equal(0, repo.ClaimsConcedidos);                          // nenhum claim consumido
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // §14/§21: caixas A/B/C — correlation_id/claim_token/tentativa distintos e UM POST por caixa.
    [Fact]
    public async Task MultiplasCaixas_ABC_ClaimsPostsIndependentes()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);

        List<Guid> correlations = [];
        List<Guid> claimTokens = [];
        foreach (int _ in Enumerable.Range(0, 3))
        {
            long codigo = await PrepararAtePronta(service, repo); // mesma OP/terminal — A confirma antes de B
            ProdutoAcabadoCaixa? antesPost = await repo.ObterPorCodigoAsync(codigo);
            correlations.Add(antesPost!.CorrelationId);
            ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
            Assert.Equal(CenarioEnvioHu.Confirmado, r.Cenario);
            claimTokens.Add(repo.UltimoClaimToken);
            Assert.Equal(1, (await repo.ObterPorCodigoAsync(codigo))!.Tentativas); // tentativa própria = 1
        }

        Assert.Equal(3, gateway.Chamadas);                    // um POST por caixa (3 no total)
        Assert.Equal(3, correlations.Distinct().Count());     // correlation_id distintos
        Assert.Equal(3, claimTokens.Distinct().Count());      // claim_token distintos
    }

    // §15/§26.12-13: mesma caixa, duplo clique concorrente ⇒ 1 claim e 1 POST.
    [Fact]
    public async Task MesmaCaixa_DuploClique_UmClaimUmPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        // §17: concorrência REAL — as duas chamadas coincidem no claim via barreira antes do check-and-set.
        repo.ClaimEntrou = new SemaphoreSlim(0);
        repo.ClaimLiberar = new TaskCompletionSource();
        Task<ResultadoEnvioHu> a = Task.Run(() => service.EnviarAsync(codigo, Usuario, Terminal));
        Task<ResultadoEnvioHu> b = Task.Run(() => service.EnviarAsync(codigo, Usuario, Terminal));
        await repo.ClaimEntrou.WaitAsync();   // ambas atingiram o claim
        await repo.ClaimEntrou.WaitAsync();
        repo.ClaimLiberar.SetResult();        // libera as duas simultaneamente
        ResultadoEnvioHu[] resultados = await Task.WhenAll(a, b);

        Assert.Equal(1, resultados.Count(x => x.Cenario == CenarioEnvioHu.Confirmado));
        Assert.Equal(1, resultados.Count(x => x.Cenario == CenarioEnvioHu.ClaimNaoObtido));
        Assert.Equal(1, gateway.Chamadas);       // POSTS_NO_FAKE_HANDLER = 1
        Assert.Equal(1, repo.ClaimsConcedidos);  // CLAIMS_VALIDOS = 1
    }

    // §16/§26.11: CONFIRMADA_SAP não reenvia.
    [Fact]
    public async Task ConfirmadaSap_NaoReenvia()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigo, Usuario, Terminal); // → CONFIRMADA_SAP

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ClaimNaoObtido, r.Cenario);
        Assert.Equal(1, gateway.Chamadas); // nenhum POST adicional
    }

    // §17/§26.14-16: timeout ⇒ 1 POST, sem retry/segundo claim/segundo POST.
    [Fact]
    public async Task Timeout_UmPost_SemRetryNemSegundoClaim()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigo, Usuario, Terminal); // → INDETERMINADO_TIMEOUT

        Assert.Equal(1, gateway.Chamadas);
        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal); // não está PRONTA
        Assert.Equal(CenarioEnvioHu.ClaimNaoObtido, r.Cenario);
        Assert.Equal(1, gateway.Chamadas); // sem segundo POST
        Assert.Equal(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // §19/§20/§26.19: sucesso da Caixa A libera nova caixa (A deixa de ser ativa no terminal).
    [Fact]
    public async Task SucessoA_LiberaCaixaB()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigoA = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigoA, Usuario, Terminal); // A → CONFIRMADA_SAP

        Assert.Null(await service.ObterAtivaPorTerminalAsync(Terminal)); // A não bloqueia mais
        ResultadoFinalizacaoHu b = await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        Assert.True(b.Sucesso); // Caixa B registrada sem reiniciar o app
        Assert.NotEqual(codigoA, b.Caixa!.CodigoProdutoAcabadoCaixa);
    }

    // §22: recuperação por estado — a caixa ativa persistida é refletida (fonte da verdade = repositório).
    [Fact]
    public async Task Recuperacao_ReflectirEstadoPersistido()
    {
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico();
        long codigo = await PrepararAtePronta(service, repo);
        ProdutoAcabadoCaixa? ativa = await service.ObterAtivaPorTerminalAsync(Terminal);
        Assert.NotNull(ativa);
        Assert.Equal(codigo, ativa!.CodigoProdutoAcabadoCaixa);
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, ativa.StatusIntegracao);
    }

    // ===================== REV3-B1 esquema não hardcoded =====================

    // §3/§13: o repositório NÃO qualifica esquema (nem desenvolvimento. nem homologacao.), nem alterna
    // search_path/ROLE em tempo de execução — depende do SearchPath configurado na conexão.
    [Fact]
    public void Repository_SemEsquemaHardcoded_SemSearchPathDinamico()
    {
        string repo = File.ReadAllText(CaminhoProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs"));
        Assert.DoesNotContain("desenvolvimento.", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("homologacao.", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET search_path", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET ROLE", repo, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== REV3-B2 recuperação persistida completa (inclui CONFIRMADA_SAP + HU) =====================

    // §4/§5: após CONFIRMADA_SAP (HU real), reabrir a OP recupera a caixa A COM a HU e libera a caixa B.
    [Fact]
    public async Task Recuperacao_IncluiConfirmadaSapComHu_ELiberaProximaCaixa()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300014350" };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigoA = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigoA, Usuario, Terminal); // A → CONFIRMADA_SAP com HU 300014350

        // Reabertura: lista persistida COMPLETA (não apenas a ativa).
        IReadOnlyList<ProdutoAcabadoCaixa> recuperadas =
            await service.ListarCaixasPersistidasAsync(CaixaValida().NumeroOrdemProducao, Terminal);
        ProdutoAcabadoCaixa a = Assert.Single(recuperadas);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, a.StatusIntegracao);
        Assert.Equal("300014350", a.HandlingUnitExternalId); // HU confirmada visível após reabertura

        Assert.Null(await service.ObterAtivaPorTerminalAsync(Terminal)); // A não é ativa → próxima caixa liberada
        ResultadoFinalizacaoHu b = await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        Assert.True(b.Sucesso);

        // Agora a lista mostra A (CONFIRMADA_SAP) + B (nova), ordenadas por número.
        IReadOnlyList<ProdutoAcabadoCaixa> apos =
            await service.ListarCaixasPersistidasAsync(CaixaValida().NumeroOrdemProducao, Terminal);
        Assert.Equal(2, apos.Count);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, apos[0].StatusIntegracao);
        Assert.Equal("300014350", apos[0].HandlingUnitExternalId);
        Assert.True(apos[0].NumeroCaixa < apos[1].NumeroCaixa);
    }

    // ===================== REV3-B4 HTTP 201 exige HandlingUnitExternalID =====================

    [Theory]
    [InlineData("""{ "d": { "HandlingUnitExternalID": "HU777" } }""", CenarioPostHandlingUnit.Confirmado)]
    [InlineData("""{ "d": { "HandlingUnitExternalID": "" } }""", CenarioPostHandlingUnit.Timeout)]
    [InlineData("""{ "d": { } }""", CenarioPostHandlingUnit.Timeout)]
    [InlineData("", CenarioPostHandlingUnit.Timeout)]
    [InlineData("not-json", CenarioPostHandlingUnit.Timeout)]
    public async Task Gateway_201_ExigeHu_SenaoIndeterminado(string corpo, CenarioPostHandlingUnit esperado)
    {
        ProdutoAcabadoHandlingUnitSapGateway gateway = GatewayComRespostas(HttpStatusCode.Created, corpo);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(
            new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(CaixaValida()).Request!,
            ProdutoAcabadoHandlingUnitCaixaRequestBuilder.EndpointRelativo);
        Assert.Equal(esperado, r.Cenario);
        if (esperado == CenarioPostHandlingUnit.Timeout)
        {
            Assert.False(r.PodeReprocessar); // sem retry, sem 2º POST
            Assert.Equal(201, r.HttpStatus);
        }
    }

    // ===================== REV4-§5/§16 póscondições de FINALIZAÇÃO =====================

    // §16.2: SalvarPreview=false ⇒ finalização NÃO retorna OK (BLOQUEADO).
    [Fact]
    public async Task Finalizacao_SalvarPreviewFalse_NaoRetornaOk()
    {
        RepoFake repo = new() { ForcarSalvarPreviewFalse = true };
        ProdutoAcabadoHuService service = new(repo, new GatewayFake(CenarioPostHandlingUnit.NaoEnviado));
        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        Assert.False(r.Sucesso);
        Assert.Contains("preview", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // §16.3: AguardarAutorizacao=false ⇒ finalização NÃO retorna OK (BLOQUEADO).
    [Fact]
    public async Task Finalizacao_AguardarAutorizacaoFalse_NaoRetornaOk()
    {
        RepoFake repo = new() { ForcarAguardarAutorizacaoFalse = true };
        ProdutoAcabadoHuService service = new(repo, new GatewayFake(CenarioPostHandlingUnit.NaoEnviado));
        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        Assert.False(r.Sucesso);
        Assert.Contains("AGUARDANDO_AUTORIZACAO_SAP", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== REV4-§6/§16 póscondições de SUCESSO SAP =====================

    // §16.4: SAP 201 + RegistrarSucesso=false ⇒ NÃO Confirmado; sem 2º POST/retry.
    [Fact]
    public async Task Sap201_RegistrarSucessoFalse_NaoConfirma_SemSegundoPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        RepoFake repo = new() { ForcarRegistrarSucessoFalse = true };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.NotEqual(CenarioEnvioHu.Confirmado, r.Cenario);
        Assert.Equal(1, gateway.Chamadas); // sem 2º POST
    }

    // §16.5: SAP 201 + persistência true, mas snapshot NÃO confirmado ⇒ NÃO Confirmado.
    [Fact]
    public async Task Sap201_PersistenciaTrue_SnapshotNaoConfirmado_NaoConfirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        RepoFake repo = new() { RegistrarSucessoTrueSemConfirmar = true };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.Equal(1, gateway.Chamadas);
    }

    // §16.6: SAP 201 + HU local persistida DIVERGENTE da resposta ⇒ NÃO Confirmado.
    [Fact]
    public async Task Sap201_HuLocalDivergente_NaoConfirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        RepoFake repo = new() { HuPersistidaSobrescrita = "HU-DIVERGENTE" };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.Equal(1, gateway.Chamadas);
    }

    // §16.7: SAP 201 + persistência true + CONFIRMADA_SAP + HU idêntica ⇒ Confirmado.
    [Fact]
    public async Task Sap201_PersistenciaTrue_ConfirmadaComHuIgual_Confirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Confirmado, r.Cenario);
        Assert.Equal("300099999", r.HandlingUnitExternalId);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
    }

    // ===================== REV4-§8/§9/§16 póscondições de ERRO/TIMEOUT =====================

    // §16.8: RegistrarErro=false ⇒ não finge ERRO_SAP; sem 2º POST.
    [Fact]
    public async Task RegistrarErroFalse_NaoFingePersistencia_SemSegundoPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.ErroDefinitivo) { EnvioAutorizado = true, HttpStatus = 500 };
        RepoFake repo = new() { ForcarRegistrarErroFalse = true };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.NotEqual(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(1, gateway.Chamadas);
    }

    // §16.9: RegistrarTimeout=false ⇒ não finge INDETERMINADO_TIMEOUT; sem 2º POST.
    [Fact]
    public async Task RegistrarTimeoutFalse_NaoFingeIndeterminado_SemSegundoPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        RepoFake repo = new() { ForcarRegistrarTimeoutFalse = true };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.NotEqual(StatusIntegracaoCaixa.IndeterminadoTimeout, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(1, gateway.Chamadas);
    }

    // ===================== REV4-§12/§16 recuperação por CONTEXTO =====================

    // §16.11: recuperação por contexto NÃO mistura caixas de lote/material/item diferentes sob a mesma OP.
    [Fact]
    public async Task Recuperacao_PorContexto_NaoMisturaLoteMaterialItem()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "HU-A" };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);

        long codigoA = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigoA, Usuario, Terminal); // A → CONFIRMADA_SAP (libera o terminal)

        ProdutoAcabadoCaixa caixaB = CaixaValida(lote: "L-OUTRO"); // MESMA OP/terminal, LOTE diferente
        await service.RegistrarEFinalizarCaixaAsync(caixaB, Usuario, Terminal);

        IReadOnlyList<ProdutoAcabadoCaixa> ctxA = await service.ListarCaixasPersistidasPorContextoAsync(
            caixaB.NumeroOrdemProducao, caixaB.ItemOrdemProducao, caixaB.Material, "L-TESTE", Terminal);
        IReadOnlyList<ProdutoAcabadoCaixa> ctxB = await service.ListarCaixasPersistidasPorContextoAsync(
            caixaB.NumeroOrdemProducao, caixaB.ItemOrdemProducao, caixaB.Material, "L-OUTRO", Terminal);

        Assert.Equal("L-TESTE", Assert.Single(ctxA).Lote);
        Assert.Equal("L-OUTRO", Assert.Single(ctxB).Lote);
        Assert.DoesNotContain(ctxB, c => c.Lote == "L-TESTE"); // isolamento: A não aparece no contexto de B
    }

    // ===================== REV5-§2 HTTP 201 LOCAL obrigatório =====================

    // Achado 1: só confirma se o snapshot fresco tiver HttpStatus == 201.
    [Fact]
    public async Task Sap201_StatusLocal201_Confirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999", HttpStatus = 201 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Confirmado, r.Cenario);
        Assert.Equal(201, (await repo.ObterPorCodigoAsync(codigo))!.HttpStatus);
        Assert.Equal(1, gateway.Chamadas);
    }

    [Fact]
    public async Task Sap201_StatusLocalNull_NaoConfirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        RepoFake repo = new() { HttpStatusPersistidoDefinido = true, HttpStatusPersistidoValor = null };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.Equal(1, gateway.Chamadas);
    }

    [Fact]
    public async Task Sap201_StatusLocalDiferente201_NaoConfirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999" };
        RepoFake repo = new() { HttpStatusPersistidoDefinido = true, HttpStatusPersistidoValor = 200 };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.Equal(1, gateway.Chamadas);
    }

    // ===================== REV5-§3 recuperação por contexto FAIL-CLOSED =====================

    // Achado 2: mesmo OP/terminal, material OU item diferente ⇒ caixa de outro contexto NÃO aparece.
    [Theory]
    [InlineData("material")]
    [InlineData("item")]
    public async Task Recuperacao_Contexto_MaterialOuItemDiferente_NaoAparece(string dimensao)
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "HU-A", HttpStatus = 201 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);

        long codigoA = await PrepararAtePronta(service, repo); // contexto padrão
        await service.EnviarAsync(codigoA, Usuario, Terminal); // A → CONFIRMADA_SAP (libera o terminal)

        // Consulta um contexto com UMA dimensão diferente da caixa A: nada deve aparecer.
        string item = dimensao == "item" ? "9999" : "0001";
        string material = dimensao == "material" ? "9999999" : "4000108";
        IReadOnlyList<ProdutoAcabadoCaixa> ctx = await service.ListarCaixasPersistidasPorContextoAsync(
            "1001951", item, material, "L-TESTE", Terminal);
        Assert.Empty(ctx);
    }

    // Achado 2: contexto obrigatório vazio (item/material/lote) NÃO amplia a consulta (fail-closed ⇒ vazio).
    [Theory]
    [InlineData("item")]
    [InlineData("material")]
    [InlineData("lote")]
    public async Task Recuperacao_Contexto_CampoVazio_NaoAmplia(string vazio)
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "HU-A", HttpStatus = 201 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigoA = await PrepararAtePronta(service, repo);
        await service.EnviarAsync(codigoA, Usuario, Terminal); // A existe no store

        string item = vazio == "item" ? string.Empty : "0001";
        string material = vazio == "material" ? string.Empty : "4000108";
        string lote = vazio == "lote" ? string.Empty : "L-TESTE";
        IReadOnlyList<ProdutoAcabadoCaixa> ctx = await service.ListarCaixasPersistidasPorContextoAsync(
            "1001951", item, material, lote, Terminal);
        Assert.Empty(ctx); // não retorna caixas de outros itens/materiais/lotes
    }

    // ===================== REV5-§4 timeout: verificação manual, sem reconciliação automática =====================

    [Fact]
    public async Task Timeout_OrientaVerificacaoManualSap_SemReconciliacao()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Timeout) { EnvioAutorizado = true };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Timeout, r.Cenario);
        Assert.Contains("Verificação manual no SAP obrigatória", r.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("reconcilia", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, gateway.Chamadas); // sem 2º POST
    }

    // ===================== REV5-§5 erro: snapshot precisa comprovar ERRO_SAP =====================

    [Fact]
    public async Task RegistrarErroTrue_SnapshotNaoErroSap_NaoFingeErro()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.ErroDefinitivo) { EnvioAutorizado = true, HttpStatus = 500 };
        RepoFake repo = new() { RegistrarErroTrueSemErroSap = true };
        ProdutoAcabadoHuService service = new(repo, gateway);
        long codigo = await PrepararAtePronta(service, repo);

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, r.Cenario);
        Assert.NotEqual(StatusIntegracaoCaixa.ErroSap, (await repo.ObterPorCodigoAsync(codigo))!.StatusIntegracao);
        Assert.Equal(1, gateway.Chamadas);
    }

    // ===================== Centro PET 3007 (defesa em profundidade no SERVICE) =====================

    // Caixa de OUTRO centro: EnviarAsync bloqueia ANTES do claim/POST — CLAIM=0, POST=0, tentativa não consumida.
    [Fact]
    public async Task EnvioCaixaForaCentroPet_BloqueiaSemClaimSemPost()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "HU", HttpStatus = 201 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        await service.RegistrarEFinalizarCaixaAsync(CaixaValida(centro: "9999"), Usuario, Terminal);
        long codigo = repo.UltimoCodigo;
        await service.AutorizarEnvioAsync(codigo, Usuario, Terminal); // → PRONTA_PARA_ENVIO

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.NaoAutorizado, r.Cenario);
        Assert.Equal(0, gateway.Chamadas);        // POST=0
        Assert.Equal(0, repo.ClaimsConcedidos);   // CLAIM=0
        ProdutoAcabadoCaixa fresco = (await repo.ObterPorCodigoAsync(codigo))!;
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, fresco.StatusIntegracao); // permanece pronta
        Assert.Equal(0, fresco.Tentativas);       // tentativa NÃO consumida
    }

    // Caixa do centro PET 3007: envio segue normalmente (qualquer número de OP é aceito quando centro=3007).
    [Fact]
    public async Task EnvioCaixaCentroPet3007_Confirma()
    {
        var gateway = new GatewayFake(CenarioPostHandlingUnit.Confirmado) { EnvioAutorizado = true, HuRetornada = "300099999", HttpStatus = 201 };
        (ProdutoAcabadoHuService service, RepoFake repo, _) = MontarServico(gateway);
        long codigo = await PrepararAtePronta(service, repo); // CaixaValida centro=3007, OP 1001951

        ResultadoEnvioHu r = await service.EnviarAsync(codigo, Usuario, Terminal);
        Assert.Equal(CenarioEnvioHu.Confirmado, r.Cenario);
        Assert.Equal(1, gateway.Chamadas);
        Assert.Equal(1, repo.ClaimsConcedidos);
    }

    [Theory]
    [InlineData("3007", true)]
    [InlineData(" 3007 ", true)]
    [InlineData("3008", false)]
    [InlineData("300", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RegraCentroPet_CentroPermitido(string? centro, bool esperado)
        => Assert.Equal(esperado, RegraCentroPetProdutoAcabado.CentroPermitido(centro));

    // ===================== helpers =====================

    private static ProdutoAcabadoCaixa CaixaValida(string lote = "L-TESTE", string centro = "3007") => new()
    {
        NumeroOrdemProducao = "1001951",
        ItemOrdemProducao = "0001",
        Material = "4000108",
        Lote = lote,
        Centro = centro,
        Deposito = "PP02",
        MaterialEmbalagem = "3000009",
        OrigemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.Sap,
        PesoBrutoKg = 10.5m,
        TaraKg = 0.5m,
        PesoLiquidoKg = 10.0m,
        UnidadePeso = "KG",
        QuantidadeProdutos = 60,
        UnidadeQuantidade = "UN",
        OrigemPesagem = "MANUAL",
        CorrelationId = Guid.NewGuid(),
        Terminal = Terminal,
        CodigoUsuario = Usuario
    };

    private static (ProdutoAcabadoHuService, RepoFake, GatewayFake) MontarServico(GatewayFake? gateway = null)
    {
        RepoFake repo = new();
        GatewayFake g = gateway ?? new GatewayFake(CenarioPostHandlingUnit.NaoEnviado);
        return (new ProdutoAcabadoHuService(repo, g), repo, g);
    }

    private static async Task<long> PrepararAtePronta(ProdutoAcabadoHuService service, RepoFake repo)
    {
        await service.RegistrarEFinalizarCaixaAsync(CaixaValida(), Usuario, Terminal);
        long codigo = repo.UltimoCodigo;
        await service.AutorizarEnvioAsync(codigo, Usuario, Terminal);
        return codigo;
    }

    private static string CaminhoProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Path.Combine(dir, Path.Combine(partes));
    }

    private static ProdutoAcabadoHandlingUnitSapGateway GatewayComRespostas(HttpStatusCode postStatus, string postBody)
        => new(
            baseUrl: "https://sap.exemplo.local/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001",
            hostsPermitidos: ["sap.exemplo.local"],
            usuario: "user", senha: "pkg-pass", envioAutorizado: true,
            fabricaHandler: () => new HandlerFake(postStatus, postBody));

    private sealed class HandlerFake(HttpStatusCode postStatus, string postBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                HttpResponseMessage csrf = new(HttpStatusCode.OK) { Content = new StringContent("{}") };
                csrf.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-fake");
                return Task.FromResult(csrf);
            }

            return Task.FromResult(new HttpResponseMessage(postStatus) { Content = new StringContent(postBody) });
        }
    }

    private sealed class GatewayFake(CenarioPostHandlingUnit cenario) : IProdutoAcabadoHandlingUnitSapServico
    {
        public bool EnvioAutorizado { get; set; }
        public int Chamadas { get; private set; }
        public int ReconciliacaoChamadas { get; private set; }
        public string HuRetornada { get; set; } = "HU-001";
        public int HttpStatus { get; set; } = 201;
        public bool PodeReprocessar { get; set; }
        public CenarioReconciliacaoHandlingUnit ReconciliacaoCenario { get; set; } = CenarioReconciliacaoHandlingUnit.Indeterminado;
        public bool ReconciliacaoComparacaoAprovada { get; set; }

        public Task<ResultadoPostHandlingUnit> CriarHandlingUnitCaixaAsync(HandlingUnitCaixaRequest request, string endpointRelativo, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(new ResultadoPostHandlingUnit
            {
                Cenario = cenario,
                HandlingUnitExternalId = cenario == CenarioPostHandlingUnit.Confirmado ? HuRetornada : null,
                Warehouse = string.Empty,
                HttpStatus = HttpStatus,
                PodeReprocessar = PodeReprocessar,
                MensagemSanitizada = cenario.ToString()
            });
        }

        public Task<ResultadoReconciliacaoHandlingUnit> ReconciliarHandlingUnitAsync(string handlingUnitExternalId, string warehouse, CancellationToken cancellationToken = default)
        {
            ReconciliacaoChamadas++;
            return Task.FromResult(new ResultadoReconciliacaoHandlingUnit
            {
                Cenario = ReconciliacaoCenario,
                HandlingUnitExternalId = handlingUnitExternalId,
                Warehouse = warehouse,
                HttpStatus = ReconciliacaoCenario == CenarioReconciliacaoHandlingUnit.Confirmado ? 200
                    : ReconciliacaoCenario == CenarioReconciliacaoHandlingUnit.NaoEncontrado ? 404 : null,
                ComparacaoAprovada = ReconciliacaoComparacaoAprovada,
                MensagemSanitizada = ReconciliacaoCenario.ToString()
            });
        }
    }

    private sealed class RepoFake : IProdutoAcabadoRepositorio
    {
        private readonly Dictionary<long, ProdutoAcabadoCaixa> _store = [];
        private readonly Dictionary<string, int> _numeroPorOp = [];
        private long _seq;
        public long UltimoCodigo { get; private set; }
        public ResultadoErroHu? UltimoResultadoErro { get; private set; }
        public bool UltimoPodeReprocessar { get; private set; }
        public int ClaimsConcedidos { get; private set; }
        public Guid UltimoClaimToken { get; private set; }
        private readonly object _travaClaim = new();

        // REV4-§5/§6/§8/§9: interruptores para simular póscondições NÃO comprovadas do contrato 044.
        public bool ForcarSalvarPreviewFalse { get; set; }
        public bool ForcarAguardarAutorizacaoFalse { get; set; }
        public bool ForcarRegistrarSucessoFalse { get; set; }
        public bool RegistrarSucessoTrueSemConfirmar { get; set; } // devolve true mas NÃO move p/ CONFIRMADA_SAP
        public string? HuPersistidaSobrescrita { get; set; }        // persiste uma HU divergente da retornada
        public bool ForcarRegistrarErroFalse { get; set; }
        public bool RegistrarErroTrueSemErroSap { get; set; } // devolve true mas NÃO move p/ ERRO_SAP (REV5-§5)
        public bool ForcarRegistrarTimeoutFalse { get; set; }

        // REV5-§2: controle do http_status persistido no sucesso. Por padrão persiste o status recebido (201).
        // Definido=true força um valor específico (inclusive null) para provar a exigência EXATA de 201.
        public bool HttpStatusPersistidoDefinido { get; set; }
        public int? HttpStatusPersistidoValor { get; set; }

        // §17: barreira de concorrência REAL — sinaliza entrada no claim e aguarda liberação conjunta.
        public SemaphoreSlim? ClaimEntrou { get; set; }
        public TaskCompletionSource? ClaimLiberar { get; set; }

        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken ct = default)
        {
            long codigo = ++_seq;
            _numeroPorOp.TryGetValue(caixa.NumeroOrdemProducao, out int n);
            n++;
            _numeroPorOp[caixa.NumeroOrdemProducao] = n;
            ProdutoAcabadoCaixa persistida = Clonar(caixa);
            persistida.CodigoProdutoAcabadoCaixa = codigo;
            persistida.NumeroCaixa = n;
            persistida.CodigoCaixaLocal = $"CX-{caixa.NumeroOrdemProducao}-{n:0000}";
            persistida.HandlingUnitCaixa = persistida.CodigoCaixaLocal;
            persistida.StatusIntegracao = StatusIntegracaoCaixa.EmPesagem;
            persistida.Tentativas = 0;
            _store[codigo] = persistida;
            UltimoCodigo = codigo;
            return Task.FromResult(Clonar(persistida));
        }

        public Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa pesagem, CancellationToken ct = default) => Task.FromResult(++_seq);

        public Task<bool> FinalizarLocalAsync(long codigo, long usuario, string terminal, CancellationToken ct = default)
            => Transicao(codigo, StatusIntegracaoCaixa.EmPesagem, StatusIntegracaoCaixa.FinalizadaLocal);

        public Task<bool> SalvarPreviewAsync(long codigo, string req, string end, CancellationToken ct = default)
            => ForcarSalvarPreviewFalse
                ? Task.FromResult(false)
                : Transicao(codigo, StatusIntegracaoCaixa.FinalizadaLocal, StatusIntegracaoCaixa.PreviewHuGerado);

        public Task<bool> AguardarAutorizacaoAsync(long codigo, CancellationToken ct = default)
            => ForcarAguardarAutorizacaoFalse
                ? Task.FromResult(false)
                : Transicao(codigo, StatusIntegracaoCaixa.PreviewHuGerado, StatusIntegracaoCaixa.AguardandoAutorizacaoSap);

        public Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken ct = default)
            => Transicao(codigo, StatusIntegracaoCaixa.AguardandoAutorizacaoSap, StatusIntegracaoCaixa.ProntaParaEnvio);

        public async Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long codigo, long usuario, string terminal, CancellationToken ct = default)
        {
            // §17: força as duas chamadas concorrentes a coincidirem ANTES do check-and-set atômico.
            if (ClaimEntrou is not null && ClaimLiberar is not null)
            {
                ClaimEntrou.Release();
                await ClaimLiberar.Task;
            }

            // Check-and-set ATÔMICO (espelha o claim atômico do banco): só um vence sob concorrência real.
            lock (_travaClaim)
            {
                if (!_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) || c.StatusIntegracao != StatusIntegracaoCaixa.ProntaParaEnvio)
                {
                    return null;
                }

                c.StatusIntegracao = StatusIntegracaoCaixa.EnviandoSap;
                c.Tentativas++;
                c.ClaimToken = Guid.NewGuid();
                ClaimsConcedidos++;
                UltimoClaimToken = c.ClaimToken.Value;
                return Clonar(c);
            }
        }

        public Task<bool> RegistrarSucessoAsync(long codigo, int tentativa, Guid claim, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? creation, CancellationToken ct = default)
        {
            if (ForcarRegistrarSucessoFalse)
            {
                return Task.FromResult(false); // persistência não comprovada; estado permanece ENVIANDO_SAP
            }

            if (RegistrarSucessoTrueSemConfirmar)
            {
                return Task.FromResult(true); // devolve true mas NÃO reflete CONFIRMADA_SAP no snapshot
            }

            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c)
                && c.StatusIntegracao == StatusIntegracaoCaixa.EnviandoSap
                && c.Tentativas == tentativa && c.ClaimToken == claim)
            {
                c.StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
                c.HandlingUnitExternalId = HuPersistidaSobrescrita ?? hu;
                // REV5-§2: por padrão persiste o http recebido (201); o teste pode forçar null/≠201.
                c.HttpStatus = HttpStatusPersistidoDefinido ? HttpStatusPersistidoValor : http;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> RegistrarErroAsync(long codigo, int tentativa, Guid claim, int? http, string? resp, string? sap, string erro, ResultadoErroHu resultado, bool podeReprocessar, CancellationToken ct = default)
        {
            UltimoResultadoErro = resultado;
            UltimoPodeReprocessar = podeReprocessar;
            if (ForcarRegistrarErroFalse)
            {
                return Task.FromResult(false); // não finge ERRO_SAP persistido
            }

            if (RegistrarErroTrueSemErroSap)
            {
                return Task.FromResult(true); // devolve true mas o snapshot NÃO fica em ERRO_SAP (REV5-§5)
            }

            return FinalizarTentativa(codigo, tentativa, claim, StatusIntegracaoCaixa.ErroSap, null);
        }

        public Task<bool> RegistrarTimeoutAsync(long codigo, int tentativa, Guid claim, string? sap, string erro, CancellationToken ct = default)
            => ForcarRegistrarTimeoutFalse
                ? Task.FromResult(false) // não finge INDETERMINADO_TIMEOUT persistido
                : FinalizarTentativa(codigo, tentativa, claim, StatusIntegracaoCaixa.IndeterminadoTimeout, null);

        public Task<bool> ConfirmarReconciliacaoAsync(long codigo, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? creation, bool aprovada, CancellationToken ct = default)
        {
            if (aprovada && http == 200 && _store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) && c.StatusIntegracao == StatusIntegracaoCaixa.IndeterminadoTimeout)
            {
                c.StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
                c.HandlingUnitExternalId = hu;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(long codigo, string hu, string? wh, int http, string? resp, string? sap, string erro, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) && c.StatusIntegracao == StatusIntegracaoCaixa.IndeterminadoTimeout);

        public Task<bool> BloquearConfiguracaoAsync(long codigo, long usuario, string terminal, string erro, string? sap, CancellationToken ct = default)
        {
            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c)
                && c.StatusIntegracao is StatusIntegracaoCaixa.FinalizadaLocal or StatusIntegracaoCaixa.PreviewHuGerado
                    or StatusIntegracaoCaixa.AguardandoAutorizacaoSap or StatusIntegracaoCaixa.ProntaParaEnvio)
            {
                c.StatusIntegracao = StatusIntegracaoCaixa.Bloqueada;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> CancelarAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken ct = default)
        {
            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c)
                && c.HandlingUnitExternalId is null
                && c.StatusIntegracao is not (StatusIntegracaoCaixa.ConfirmadaSap or StatusIntegracaoCaixa.Cancelada or StatusIntegracaoCaixa.IndeterminadoTimeout or StatusIntegracaoCaixa.EnviandoSap))
            {
                c.StatusIntegracao = StatusIntegracaoCaixa.Cancelada;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> LiberarReprocessamentoAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken ct = default)
        {
            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) && c.StatusIntegracao == StatusIntegracaoCaixa.ErroSap && UltimoPodeReprocessar)
            {
                c.StatusIntegracao = StatusIntegracaoCaixa.ProntaParaEnvio;
                c.ClaimToken = null;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) ? Clonar(c) : null);

        public Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string terminal, CancellationToken ct = default)
        {
            ProdutoAcabadoCaixa? ativa = _store.Values.FirstOrDefault(c =>
                string.Equals(c.Terminal, terminal, StringComparison.OrdinalIgnoreCase)
                && c.StatusIntegracao is not (StatusIntegracaoCaixa.ConfirmadaSap or StatusIntegracaoCaixa.Cancelada));
            return Task.FromResult(ativa is null ? null : Clonar(ativa));
        }

        // REV3-B2: recuperação persistida COMPLETA por OP+terminal (todos os estados), ordenada por número.
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(string numeroOrdemProducao, string terminal, CancellationToken ct = default)
        {
            IReadOnlyList<ProdutoAcabadoCaixa> lista = _store.Values
                .Where(c => string.Equals(c.NumeroOrdemProducao?.Trim(), numeroOrdemProducao?.Trim(), StringComparison.Ordinal)
                    && string.Equals(c.Terminal?.Trim(), terminal?.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.NumeroCaixa)
                .Select(Clonar)
                .ToList();
            return Task.FromResult(lista);
        }

        // REV5-§3: recuperação por CONTEXTO INEQUÍVOCO, FAIL-CLOSED. Igualdade OBRIGATÓRIA em todos os campos;
        // qualquer campo obrigatório vazio ⇒ lista VAZIA (nunca amplia para OP+terminal).
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal, CancellationToken ct = default)
        {
            static string N(string? v) => (v ?? string.Empty).Trim();
            if (N(numeroOrdemProducao).Length == 0 || N(itemOrdemProducao).Length == 0
                || N(material).Length == 0 || N(lote).Length == 0 || N(terminal).Length == 0)
            {
                return Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);
            }

            IReadOnlyList<ProdutoAcabadoCaixa> lista = _store.Values
                .Where(c => string.Equals(N(c.NumeroOrdemProducao), N(numeroOrdemProducao), StringComparison.Ordinal)
                    && string.Equals(N(c.Terminal), N(terminal), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(N(c.ItemOrdemProducao), N(itemOrdemProducao), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(N(c.Material), N(material), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(N(c.Lote), N(lote), StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.NumeroCaixa)
                .Select(Clonar)
                .ToList();
            return Task.FromResult(lista);
        }

        // INC-047: leituras por HU (fake em memória).
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(IReadOnlyList<string> husExternais, CancellationToken ct = default)
        {
            HashSet<string> hus = (husExternais ?? []).Select(h => (h ?? string.Empty).Trim()).Where(h => h.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ProdutoAcabadoCaixa> lista = _store.Values
                .Where(c => c.HandlingUnitExternalId is { Length: > 0 } hu && hus.Contains(hu))
                .OrderBy(c => c.NumeroCaixa).Select(Clonar).ToList();
            return Task.FromResult(lista);
        }

        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(string huInicial, string huFinal, string? material, CancellationToken ct = default)
        {
            string ini = (huInicial ?? string.Empty).Trim(); string fim = (huFinal ?? string.Empty).Trim();
            if (ini.Length == 0 || fim.Length == 0) { return Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]); }
            if (string.CompareOrdinal(ini, fim) > 0) { (ini, fim) = (fim, ini); }
            string mat = (material ?? string.Empty).Trim();
            IReadOnlyList<ProdutoAcabadoCaixa> lista = _store.Values
                .Where(c => c.HandlingUnitExternalId is { Length: > 0 } hu
                    && string.CompareOrdinal(hu, ini) >= 0 && string.CompareOrdinal(hu, fim) <= 0
                    && (mat.Length == 0 || string.Equals((c.Material ?? string.Empty).Trim(), mat, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.NumeroCaixa).Select(Clonar).ToList();
            return Task.FromResult(lista);
        }

        private Task<bool> Transicao(long codigo, StatusIntegracaoCaixa de, StatusIntegracaoCaixa para)
        {
            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) && c.StatusIntegracao == de)
            {
                c.StatusIntegracao = para;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        private Task<bool> FinalizarTentativa(long codigo, int tentativa, Guid claim, StatusIntegracaoCaixa destino, string? hu)
        {
            if (_store.TryGetValue(codigo, out ProdutoAcabadoCaixa? c)
                && c.StatusIntegracao == StatusIntegracaoCaixa.EnviandoSap
                && c.Tentativas == tentativa && c.ClaimToken == claim)
            {
                c.StatusIntegracao = destino;
                if (hu is not null)
                {
                    c.HandlingUnitExternalId = hu;
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        private static ProdutoAcabadoCaixa Clonar(ProdutoAcabadoCaixa c) => new()
        {
            CodigoProdutoAcabadoCaixa = c.CodigoProdutoAcabadoCaixa,
            NumeroCaixa = c.NumeroCaixa,
            CodigoCaixaLocal = c.CodigoCaixaLocal,
            NumeroOrdemProducao = c.NumeroOrdemProducao,
            ItemOrdemProducao = c.ItemOrdemProducao,
            Material = c.Material,
            Lote = c.Lote,
            Centro = c.Centro,
            Deposito = c.Deposito,
            MaterialEmbalagem = c.MaterialEmbalagem,
            OrigemMaterialEmbalagem = c.OrigemMaterialEmbalagem,
            PesoBrutoKg = c.PesoBrutoKg,
            TaraKg = c.TaraKg,
            PesoLiquidoKg = c.PesoLiquidoKg,
            UnidadePeso = c.UnidadePeso,
            QuantidadeProdutos = c.QuantidadeProdutos,
            UnidadeQuantidade = c.UnidadeQuantidade,
            OrigemPesagem = c.OrigemPesagem,
            CodigoBalanca = c.CodigoBalanca,
            CorrelationId = c.CorrelationId,
            StatusIntegracao = c.StatusIntegracao,
            HandlingUnitExternalId = c.HandlingUnitExternalId,
            RequestPayload = c.RequestPayload,
            ResponsePayload = c.ResponsePayload,
            ErroSanitizado = c.ErroSanitizado,
            HttpStatus = c.HttpStatus,
            Tentativas = c.Tentativas,
            ClaimToken = c.ClaimToken,
            Terminal = c.Terminal,
            CodigoUsuario = c.CodigoUsuario,
            HandlingUnitCaixa = c.HandlingUnitCaixa
        };
    }
}
