using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Regra definitiva: uma etiqueta por pesagem individual; o total fica só na linha (consulta). A janela imprime
/// cada nova leitura imediatamente e NÃO imprime etiqueta consolidada ao concluir; Fechar preserva as leituras.
/// </summary>
public sealed class PesagemMultiplaItemFormTests
{
    [Fact]
    public void Concluir_IncorporaPesoManualPendente_SemImprimirConsolidado()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");

        int concluir = conteudo.IndexOf("private async Task ConcluirAsync()", StringComparison.Ordinal);
        Assert.True(concluir >= 0, "ConcluirAsync não encontrado.");
        int pendente = conteudo.IndexOf("bool adicionada = await AdicionarPesoManualAsync();", concluir, StringComparison.Ordinal);
        int ok = conteudo.IndexOf("DialogResult = DialogResult.OK;", concluir, StringComparison.Ordinal);
        Assert.True(pendente > concluir);
        Assert.True(ok > pendente);
        // Concluir não imprime etiqueta consolidada.
        string corpoConcluir = conteudo.Substring(concluir, ok - concluir);
        Assert.DoesNotContain("PesoTotalTexto", corpoConcluir, StringComparison.Ordinal);
    }

    [Fact]
    public void Fechar_PreservaPesagens_RetornandoOk()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        // "Fechar" agora retorna OK (preserva), nunca Cancel (descarte silencioso).
        int fechar = conteudo.IndexOf("_cancelarButton.Click += (_, _) =>", StringComparison.Ordinal);
        Assert.True(fechar >= 0);
        int ok = conteudo.IndexOf("DialogResult = DialogResult.OK;", fechar, StringComparison.Ordinal);
        Assert.True(ok > fechar);
        Assert.DoesNotContain("DialogResult = DialogResult.Cancel;", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogo_ImprimeCadaNovaPesagem_ComCallbackPorPesagem()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        // Cada nova leitura dispara impressão individual (callback), não uma consolidada.
        Assert.Contains("Func<EntradaProdutoPesagem, Task<bool>>? _imprimirPesagemAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("await _imprimirPesagemAsync(nova)", conteudo, StringComparison.Ordinal);
        // Duplo clique reimprime somente aquela pesagem.
        Assert.Contains("_pesagensGrid.CellDoubleClick", conteudo, StringComparison.Ordinal);
        Assert.Contains("await _reimprimirPesagemAsync(pesagem)", conteudo, StringComparison.Ordinal);
        // Só pesagem VÁLIDA reimprime.
        Assert.Contains("Só é possível reimprimir pesagens com status VÁLIDA.", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogo_FalhaImpressao_MantemPesagem()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        int adicionar = conteudo.IndexOf("private async Task<bool> AdicionarPesoAsync", StringComparison.Ordinal);
        int addLista = conteudo.IndexOf("_pesagens.Add(nova);", adicionar, StringComparison.Ordinal);
        int imprimir = conteudo.IndexOf("await _imprimirPesagemAsync(nova)", adicionar, StringComparison.Ordinal);
        // A pesagem é adicionada ANTES da impressão; falha de impressão não a remove.
        Assert.True(addLista > adicionar && addLista < imprimir);
        int fim = conteudo.IndexOf("private async Task<bool> AdicionarPesoCanonicoAsync", adicionar, StringComparison.Ordinal);
        string corpo = conteudo.Substring(adicionar, fim - adicionar);
        Assert.DoesNotContain("_pesagens.Remove", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("_pesagens.RemoveAt", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelarLeitura_JaImpressa_ConfirmaEOrientaDescarteFisico()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        Assert.Contains("A etiqueta desta pesagem já pode ter sido impressa. Descarte fisicamente a etiqueta cancelada.", conteudo, StringComparison.Ordinal);
        Assert.Contains("with { StatusPesagem = \"CANCELADA\" }", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_PesagemMultipla_DeveUsarCallbacksCanonicosSemImpressaoNestaFase()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int abrir = conteudo.IndexOf("private async Task AbrirPesagemMultiplaParaLinhaAsync", StringComparison.Ordinal);
        int fim = conteudo.IndexOf("private async Task<bool> TentarReimprimirEtiquetaPesagemAsync", abrir, StringComparison.Ordinal);
        string corpo = conteudo.Substring(abrir, fim - abrir);

        Assert.Contains("_controller.ObterPesagensLoteAtivoOperacaoComLotes(codigoItem)", corpo, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarPesagemOperacaoComLotes", corpo, StringComparison.Ordinal);
        Assert.Contains("_controller.CancelarPesagemOperacaoComLotes", corpo, StringComparison.Ordinal);
        Assert.Contains("imprimirPesagemAsync: null", corpo, StringComparison.Ordinal);
        Assert.Contains("reimprimirPesagemAsync: null", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirEtiquetaPorPesagem(linhaItem, pesagem)", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirDadosEtiquetaMateriaPrima(linhaAlvo)", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_DevePreservarTaraAoRelocalizarLinhaESincronizarDoOrquestrador()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        int localizarLinha = conteudo.IndexOf(
            "DataGridViewRow linhaAlvo = LocalizarLinhaProducaoPorItemId(itemId) ?? linhaItem;",
            StringComparison.Ordinal);
        int preservarTara = conteudo.IndexOf("linhaAlvo.Tag = tara;", localizarLinha, StringComparison.Ordinal);
        int sincronizar = conteudo.IndexOf(
            "SincronizarLeiturasItemComOperacaoLotes(linhaAlvo, codigoItem)",
            preservarTara,
            StringComparison.Ordinal);

        Assert.True(localizarLinha >= 0);
        Assert.True(preservarTara > localizarLinha);
        Assert.True(sincronizar > preservarTara);
    }

    [Fact]
    public void TelaEntrada_DeveFinalizarLotesEmMemoriaAntesDeAlterarEstadoVisual()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int parar = conteudo.IndexOf("private async void StopProduction_Click", StringComparison.Ordinal);
        int finalizar = conteudo.IndexOf("FinalizarLotesAtivosEmMemoria()", parar, StringComparison.Ordinal);
        int atualizarEstado = conteudo.IndexOf("UpdateProductionState(false);", finalizar, StringComparison.Ordinal);

        Assert.True(parar >= 0);
        Assert.True(finalizar > parar);
        Assert.True(atualizarEstado > finalizar);
    }

    [Fact]
    public void ConstrutorLegado_DeveContinuarFuncionando()
    {
        using PesagemMultiplaItemForm form = new(
            CriarBalanca(),
            "4500001/10",
            CriarTara(),
            null,
            Array.Empty<EntradaProdutoPesagem>());

        Assert.Empty(form.Pesagens);
        Assert.Empty(form.PesagensComCodigoLocal);
    }

    [Fact]
    public async Task ModoCanonico_RegistroManual_DeveChamarCallbackPreservarGuidSequenciaEPesos()
    {
        Guid codigo = Guid.NewGuid();
        List<(decimal Peso, string Origem, string Leitura)> chamadas = [];
        List<EntradaProdutoPesagem> impressas = [];
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) =>
            {
                chamadas.Add((peso, origem, leitura));
                return Task.FromResult(PesagemCanonica(codigo, 7, peso, 0.2m, peso - 0.2m, origem, leitura));
            },
            guid => Task.FromResult(PesagemCanonica(guid, 7, 2m, 0.2m, 1.8m, EntradaProdutoPesagemCalculos.OrigemManual, "2")),
            pesagem =>
            {
                impressas.Add(pesagem);
                return Task.FromResult(true);
            });

        await AdicionarManualAsync(form, "2");

        Assert.Equal([(2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")], chamadas);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal(codigo, form.PesagensComCodigoLocal[0].CodigoLocalPesagem);
        Assert.Equal(7, form.Pesagens[0].Sequencia);
        Assert.Equal(0.2m, form.Pesagens[0].PesoTaraKg);
        Assert.Equal(1.8m, form.Pesagens[0].PesoLiquidoKg);
        Assert.Single(impressas);
        Assert.Equal(7, impressas[0].Sequencia);
    }

    [Fact]
    public async Task ModoCanonico_RegistroBalanca_DeveEncaminharOrigemELeituraOriginal()
    {
        List<(decimal Peso, string Origem, string Leitura)> chamadas = [];
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) =>
            {
                chamadas.Add((peso, origem, leitura));
                return Task.FromResult(PesagemCanonica(Guid.NewGuid(), 3, peso, 0m, peso, origem, leitura));
            });

        await AdicionarPesoAsync(form, 4m, EntradaProdutoPesagemCalculos.OrigemBalanca, "BAL=4");

        Assert.Equal([(4m, EntradaProdutoPesagemCalculos.OrigemBalanca, "BAL=4")], chamadas);
        Assert.Equal(3, form.Pesagens[0].Sequencia);
        Assert.Equal("BAL=4", form.Pesagens[0].LeituraOriginal);
    }

    [Fact]
    public async Task ModoCanonico_FalhaRegistro_NaoAdicionaNaoImprimeEExibeMensagem()
    {
        int impressoes = 0;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (_, _, _) => throw new InvalidOperationException("falha controlada"),
            imprimirPesagemAsync: _ =>
            {
                impressoes++;
                return Task.FromResult(true);
            });

        await AdicionarManualAsync(form, "1");

        Assert.Empty(form.Pesagens);
        Assert.Empty(form.PesagensComCodigoLocal);
        Assert.Equal(0, impressoes);
        Assert.Contains("falha controlada", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModoCanonico_FalhaImpressao_DevePreservarPesagem()
    {
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(Guid.NewGuid(), 1, peso, 0m, peso, origem, leitura)),
            imprimirPesagemAsync: _ => Task.FromResult(false));

        await AdicionarManualAsync(form, "5");

        Assert.Single(form.Pesagens);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Contains("não foi impressa", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModoCanonico_CancelamentoPorCallback_DeveSubstituirSomenteMesmoGuidSemRenumerar()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria p1 = PesagemCanonica(primeira, 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2");
        EntradaProdutoPesagemEmMemoria p2 = PesagemCanonica(segunda, 2, 3m, 0m, 3m, EntradaProdutoPesagemCalculos.OrigemManual, "3");
        Guid guidCancelado = Guid.Empty;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [p1, p2],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(Guid.NewGuid(), 3, peso, 0m, peso, origem, leitura)),
            guid =>
            {
                guidCancelado = guid;
                return Task.FromResult(p1.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada));
            });

        await CancelarPesoCanonicoAsync(form, 0);

        Assert.Equal(primeira, guidCancelado);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusCancelada, form.PesagensComCodigoLocal[0].Pesagem.StatusPesagem);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusValida, form.PesagensComCodigoLocal[1].Pesagem.StatusPesagem);
        Assert.Equal([1, 2], form.Pesagens.Select(p => p.Sequencia));
        Assert.Equal([primeira, segunda], form.PesagensComCodigoLocal.Select(p => p.CodigoLocalPesagem));
    }

    [Fact]
    public async Task ModoCanonico_FalhaCancelamento_NaoAlteraColecao()
    {
        Guid codigo = Guid.NewGuid();
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [PesagemCanonica(codigo, 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(Guid.NewGuid(), 2, peso, 0m, peso, origem, leitura)),
            _ => throw new InvalidOperationException("cancelamento indisponível"));

        await CancelarPesoCanonicoAsync(form, 0);

        Assert.Equal(EntradaProdutoPesagemCalculos.StatusValida, form.PesagensComCodigoLocal[0].Pesagem.StatusPesagem);
        Assert.Contains("cancelamento indisponível", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModoCanonico_NaoDeveCriarPesagemSequenciaGuidOuAcessarCamadasExternas()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        int inicio = conteudo.IndexOf("private async Task<bool> AdicionarPesoCanonicoAsync", StringComparison.Ordinal);
        int fim = conteudo.IndexOf("private void ManterPesoManualParaCorrecao", inicio, StringComparison.Ordinal);
        string metodo = conteudo[inicio..fim];

        Assert.DoesNotContain("_pesagens.Count + 1", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("new EntradaProdutoPesagem", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoController", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Repositorio", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Sap", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EstadoSessaoUsuarioAtual", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_DeveUsarOverloadCanonicoComoProjecaoDoOrquestrador()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("ObterPesagensLoteAtivoOperacaoComLotes", conteudo, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemOperacaoComLotes", conteudo, StringComparison.Ordinal);
        Assert.Contains("CancelarPesagemOperacaoComLotes", conteudo, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagensAtuais", conteudo, StringComparison.Ordinal);
        Assert.Contains("imprimirPesagemAsync: null", conteudo, StringComparison.Ordinal);
        Assert.Contains("reimprimirPesagemAsync: null", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("_leiturasPorItem[codigoItem] = form.Pesagens.ToList();", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("_leiturasPorItem[codigoItem] = form.PesagensComCodigoLocal", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_ModoCanonico_DeveExibirSequenciaCanonicaELegadoDeveExibirIndice()
    {
        using PesagemMultiplaItemForm canonico = CriarFormCanonico(
            [PesagemCanonica(Guid.NewGuid(), 3, 5m, 0m, 5m, EntradaProdutoPesagemCalculos.OrigemManual, "5")],
            RegistrarPadrao);
        using PesagemMultiplaItemForm legado = new(
            CriarBalanca(),
            "4500001/10",
            CriarTara(),
            null,
            [new EntradaProdutoPesagem { Sequencia = 3, PesoBrutoKg = 5m, PesoLiquidoKg = 5m }]);

        Assert.Equal(3, ObterGrid(canonico).Rows[0].Cells[0].Value);
        Assert.Equal(0, ObterGrid(canonico).Rows[0].Index);
        Assert.Equal(1, ObterGrid(legado).Rows[0].Cells[0].Value);
    }

    [Fact]
    public async Task CancelamentoCanonico_GuidDivergente_NaoAlteraNenhumaPesagem()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria p1 = PesagemCanonica(primeira, 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2");
        EntradaProdutoPesagemEmMemoria p2 = PesagemCanonica(segunda, 2, 3m, 0m, 3m, EntradaProdutoPesagemCalculos.OrigemManual, "3");
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [p1, p2],
            RegistrarPadrao,
            _ => Task.FromResult(p2.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada)));

        await CancelarPesoCanonicoAsync(form, 0);

        Assert.Equal(EntradaProdutoPesagemCalculos.StatusValida, form.PesagensComCodigoLocal[0].Pesagem.StatusPesagem);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusValida, form.PesagensComCodigoLocal[1].Pesagem.StatusPesagem);
        Assert.Contains("diferente da solicitada", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("marcada como cancelada", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("sequencia")]
    [InlineData("bruto")]
    [InlineData("tara")]
    [InlineData("liquido")]
    [InlineData("origem")]
    [InlineData("leitura")]
    [InlineData("pesadoEm")]
    [InlineData("statusValida")]
    public async Task CancelamentoCanonico_CampoImutavelDivergente_NaoAlteraColecao(string campo)
    {
        Guid codigo = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria original = PesagemCanonica(codigo, 4, 10m, 1m, 9m, EntradaProdutoPesagemCalculos.OrigemBalanca, "BAL=10");
        EntradaProdutoPesagemEmMemoria retornada = AlterarCampoCancelamento(original, campo);
        using PesagemMultiplaItemForm form = CriarFormCanonico([original], RegistrarPadrao, _ => Task.FromResult(retornada));

        await CancelarPesoCanonicoAsync(form, 0);

        Assert.Equal(original, form.PesagensComCodigoLocal.Single());
        Assert.Equal(10m, form.PesoTotal);
        Assert.Contains("dados divergentes", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistroCanonico_GuidDuplicado_NaoAdicionaNaoImprime()
    {
        Guid codigo = Guid.NewGuid();
        int impressoes = 0;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [PesagemCanonica(codigo, 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(codigo, 2, peso, 0m, peso, origem, leitura)),
            imprimirPesagemAsync: _ => { impressoes++; return Task.FromResult(true); });

        await AdicionarManualAsync(form, "3");

        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal(0, impressoes);
        Assert.Contains("identificador de pesagem já existente", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, ObterGrid(form).Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow));
    }

    [Fact]
    public async Task RegistroCanonico_SequenciaDuplicada_NaoAdicionaNaoImprime()
    {
        Guid novo = Guid.NewGuid();
        int impressoes = 0;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [PesagemCanonica(Guid.NewGuid(), 3, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(novo, 3, peso, 0m, peso, origem, leitura)),
            imprimirPesagemAsync: _ => { impressoes++; return Task.FromResult(true); });

        await AdicionarManualAsync(form, "3");

        Assert.DoesNotContain(form.PesagensComCodigoLocal, p => p.CodigoLocalPesagem == novo);
        Assert.Equal(0, impressoes);
        Assert.Contains("sequência de pesagem já existente", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistroCanonico_StatusInvalido_NaoAdicionaNaoImprime()
    {
        int impressoes = 0;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(
                Guid.NewGuid(),
                1,
                peso,
                0m,
                peso,
                origem,
                leitura,
                EntradaProdutoPesagemCalculos.StatusCancelada)),
            imprimirPesagemAsync: _ => { impressoes++; return Task.FromResult(true); });

        await AdicionarManualAsync(form, "3");

        Assert.Empty(form.PesagensComCodigoLocal);
        Assert.Equal(0m, form.PesoTotal);
        Assert.Equal(0, impressoes);
        Assert.Contains("não está válida", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImpressaoCanonica_ExcecaoPreservaPesagemEGridSemPropagar()
    {
        Guid codigo = Guid.NewGuid();
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(codigo, 5, peso, 0m, peso, origem, leitura)),
            imprimirPesagemAsync: _ => throw new InvalidOperationException("printer down"));

        await AdicionarManualAsync(form, "4");

        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Single(form.Pesagens);
        Assert.Equal(codigo, form.PesagensComCodigoLocal[0].CodigoLocalPesagem);
        Assert.Equal(5, form.Pesagens[0].Sequencia);
        Assert.Equal(1, ObterGrid(form).Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow));
        Assert.Contains("falha ao imprimir", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PesagensComCodigoLocal_DeveSerSomenteLeituraReal()
    {
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [PesagemCanonica(Guid.NewGuid(), 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")],
            RegistrarPadrao);

        IReadOnlyList<EntradaProdutoPesagemEmMemoria> leitura = form.PesagensComCodigoLocal;
        Assert.False(leitura is List<EntradaProdutoPesagemEmMemoria>);
        ICollection<EntradaProdutoPesagemEmMemoria> colecao = Assert.IsAssignableFrom<ICollection<EntradaProdutoPesagemEmMemoria>>(leitura);
        Assert.True(colecao.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => colecao.Add(PesagemCanonica(Guid.NewGuid(), 2, 1m, 0m, 1m, EntradaProdutoPesagemCalculos.OrigemManual, "1")));
        Assert.Single(form.PesagensComCodigoLocal);
    }

    [Fact]
    public async Task CanceladaCanonica_DevePermanecerVisivelETotalConsiderarSomenteValida()
    {
        Guid primeira = Guid.NewGuid();
        Guid segunda = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria p1 = PesagemCanonica(primeira, 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2");
        EntradaProdutoPesagemEmMemoria p2 = PesagemCanonica(segunda, 2, 3m, 0m, 3m, EntradaProdutoPesagemCalculos.OrigemManual, "3");
        using PesagemMultiplaItemForm form = CriarFormCanonico([p1, p2], RegistrarPadrao, _ => Task.FromResult(p1.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada)));

        await CancelarPesoCanonicoAsync(form, 0);

        DataGridView grid = ObterGrid(form);
        Assert.Equal(2, grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow));
        Assert.Equal(1, grid.Rows[0].Cells[0].Value);
        Assert.Equal(EntradaProdutoPesagemCalculos.StatusCancelada, grid.Rows[0].Cells["statusColumn"].Value);
        Assert.Equal(3m, form.PesoTotal);
        Assert.True(ObterBotao(form, "_concluirButton").Enabled);
    }

    [Fact]
    public async Task ReimpressaoCanonica_DeveUsarPesagemSelecionadaSemAlterarColecao()
    {
        Guid codigo = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria pesagem = PesagemCanonica(codigo, 6, 7m, 0m, 7m, EntradaProdutoPesagemCalculos.OrigemManual, "7");
        EntradaProdutoPesagem? reimpressa = null;
        using PesagemMultiplaItemForm form = new(
            CriarBalanca(),
            "4500001/10",
            CriarTara(),
            null,
            [pesagem],
            RegistrarPadrao,
            guid => Task.FromResult(pesagem.ComStatus(EntradaProdutoPesagemCalculos.StatusCancelada)),
            reimprimirPesagemAsync: p => { reimpressa = p; return Task.FromResult(true); });

        await ReimprimirAsync(form, 0);

        Assert.Equal(pesagem.Pesagem, reimpressa);
        Assert.Equal(codigo, form.PesagensComCodigoLocal.Single().CodigoLocalPesagem);
        Assert.Equal(6, form.PesagensComCodigoLocal.Single().Pesagem.Sequencia);
    }

    [Fact]
    public async Task ReimpressaoCanonica_CanceladaNaoChamaCallback()
    {
        int chamadas = 0;
        EntradaProdutoPesagemEmMemoria cancelada = PesagemCanonica(
            Guid.NewGuid(),
            1,
            2m,
            0m,
            2m,
            EntradaProdutoPesagemCalculos.OrigemManual,
            "2",
            EntradaProdutoPesagemCalculos.StatusCancelada);
        using PesagemMultiplaItemForm form = new(
            CriarBalanca(),
            "4500001/10",
            CriarTara(),
            null,
            [cancelada],
            RegistrarPadrao,
            guid => Task.FromResult(cancelada),
            reimprimirPesagemAsync: _ => { chamadas++; return Task.FromResult(true); });

        await ReimprimirAsync(form, 0);

        Assert.Equal(0, chamadas);
        Assert.Contains("status", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ConcluirCanonico_CallbackFalha_NaoFechaPreservaTextoEColecao()
    {
        EntradaProdutoPesagemEmMemoria existente = PesagemCanonica(
            Guid.NewGuid(),
            1,
            2m,
            0m,
            2m,
            EntradaProdutoPesagemCalculos.OrigemManual,
            "2");
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [existente],
            (_, _, _) => throw new InvalidOperationException("falha callback"));

        TextBox pesoManual = ObterPesoManualTextBox(form);
        pesoManual.Text = "3";

        await ConcluirAsync(form);

        Assert.Equal(DialogResult.None, form.DialogResult);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal("3", pesoManual.Text);
        Assert.Contains("falha callback", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("guidDuplicado")]
    [InlineData("sequenciaDuplicada")]
    [InlineData("statusCancelada")]
    public async Task ConcluirCanonico_RetornoInvalido_NaoFechaNaoImprimeEPreservaTexto(string caso)
    {
        Guid codigoExistente = Guid.NewGuid();
        EntradaProdutoPesagemEmMemoria existente = PesagemCanonica(
            codigoExistente,
            1,
            2m,
            0m,
            2m,
            EntradaProdutoPesagemCalculos.OrigemManual,
            "2");
        int impressoes = 0;
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [existente],
            (peso, origem, leitura) => Task.FromResult(caso switch
            {
                "guidDuplicado" => PesagemCanonica(codigoExistente, 2, peso, 0m, peso, origem, leitura),
                "sequenciaDuplicada" => PesagemCanonica(Guid.NewGuid(), 1, peso, 0m, peso, origem, leitura),
                "statusCancelada" => PesagemCanonica(Guid.NewGuid(), 2, peso, 0m, peso, origem, leitura, EntradaProdutoPesagemCalculos.StatusCancelada),
                _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, "Cen?rio de teste inv?lido.")
            }),
            imprimirPesagemAsync: _ =>
            {
                impressoes++;
                return Task.FromResult(true);
            });

        TextBox pesoManual = ObterPesoManualTextBox(form);
        pesoManual.Text = "3";

        await ConcluirAsync(form);

        Assert.Equal(DialogResult.None, form.DialogResult);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal("3", pesoManual.Text);
        Assert.Equal(0, impressoes);
    }

    [Fact]
    public async Task ConcluirCanonico_Sucesso_FechaOkLimpaTextoEPreservaGuidSequencia()
    {
        Guid novoCodigo = Guid.NewGuid();
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [PesagemCanonica(Guid.NewGuid(), 1, 2m, 0m, 2m, EntradaProdutoPesagemCalculos.OrigemManual, "2")],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(novoCodigo, 9, peso, 0m, peso, origem, leitura)));

        TextBox pesoManual = ObterPesoManualTextBox(form);
        pesoManual.Text = "3";

        await ConcluirAsync(form);

        Assert.Equal(DialogResult.OK, form.DialogResult);
        Assert.Equal(2, form.PesagensComCodigoLocal.Count);
        Assert.Equal(novoCodigo, form.PesagensComCodigoLocal[1].CodigoLocalPesagem);
        Assert.Equal(9, form.PesagensComCodigoLocal[1].Pesagem.Sequencia);
        Assert.Equal(string.Empty, pesoManual.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcluirCanonico_FalhaImpressao_AindaFechaOkEPreservaPesagem(bool lancarExcecao)
    {
        Guid novoCodigo = Guid.NewGuid();
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(novoCodigo, 4, peso, 0m, peso, origem, leitura)),
            imprimirPesagemAsync: _ => lancarExcecao
                ? throw new InvalidOperationException("falha impressora")
                : Task.FromResult(false));

        ObterPesoManualTextBox(form).Text = "5";

        await ConcluirAsync(form);

        Assert.Equal(DialogResult.OK, form.DialogResult);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal(novoCodigo, form.PesagensComCodigoLocal[0].CodigoLocalPesagem);
        Assert.Equal(4, form.PesagensComCodigoLocal[0].Pesagem.Sequencia);
    }

    [Fact]
    public async Task AdicionarManualCanonico_FalhaPreservaTextoERetornaFalse()
    {
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (_, _, _) => throw new InvalidOperationException("registro indispon?vel"));

        bool adicionado = await AdicionarManualAsync(form, "6");

        Assert.False(adicionado);
        Assert.Empty(form.PesagensComCodigoLocal);
        Assert.Equal("6", ObterPesoManualTextBox(form).Text);
        Assert.Contains("registro indispon?vel", ObterStatus(form).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdicionarManualCanonico_SucessoLimpaTextoERetornaTrue()
    {
        using PesagemMultiplaItemForm form = CriarFormCanonico(
            [],
            (peso, origem, leitura) => Task.FromResult(PesagemCanonica(Guid.NewGuid(), 1, peso, 0m, peso, origem, leitura)));

        bool adicionado = await AdicionarManualAsync(form, "7");

        Assert.True(adicionado);
        Assert.Single(form.PesagensComCodigoLocal);
        Assert.Equal(string.Empty, ObterPesoManualTextBox(form).Text);
    }

    private static PesagemMultiplaItemForm CriarFormCanonico(
        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagensAtuais,
        Func<decimal, string, string, Task<EntradaProdutoPesagemEmMemoria>> registrarPesagemAsync,
        Func<Guid, Task<EntradaProdutoPesagemEmMemoria>>? cancelarPesagemAsync = null,
        Func<EntradaProdutoPesagem, Task<bool>>? imprimirPesagemAsync = null)
        => new(
            CriarBalanca(),
            "4500001/10",
            CriarTara(),
            null,
            pesagensAtuais,
            registrarPesagemAsync,
            cancelarPesagemAsync ?? (guid => Task.FromResult(PesagemCanonica(guid, 1, 1m, 0m, 1m, EntradaProdutoPesagemCalculos.OrigemManual, "1"))),
            imprimirPesagemAsync);

    private static async Task<bool> AdicionarManualAsync(PesagemMultiplaItemForm form, string peso)
    {
        TextBox tb = (TextBox)typeof(PesagemMultiplaItemForm)
            .GetField("_pesoManualTextBox", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;
        tb.Text = peso;
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("AdicionarPesoManualAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return await (Task<bool>)mi.Invoke(form, null)!;
    }

    private static async Task<bool> AdicionarPesoAsync(PesagemMultiplaItemForm form, decimal peso, string origem, string leituraOriginal)
    {
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("AdicionarPesoAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return await (Task<bool>)mi.Invoke(form, [peso, origem, leituraOriginal])!;
    }

    private static async Task CancelarPesoCanonicoAsync(PesagemMultiplaItemForm form, int indice)
    {
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("CancelarPesoCanonicoAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)mi.Invoke(form, [indice])!;
    }

    private static async Task ReimprimirAsync(PesagemMultiplaItemForm form, int indice)
    {
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("ReimprimirPesagemAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)mi.Invoke(form, [indice])!;
    }


    private static async Task ConcluirAsync(PesagemMultiplaItemForm form)
    {
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("ConcluirAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)mi.Invoke(form, null)!;
    }

    private static TextBox ObterPesoManualTextBox(PesagemMultiplaItemForm form)
        => (TextBox)typeof(PesagemMultiplaItemForm)
            .GetField("_pesoManualTextBox", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;

    private static Label ObterStatus(PesagemMultiplaItemForm form)
        => (Label)typeof(PesagemMultiplaItemForm)
            .GetField("_statusLabel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;

    private static DataGridView ObterGrid(PesagemMultiplaItemForm form)
        => (DataGridView)typeof(PesagemMultiplaItemForm)
            .GetField("_pesagensGrid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;

    private static Button ObterBotao(PesagemMultiplaItemForm form, string nomeCampo)
        => (Button)typeof(PesagemMultiplaItemForm)
            .GetField(nomeCampo, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;

    private static TaraCadastro CriarTara(decimal pesoKg = 0m)
        => new() { CodigoTara = 1, NomeTara = "Tara teste", PesoKg = pesoKg };

    private static BalancaLeituraServico CriarBalanca()
        => new(new BalancaRepositorio(null!), new LeitorBalancaSerialServico());

    private static Task<EntradaProdutoPesagemEmMemoria> RegistrarPadrao(decimal peso, string origem, string leitura)
        => Task.FromResult(PesagemCanonica(Guid.NewGuid(), 99, peso, 0m, peso, origem, leitura));

    private static EntradaProdutoPesagemEmMemoria AlterarCampoCancelamento(
        EntradaProdutoPesagemEmMemoria original,
        string campo)
    {
        EntradaProdutoPesagem cancelada = original.Pesagem with
        {
            StatusPesagem = EntradaProdutoPesagemCalculos.StatusCancelada
        };

        cancelada = campo switch
        {
            "sequencia" => cancelada with { Sequencia = cancelada.Sequencia + 1 },
            "bruto" => cancelada with { PesoBrutoKg = cancelada.PesoBrutoKg + 1m },
            "tara" => cancelada with { PesoTaraKg = cancelada.PesoTaraKg + 0.1m },
            "liquido" => cancelada with { PesoLiquidoKg = cancelada.PesoLiquidoKg + 1m },
            "origem" => cancelada with { Origem = EntradaProdutoPesagemCalculos.OrigemManual },
            "leitura" => cancelada with { LeituraOriginal = "OUTRA" },
            "pesadoEm" => cancelada with { PesadoEm = cancelada.PesadoEm.AddMinutes(1) },
            "statusValida" => cancelada with { StatusPesagem = EntradaProdutoPesagemCalculos.StatusValida },
            _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo de teste inválido.")
        };

        return original with { Pesagem = cancelada };
    }

    private static EntradaProdutoPesagemEmMemoria PesagemCanonica(
        Guid codigoLocal,
        int sequencia,
        decimal bruto,
        decimal tara,
        decimal liquido,
        string origem,
        string leituraOriginal,
        string status = EntradaProdutoPesagemCalculos.StatusValida)
        => new()
        {
            CodigoLocalPesagem = codigoLocal,
            Pesagem = new EntradaProdutoPesagem
            {
                Sequencia = sequencia,
                PesoBrutoKg = bruto,
                PesoTaraKg = tara,
                PesoLiquidoKg = liquido,
                CodigoTara = tara > 0 ? 1 : null,
                CodigoBalanca = origem == EntradaProdutoPesagemCalculos.OrigemBalanca ? 9 : null,
                Origem = origem,
                LeituraOriginal = leituraOriginal,
                StatusPesagem = status,
                PesadoEm = new DateTimeOffset(2026, 7, 24, 8, sequencia, 0, TimeSpan.Zero)
            }
        };

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));
}
