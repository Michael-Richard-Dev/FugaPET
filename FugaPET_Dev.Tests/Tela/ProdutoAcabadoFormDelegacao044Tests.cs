using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Correção 044 (§1-§7): prova que a Form delega a persistência REAL ao Controller/Service (banco = fonte
/// da verdade), recupera a caixa ativa por terminal, cancela de forma persistente, é fail-closed em DEV
/// (sem POST/claim) e que o runtime NÃO é aprovado quando a configuração efetiva usa <c>postgres</c>.
/// Testes de fonte + comportamentais puros (não instanciam a Form nem tocam banco/SAP).
/// </summary>
public sealed class ProdutoAcabadoFormDelegacao044Tests
{
    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string Form => LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

    // §1: finalização delega ao Controller persistente; snapshot substitui o objeto temporário.
    [Fact]
    public void Form_DelegaFinalizacaoAoControllerPersistente_ComSnapshot()
    {
        string form = Form;
        Assert.Contains("await _controller.FinalizarCaixaLocalAsync(", form, StringComparison.Ordinal);
        Assert.Contains("_caixasPesadas.Add(resultado.Caixa);", form, StringComparison.Ordinal);
        // Não usa MontarCaixaSemIdentidadeSequencial na Form (identidade é do banco).
        Assert.DoesNotContain("_controller.MontarCaixaSemIdentidadeSequencial(", form, StringComparison.Ordinal);
        // Não simula persistência com mensagens "em memória".
        Assert.DoesNotContain("registrada em memória", form, StringComparison.Ordinal);
    }

    // §1: a Form não muda estados PERSISTENTES manualmente no fluxo de registro.
    [Fact]
    public void Form_NaoAlteraEstadosPersistentesManualmenteNoRegistro()
    {
        string form = Form;
        int inicio = form.IndexOf("private async Task<bool> RegistrarCaixaProdutoAcabadoAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0);
        int fim = form.IndexOf("private async Task<string> RecuperarCaixasPersistidasAsync(", StringComparison.Ordinal);
        Assert.True(fim > inicio);
        string registro = form[inicio..fim];
        Assert.DoesNotContain("TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado)", registro, StringComparison.Ordinal);
        Assert.DoesNotContain("TransicionarPara(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)", registro, StringComparison.Ordinal);
    }

    // §2 (REV3-B2 + REV4-§12): abertura recupera do banco TODAS as caixas persistidas por CONTEXTO
    // (OP+item+material+lote+terminal), inclusive CONFIRMADA_SAP com HU — não apenas a caixa ativa.
    [Fact]
    public void Form_RecuperaCaixasPersistidasNaAbertura()
    {
        string form = Form;
        Assert.Contains("await RecuperarCaixasPersistidasAsync()", form, StringComparison.Ordinal);
        // REV4-§12: recuperação por CONTEXTO (isola item/material/lote), não mais só OP+terminal.
        Assert.Contains("_controller.ListarCaixasPersistidasPorContextoAsync(", form, StringComparison.Ordinal);
        Assert.Contains("_ordemAtual.MaterialProduzido", form, StringComparison.Ordinal);
        Assert.Contains("_ordemAtual.Lote", form, StringComparison.Ordinal);
        // Recupera a lista COMPLETA (substitui o cache), não só a caixa ativa por terminal.
        Assert.Contains("_caixasPesadas.AddRange(caixas);", form, StringComparison.Ordinal);
    }

    // §3: cancelamento delega ao Controller com o CÓDIGO PERSISTIDO + usuário + terminal + motivo.
    [Fact]
    public void Form_CancelamentoDelegaComCodigoPersistido()
    {
        string form = Form;
        Assert.Contains("caixa.CodigoProdutoAcabadoCaixa is not long codigo", form, StringComparison.Ordinal);
        Assert.Contains("await _controller.CancelarCaixaHandlingUnitAsync(codigo, usuario, terminal,", form, StringComparison.Ordinal);
        // Só atualiza o cache após confirmar CANCELADA no banco.
        Assert.Contains("resultado.Caixa is not { StatusIntegracao: StatusIntegracaoCaixa.Cancelada }", form, StringComparison.Ordinal);
    }

    // §4/REV2: fail-closed quando o gateway não está autorizado (habilitação dinâmica); a Form nunca faz
    // POST/HTTP/claim diretamente — tudo delegado ao Controller/Service.
    [Fact]
    public void Form_FailClosed_SemPostSemClaimSemHttp()
    {
        string form = Form;
        // REV4-§11: texto NEUTRO da regra de envio (nunca hardcoda DEV/HML).
        Assert.Contains("Envio de HU SAP não habilitado nesta execução", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Envio SAP não autorizado no ambiente DEV", form, StringComparison.Ordinal);
        // Habilitação DINÂMICA governada pelo gateway (não mais fixa em false).
        Assert.Contains("_controller.EnvioHuAutorizado && elegivelEnvio", form, StringComparison.Ordinal);
        Assert.Contains("!_envioCaixaSapEmAndamento && !bloqueadaPorEstadoIndeterminado", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".PatchAsync(", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new HttpClient", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ClaimEnvioAsync", form, StringComparison.Ordinal); // claim é do Service, nunca da Form
    }

    // REV3-B5 (§9): o catch do envio NUNCA instrui "tente novamente" (risco de 2º POST); ao falhar,
    // recarrega o snapshot persistido e, se não obtiver, bloqueia o reenvio local com mensagem segura.
    [Fact]
    public void Form_ExcecaoEnvio_SemRetry_RecarregaSnapshotOuBloqueia()
    {
        string form = Form;
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0);
        int fim = form.IndexOf("private void AtualizarEstadoEnvioCaixaSap(", StringComparison.Ordinal);
        if (fim < 0 || fim < inicio)
        {
            fim = form.Length;
        }

        string envio = form[inicio..fim];
        // A frase proibida não existe em NENHUM lugar do envio.
        Assert.DoesNotContain("Verifique o estado e tente novamente", envio, StringComparison.Ordinal);
        // Ao falhar, recarrega o snapshot persistido pelo código.
        Assert.Contains("await _controller.ObterCaixaPorCodigoAsync(codigo)", envio, StringComparison.Ordinal);
        // Sem snapshot: bloqueia o reenvio local e informa a mensagem segura (sem incentivar novo POST).
        Assert.Contains("_codigoCaixaEnvioIndeterminado = codigo", envio, StringComparison.Ordinal);
        Assert.Contains("Não foi possível determinar o estado final da caixa. Não tente enviar novamente.", envio, StringComparison.Ordinal);
        // A Form nunca dispara um 2º POST no catch.
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", envio[envio.IndexOf("catch (Exception ex)", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // REV3-§10: o texto operacional do rodapé reflete o estado REAL do gate de escrita HU
    // (não afirma "POST SAP desativado" fixo quando o envio HU está autorizado no ambiente).
    [Fact]
    public void Form_TextoRodape_RefleteEstadoRealDoGateHu()
    {
        string form = Form;
        Assert.DoesNotContain("POST SAP desativado", form, StringComparison.Ordinal);
        Assert.Contains("_controller.EnvioHuAutorizado", form, StringComparison.Ordinal);
        Assert.Contains("Envio SAP (Handling Unit) autorizado", form, StringComparison.Ordinal);
    }

    // REV5-§6: a Form NÃO ignora AutorizarEnvioCaixaAsync=false — fail-closed ANTES do claim (POST_COUNT=0):
    // captura o bool, e quando false recarrega o snapshot, informa e RETORNA sem chamar EnviarCaixaHandlingUnitAsync.
    [Fact]
    public void Form_AutorizacaoLocalFalse_NaoEnvia_FailClosed()
    {
        string form = Form;
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0);
        int fim = form.IndexOf("private void AtualizarEstadoEnvioCaixaSap(", StringComparison.Ordinal);
        if (fim < 0 || fim < inicio)
        {
            fim = form.Length;
        }

        string envio = form[inicio..fim];
        Assert.Contains("bool autorizado = await _controller.AutorizarEnvioCaixaAsync(codigo, usuario, terminal)", envio, StringComparison.Ordinal);
        Assert.Contains("if (!autorizado)", envio, StringComparison.Ordinal);
        Assert.Contains("Autorização local de envio não foi aplicada", envio, StringComparison.Ordinal);
        // O return do ramo !autorizado precede a chamada de envio (nenhum POST quando não autorizado).
        int posAutorizacaoFalse = envio.IndexOf("if (!autorizado)", StringComparison.Ordinal);
        int posEnvio = envio.IndexOf("await _controller.EnviarCaixaHandlingUnitAsync(", StringComparison.Ordinal);
        Assert.True(posAutorizacaoFalse < posEnvio);
        Assert.Contains("return; // sai pelo finally; POST_COUNT=0", envio, StringComparison.Ordinal);
    }

    // ===================== UX pós-confirmação de HU SAP =====================

    // Feedback de sucesso INEQUÍVOCO após HTTP 201 + CONFIRMADA_SAP: número da caixa + HU SAP.
    [Fact]
    public void Form_ConfirmacaoHu_ExibeFeedbackDeSucessoComCaixaEHu()
    {
        string form = Form;
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        int fim = form.IndexOf("private void SubstituirCaixaNoCache(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string envio = form[inicio..fim];

        // Só apresenta o sucesso quando o cenário é Confirmado com snapshot persistido.
        Assert.Contains("resultado.Cenario == CenarioEnvioCaixaHu.Confirmado && resultado.Caixa is not null", envio, StringComparison.Ordinal);
        Assert.Contains("Caixa enviada ao SAP com sucesso.", envio, StringComparison.Ordinal);
        // Feedback contém número da caixa e HU SAP.
        Assert.Contains("Caixa: {numeroCaixaFmt}", envio, StringComparison.Ordinal);
        Assert.Contains("HU SAP: {confirmada.HandlingUnitExternalId}", envio, StringComparison.Ordinal);
        Assert.Contains("MessageBoxIcon.Information", envio, StringComparison.Ordinal);
    }

    // Grid é atualizada e a caixa confirmada é destacada/selecionável (apenas apresentação).
    [Fact]
    public void Form_Grid_SelecaoDeLinhaParaInspecao_SemHabilitarEnvio()
    {
        string form = Form;
        Assert.Contains("productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;", form, StringComparison.Ordinal);
        Assert.Contains("private void DestacarLinhaCaixaConfirmada()", form, StringComparison.Ordinal);
        // A grid é reconstruída após o envio (finally chama AtualizarGridCaixas, que destaca a linha).
        Assert.Contains("DestacarLinhaCaixaConfirmada();", form, StringComparison.Ordinal);
    }

    // REGRA CRÍTICA: a habilitação do envio depende do ESTADO da caixa ativa, NUNCA da linha selecionada.
    // Selecionar CONFIRMADA_SAP/CANCELADA/INDETERMINADO_TIMEOUT jamais habilita ENVIAR CAIXA SAP.
    [Fact]
    public void Form_SelecaoNaoHabilitaEnvio_ElegibilidadeSoPorEstado()
    {
        string form = Form;
        int inicio = form.IndexOf("private void AtualizarEstadoEnvioCaixaSap()", StringComparison.Ordinal);
        int fim = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string atualizar = form[inicio..fim];

        // Elegibilidade é derivada da caixa ATIVA (LastOrDefault), não da seleção da grid.
        Assert.Contains("_caixasPesadas.LastOrDefault()", atualizar, StringComparison.Ordinal);
        Assert.Contains("StatusIntegracaoCaixa.AguardandoAutorizacaoSap", atualizar, StringComparison.Ordinal);
        Assert.Contains("StatusIntegracaoCaixa.ProntaParaEnvio", atualizar, StringComparison.Ordinal);
        // O cálculo de estado do botão NÃO consulta a seleção/linha atual da grid.
        Assert.DoesNotContain("productionDataGridView.SelectedRows", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("productionDataGridView.CurrentRow", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("productionDataGridView.SelectedCells", atualizar, StringComparison.Ordinal);
        // CONFIRMADA_SAP/CANCELADA/INDETERMINADO_TIMEOUT não constam como estados elegíveis.
        int posElegivel = atualizar.IndexOf("bool elegivelEnvio", StringComparison.Ordinal);
        int posFimElegivel = atualizar.IndexOf(';', posElegivel);
        string expressaoElegivel = atualizar[posElegivel..posFimElegivel];
        Assert.DoesNotContain("ConfirmadaSap", expressaoElegivel, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelada", expressaoElegivel, StringComparison.Ordinal);
        Assert.DoesNotContain("IndeterminadoTimeout", expressaoElegivel, StringComparison.Ordinal);
    }

    // Continuidade: sucesso NÃO limpa a OP/contexto produtivo (fluxo da próxima caixa permanece funcional).
    [Fact]
    public void Form_ConfirmacaoHu_NaoLimpaOrdemNemContexto()
    {
        string form = Form;
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync(", StringComparison.Ordinal);
        int fim = form.IndexOf("private void SubstituirCaixaNoCache(", StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio);
        string envio = form[inicio..fim];
        // O envio não zera a OP nem chama LimparOp no sucesso (contexto produtivo preservado).
        Assert.DoesNotContain("_ordemAtual = null", envio, StringComparison.Ordinal);
        Assert.DoesNotContain("LimparOp()", envio, StringComparison.Ordinal);
    }

    // §6: runtime NÃO aprovado quando a configuração efetiva usa postgres; aprovado só com fugapet_dev_app.
    [Fact]
    public void Runtime_NaoAprovado_QuandoConfiguracaoUsaPostgres()
    {
        ConfiguracaoBancoPostgreSql comPostgres = new() { Usuario = "postgres" };
        Assert.False(DiagnosticoRuntimeBanco.RuntimeAplicacaoAprovado(comPostgres));
        Assert.Contains("RUNTIME_DB_ROLE_INCOMPATIVEL=fugapet_dev_app_nao_configurado",
            DiagnosticoRuntimeBanco.Classificar(comPostgres), StringComparison.Ordinal);

        ConfiguracaoBancoPostgreSql comApp = new() { Usuario = "fugapet_dev_app" };
        Assert.True(DiagnosticoRuntimeBanco.RuntimeAplicacaoAprovado(comApp));
        Assert.Equal("RUNTIME_FUGAPET_DEV_APP_VALIDADO", DiagnosticoRuntimeBanco.Classificar(comApp));
    }

    // §6: a Fábrica do repositório reporta o diagnóstico e NÃO usa SET ROLE em código produtivo.
    [Fact]
    public void Fabrica_ReportaRuntime_SemSetRoleProdutivo()
    {
        string fabrica = LerFonte("AcessoDados", "Repositorio", "FabricaProdutoAcabadoRepositorio.cs");
        Assert.Contains("DiagnosticoRuntimeBanco.AvisarSeRuntimeNaoAprovado", fabrica, StringComparison.Ordinal);

        string repo = LerFonte("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");
        Assert.DoesNotContain("SET ROLE", repo, StringComparison.OrdinalIgnoreCase);
        string service = LerFonte("Servicos", "Operacao", "ProdutoAcabadoHuService.cs");
        Assert.DoesNotContain("SET ROLE", service, StringComparison.OrdinalIgnoreCase);
    }
}
