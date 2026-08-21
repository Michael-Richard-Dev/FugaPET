using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoEntradaProdutoLotesIntegracaoTelaTests
{
    [Fact]
    public void Start_DeveIniciarOperacaoPorContextoDaTela()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string start = ExtrairTrecho(form, "private async void StartProduction_Click", "private void StartProductionDevicesWarmUp");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");

        Assert.Contains("GarantirOperacaoLotesIniciada()", start, StringComparison.Ordinal);
        Assert.Contains("ContextoOperacaoEntradaProdutoLotes context", iniciar, StringComparison.Ordinal);
        Assert.Contains("NumeroPedido = numeroPedido", iniciar, StringComparison.Ordinal);
        Assert.Contains("Fornecedor = lotTextBox.Text.Trim()", iniciar, StringComparison.Ordinal);
        Assert.Contains("CodigoSetor = codigoSetor", iniciar, StringComparison.Ordinal);
        Assert.Contains("Terminal = ObterNomeTerminalAtual()", iniciar, StringComparison.Ordinal);
        Assert.Contains("ModoEntradaMaterial = _modoEntrada", iniciar, StringComparison.Ordinal);
        Assert.Contains("_controller.IniciarOperacaoComLotes(", iniciar, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_DeveSuportarMateriaPrimaEQuimicosSemDuplicarTela()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");

        Assert.Contains("ModoEntradaMaterial modo", form, StringComparison.Ordinal);
        Assert.Contains("readonly global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial _modoEntrada", form, StringComparison.Ordinal);
        Assert.Contains("ModoEntradaMaterial.MateriaPrima", form, StringComparison.Ordinal);
        Assert.Contains("ModoEntradaMaterial = _modoEntrada", iniciar, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessoEntradaQuimico", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_DeveUsarItensSapCarregadosENaoLinhasDoGridComoFonte()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");

        Assert.Contains("_itensPedidoCarregados", iniciar, StringComparison.Ordinal);
        Assert.Contains("_controller.IniciarOperacaoComLotes(", iniciar, StringComparison.Ordinal);
        Assert.DoesNotContain("productionDataGridView.Rows", iniciar, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_DeveBloquearSetorInvalidoEFalhaSemAtivarLeitura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string start = ExtrairTrecho(form, "private async void StartProduction_Click", "private void StartProductionDevicesWarmUp");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");

        Assert.Contains("_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0", iniciar, StringComparison.Ordinal);
        Assert.Contains("_isProductionStarted = false;", start, StringComparison.Ordinal);
        Assert.Contains("UpdateProductionState(false);", start, StringComparison.Ordinal);
    }

    [Fact]
    public void RetomadaMesmoPedidoPreservaOperacaoEDiferenteBloqueiaTroca()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");
        string consulta = ExtrairMetodo(form, "private async Task AtualizarDadosPedidoSelecionadoAsync");

        Assert.Contains("estadoAtual.OperacaoIniciada", iniciar, StringComparison.Ordinal);
        Assert.Contains("string.Equals(estadoAtual.NumeroPedido, numeroPedido", iniciar, StringComparison.Ordinal);
        Assert.Contains("estadoAtual.ModoEntradaMaterial == _modoEntrada", iniciar, StringComparison.Ordinal);
        Assert.Contains("Operação por lotes retomada", iniciar, StringComparison.Ordinal);
        Assert.Contains("BloquearTrocaPedidoComOperacaoEmMemoria(numeroPedido, restaurarTexto: true)", consulta, StringComparison.Ordinal);
        Assert.Contains("BloquearTrocaPedidoComOperacaoEmMemoria", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimeiroPesoDoItemSemLote_DeveSolicitarDialogoEConfirmarLoteNoController()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string garantirLote = ExtrairMetodo(form, "private async Task<bool> GarantirLoteAtivoParaLinhaAsync");
        string dialogoPadrao = ExtrairMetodo(form, "private static DadosLoteEntrada? SolicitarDadosLotePadrao");

        Assert.Contains("_solicitarDadosLote(this, _modoEntrada)", garantirLote, StringComparison.Ordinal);
        Assert.Contains("if (dados is null)", garantirLote, StringComparison.Ordinal);
        Assert.Contains("return false;", garantirLote, StringComparison.Ordinal);
        Assert.Contains("_controller.ConfirmarLoteOperacaoComLotes", garantirLote, StringComparison.Ordinal);
        Assert.Contains("item?.PodePesar", garantirLote, StringComparison.Ordinal);
        Assert.Contains("using EntradaProdutoDadosLoteForm form = new(modoEntrada)", dialogoPadrao, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogo_DeveReceberModoCorretoSemAlterarDesigner()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.Designer.cs");

        Assert.Contains("Func<IWin32Window, ModoEntradaMaterial, DadosLoteEntrada?>", form, StringComparison.Ordinal);
        Assert.Contains("solicitarDadosLote", form, StringComparison.Ordinal);
        Assert.Contains("_solicitarDadosLote = solicitarDadosLote ?? SolicitarDadosLotePadrao", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoDadosLoteForm", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void LotTextBox_ContinuaFornecedorENaoArmazenaNumeroDoLote()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string iniciar = ExtrairMetodo(form, "private bool GarantirOperacaoLotesIniciada");

        Assert.Contains("Fornecedor = lotTextBox.Text.Trim()", iniciar, StringComparison.Ordinal);
        Assert.DoesNotContain("lotTextBox.Text = dados.NumeroLote", form, StringComparison.Ordinal);
        Assert.DoesNotContain("lotTextBox.Text = estadoLote", form, StringComparison.Ordinal);
    }

    [Fact]
    public void LeituraBalanca_DeveRegistrarPesoSomenteViaOrquestrador()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string leitura = ExtrairTrecho(form, "private async void ReadWeightLegend_Click", "private bool RegistrarPesoLido");
        string registrar = ExtrairMetodo(form, "private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync");

        Assert.Contains("RegistrarPesoLidoOperacaoComLotesAsync", leitura, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.OrigemBalanca", leitura, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarPesagemOperacaoComLotes", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarPesoLido(linhaItem, weight", leitura, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirEtiquetaPorPesagem", leitura, StringComparison.Ordinal);
    }

    [Fact]
    public void LeituraManual_DeveRegistrarPesoSomenteViaOrquestrador()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string manual = ExtrairTrecho(form, "private async void LeituraManual_Click", "private void UpdateProductionState");
        string registrar = ExtrairMetodo(form, "private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync");

        Assert.Contains("RegistrarPesoLidoOperacaoComLotesAsync", manual, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.OrigemManual", manual, StringComparison.Ordinal);
        Assert.Contains("manualWeight", manual, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarPesagemOperacaoComLotes", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirEtiquetaPorPesagem", manual, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_NaoDeveCriarSequenciaGuidOuPesagemCanonicaNaTelaPrincipal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string registrar = ExtrairMetodo(form, "private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync");
        string multipla = ExtrairMetodo(form, "private async Task AbrirPesagemMultiplaParaLinhaAsync");

        Assert.DoesNotContain("Sequencia =", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("new EntradaProdutoPesagem", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", multipla, StringComparison.Ordinal);
    }

    [Fact]
    public void Registro_DevePreservarOrigemBalancaOuManualComMetadadosCorretos()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string registrar = ExtrairMetodo(form, "private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync");

        Assert.Contains("string.Equals(origem, EntradaProdutoPesagemCalculos.OrigemBalanca", registrar, StringComparison.Ordinal);
        Assert.Contains("? _idBalancaSelecionada", registrar, StringComparison.Ordinal);
        Assert.Contains("origem", registrar, StringComparison.Ordinal);
        Assert.Contains("leituraOriginal", registrar, StringComparison.Ordinal);
        Assert.Contains("DateTimeOffset.Now", registrar, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjecaoVisual_DeveSincronizarLeiturasAPartirDoEstadoDoController()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string sincronizar = ExtrairMetodo(form, "private bool SincronizarLeiturasItemComOperacaoLotes");
        string totais = ExtrairMetodo(form, "private static bool AtualizarTotaisDaLinha");

        Assert.Contains("_controller.ObterPesagensItemOperacaoComLotes", sincronizar, StringComparison.Ordinal);
        Assert.Contains("Select(p => p.Pesagem)", sincronizar, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.SomarPesoBrutoValido", totais, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.SomarPesoBrutoValido", totais, StringComparison.Ordinal);
    }

    [Fact]
    public void PesagemMultipla_DeveReceberSomenteLoteAtivoECallbacksCanonicos()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string multipla = ExtrairMetodo(form, "private async Task AbrirPesagemMultiplaParaLinhaAsync");

        Assert.Contains("_controller.ObterPesagensLoteAtivoOperacaoComLotes(codigoItem)", multipla, StringComparison.Ordinal);
        Assert.Contains("(peso, origem, leitura) =>", multipla, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarPesagemOperacaoComLotes", multipla, StringComparison.Ordinal);
        Assert.Contains("codigoLocalPesagem =>", multipla, StringComparison.Ordinal);
        Assert.Contains("_controller.CancelarPesagemOperacaoComLotes", multipla, StringComparison.Ordinal);
        Assert.Contains("imprimirPesagemAsync: null", multipla, StringComparison.Ordinal);
        Assert.Contains("reimprimirPesagemAsync: null", multipla, StringComparison.Ordinal);
    }

    [Fact]
    public void PesagemMultipla_NaoDeveUsarDialogResultComoFonteDefinitiva()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string multipla = ExtrairMetodo(form, "private async Task AbrirPesagemMultiplaParaLinhaAsync");

        Assert.DoesNotContain("_leiturasPorItem[codigoItem] = form.Pesagens.ToList();", multipla, StringComparison.Ordinal);
        Assert.DoesNotContain("_leiturasPorItem[codigoItem] = form.PesagensComCodigoLocal", multipla, StringComparison.Ordinal);
        Assert.Contains("SincronizarLeiturasItemComOperacaoLotes", multipla, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelamentoLinha_DeveCancelarSomentePesagensDoLoteAtivoNoOrquestrador()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string cancelar = ExtrairMetodo(form, "private bool CancelarLeiturasDaLinha");

        Assert.Contains("_controller.CancelarPesagensOperacaoComLotes(codigoItem)", cancelar, StringComparison.Ordinal);
        Assert.Contains("SincronizarLeiturasItemComOperacaoLotes", cancelar, StringComparison.Ordinal);
        Assert.DoesNotContain("_leiturasPorItem[codigoItem].Clear()", cancelar, StringComparison.Ordinal);
    }

    [Fact]
    public void Stop_DeveUsarSeamDePersistenciaESomenteOrquestradorSemFluxoLegado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string stop = ExtrairMetodo(form, "private async void StopProduction_Click");
        string finalizar = ExtrairMetodo(form, "internal async Task FinalizarPersistenciaEAtualizarProntidaoSapAsync");
        string executar = ExtrairMetodo(form, "internal async Task ExecutarPersistenciaLotesAsync");

        // §3/§4: usa exclusivamente o orquestrador (montar) e o seam de persistência.
        Assert.Contains("_controller.MontarLancamentoComLotesParaPersistencia()", executar, StringComparison.Ordinal);
        Assert.Contains("_registrarOuRecuperarLancamentoComLotes(", executar, StringComparison.Ordinal);
        // §5 (refactor): o async void é wrapper mínimo → delega ao método Task testável, que persiste e reavalia.
        Assert.Contains("FinalizarPersistenciaEAtualizarProntidaoSapAsync()", stop, StringComparison.Ordinal);
        Assert.Contains("ExecutarPersistenciaLotesAsync()", finalizar, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Finalizar", stop, StringComparison.Ordinal);

        // §3/§15: nada do fluxo legado.
        Assert.DoesNotContain("GravarPesagensAsync", executar, StringComparison.Ordinal);
        Assert.DoesNotContain("MontarLancamentoDoGrid", executar, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalizarLeituraAsync", executar, StringComparison.Ordinal);
        Assert.DoesNotContain("SalvarLancamentoAsync", executar, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarLancamentoComLotesAsync(", executar, StringComparison.Ordinal);
    }

    [Fact]
    public void Stop_SucessoDeveAtribuirCodigoLimparOperacaoEEncerrarLeituraNaOrdemCerta()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string sucesso = ExtrairMetodo(form, "private void AplicarSucessoPersistenciaLotes");

        int codigo = sucesso.IndexOf("_codigoLancamentoPersistido = resultado.CodigoLancamento;", StringComparison.Ordinal);
        int encerra = sucesso.IndexOf("_isProductionStarted = false;", StringComparison.Ordinal);
        int limpa = sucesso.IndexOf("_controller.LimparOperacaoComLotes();", StringComparison.Ordinal);

        // §7: só após salvar o código e atualizar o estado visual limpa a operação em memória.
        Assert.True(codigo >= 0 && encerra > codigo && limpa > encerra);
        Assert.Contains("resultado.PersistenciaRecuperada", sucesso, StringComparison.Ordinal);
    }

    [Fact]
    public void Stop_FalhaDeveManterOperacaoSemLimparNemMarcarGravado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string executar = ExtrairMetodo(form, "internal async Task ExecutarPersistenciaLotesAsync");
        string aguardando = ExtrairMetodo(form, "private void MarcarAguardandoPersistencia");

        Assert.Contains("catch (ErroOperacionalEsperadoException ex)", executar, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", executar, StringComparison.Ordinal);
        Assert.Contains("MarcarAguardandoPersistencia", executar, StringComparison.Ordinal);
        Assert.Contains("_lotesFinalizadosAguardandoPersistencia = true;", aguardando, StringComparison.Ordinal);
        // A falha não pode limpar a operação nem atribuir código.
        Assert.DoesNotContain("_controller.LimparOperacaoComLotes();", executar, StringComparison.Ordinal);
        Assert.DoesNotContain("_codigoLancamentoPersistido =", aguardando, StringComparison.Ordinal);
    }

    [Fact]
    public void NovoFluxo_DeveBloquearSapEImpressaoAtePersistenciaFutura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string start = ExtrairTrecho(form, "private async void StartProduction_Click", "private void StartProductionDevicesWarmUp");
        string leitura = ExtrairTrecho(form, "private async void ReadWeightLegend_Click", "private bool RegistrarPesoLido");
        string manual = ExtrairTrecho(form, "private async void LeituraManual_Click", "private void UpdateProductionState");
        string multipla = ExtrairMetodo(form, "private async Task AbrirPesagemMultiplaParaLinhaAsync");

        Assert.Contains("EstadoVisualIntegracaoSap.AguardandoGravacaoLocal", start, StringComparison.Ordinal);
        Assert.Contains("SAP bloqueado nesta fase", start, StringComparison.Ordinal);
        Assert.Contains("Impressão do novo fluxo de lotes ainda não habilitada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", leitura.Replace("private Task<bool> TentarImprimirEtiquetaAposLeituraAsync", string.Empty), StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", manual, StringComparison.Ordinal);
        Assert.Contains("imprimirPesagemAsync: null", multipla, StringComparison.Ordinal);
    }

    [Fact]
    public void ReimpressaoPersistidaLegada_DevePermanecerDisponivel()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("TentarReimprimirEtiquetaPesagemAsync", form, StringComparison.Ordinal);
        Assert.Contains("AbrirPesagensPersistidasParaLinhaAsync", form, StringComparison.Ordinal);
        Assert.Contains("ListarPesagensPersistidasAsync", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TrocaDePedido_DeveSerBloqueadaQuandoHaOperacaoEmMemoriaNaoPersistida()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string consulta = ExtrairMetodo(form, "private async Task AtualizarDadosPedidoSelecionadoAsync");

        Assert.Contains("BloquearTrocaPedidoComOperacaoEmMemoria(numeroPedido, restaurarTexto: true)", consulta, StringComparison.Ordinal);
        Assert.Contains("private bool BloquearTrocaPedidoComOperacaoEmMemoria", form, StringComparison.Ordinal);
        Assert.Contains("pedidoComboBox.Text = pedidoOriginal", form, StringComparison.Ordinal);
        Assert.Contains("BloquearTrocaPedidoComOperacaoEmMemoria", form, StringComparison.Ordinal);
        Assert.Contains("return;", consulta, StringComparison.Ordinal);
    }

    [Fact]
    public void FechamentoDaTela_DeveLimparOperacaoEmMemoria()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("FormClosed += (_, _) =>", form, StringComparison.Ordinal);
        Assert.Contains("_controller.LimparOperacaoComLotes();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Designer_NaoDeveSerAlteradoParaFluxoDeLotes()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.Designer.cs");

        Assert.DoesNotContain("EntradaProdutoDadosLoteForm", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("DadosLoteEntrada", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmarLoteOperacaoComLotes", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Fluxo_NaoDeveAlterarSapSqlZplOuPacote043()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string controller = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string modeloSap = LerArquivoProjeto("Modelo", "IntegracaoSap", "MaterialDocumentSapItemRequest.cs");

        Assert.Contains("GoodsMovementRefDocType", modeloSap, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("API_PURCHASEORDER_2", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PATCH", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ZPL", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("043", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenciaLotes_NaoTemEfeitosColaterais_SapImpressaoSqlOuRepository()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string executar = ExtrairMetodo(form, "internal async Task ExecutarPersistenciaLotesAsync");
        string sucesso = ExtrairMetodo(form, "private void AplicarSucessoPersistenciaLotes");

        // §15: sem SAP, sem impressão/ZPL, sem SQL/Repository e sem fluxo legado no novo caminho de gravação.
        foreach (string trecho in new[] { executar, sucesso })
        {
            Assert.DoesNotContain("EnviarSap", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("AtualizarProntidaoEnvioSapAsync", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("DiagnosticarEnvioSap", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("ImpressaoEntradaServico", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("ImprimirEtiqueta", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("Repositorio", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("GravarPesagensAsync", trecho, StringComparison.Ordinal);
            Assert.DoesNotContain("FinalizarLeituraAsync", trecho, StringComparison.Ordinal);
        }

        // A fronteira de persistência é o Controller (seam), nunca o Repository direto.
        Assert.Contains("_registrarOuRecuperarLancamentoComLotes", form, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarOuRecuperarLancamentoComLotesAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoRepositorio", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private bool GarantirOperacaoLotesIniciada")]
    [InlineData("private async Task<bool> GarantirLoteAtivoParaLinhaAsync")]
    [InlineData("private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync")]
    [InlineData("private bool SincronizarLeiturasItemComOperacaoLotes")]
    [InlineData("private bool FinalizarLotesAtivosEmMemoria")]
    [InlineData("private bool OperacaoComLotesPossuiLotes")]
    public void MetodosDeIntegracaoLocalizada_DevemExistir(string assinatura)
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains(assinatura, form, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_DeveExporContratosUsadosPelaTela()
    {
        string controller = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");

        Assert.Contains("IniciarOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("SelecionarItemOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("ConfirmarLoteOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("CancelarPesagensOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("CancelarPesagemOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("ObterPesagensItemOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("ObterPesagensLoteAtivoOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("FinalizarLoteOperacaoComLotes", controller, StringComparison.Ordinal);
        Assert.Contains("LimparOperacaoComLotes", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void TextUpdate_ComLoteEmMemoria_DevePreservarTelaERestaurarPedidoSemConsulta()
    {
        ExecutarEmSta(() =>
        {
            PedidoCompraSapItem item = CriarItem(101, "10", "MAT-001");
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", item);
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");

            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
            Guid loteAntes = controller.ObterEstadoOperacaoComLotes().Itens.Single(i => i.CodigoSapPedidoCompraItem == 101).CodigoLoteAtivoLocal!.Value;

            ComboBox pedidoComboBox = Controle<ComboBox>(form, "pedidoComboBox");
            TextBox lotTextBox = Controle<TextBox>(form, "lotTextBox");
            DataGridView grid = Controle<DataGridView>(form, "productionDataGridView");
            int linhasAntes = grid.Rows.Count;
            object? tagAntes = grid.Rows[0].Tag;

            pedidoComboBox.Text = "4500000002";
            Invocar(form, "PedidoComboBox_TextUpdate", pedidoComboBox, EventArgs.Empty);

            Assert.Equal("4500000001", pedidoComboBox.Text);
            Assert.Equal("4500000001", Campo<string>(form, "_numeroPedidoCarregado"));
            Assert.Equal("FORN-A", lotTextBox.Text);
            Assert.Equal(linhasAntes, grid.Rows.Count);
            Assert.Same(tagAntes, grid.Rows[0].Tag);
            Assert.Single(Campo<IReadOnlyList<PedidoCompraSapItem>>(form, "_itensPedidoCarregados"));
            Assert.False(Campo<bool>(form, "_restaurandoPedidoOperacaoLotes"));

            EstadoOperacaoEntradaProdutoLotes estadoDepois = controller.ObterEstadoOperacaoComLotes();
            Assert.True(estadoDepois.OperacaoIniciada);
            Assert.Equal("4500000001", estadoDepois.NumeroPedido);
            Assert.Equal(loteAntes, estadoDepois.Itens.Single(i => i.CodigoSapPedidoCompraItem == 101).CodigoLoteAtivoLocal);
            Assert.Single(controller.ObterPesagensItemOperacaoComLotes(101));
        });
    }

    [Fact]
    public void TextUpdate_OperacaoSemLote_DeveLimparOperacaoEPermitirNovoPedido()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"));
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));

            ComboBox pedidoComboBox = Controle<ComboBox>(form, "pedidoComboBox");
            pedidoComboBox.Text = "4500000002";
            Invocar(form, "PedidoComboBox_TextUpdate", pedidoComboBox, EventArgs.Empty);

            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Equal(string.Empty, Campo<string>(form, "_numeroPedidoCarregado"));

            ConfigurarPedidoNaTela(form, "4500000002", "FORN-B", CriarItem(202, "20", "MAT-002"));
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            Assert.Equal("4500000002", controller.ObterEstadoOperacaoComLotes().NumeroPedido);
        });
    }

    [Fact]
    public void FinalizarLotesAtivosEmMemoria_ComUmLoteInvalido_NaoFinalizaParcialmente()
    {
        ExecutarEmSta(() =>
        {
            PedidoCompraSapItem itemValido = CriarItem(101, "10", "MAT-001");
            PedidoCompraSapItem itemSemPeso = CriarItem(202, "20", "MAT-002");
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", itemValido, itemSemPeso);
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");

            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
            controller.ConfirmarLoteOperacaoComLotes(202, "LOTE-B", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            DefinirCampo(form, "_isProductionStarted", true);

            bool finalizado = Invocar<bool>(form, "FinalizarLotesAtivosEmMemoria");

            Assert.False(finalizado);
            Assert.True(Campo<bool>(form, "_isProductionStarted"));
            Label statusLabel = Controle<Label>(form, "statusLabel");
            Assert.Contains("lote", statusLabel.Text, StringComparison.OrdinalIgnoreCase);
            EstadoOperacaoEntradaProdutoLotes estado = controller.ObterEstadoOperacaoComLotes();
            Assert.All(estado.Itens.Where(i => i.CodigoLoteAtivoLocal.HasValue), item => Assert.NotEqual(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, item.Lotes.Single(l => l.CodigoLocal == item.CodigoLoteAtivoLocal).Estado));
        });
    }

    [Fact]
    public void FinalizarLotesAtivosEmMemoria_TodosValidos_FinalizaTodosSemPersistir()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"), CriarItem(202, "20", "MAT-002"));
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");

            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
            controller.ConfirmarLoteOperacaoComLotes(202, "LOTE-B", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(202, 3m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", DateTimeOffset.Now);

            bool finalizado = Invocar<bool>(form, "FinalizarLotesAtivosEmMemoria");

            Assert.True(finalizado);
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            EstadoOperacaoEntradaProdutoLotes estado = controller.ObterEstadoOperacaoComLotes();
            Assert.All(estado.Itens, item => Assert.Equal(EstadoOperacionalLoteEntrada.FinalizadoEmMemoria, item.Lotes.Single(l => l.CodigoLocal == item.CodigoLoteAtivoLocal).Estado));
        });
    }

    [Fact]
    public void TextUpdate_DeveBloquearAntesDeLimparTela()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string textUpdate = ExtrairMetodo(form, "private void PedidoComboBox_TextUpdate");

        int capturaPedido = textUpdate.IndexOf("string numeroPedidoSolicitado = pedidoComboBox.Text.Trim();", StringComparison.Ordinal);
        int bloqueio = textUpdate.IndexOf("BloquearTrocaPedidoComOperacaoEmMemoria(numeroPedidoSolicitado, restaurarTexto: true)", StringComparison.Ordinal);
        int cancelamento = textUpdate.IndexOf("_consultaPedidoCts?.Cancel();", StringComparison.Ordinal);
        int limpeza = textUpdate.IndexOf("LimparDadosPedidoSelecionado();", StringComparison.Ordinal);

        Assert.True(capturaPedido >= 0);
        Assert.True(bloqueio > capturaPedido);
        Assert.True(cancelamento > bloqueio);
        Assert.True(limpeza > bloqueio);
    }

    [Fact]
    public void Finalizacao_DeveSepararValidacaoEMutacaoSemEstadoParcial()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string finalizar = ExtrairMetodo(form, "private bool FinalizarLotesAtivosEmMemoria");

        int adicionaValidado = finalizar.IndexOf("itensValidados.Add(item);", StringComparison.Ordinal);
        int inicioMutacao = finalizar.IndexOf("foreach (EstadoItemEntradaProdutoLotes itemValidado in itensValidados)", StringComparison.Ordinal);
        int finaliza = finalizar.IndexOf("_controller.FinalizarLoteOperacaoComLotes", StringComparison.Ordinal);

        Assert.True(adicionaValidado >= 0);
        Assert.True(inicioMutacao > adicionaValidado);
        Assert.True(finaliza > inicioMutacao);
        Assert.DoesNotContain("FinalizarLoteOperacaoComLotes", finalizar[..inicioMutacao], StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateProductionState_DeveDesabilitarPedidoDuranteLeituraAtiva()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string update = ExtrairMetodo(form, "private void UpdateProductionState");

        Assert.Contains("pedidoComboBox.Enabled = !started", update, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Consultar", update, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.SincronizarCache", update, StringComparison.Ordinal);
    }

    // ===================================================================================================
    // §14 — Testes comportamentais STA do fluxo Parar → persistência local idempotente por lotes.
    // ===================================================================================================

    [Fact]
    public void Persistencia_SucessoInedito_AtribuiCodigoLimpaOperacaoEPreservaProjecao()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            int chamadas = 0;
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return Task.FromResult(ResultadoInedito(555)); });
            DataGridView grid = Controle<DataGridView>(form, "productionDataGridView");
            int linhasAntes = grid.Rows.Count;

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(1, chamadas);
            Assert.Equal(555L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.False(Campo<bool>(form, "_isProductionStarted"));
            Assert.False(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Equal(linhasAntes, grid.Rows.Count);
            Assert.Contains("gravado com sucesso", Controle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Persistencia_RecuperacaoIdempotente_ExibeMesmoCodigoEMensagemDeRecuperacao()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => Task.FromResult(ResultadoRecuperado(777)));

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(777L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.Contains("recuperado com segurança", Controle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Persistencia_FalhaOperacional_PreservaArvoreEHabilitaRetry()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            Guid? correlacaoNoEnvio = null;
            InjetarSeamPersistencia(form, (arvore, _) =>
            {
                correlacaoNoEnvio = arvore.Itens[0].Lotes[0].CorrelationId;
                throw new ErroOperacionalEsperadoException("A correlation_id informada já pertence a uma árvore diferente.");
            });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.True(Campo<bool>(form, "_isProductionStarted"));
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            // Após a falha a árvore permanece finalizada; a mesma correlation_id é reofertada no retry.
            Assert.NotNull(correlacaoNoEnvio);
            Assert.Equal(correlacaoNoEnvio, controller.MontarLancamentoComLotesParaPersistencia().Itens[0].Lotes[0].CorrelationId);
            Assert.Contains("correlation_id", Controle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Persistencia_RetryAposFalha_UsaMesmaArvoreSemNovoLoteELimpaSoNoSucesso()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            List<Guid> correlacoesVistas = [];
            bool falhar = true;
            InjetarSeamPersistencia(form, (arvore, _) =>
            {
                correlacoesVistas.Add(arvore.Itens[0].Lotes[0].CorrelationId);
                return falhar
                    ? throw new ErroOperacionalEsperadoException("falha transitória")
                    : Task.FromResult(ResultadoInedito(900));
            });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);

            falhar = false;
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(900L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.False(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Equal(2, correlacoesVistas.Count);
            Assert.Equal(correlacoesVistas[0], correlacoesVistas[1]);
        });
    }

    [Fact]
    public void Persistencia_ExcecaoInesperada_NaoPerdeArvoreNemMarcaGravado()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            InjetarSeamPersistencia(form, (_, _) => throw new InvalidOperationException("erro inesperado"));

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
        });
    }

    [Fact]
    public void Persistencia_Cancelamento_NaoPerdeArvoreNemMarcaGravado()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            InjetarSeamPersistencia(form, (_, _) => throw new OperationCanceledException());

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
        });
    }

    [Fact]
    public void Persistencia_CliqueDuplo_ChamaOSeamUmaUnicaVez()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            int chamadas = 0;
            TaskCompletionSource<ResultadoPersistenciaEntradaComLotes> pendente = new();
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return pendente.Task; });

            Task primeira = (Task)Invocar(form, "ExecutarPersistenciaLotesAsync")!;
            // Segundo "clique" enquanto a primeira persistência ainda está em andamento.
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            Assert.Equal(1, chamadas);

            pendente.SetResult(ResultadoInedito(321));
            primeira.GetAwaiter().GetResult();

            Assert.Equal(1, chamadas);
            Assert.Equal(321L, Campo<long?>(form, "_codigoLancamentoPersistido"));
        });
    }

    [Fact]
    public void Persistencia_SemNenhumLote_NaoChamaOSeam()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"));
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            DefinirCampo(form, "_isProductionStarted", true);
            int chamadas = 0;
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return Task.FromResult(ResultadoInedito(1)); });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(0, chamadas);
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(Campo<bool>(form, "_isProductionStarted"));
        });
    }

    [Fact]
    public void Persistencia_LoteSemPesagemValida_NaoChamaOSeam()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"));
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            DefinirCampo(form, "_isProductionStarted", true);
            int chamadas = 0;
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return Task.FromResult(ResultadoInedito(1)); });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(0, chamadas);
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
        });
    }

    [Fact]
    public void Persistencia_DoisLotesValidos_FinalizaAmbosEChamaSeamComDoisItens()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"), CriarItem(202, "20", "MAT-002"));
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
            controller.ConfirmarLoteOperacaoComLotes(202, "LOTE-B", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(202, 3m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "3", DateTimeOffset.Now);
            DefinirCampo(form, "_isProductionStarted", true);
            int itensNaArvore = 0;
            InjetarSeamPersistencia(form, (arvore, _) => { itensNaArvore = arvore.Itens.Count; return Task.FromResult(ResultadoInedito(42)); });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(2, itensNaArvore);
            Assert.Equal(42L, Campo<long?>(form, "_codigoLancamentoPersistido"));
        });
    }

    [Fact]
    public void Persistencia_FalhaNoSegundoLote_NaoFinalizaParcialmenteNemChamaSeam()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"), CriarItem(202, "20", "MAT-002"));
            EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
            controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
            controller.ConfirmarLoteOperacaoComLotes(202, "LOTE-B", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31)); // sem pesagem
            DefinirCampo(form, "_isProductionStarted", true);
            int chamadas = 0;
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return Task.FromResult(ResultadoInedito(1)); });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(0, chamadas);
            EstadoOperacaoEntradaProdutoLotes estado = controller.ObterEstadoOperacaoComLotes();
            Assert.All(
                estado.Itens.Where(i => i.CodigoLoteAtivoLocal.HasValue),
                item => Assert.NotEqual(
                    EstadoOperacionalLoteEntrada.FinalizadoEmMemoria,
                    item.Lotes.Single(l => l.CodigoLocal == item.CodigoLoteAtivoLocal).Estado));
        });
    }

    [Fact]
    public void Persistencia_DepoisDeGravado_BloqueiaNovaLeituraELiberaTrocaDePedido()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            InjetarSeamPersistencia(form, (_, _) => Task.FromResult(ResultadoInedito(50)));
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.False(Invocar<bool>(form, "PodeIniciarLeitura"));
            // Operação limpa após o sucesso ⇒ troca de pedido não é mais bloqueada pela árvore em memória.
            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.False(Invocar<bool>(form, "BloquearTrocaPedidoComOperacaoEmMemoria", "4500000002", false));
        });
    }

    [Fact]
    public void Fechamento_DuranteRetry_DeveSerBloqueadoComMensagemEspecifica()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => throw new ErroOperacionalEsperadoException("falha"));
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            FormClosingEventArgs args = new(CloseReason.UserClosing, false);
            Invocar(form, "ProcessoProdutoAcabadoForm_FormClosing", form, args);

            Assert.True(args.Cancel);
            Assert.Contains("ainda não gravada", Controle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Fechamento_AposSucesso_DeveSerPermitido()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => Task.FromResult(ResultadoInedito(60)));
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            FormClosingEventArgs args = new(CloseReason.UserClosing, false);
            Invocar(form, "ProcessoProdutoAcabadoForm_FormClosing", form, args);

            Assert.False(args.Cancel);
        });
    }

    // ===================================================================================================
    // §8 (correção 4G) — estados reais dos controles + leitura serial + finally da balança.
    // ===================================================================================================

    [Fact]
    public void Controles_DurantePersistencia_DevemFicarDesabilitados()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            int chamadas = 0;
            TaskCompletionSource<ResultadoPersistenciaEntradaComLotes> pendente = new();
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return pendente.Task; });

            Task emAndamento = (Task)Invocar(form, "ExecutarPersistenciaLotesAsync")!;

            Assert.True(Campo<bool>(form, "_finalizandoPesagem"));
            Assert.False(Controle<Panel>(form, "stopActionPanel").Enabled);
            Assert.False(Controle<Panel>(form, "readWeightLegendPanel").Enabled);
            Assert.False(Controle<Control>(form, "leituraManualButton").Enabled);
            Assert.False(Controle<Panel>(form, "deleteLastLegendPanel").Enabled);
            Assert.False(Controle<Panel>(form, "deleteByCodeLegendPanel").Enabled);
            Assert.False(Controle<ComboBox>(form, "pedidoComboBox").Enabled);

            // Segundo "clique" durante a persistência não chama o seam novamente.
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            Assert.Equal(1, chamadas);

            pendente.SetResult(ResultadoInedito(111));
            emAndamento.GetAwaiter().GetResult();
        }));
    }

    [Fact]
    public void Controles_DuranteRetry_MantemPararHabilitadoEPesagemDesabilitada()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => throw new ErroOperacionalEsperadoException("falha transitória"));

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.True(Controle<Panel>(form, "stopActionPanel").Visible);
            Assert.True(Controle<Panel>(form, "stopActionPanel").Enabled);
            // Botão lateral (F5) desabilitado no retry: só o painel Parar inicia a nova tentativa.
            Assert.False(Controle<Control>(form, "iniciarLeituraButton").Enabled);
            Assert.False(Controle<Panel>(form, "readWeightLegendPanel").Enabled);
            Assert.False(Controle<Control>(form, "leituraManualButton").Enabled);
            Assert.False(Controle<Panel>(form, "deleteLastLegendPanel").Enabled);
            Assert.False(Controle<Panel>(form, "deleteByCodeLegendPanel").Enabled);
            Assert.False(Controle<ComboBox>(form, "pedidoComboBox").Enabled);
            Assert.Contains("Clique em Parar para tentar novamente.", Controle<Label>(form, "statusLabel").Text, StringComparison.Ordinal);
        }));
    }

    [Fact]
    public void Controles_AposRetrySucesso_LiberamPedidoEBloqueiamNovaLeitura()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            bool falhar = true;
            InjetarSeamPersistencia(form, (_, _) => falhar
                ? throw new ErroOperacionalEsperadoException("falha")
                : Task.FromResult(ResultadoInedito(222)));

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            falhar = false;
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.False(Campo<bool>(form, "_finalizandoPesagem"));
            Assert.False(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(Controle<Panel>(form, "stopActionPanel").Visible);
            Assert.False(Controle<Panel>(form, "readWeightLegendPanel").Enabled);
            Assert.False(Controle<Control>(form, "leituraManualButton").Enabled);
            Assert.True(Controle<ComboBox>(form, "pedidoComboBox").Enabled);
            Assert.False(Invocar<bool>(form, "PodeIniciarLeitura"));
        }));
    }

    [Fact]
    public void LeituraSerialEmAndamento_NaoPersisteENaoFinalizaLotes()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            DefinirCampo(form, "_isReadingWeight", true);
            int chamadas = 0;
            InjetarSeamPersistencia(form, (_, _) => { chamadas++; return Task.FromResult(ResultadoInedito(1)); });

            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));

            Assert.Equal(0, chamadas);
            Assert.False(Campo<bool>(form, "_finalizandoPesagem"));
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            // Nenhum lote finalizado: o lote ativo continua não-finalizado.
            EstadoOperacaoEntradaProdutoLotes estado = controller.ObterEstadoOperacaoComLotes();
            Assert.All(
                estado.Itens.Where(i => i.CodigoLoteAtivoLocal.HasValue),
                item => Assert.NotEqual(
                    EstadoOperacionalLoteEntrada.FinalizadoEmMemoria,
                    item.Lotes.Single(l => l.CodigoLocal == item.CodigoLoteAtivoLocal).Estado));
            Assert.Contains("Aguarde a conclusão da leitura da balança", Controle<Label>(form, "statusLabel").Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void FinallyDaBalanca_DuranteRetry_MantemLeituraDesabilitada()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            DefinirCampo(form, "_lotesFinalizadosAguardandoPersistencia", true);

            // Replica o efeito do finally da leitura (que agora considera o bloqueio do fluxo de lotes).
            Invocar(form, "AtualizarControlesFluxoLotes");

            Assert.False(Controle<Panel>(form, "readWeightLegendPanel").Enabled);
            Assert.False(Controle<Control>(form, "leituraManualButton").Enabled);
        }));
    }

    [Fact]
    public void F5_DuranteRetry_NaoDeveDispararNovaPersistencia()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            int chamadas = 0;
            Guid? correlacaoNoEnvio = null;
            InjetarSeamPersistencia(form, (arvore, _) =>
            {
                chamadas++;
                correlacaoNoEnvio = arvore.Itens[0].Lotes[0].CorrelationId;
                throw new ErroOperacionalEsperadoException("falha transitória");
            });

            // 1ª tentativa falha ⇒ entra em retry.
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            Assert.Equal(1, chamadas);
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(Controle<Control>(form, "iniciarLeituraButton").Enabled);
            Assert.True(Controle<Panel>(form, "stopActionPanel").Enabled);

            // F5 durante o retry: botão lateral desabilitado ⇒ ProcessCmdKey consome o evento sem efeito.
            bool consumido = InvocarProcessCmdKey(form, Keys.F5);

            Assert.True(consumido);
            Assert.Equal(1, chamadas); // o seam NÃO foi chamado de novo pelo F5.
            Assert.False(Campo<bool>(form, "_finalizandoPesagem"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada); // árvore preservada.
            Assert.NotNull(correlacaoNoEnvio);
            Assert.Equal(correlacaoNoEnvio, controller.MontarLancamentoComLotesParaPersistencia().Itens[0].Lotes[0].CorrelationId);

            // §5: o painel Parar continua sendo a ação oficial do retry, com a MESMA correlation_id.
            InjetarSeamPersistencia(form, (arvore, _) =>
            {
                chamadas++;
                correlacaoNoEnvio = arvore.Itens[0].Lotes[0].CorrelationId;
                return Task.FromResult(ResultadoInedito(909));
            });
            AwaitTaskSta(Invocar(form, "ExecutarPersistenciaLotesAsync"));
            Assert.Equal(2, chamadas);
            Assert.Equal(909L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.False(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
        }));
    }

    // ===================================================================================================
    // §6 (correção DEV) — prontidão do envio SAP manual APÓS a persistência local (comportamental STA).
    // Cobre A..E do escopo. Nunca há POST ao SAP: o envio só ocorre pelo clique manual, jamais aqui.
    // ===================================================================================================

    // A. Persistência local bem-sucedida + diagnóstico pronto ⇒ botão liberado, chip LIBERADO (LiberadoParaEnvio).
    [Fact]
    public void ProntidaoSap_SucessoLocalEDiagnosticoPronto_HabilitaBotaoEExibeLiberado()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            int persistencias = 0, diagnosticos = 0;
            InjetarSeamPersistencia(form, (_, _) => { persistencias++; return Task.FromResult(ResultadoInedito(555)); });
            InjetarSeamDiagnosticoSap(form, (codigo, _) => { diagnosticos++; return Task.FromResult(DiagnosticoPronto(codigo ?? 0)); });

            AwaitTaskSta(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync"));

            Assert.Equal(1, persistencias);
            Assert.Equal(1, diagnosticos);
            Assert.Equal(555L, Campo<long?>(form, "_codigoLancamentoPersistido"));

            Control botao = Controle<Control>(form, "productionActionsButton");
            Assert.True(botao.Enabled); // BUG original: ficava desabilitado; agora a prontidão o habilita.
            // Estado semântico = LiberadoParaEnvio (chip aprovado do DEV é "SAP HML: PENDENTE"); NÃO é "AGUARDANDO".
            Assert.Equal("LiberadoParaEnvio", Campo<object>(form, "_estadoIntegracaoSapAtual").ToString());
            Assert.DoesNotContain("AGUARDANDO", Controle<Label>(form, "sapStatusLabel").Text, StringComparison.Ordinal);
            // Visibilidade EFETIVA depende do form estar exibido (walk pelos ancestrais). Validamos a visibilidade
            // PRÓPRIA que a prontidão definiu (UsuarioTemPermissao=true) destacando o botão do container:
            // com Parent=null o getter Visible reflete apenas o estado próprio do controle.
            botao.Parent!.Controls.Remove(botao);
            Assert.True(botao.Visible);
            // Nenhum Material Document: o envio só ocorre no clique manual; a tarefa de envio segue concluída/ociosa.
            Assert.True(Campo<Task>(form, "_envioSapTask").IsCompleted);
        }));
    }

    // B. Persistência local bem-sucedida, mas diagnóstico bloqueado ⇒ código preservado, botão desabilitado,
    //    chip deixa de mostrar AGUARDANDO e o motivo real é apresentado no tooltip.
    [Fact]
    public void ProntidaoSap_SucessoLocalMasDiagnosticoBloqueado_PreservaCodigoEMostraMotivo()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => Task.FromResult(ResultadoInedito(556)));
            InjetarSeamDiagnosticoSap(form, (codigo, _) =>
                Task.FromResult(DiagnosticoBloqueado(codigo ?? 0, "O ambiente atual não é homologação.")));

            AwaitTaskSta(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync"));

            Assert.Equal(556L, Campo<long?>(form, "_codigoLancamentoPersistido")); // código NÃO é apagado
            Assert.False(Controle<Control>(form, "productionActionsButton").Enabled);
            Assert.DoesNotContain("AGUARDANDO", Controle<Label>(form, "sapStatusLabel").Text, StringComparison.Ordinal);
            Assert.StartsWith("Bloqueado", Campo<object>(form, "_estadoIntegracaoSapAtual").ToString());
            ToolTip tip = Campo<ToolTip>(form, "_envioSapToolTip");
            Assert.Contains("homologação", tip.GetToolTip(Controle<Control>(form, "productionActionsButton")), StringComparison.OrdinalIgnoreCase);
        }));
    }

    // C. Diagnóstico lança exceção ⇒ não escapa, botão desabilitado, lançamento preservado, chip FALHA,
    //    mensagem amigável e tela operacional. Nenhum envio SAP.
    [Fact]
    public void ProntidaoSap_DiagnosticoLancaExcecao_NaoEscapaPreservaCodigoEChipFalha()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            InjetarSeamPersistencia(form, (_, _) => Task.FromResult(ResultadoInedito(557)));
            InjetarSeamDiagnosticoSap(form, (_, _) => throw new InvalidOperationException("falha inesperada de diagnóstico"));

            Exception? erro = Record.Exception(() => AwaitTaskSta(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync")));

            Assert.Null(erro); // exceção não escapa do fluxo (nem do async void real)
            Assert.Equal(557L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.False(Controle<Control>(form, "productionActionsButton").Enabled);
            Assert.Equal("Falha", Campo<object>(form, "_estadoIntegracaoSapAtual").ToString());
            Assert.Contains("FALHA", Controle<Label>(form, "sapStatusLabel").Text, StringComparison.Ordinal);
            Assert.Contains("preservado", Controle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
            Assert.False(form.IsDisposed); // tela permanece operacional
        }));
    }

    // D. Persistência local falha/aguarda retry ⇒ diagnóstico NÃO é chamado; botão desabilitado; retry preservado.
    [Fact]
    public void ProntidaoSap_PersistenciaFalhaOuRetry_NaoChamaDiagnostico()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out EntradaProdutoController controller);
            InjetarSeamPersistencia(form, (_, _) => throw new ErroOperacionalEsperadoException("falha transitória"));
            int diagnosticos = 0;
            InjetarSeamDiagnosticoSap(form, (codigo, _) => { diagnosticos++; return Task.FromResult(DiagnosticoPronto(codigo ?? 0)); });

            AwaitTaskSta(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync"));

            Assert.Equal(0, diagnosticos);
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.True(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(Controle<Control>(form, "productionActionsButton").Enabled);
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
        }));
    }

    // E. Clique duplo ⇒ não duplica persistência nem diagnóstico; diagnóstico só após o sucesso.
    [Fact]
    public void ProntidaoSap_CliqueDuplo_NaoDuplicaPersistenciaNemDiagnostico()
    {
        ComPermissoesEntrada(() => ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = PrepararFormLoteFinalizavel(out _);
            int persistencias = 0, diagnosticos = 0;
            TaskCompletionSource<ResultadoPersistenciaEntradaComLotes> pendente = new();
            InjetarSeamPersistencia(form, (_, _) => { persistencias++; return pendente.Task; });
            InjetarSeamDiagnosticoSap(form, (codigo, _) => { diagnosticos++; return Task.FromResult(DiagnosticoPronto(codigo ?? 0)); });

            Task primeira = (Task)Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync")!;
            // 2º "clique" enquanto a 1ª persistência ainda está em andamento.
            AwaitTaskSta(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync"));
            Assert.Equal(1, persistencias);
            Assert.Equal(0, diagnosticos); // persistência não concluiu ⇒ sem diagnóstico ainda

            pendente.SetResult(ResultadoInedito(321));
            primeira.GetAwaiter().GetResult();

            Assert.Equal(1, persistencias);
            Assert.Equal(1, diagnosticos); // diagnóstico UMA vez, só após o sucesso
            Assert.Equal(321L, Campo<long?>(form, "_codigoLancamentoPersistido"));
        }));
    }

    private static bool InvocarProcessCmdKey(ProcessoEntradaProdutoForm form, Keys tecla)
    {
        MethodInfo metodo = typeof(ProcessoEntradaProdutoForm).GetMethod(
            "ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(ProcessoEntradaProdutoForm), "ProcessCmdKey");
        Message msg = default;
        object[] argumentos = [msg, tecla];
        return (bool)metodo.Invoke(form, argumentos)!;
    }

    private static void ComPermissoesEntrada(Action acao)
    {
        SessaoUsuarioAplicacao? anterior = EstadoSessaoUsuarioAtual.SessaoAtual;
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "teste-4g",
            Nome = "Teste 4G",
            IntegracaoBancoHabilitada = true,
            Permissoes =
            [
                Perm(PermissoesSistema.Rotinas.LeituraProducao, PermissoesSistema.Acoes.Finalizar),
                Perm(PermissoesSistema.Rotinas.LeituraProducao, PermissoesSistema.Acoes.Executar),
                Perm(PermissoesSistema.Rotinas.LeituraProducao, PermissoesSistema.Acoes.Cancelar),
                Perm(PermissoesSistema.Rotinas.LeituraProducao, PermissoesSistema.Acoes.Consultar)
            ]
        });
        try
        {
            acao();
        }
        finally
        {
            if (anterior is null)
            {
                EstadoSessaoUsuarioAtual.Limpar();
            }
            else
            {
                EstadoSessaoUsuarioAtual.Definir(anterior);
            }
        }
    }

    private static PermissaoSessaoAplicacao Perm(string rotina, string acao)
        => new()
        {
            Modulo = PermissoesSistema.Modulos.ProcessoProducao,
            Rotina = rotina,
            Acao = acao
        };

    private static ProcessoEntradaProdutoForm PrepararFormLoteFinalizavel(out EntradaProdutoController controller)
    {
        ProcessoEntradaProdutoForm form = CriarFormComPedido("4500000001", "FORN-A", CriarItem(101, "10", "MAT-001"));
        controller = Campo<EntradaProdutoController>(form, "_controller");
        Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
        controller.ConfirmarLoteOperacaoComLotes(101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
        controller.RegistrarPesagemOperacaoComLotes(101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);
        DefinirCampo(form, "_isProductionStarted", true);
        return form;
    }

    private static void InjetarSeamPersistencia(
        ProcessoEntradaProdutoForm form,
        Func<EntradaProdutoLancamentoComLotesPersistencia, CancellationToken, Task<ResultadoPersistenciaEntradaComLotes>> seam)
        => DefinirCampo(form, "_registrarOuRecuperarLancamentoComLotes", seam);

    private static void InjetarSeamDiagnosticoSap(
        ProcessoEntradaProdutoForm form,
        Func<long?, CancellationToken, Task<DiagnosticoEnvioSapEntrada>> seam)
        => DefinirCampo(form, "_diagnosticarEnvioSapEntrada", seam);

    private static DiagnosticoEnvioSapEntrada DiagnosticoPronto(long codigo)
        => new()
        {
            PodeEnviar = true,
            MotivoBloqueio = null,
            CodigoLancamento = codigo,
            TotalItensPersistidos = 1,
            AmbienteHomologacao = true,
            UsuarioTemPermissao = true,
            SapConfigurado = true,
            EscritaSapHabilitada = true,
            MaterialDocumentConfigurado = true,
            IntegracaoSapAtiva = true
        };

    private static DiagnosticoEnvioSapEntrada DiagnosticoBloqueado(long codigo, string motivo)
        => new()
        {
            PodeEnviar = false,
            MotivoBloqueio = motivo,
            CodigoLancamento = codigo,       // código presente ⇒ estado NÃO cai em AguardandoGravacaoLocal
            TotalItensPersistidos = 0,
            AmbienteHomologacao = false,     // roteia para BloqueadoAmbiente (chip "SAP HML: BLOQUEADO")
            UsuarioTemPermissao = true,
            SapConfigurado = true,
            EscritaSapHabilitada = true,
            MaterialDocumentConfigurado = true,
            IntegracaoSapAtiva = true
        };

    private static void AwaitTaskSta(object? possivelTask)
    {
        if (possivelTask is Task tarefa)
        {
            tarefa.GetAwaiter().GetResult();
        }
    }

    private static ResultadoPersistenciaEntradaComLotes ResultadoInedito(long codigo)
        => ResultadoPersistenciaEntradaComLotes.Criar(
            codigo,
            new Dictionary<string, long> { ["00010"] = 1 },
            new Dictionary<Guid, long>(),
            new Dictionary<Guid, long>(),
            new Dictionary<Guid, decimal>());

    private static ResultadoPersistenciaEntradaComLotes ResultadoRecuperado(long codigo)
        => ResultadoPersistenciaEntradaComLotes.CriarRecuperado(
            codigo,
            new Dictionary<string, long> { ["00010"] = 1 },
            new Dictionary<Guid, long>(),
            new Dictionary<Guid, long>(),
            new Dictionary<Guid, decimal>());

    private static ProcessoEntradaProdutoForm CriarFormComPedido(string numeroPedido, string fornecedor, params PedidoCompraSapItem[] itens)
    {
        EntradaProdutoController controller = new();
        ProcessoEntradaProdutoForm form = new(
            controller,
            ModoEntradaMaterial.MateriaPrima,
            (_, _) => new DadosLoteEntrada("LOTE-TESTE", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31)));
        ConfigurarPedidoNaTela(form, numeroPedido, fornecedor, itens);
        return form;
    }

    private static void ConfigurarPedidoNaTela(ProcessoEntradaProdutoForm form, string numeroPedido, string fornecedor, params PedidoCompraSapItem[] itens)
    {
        DefinirCampo(form, "_idSetorSelecionado", 1L);
        DefinirCampo(form, "_numeroPedidoCarregado", numeroPedido);
        DefinirCampo(form, "_itensPedidoCarregados", itens.ToList());

        Dictionary<long, PedidoCompraSapItem> itensPorCodigo = Campo<Dictionary<long, PedidoCompraSapItem>>(form, "_itensCarregadosPorCodigo");
        itensPorCodigo.Clear();
        foreach (PedidoCompraSapItem item in itens)
        {
            itensPorCodigo[item.CodigoItem] = item;
        }

        ComboBox pedidoComboBox = Controle<ComboBox>(form, "pedidoComboBox");
        TextBox lotTextBox = Controle<TextBox>(form, "lotTextBox");
        DataGridView grid = Controle<DataGridView>(form, "productionDataGridView");
        pedidoComboBox.Text = numeroPedido;
        lotTextBox.Text = fornecedor;
        grid.Rows.Clear();
        foreach (PedidoCompraSapItem item in itens)
        {
            int rowIndex = grid.Rows.Add();
            DataGridViewRow row = grid.Rows[rowIndex];
            row.Cells["productionCodeColumn"].Value = item.CodigoMaterial ?? string.Empty;
            row.Cells["productionProductColumn"].Value = item.Descricao ?? string.Empty;
            row.Cells["productionWeightColumn"].Value = item.UnidadeMedida ?? string.Empty;
            row.Cells["productionPesoLidoColumn"].Value = string.Empty;
            row.Cells["productionItemIdColumn"].Value = item.CodigoItem.ToString();
            row.Cells["productionPesoOrigemColumn"].Value = string.Empty;
            row.Tag = new TaraCadastro { CodigoTara = 1, NomeTara = "Sem tara", PesoKg = 0m, SituacaoTara = true };
        }
    }

    private static PedidoCompraSapItem CriarItem(long codigoItem, string numeroItem, string material)
        => new()
        {
            CodigoItem = codigoItem,
            NumeroItem = numeroItem,
            CodigoMaterial = material,
            Descricao = $"Material {material}",
            Quantidade = 10m,
            UnidadeMedida = "KG",
            PesoItem = 10m,
            Centro = "3007",
            Deposito = "PP01",
            TipoMaterialSap = "ROH",
            ClassificacaoEntrada = ClassificacaoEntradaMaterial.MateriaPrima
        };

    private static T Campo<T>(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(alvo.GetType().FullName, nome);
        return (T)campo.GetValue(alvo)!;
    }

    private static void DefinirCampo(object alvo, string nome, object? valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(alvo.GetType().FullName, nome);
        campo.SetValue(alvo, valor);
    }

    private static T Controle<T>(object alvo, string nome) where T : Control
        => Campo<T>(alvo, nome);

    private static object? Invocar(object alvo, string nome, params object?[] argumentos)
    {
        MethodInfo metodo = alvo.GetType().GetMethod(nome, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(alvo.GetType().FullName, nome);
        return metodo.Invoke(alvo, argumentos);
    }

    private static T Invocar<T>(object alvo, string nome, params object?[] argumentos)
        => (T)Invocar(alvo, nome, argumentos)!;

    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread thread = new(() =>
        {
            try
            {
                acao();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (erro is not null)
        {
            throw erro;
        }
    }
    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximo < 0)
        {
            proximo = fonte.IndexOf("\n    internal ", inicio + assinatura.Length, StringComparison.Ordinal);
        }

        if (proximo < 0)
        {
            proximo = fonte.Length;
        }

        return fonte[inicio..proximo];
    }

    private static string ExtrairTrecho(string fonte, string inicio, string fim)
    {
        int indiceInicio = fonte.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0, $"Início não encontrado: {inicio}");
        int indiceFim = fonte.IndexOf(fim, indiceInicio + 1, StringComparison.Ordinal);
        Assert.True(indiceFim > indiceInicio, $"Fim não encontrado: {fim}");
        return fonte[indiceInicio..indiceFim];
    }

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}