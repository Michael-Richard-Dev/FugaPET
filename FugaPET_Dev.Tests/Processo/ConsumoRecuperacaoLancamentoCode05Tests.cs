using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 101J — recuperação READ-ONLY de consumo PENDENTE_SAP persistido, correspondente por MATCH
/// item-a-item à ocorrência do apontamento (OP + operação/sequência via conjunto de componentes filtrado),
/// e envio pela cerimônia/capability 261 bound ao MESMO PK. Reescreve o Code05 histórico (stale no 101E)
/// contra o CONTRATO REAL do HEAD 744d718 (resolvedor em <see cref="ConsumoMaterialConsultaServico"/> +
/// wiring da <c>ProcessoConsumoMaterialForm</c>). Fail-closed: nunca autosseleciona; ENVIANDO bloqueia.
/// </summary>
public sealed class ConsumoRecuperacaoLancamentoCode05Tests
{
    // ======================================================================================
    // MATCH item-a-item PURO (ConsumoMaterialConsultaServico.ItensCasamComComponentes)
    // ======================================================================================

    [Fact] // Base: dois itens casam 1:1 com dois componentes distintos.
    public void Match_DoisItens_CasamComComponentesDistintos()
    {
        var componentes = new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", dep: "PP01"), Comp("185", "2", "3500024", dep: "PP02") };
        var itens = new List<ConsumoMaterialItem> { Item("185", "1", "1000186", dep: "PP01"), Item("185", "2", "3500024", dep: "PP02") };
        Assert.True(ConsumoMaterialConsultaServico.ItensCasamComComponentes(itens, componentes));
    }

    [Fact] // Candidato vazio: nenhum item → inválido.
    public void Match_SemItens_Invalido()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes([], new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    [Fact] // C05: reserva divergente → rejeita.
    public void Match_ReservaDivergente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("999", "1", "1000186") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    [Fact] // C06: item da reserva divergente → rejeita.
    public void Match_ItemReservaDivergente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "9", "1000186") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    [Fact] // C04: material divergente → rejeita.
    public void Match_MaterialDivergente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "9999999") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    [Fact] // C08: depósito divergente → rejeita.
    public void Match_DepositoDivergente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", dep: "PP09") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", dep: "PP01") }));

    [Fact] // C07: lote divergente → rejeita.
    public void Match_LoteDivergente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", lote: "LOTE-B") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", lote: "LOTE-A") }));

    [Fact] // C10: identidade obrigatória de RESERVA ausente (depósito vazio) → fail-closed.
    public void Match_IdentidadeObrigatoriaAusente_FailClosed()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", dep: "") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", dep: "") }));

    [Fact] // GATE 102B: componente de ocorrência SEM lote (reserva pré-pesagem) casa com item persistido que
           // tem lote — o batch é atributo do consumo, não da reserva. Reproduz o incidente OP1000170/PK8.
    public void Match_ComponenteSemLote_CasaComItemComLote_102B()
        => Assert.True(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", dep: "PP01", lote: "0000000222") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", dep: "PP01", lote: "") }));

    [Fact] // GATE 102B: divergência REAL de batch (ambos com lote, diferentes) continua rejeitada.
    public void Match_LoteDivergenteAmbosPresentes_Rejeita_102B()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", dep: "PP01", lote: "LOTE-B") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186", dep: "PP01", lote: "LOTE-A") }));

    [Fact] // C09: movimento != 261 → rejeita.
    public void Match_MovimentoNao261_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186", tipoMov: "531") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    [Fact] // Ambiguidade: item casa com >1 componente idêntico → rejeita.
    public void Match_ItemComMaisDeUmComponente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186"), Comp("185", "1", "1000186") }));

    [Fact] // Ambiguidade: dois itens disputam o mesmo componente → rejeita.
    public void Match_DoisItensMesmoComponente_Rejeita()
        => Assert.False(ConsumoMaterialConsultaServico.ItensCasamComComponentes(
            new List<ConsumoMaterialItem> { Item("185", "1", "1000186"), Item("185", "1", "1000186") },
            new List<ComponenteConsumoMaterial> { Comp("185", "1", "1000186") }));

    // ======================================================================================
    // RESOLVEDOR READ-ONLY (ResolverRecuperacaoPendenteAsync) — cardinalidade/status
    // ======================================================================================

    [Fact] // C01: zero candidatos persistidos → Nenhum (fluxo novo).
    public async Task Resolver_ZeroCandidatos_Nenhum()
    {
        var repo = new FakeRepoRecuperacao();
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
        Assert.Null(res.CodigoLancamento);
    }

    [Fact] // C02/C19: exatamente um PENDENTE exato → recupera o PK EXATO (sem hardcode; chega por match).
    public async Task Resolver_UmPendenteExato_RecuperaPkExato()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8)],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]) }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, res.Modalidade);
        Assert.Equal(8, res.CodigoLancamento);
    }

    [Fact] // C03/C11-ambiguidade: dois PENDENTE exatos → Ambíguo, nenhum escolhido automaticamente.
    public async Task Resolver_DoisPendentesExatos_Ambiguo()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8), Resumo(9)],
            Detalhes =
            {
                [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]),
                [9] = Lanc(9, [Item("185", "1", "1000186", dep: "PP01")])
            }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.AmbiguoPendente, res.Modalidade);
        Assert.Null(res.CodigoLancamento);
        Assert.Equal(2, res.Candidatos.Count);
    }

    [Fact] // C04: candidato com material divergente → não casa → Nenhum.
    public async Task Resolver_MaterialDivergente_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8)],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "9999999", dep: "PP01")]) }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // C11: PENDENTE com documento SAP preenchido → não elegível → Nenhum.
    public async Task Resolver_PendenteComDocumento_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, doc: "4900006518")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], doc: "4900006518") }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // C12: PENDENTE com ano SAP preenchido → não elegível → Nenhum.
    public async Task Resolver_PendenteComAno_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, ex: "2026")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], ex: "2026") }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // C13: CONFIRMADO_SAP não é candidato de envio (nem blocker) → Nenhum.
    public async Task Resolver_Confirmado_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, status: "CONFIRMADO_SAP", doc: "4900006518", ex: "2026")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], status: "CONFIRMADO_SAP", doc: "4900006518", ex: "2026") }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // C14: CANCELADO_LOCAL não é candidato → Nenhum.
    public async Task Resolver_Cancelado_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, status: "CANCELADO_LOCAL")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], status: "CANCELADO_LOCAL") }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // C15/C27: ENVIANDO_SAP correspondente → EnviandoReconciliacao (blocker persistente), sem PK de envio.
    public async Task Resolver_Enviando_ReconciliacaoBlocker()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, status: "ENVIANDO_SAP")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], status: "ENVIANDO_SAP") }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, res.Modalidade);
        Assert.Null(res.CodigoLancamento);
    }

    [Fact] // §10: ENVIANDO domina mesmo havendo um PENDENTE elegível para a ocorrência.
    public async Task Resolver_EnviandoDominaPendente_Reconciliacao()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8, status: "PENDENTE_SAP"), Resumo(9, status: "ENVIANDO_SAP")],
            Detalhes =
            {
                [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]),
                [9] = Lanc(9, [Item("185", "1", "1000186", dep: "PP01")], status: "ENVIANDO_SAP")
            }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, res.Modalidade);
    }

    [Fact] // C17/C18: a RESOLUÇÃO é READ-ONLY — zero save, zero reserva/claim, zero confirmação, zero pesagem.
    public async Task Resolver_ReadOnly_ZeroEscritaZeroClaim()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8)],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]) }
        };
        _ = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(0, repo.Saves);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, repo.Confirmacoes);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact] // Contexto de OUTRO tipo de processo → resolvedor não atua (Nenhum).
    public async Task Resolver_TipoProcessoDiferente_Nenhum()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [Resumo(8)],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]) }
        };
        var contexto = new ContextoApontamentoProcesso
        { NumeroOrdem = "1000170", Operacao = "0010", Sequencia = "0", TipoProcesso = "RESULTADO_APONTAMENTO" };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(contexto, Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    // ======================================================================================
    // WIRING DA FORM (prova estrutural: seam sem behavioral acessível numa WinForms)
    // ======================================================================================

    [Fact] // §12 passo 5: resolve SOMENTE depois de materializar OP/componentes da ocorrência.
    public void Form_ResolveDepoisDeMaterializarComponentes()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string consulta = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");
        int idxPreencher = consulta.IndexOf("PreencherOrdemCarregada(resultado.Ordem, componentesOperacionais)", StringComparison.Ordinal);
        int idxResolver = consulta.IndexOf("ResolverRecuperacaoPersistidaDoContextoAsync(componentesOperacionais)", StringComparison.Ordinal);
        Assert.True(idxPreencher >= 0 && idxResolver >= 0 && idxPreencher < idxResolver,
            "A recuperação deve ocorrer DEPOIS de materializar OP/componentes.");
    }

    [Fact] // C19/C23/C25: recovery envia o PK RECUPERADO pela MESMA cerimônia/seam 261 (uma única).
    public void Form_EnvioRecuperado_UsaSeamUnicoComMesmoPk()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task EnviarSap261RecuperadoAsync");
        // Uma única cerimônia (AutorizarEEnviarSap261Async), com o PK recuperado.
        Assert.Contains("AutorizarEEnviarSap261Async(codigoLancamento, usuario)", metodo, StringComparison.Ordinal);
        Assert.Equal(1, ContarOcorrencias(metodo, "AutorizarEEnviarSap261Async"));
        // Não implementa segundo writer nem salva/cria pesagem no recovery.
        Assert.DoesNotContain("SalvarConsumoLocalAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("_pesagensPorComponente", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarPesagem", metodo, StringComparison.Ordinal);
    }

    [Fact] // C20: troca de contexto invalida o PK recuperado antes do envio (zero claim/POST).
    public void Form_TrocaContexto_InvalidaPkRecuperado()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task EnviarSap261RecuperadoAsync");
        Assert.Contains("!snapshot.Equals(SnapshotContextoRecuperacaoAtual())", metodo, StringComparison.Ordinal);
        Assert.Contains("LimparEstadoRecuperacao();", metodo, StringComparison.Ordinal);
    }

    [Fact] // C21/C22: recusa humana OU falha de auditoria (seam retorna null) ⇒ return, sem conclusão/HTTP extra.
    public void Form_RecusaOuAuditoriaFalha_NaoConclui()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task EnviarSap261RecuperadoAsync");
        int idxNull = metodo.IndexOf("if (resultado is null)", StringComparison.Ordinal);
        int idxReturn = idxNull >= 0 ? metodo.IndexOf("return;", idxNull, StringComparison.Ordinal) : -1;
        Assert.True(idxNull >= 0 && idxReturn > idxNull, "Seam null (recusa/auditoria) deve retornar sem confirmar.");
    }

    [Fact] // C26: happy path materializa ConfirmadoSap com vínculo = MESMO PK recuperado.
    public void Form_HappyPath_ConfirmadoSapMesmoPk()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task EnviarSap261RecuperadoAsync");
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConfirmadoSap, codigoLancamento", metodo, StringComparison.Ordinal);
        Assert.Contains("confirmadoSap: true", metodo, StringComparison.Ordinal);
    }

    [Fact] // C16: recovery ativo bloqueia CONFIRMAR CONSUMO (não cria PK concorrente).
    public void Form_RecoveryAtivo_BloqueiaNovoConsumo()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarConsumoAsync");
        Assert.Contains("if (RecuperacaoBloqueiaNovoConsumo())", metodo, StringComparison.Ordinal);

        string iniciar = ExtrairMetodo(form, "private bool PodeIniciarLeituraConsumo");
        Assert.Contains("!RecuperacaoBloqueiaNovoConsumo()", iniciar, StringComparison.Ordinal);
    }

    [Fact] // §13: ONE_PENDING materializa "Enviar SAP 261" e bloqueia ações concorrentes.
    public void Form_UmPendente_MaterializaEnvioEBloqueia()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AplicarMaterializacaoRecuperacaoPendente");
        Assert.Contains("_enviarSap261Button.Visible = true;", metodo, StringComparison.Ordinal);
        Assert.Contains("BloquearAcoesConcorrentesRecuperacao();", metodo, StringComparison.Ordinal);

        string bloqueio = ExtrairMetodo(form, "private void BloquearAcoesConcorrentesRecuperacao");
        Assert.Contains("iniciarLeituraButton.Enabled = false;", bloqueio, StringComparison.Ordinal);
        Assert.Contains("SetReadWeightEnabled(false);", bloqueio, StringComparison.Ordinal);
    }

    [Fact] // §10/§11: ambíguo/ENVIANDO bloqueiam novo consumo e NÃO materializam envio.
    public void Form_AmbiguoOuEnviando_BloqueiaSemEnvio()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AplicarBloqueioReconciliacao");
        Assert.Contains("_enviarSap261Button.Visible = false;", metodo, StringComparison.Ordinal);
        Assert.Contains("BloquearAcoesConcorrentesRecuperacao();", metodo, StringComparison.Ordinal);
    }

    [Fact] // C28: recovery de CONFIRMADO_SAP existente segue intacto no Controle de Apontamentos.
    public void ControleApontamentos_RecoveryConfirmado_Preservado()
    {
        string servico = Fonte("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");
        Assert.Contains("public async Task<bool> RecuperarConsumoConfirmadoAsync(", servico, StringComparison.Ordinal);
        Assert.Contains("TentarRecuperarConsumoConfirmadoAsync(", servico, StringComparison.Ordinal);
    }

    // ======================================================================================
    // GATE 101L — FAIL-CLOSED da resolução persistida (F01–F16)
    // ======================================================================================

    [Fact] // F01: resolvedor lança exceção → NÃO devolve Nenhum; propaga (a Form materializa FalhaResolucaoPersistida).
    public async Task F01_ResolvedorLancaExcecao_NaoViraNenhum()
    {
        var repo = new FakeRepoFalha();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia()));
    }

    [Fact] // F01(Form): o catch materializa FalhaResolucaoPersistida (≠ Nenhum) e NÃO chama LimparEstadoRecuperacao.
    public void F01_FormCatch_MaterializaFalhaFailClosed()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ResolverRecuperacaoPersistidaDoContextoAsync");
        string corpoCatch = ExtrairBloco(metodo, "catch (Exception ex)");
        // O catch delega ao helper fail-closed compartilhado; NÃO reduz a Nenhum via LimparEstadoRecuperacao.
        Assert.Contains("AplicarFalhaResolucaoPersistida()", corpoCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("LimparEstadoRecuperacao()", corpoCatch, StringComparison.Ordinal);
        // O helper materializa FalhaResolucaoPersistida (≠ Nenhum).
        string helper = ExtrairMetodo(form, "private void AplicarFalhaResolucaoPersistida");
        Assert.Contains("ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida", helper, StringComparison.Ordinal);
    }

    [Theory] // F02–F07: falha bloqueia leitura/pesagem(ler+digitar)/confirmar/save/lançamento (default-deny).
    [InlineData(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida)]
    [InlineData(ModalidadeRecuperacaoConsumo.UmPendente)]
    [InlineData(ModalidadeRecuperacaoConsumo.AmbiguoPendente)]
    [InlineData(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao)]
    public void F02_a_F07_ModalidadesBloqueantes_BloqueiamNovoConsumo(ModalidadeRecuperacaoConsumo modalidade)
        => Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(modalidade));

    [Fact] // Fresh flow só com Nenhum.
    public void SomenteNenhum_LiberaNovoConsumo()
        => Assert.False(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo.Nenhum));

    [Fact] // F08/F09/F10/F11: falha NÃO habilita envio recuperado ⇒ zero capability/claim/HTTP; PK não é inventado.
    public void F08_a_F11_Falha_NaoHabilitaEnvioRecuperado()
    {
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida, null));
        // Mesmo que um PK residual existisse, a modalidade Falha nunca autoriza envio.
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida, 8));
        // Só UmPendente + PK autoriza.
        Assert.True(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo.UmPendente, 8));
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo.UmPendente, null));
    }

    [Fact] // F11(Form): o catch zera o PK recuperado (nenhum PK inventado na falha).
    public void F11_FormCatch_ZeraPkRecuperado()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        // O catch delega ao helper compartilhado, que zera PK/snapshot (nenhum PK inventado na falha).
        string helper = ExtrairMetodo(form, "private void AplicarFalhaResolucaoPersistida");
        Assert.Contains("_codigoLancamentoRecuperado = null;", helper, StringComparison.Ordinal);
        Assert.Contains("_snapshotRecuperacao = null;", helper, StringComparison.Ordinal);
    }

    [Fact] // F12: falha seguida de nova resolução bem-sucedida com 0 candidatos → SOMENTE então Nenhum.
    public async Task F12_FalhaDepoisSucessoZero_ViraNenhum()
    {
        var repo = new FakeRepoFalhaDepoisSucesso { Candidatos = [] };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia()));
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, res.Modalidade);
    }

    [Fact] // F13: falha seguida de nova resolução com 1 PENDENTE → UmPendente / PK correto.
    public async Task F13_FalhaDepoisSucessoUmPendente_RecuperaPk()
    {
        var repo = new FakeRepoFalhaDepoisSucesso
        {
            Candidatos = [Resumo(8)],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")]) }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia()));
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, res.Modalidade);
        Assert.Equal(8, res.CodigoLancamento);
    }

    [Fact] // F14: falha seguida de ENVIANDO → EnviandoReconciliacao.
    public async Task F14_FalhaDepoisEnviando_Reconciliacao()
    {
        var repo = new FakeRepoFalhaDepoisSucesso
        {
            Candidatos = [Resumo(8, status: "ENVIANDO_SAP")],
            Detalhes = { [8] = Lanc(8, [Item("185", "1", "1000186", dep: "PP01")], status: "ENVIANDO_SAP") }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia()));
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(Contexto(), Ocorrencia());
        Assert.Equal(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, res.Modalidade);
    }

    [Fact] // F15: diagnóstico do catch NÃO expõe exception bruta/segredo (só EXCEPTION_TYPE + tipo).
    public void F15_Diagnostico_NaoExpoeExceptionBruta()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ResolverRecuperacaoPersistidaDoContextoAsync");
        string corpoCatch = ExtrairBloco(metodo, "catch (Exception ex)");
        Assert.Contains("ex.GetType().Name", corpoCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.Message", corpoCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.ToString()", corpoCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.StackTrace", corpoCatch, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.InnerException", corpoCatch, StringComparison.Ordinal);
    }

    [Fact] // F16: valor de modalidade DESCONHECIDO → bloqueia por padrão (default-deny).
    public void F16_ModalidadeDesconhecida_BloqueiaPorPadrao()
    {
        var desconhecida = (ModalidadeRecuperacaoConsumo)999;
        Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(desconhecida));
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(desconhecida, 8));
    }

    // ======================================================================================
    // GATE 101N — modalidade DESCONHECIDA fail-closed no wiring da Form (U01–U12)
    // ======================================================================================

    private const ModalidadeRecuperacaoConsumo ModalidadeDesconhecida = (ModalidadeRecuperacaoConsumo)999;

    [Fact] // U01(wiring): a Form decide por RecuperacaoConsumoPolitica.ModalidadeEfetiva e o switch NÃO tem
           // default→LimparEstadoRecuperacao; valor desconhecido cai em FalhaResolucaoPersistida (não Nenhum).
    public void U01_FormWiring_DesconhecidoNaoViraNenhum()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ResolverRecuperacaoPersistidaDoContextoAsync");
        // A tela decide pelo mapeamento canônico default-deny.
        Assert.Contains("RecuperacaoConsumoPolitica.ModalidadeEfetiva(resultado.Modalidade)", metodo, StringComparison.Ordinal);
        Assert.Contains("switch (modalidadeEfetiva)", metodo, StringComparison.Ordinal);
        // Existe case Nenhum EXPLÍCITO (único a limpar/liberar) e case Falha+default fail-closed.
        Assert.Contains("case global::FugaPET_Dev.Modelo.Consumo.ModalidadeRecuperacaoConsumo.Nenhum:", metodo, StringComparison.Ordinal);
        Assert.Contains("case global::FugaPET_Dev.Modelo.Consumo.ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida:", metodo, StringComparison.Ordinal);
        // O bloco default NÃO pode reduzir a Nenhum via LimparEstadoRecuperacao.
        string blocoDefault = ExtrairBlocoDefault(metodo);
        Assert.Contains("AplicarFalhaResolucaoPersistida()", blocoDefault, StringComparison.Ordinal);
        Assert.DoesNotContain("LimparEstadoRecuperacao()", blocoDefault, StringComparison.Ordinal);
    }

    [Fact] // U02: modalidade desconhecida → efetiva = FalhaResolucaoPersistida (≠ Nenhum). Mesma função que a Form usa.
    public void U02_Desconhecido_EfetivaFalha()
    {
        ModalidadeRecuperacaoConsumo efetiva = RecuperacaoConsumoPolitica.ModalidadeEfetiva(ModalidadeDesconhecida);
        Assert.Equal(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida, efetiva);
        Assert.NotEqual(ModalidadeRecuperacaoConsumo.Nenhum, efetiva);
    }

    [Fact] // U03–U07: modalidade efetiva de um desconhecido bloqueia leitura/peso/confirmar/save/lançamento.
    public void U03_a_U07_Desconhecido_BloqueiaNovoConsumo()
    {
        ModalidadeRecuperacaoConsumo efetiva = RecuperacaoConsumoPolitica.ModalidadeEfetiva(ModalidadeDesconhecida);
        Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(efetiva));
    }

    [Fact] // U08–U10: modalidade efetiva de um desconhecido não habilita envio ⇒ zero capability/claim/HTTP.
    public void U08_a_U10_Desconhecido_NaoHabilitaEnvio()
    {
        ModalidadeRecuperacaoConsumo efetiva = RecuperacaoConsumoPolitica.ModalidadeEfetiva(ModalidadeDesconhecida);
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(efetiva, null));
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(efetiva, 8));
    }

    [Fact] // U01(estado): o case default materializa PK/snapshot null (via AplicarFalhaResolucaoPersistida).
    public void U01_Default_ZeraPkSnapshot()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string helper = ExtrairMetodo(form, "private void AplicarFalhaResolucaoPersistida");
        Assert.Contains("_codigoLancamentoRecuperado = null;", helper, StringComparison.Ordinal);
        Assert.Contains("_snapshotRecuperacao = null;", helper, StringComparison.Ordinal);
        Assert.Contains("ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida", helper, StringComparison.Ordinal);
        Assert.Contains("AplicarBloqueioReconciliacao(", helper, StringComparison.Ordinal);
    }

    [Fact] // U11: SOMENTE Nenhum explícito libera fresh flow (mapeia para si mesmo e não bloqueia).
    public void U11_NenhumExplicito_LiberaFreshFlow()
    {
        Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, RecuperacaoConsumoPolitica.ModalidadeEfetiva(ModalidadeRecuperacaoConsumo.Nenhum));
        Assert.False(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo.Nenhum));
    }

    [Fact] // U12: FalhaResolucaoPersistida explícita continua bloqueando (mapeia para si mesma).
    public void U12_FalhaExplicita_ContinuaBloqueando()
    {
        Assert.Equal(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida,
            RecuperacaoConsumoPolitica.ModalidadeEfetiva(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida));
        Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida));
    }

    [Theory] // Todo valor conhecido ≠ Nenhum é bloqueante; Nenhum é o único liberador (mapeamento identidade).
    [InlineData(ModalidadeRecuperacaoConsumo.Nenhum, false)]
    [InlineData(ModalidadeRecuperacaoConsumo.UmPendente, true)]
    [InlineData(ModalidadeRecuperacaoConsumo.AmbiguoPendente, true)]
    [InlineData(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, true)]
    [InlineData(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida, true)]
    public void ModalidadeEfetiva_MapeamentoConhecido(ModalidadeRecuperacaoConsumo entrada, bool bloqueia)
    {
        Assert.Equal(entrada, RecuperacaoConsumoPolitica.ModalidadeEfetiva(entrada));
        Assert.Equal(bloqueia, RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(RecuperacaoConsumoPolitica.ModalidadeEfetiva(entrada)));
    }

    // ======================================================================================
    // GATE 101U — pós-retomada: refresh genérico NÃO reabilita fresh-flow (P01–P14)
    // Contexto operacional congelado: OP 1000170 / operação 0010 / PK 8 / material 1000186 /
    // reserva 185 item 1 / depósito PP01 / lote 0000000222 / movimento 261.
    // ======================================================================================

    private static ContextoApontamentoProcesso ContextoRetomada()
        => new() { NumeroOrdem = "1000170", Operacao = "0010", Sequencia = "0", TipoProcesso = "CONSUMO_MATERIA_PRIMA" };

    private static List<ComponenteConsumoMaterial> OcorrenciaRetomada()
        => [new()
        {
            CodigoMaterial = "1000186", NumeroReserva = "185", ItemReserva = "1",
            DepositoConsumo = "PP01", Lote = "0000000222", TipoMovimento = "261"
        }];

    private static ConsumoMaterialItem ItemPk8()
        => new()
        {
            CodigoMaterial = "1000186", NumeroReserva = "185", ItemReserva = "1",
            DepositoConsumo = "PP01", Lote = "0000000222", TipoMovimentoSap = "261"
        };

    [Fact] // P01/P02: retomada + exatamente 1 PENDENTE (PK8) → UmPendente / PK recuperado 8 (por match, sem hardcode).
    public async Task P01_P02_Retomada_UmPendentePk8()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }],
            Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] } }
        };
        var res = await Servico(repo).ResolverRecuperacaoPendenteAsync(ContextoRetomada(), OcorrenciaRetomada());
        Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, res.Modalidade);
        Assert.Equal(8, res.CodigoLancamento);
    }

    [Fact] // P14: a RESOLUÇÃO (materialização) é read-only: zero save/weight/claim/capability/HTTP.
    public async Task P14_Materializacao_ZeroEfeitos()
    {
        var repo = new FakeRepoRecuperacao
        {
            Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }],
            Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] } }
        };
        _ = await Servico(repo).ResolverRecuperacaoPendenteAsync(ContextoRetomada(), OcorrenciaRetomada());
        Assert.Equal(0, repo.Saves);      // SAVE_COUNT
        Assert.Equal(0, repo.Reservas);   // CLAIM_COUNT (TentarReservarEnvioSap)
        Assert.Equal(0, repo.Confirmacoes);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact] // P03/P04/P05: sob UmPendente, o refresh genérico de seleção YIELDS ao recovery (não segue fresh-flow).
    public void P03_a_P05_RefreshGenerico_YieldsAoRecovery()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarComponenteSelecionadoDoGrid");
        // O entrypoint de refresh de seleção consulta o blocker ANTES de capturar/normalizar seleção.
        Assert.Contains("if (RecuperacaoBloqueiaNovoConsumo())", metodo, StringComparison.Ordinal);
        Assert.Contains("ReaplicarEstadoRecuperacao();", metodo, StringComparison.Ordinal);
        int idxGuard = metodo.IndexOf("RecuperacaoBloqueiaNovoConsumo()", StringComparison.Ordinal);
        int idxCaptura = metodo.IndexOf("CapturarComponenteSelecionadoDoGridPrincipal()", StringComparison.Ordinal);
        Assert.True(idxGuard >= 0 && idxCaptura > idxGuard, "O guard de recovery deve preceder a captura normal.");
    }

    [Fact] // Leitura/peso/confirmar continuam bloqueados sob UmPendente (blocker é autoridade final).
    public void P03_a_P05_UmPendente_BloqueiaLeituraPesoConfirmar()
    {
        // PodeIniciarLeituraConsumo consulta o blocker (leitura); a reaplicação bloqueia peso/confirmar.
        Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo.UmPendente));
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string pode = ExtrairMetodo(form, "private bool PodeIniciarLeituraConsumo");
        Assert.Contains("!RecuperacaoBloqueiaNovoConsumo()", pode, StringComparison.Ordinal);
        string reaplicar = ExtrairMetodo(form, "private void ReaplicarEstadoRecuperacao");
        Assert.Contains("AplicarMaterializacaoRecuperacaoPendente", reaplicar, StringComparison.Ordinal);
        string materializa = ExtrairMetodo(form, "private void AplicarMaterializacaoRecuperacaoPendente");
        Assert.Contains("BloquearAcoesConcorrentesRecuperacao();", materializa, StringComparison.Ordinal);
    }

    [Fact] // P06/P07: sob UmPendente o refresh reaplica "Enviar SAP 261" para o PK recuperado (mesma cerimônia/PK).
    public void P06_P07_RefreshMantemEnvioComPkRecuperado()
    {
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string reaplicar = ExtrairMetodo(form, "private void ReaplicarEstadoRecuperacao");
        // Só materializa envio quando a política autoriza (UmPendente + PK), usando o PK recuperado.
        Assert.Contains("PermiteEnvioRecuperado(", reaplicar, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoRecuperado is long codigoRecuperado", reaplicar, StringComparison.Ordinal);
        Assert.Contains("AplicarMaterializacaoRecuperacaoPendente(codigoRecuperado)", reaplicar, StringComparison.Ordinal);
        // O envio recuperado usa o PK recuperado (não save de sessão), pela cerimônia única.
        string envio = ExtrairMetodo(form, "private async Task EnviarSap261RecuperadoAsync");
        Assert.Contains("AutorizarEEnviarSap261Async(codigoLancamento, usuario)", envio, StringComparison.Ordinal);
    }

    [Fact] // P08: Nenhum (resolução com sucesso, sem estado) → fresh-flow preservado (refresh segue caminho normal).
    public void P08_Nenhum_FreshFlowPreservado()
    {
        Assert.False(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo.Nenhum));
        // Com Nenhum, AtualizarComponenteSelecionadoDoGrid NÃO entra no guard e segue para a captura normal.
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarComponenteSelecionadoDoGrid");
        Assert.Contains("CapturarComponenteSelecionadoDoGridPrincipal();", metodo, StringComparison.Ordinal);
    }

    [Theory] // P09/P10/P11/P12: Ambíguo/Enviando/Falha/desconhecido continuam bloqueados após refresh (default-deny).
    [InlineData(ModalidadeRecuperacaoConsumo.AmbiguoPendente)]
    [InlineData(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao)]
    [InlineData(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida)]
    [InlineData((ModalidadeRecuperacaoConsumo)999)]
    public void P09_a_P12_EstadosBloqueantes_ContinuamBloqueadosAposRefresh(ModalidadeRecuperacaoConsumo modalidade)
    {
        Assert.True(RecuperacaoConsumoPolitica.BloqueiaNovoConsumo(RecuperacaoConsumoPolitica.ModalidadeEfetiva(modalidade)));
        // Não materializam envio (não são UmPendente).
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(RecuperacaoConsumoPolitica.ModalidadeEfetiva(modalidade), 8));
    }

    [Fact] // P13: sem recovered PK, "Enviar SAP 261" não pode ficar acionável como recovery.
    public void P13_SemPkRecuperado_EnvioRecoveryNaoAcionavel()
    {
        Assert.False(RecuperacaoConsumoPolitica.PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo.UmPendente, null));
        // O disparo de envio recuperado no botão exige o PK do recovery (via seam PkEnvioRecuperadoAtual, que
        // por sua vez exige PermiteEnvioRecuperado + PK). Prova comportamental do PK está no teste STA da Form.
        string form = Fonte("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string enviar = ExtrairMetodo(form, "private async Task EnviarSap261Async");
        Assert.Contains("PkEnvioRecuperadoAtual() is long codigoRecuperado", enviar, StringComparison.Ordinal);
        string seam = ExtrairMetodo(form, "internal long? PkEnvioRecuperadoAtual");
        Assert.Contains("PermiteEnvioRecuperado(", seam, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoRecuperado", seam, StringComparison.Ordinal);
    }

    // ======================================================================================
    // helpers
    // ======================================================================================

    private static ConsumoMaterialConsultaServico Servico(IConsumoMaterialRepositorio repo)
        => new(() => repo, new ConsumoMaterialServico());

    private static ContextoApontamentoProcesso Contexto()
        => new() { NumeroOrdem = "1000170", Operacao = "0010", Sequencia = "0", TipoProcesso = "CONSUMO_MATERIA_PRIMA" };

    private static List<ComponenteConsumoMaterial> Ocorrencia()
        => [Comp("185", "1", "1000186", dep: "PP01")];

    private static ComponenteConsumoMaterial Comp(
        string reserva, string itemReserva, string material,
        string dep = "PP01", string lote = "0000000222")
        => new()
        {
            CodigoMaterial = material, NumeroReserva = reserva, ItemReserva = itemReserva,
            DepositoConsumo = dep, Lote = lote, TipoMovimento = "261"
        };

    private static ConsumoMaterialItem Item(
        string reserva, string itemReserva, string material,
        string dep = "PP01", string lote = "0000000222", string tipoMov = "261")
        => new()
        {
            CodigoMaterial = material, NumeroReserva = reserva, ItemReserva = itemReserva,
            DepositoConsumo = dep, Lote = lote, TipoMovimentoSap = tipoMov
        };

    private static ResumoConsumoMaterialLancamento Resumo(long codigo, string status = "PENDENTE_SAP", string? doc = null, string? ex = null)
        => new() { CodigoLancamento = codigo, NumeroOrdem = "1000170", StatusLancamento = status, DocumentoMaterialSap = doc, ExercicioDocumentoMaterialSap = ex };

    private static ConsumoMaterialLancamento Lanc(long codigo, List<ConsumoMaterialItem> itens, string status = "PENDENTE_SAP", string? doc = null, string? ex = null)
        => new() { Codigo = codigo, NumeroOrdem = "1000170", StatusLancamento = status, DocumentoMaterialSap = doc, ExercicioDocumentoMaterialSap = ex, Itens = itens };

    private static int ContarOcorrencias(string fonte, string agulha)
    {
        int total = 0, i = 0;
        while ((i = fonte.IndexOf(agulha, i, StringComparison.Ordinal)) >= 0) { total++; i += agulha.Length; }
        return total;
    }

    private static string Fonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && raiz is not null; i++)
        {
            string candidato = Path.Combine(raiz, Path.Combine(partes));
            if (File.Exists(candidato))
            {
                return File.ReadAllText(candidato);
            }

            string projeto = Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes));
            if (File.Exists(projeto))
            {
                return File.ReadAllText(projeto);
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException($"Fonte não encontrada: {string.Join('/', partes)}");
    }

    /// <summary>Trecho do case default de um switch: de "default:" até o primeiro "break;" seguinte.</summary>
    private static string ExtrairBlocoDefault(string metodo)
    {
        int inicio = metodo.IndexOf("default:", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "case default não encontrado");
        int fim = metodo.IndexOf("break;", inicio, StringComparison.Ordinal);
        return fim >= 0 ? metodo[inicio..(fim + "break;".Length)] : metodo[inicio..];
    }

    /// <summary>Extrai o bloco { ... } balanceado que começa no primeiro '{' após <paramref name="ancora"/>.</summary>
    private static string ExtrairBloco(string fonte, string ancora)
    {
        int inicio = fonte.IndexOf(ancora, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Âncora não encontrada: {ancora}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte.Substring(abre, i - abre + 1);
                }
            }
        }

        return fonte[abre..];
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte.Substring(inicio, i - inicio + 1);
                }
            }
        }

        return fonte[inicio..];
    }

    private sealed class FakeRepoRecuperacao : IConsumoMaterialRepositorio
    {
        public IReadOnlyList<ResumoConsumoMaterialLancamento> Candidatos { get; init; } = [];
        public Dictionary<long, ConsumoMaterialLancamento> Detalhes { get; init; } = new();
        public int Reservas { get; private set; }
        public int Confirmacoes { get; private set; }
        public int Falhas { get; private set; }
        public int Saves { get; private set; }

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.FromResult(1L);
        }

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
            ConsultaConsumoMaterialFiltro filtro, CancellationToken cancellationToken = default)
            => Task.FromResult(Candidatos);

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
        {
            Reservas++;
            return Task.FromResult(false);
        }

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
        {
            Falhas++;
            return Task.CompletedTask;
        }

        public Task MarcarConsumoConfirmadoSapAsync(long codigoLancamento, string? documentoMaterialSap, string? exercicioDocumentoMaterialSap, DateTime enviadoSapEmUtc, CancellationToken cancellationToken = default)
        {
            Confirmacoes++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Repo que SEMPRE falha ao consultar — simula indisponibilidade técnica na resolução (F01–F11).</summary>
    private sealed class FakeRepoFalha : IConsumoMaterialRepositorio
    {
        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(ConsultaConsumoMaterialFiltro filtro, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");

        public Task MarcarConsumoConfirmadoSapAsync(long codigoLancamento, string? documentoMaterialSap, string? exercicioDocumentoMaterialSap, DateTime enviadoSapEmUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");
    }

    /// <summary>Repo que FALHA na 1ª consulta e SUCEDE nas seguintes — prova retentativa READ-ONLY (F12–F14).</summary>
    private sealed class FakeRepoFalhaDepoisSucesso : IConsumoMaterialRepositorio
    {
        private int _consultas;

        public IReadOnlyList<ResumoConsumoMaterialLancamento> Candidatos { get; init; } = [];
        public Dictionary<long, ConsumoMaterialLancamento> Detalhes { get; init; } = new();

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(ConsultaConsumoMaterialFiltro filtro, CancellationToken cancellationToken = default)
        {
            _consultas++;
            if (_consultas == 1)
            {
                throw new InvalidOperationException("falha simulada na primeira consulta");
            }

            return Task.FromResult(Candidatos);
        }

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarcarConsumoConfirmadoSapAsync(long codigoLancamento, string? documentoMaterialSap, string? exercicioDocumentoMaterialSap, DateTime enviadoSapEmUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
