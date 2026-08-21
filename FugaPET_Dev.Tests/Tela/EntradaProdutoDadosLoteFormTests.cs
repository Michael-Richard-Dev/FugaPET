using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;
using System.Drawing;
using System.Windows.Forms;

namespace FugaPET_Dev.Tests.Tela;

public sealed class EntradaProdutoDadosLoteFormTests
{
    private static readonly DateTime Hoje = new(2026, 7, 24);

    [Fact]
    public void Dialogo_DeveExistirComoUnicaFormParaOsDoisModos()
    {
        string raiz = LocalizarRaizProjeto();
        string[] forms = Directory.GetFiles(Path.Combine(raiz, "Tela", "Processo"), "*DadosLoteForm.cs", SearchOption.TopDirectoryOnly)
            .Where(arquivo => !arquivo.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Single(forms);
        Assert.EndsWith("EntradaProdutoDadosLoteForm.cs", forms[0], StringComparison.Ordinal);
        Assert.DoesNotContain(forms, arquivo => arquivo.Contains("MateriaPrima", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forms, arquivo => arquivo.Contains("Quimico", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ModoMateriaPrima_DeveDefinirTextoCorreto()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);

            Assert.Equal("Dados do lote", form.Text);
            Assert.Contains("matéria-prima", ObterControle<Label>(form, "modoLabel").Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Batch", form.Text + ObterControle<Label>(form, "modoLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void ModoQuimico_DeveDefinirTextoCorreto()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.Quimico);

            Assert.Contains("produto químico", ObterControle<Label>(form, "modoLabel").Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fornecedor", ObterTodosTextos(form), StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void EnumInvalido_DeveSerBloqueado()
        => ExecutarEmSta(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CriarForm((ModoEntradaMaterial)999));
        });

    [Fact]
    public void NumeroLote_DeveTerMaxLengthUpperSemMultilinhaEAcessibilidade()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            TextBox lote = ObterControle<TextBox>(form, "numeroLoteTextBox");

            Assert.Equal(10, lote.MaxLength);
            Assert.Equal(CharacterCasing.Upper, lote.CharacterCasing);
            Assert.False(lote.Multiline);
            Assert.Equal("Número do lote", lote.AccessibleName);
        });

    [Fact]
    public void Datas_DeveIniciarSemCheckEComFormatoAcessivel()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            DateTimePicker fabricacao = ObterControle<DateTimePicker>(form, "dataFabricacaoDateTimePicker");
            DateTimePicker vencimento = ObterControle<DateTimePicker>(form, "dataVencimentoDateTimePicker");

            Assert.True(fabricacao.ShowCheckBox);
            Assert.True(vencimento.ShowCheckBox);
            Assert.False(fabricacao.Checked);
            Assert.False(vencimento.Checked);
            Assert.Equal("dd/MM/yyyy", fabricacao.CustomFormat);
            Assert.Equal("dd/MM/yyyy", vencimento.CustomFormat);
            Assert.Equal("Data de fabricação", fabricacao.AccessibleName);
            Assert.Equal("Data de vencimento", vencimento.AccessibleName);
        });

    [Fact]
    public void ConfirmacaoSemNumero_DeveFalharManterNullENaoFechar()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.NotEqual(DialogResult.OK, form.DialogResult);
            Assert.False(form.IsDisposed);
            Assert.Contains("número do lote", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void ConfirmacaoSemFabricacao_DeveFalhar()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
            DateTimePicker vencimento = ObterControle<DateTimePicker>(form, "dataVencimentoDateTimePicker");
            vencimento.Value = Hoje.AddDays(30);
            vencimento.Checked = true;

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Contains("fabricação", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void ConfirmacaoSemVencimento_DeveFalhar()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
            DateTimePicker fabricacao = ObterControle<DateTimePicker>(form, "dataFabricacaoDateTimePicker");
            fabricacao.Value = Hoje;
            fabricacao.Checked = true;

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Contains("vencimento", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void LoteMaiorQueDez_DeveSerBloqueadoPeloFluxoDeValidacao()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            TextBox lote = ObterControle<TextBox>(form, "numeroLoteTextBox");
            lote.MaxLength = 20;
            lote.Text = "12345678901";
            MarcarDatasValidas(form);

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Contains("10 caracteres", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void FabricacaoFutura_DeveSerBloqueada()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
            MarcarDatas(form, Hoje.AddDays(1), Hoje.AddDays(30));

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Contains("futura", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void VencimentoAnteriorAFabricacao_DeveSerBloqueado()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
            MarcarDatas(form, Hoje, Hoje.AddDays(-1));

            ClicarConfirmar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Contains("anterior", ObterControle<Label>(form, "statusLabel").Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void ConfirmacaoValida_DeveRetornarDadosNormalizadosSemHorario()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = " lote-a ";
            MarcarDatas(form, Hoje.AddHours(3), Hoje.AddDays(30).AddHours(5));

            ClicarConfirmar(form);

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.NotNull(form.DadosConfirmados);
            Assert.Equal("LOTE-A", form.DadosConfirmados!.NumeroLote);
            Assert.Equal(Hoje.Date, form.DadosConfirmados.DataFabricacao);
            Assert.Equal(Hoje.AddDays(30).Date, form.DadosConfirmados.DataVencimento);
        });

    [Fact]
    public void Cancelar_DeveManterDadosConfirmadosNulo()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
            MarcarDatasValidas(form);

            ClicarCancelar(form);

            Assert.Null(form.DadosConfirmados);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
        });

    [Fact]
    public void ResultadoInvalidoNaoFechaENovaTentativaValidaPreencheSomenteDepois()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.Quimico);
            ClicarConfirmar(form);
            Assert.Null(form.DadosConfirmados);
            Assert.False(form.IsDisposed);

            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "Q1";
            MarcarDatasValidas(form);
            ClicarConfirmar(form);

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal("Q1", form.DadosConfirmados!.NumeroLote);
        });

    [Fact]
    public void Dialogo_DeveConfigurarAcceptCancelTabIndexEFoco()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);

            Assert.Same(ObterControle<Button>(form, "confirmarButton"), form.AcceptButton);
            Assert.Same(ObterControle<Button>(form, "cancelarButton"), form.CancelButton);
            Assert.Equal(0, ObterControle<TextBox>(form, "numeroLoteTextBox").TabIndex);
            Assert.Equal(1, ObterControle<DateTimePicker>(form, "dataFabricacaoDateTimePicker").TabIndex);
            Assert.Equal(2, ObterControle<DateTimePicker>(form, "dataVencimentoDateTimePicker").TabIndex);
        });

    [Fact]
    public void Dialogo_NaoDeveReferenciarBancoSapImpressaoControllerOuTelaPrincipal()
    {
        string conteudo = LerDialogo();

        Assert.DoesNotContain("Npgsql", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Repositorio", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("Sap", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Impress", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProcessoEntradaProdutoForm", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoController", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("PedidoCompraSapItem", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("DataGridView", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("EstadoSessaoUsuarioAtual", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaPrincipal_DeveConectarDialogoSomentePorInjecaoControlada()
    {
        string raiz = LocalizarRaizProjeto();
        string form = File.ReadAllText(Path.Combine(raiz, "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));
        string dialogo = File.ReadAllText(Path.Combine(raiz, "Tela", "Processo", "EntradaProdutoDadosLoteForm.cs"));
        string designer = File.ReadAllText(Path.Combine(raiz, "Tela", "Processo", "ProcessoEntradaProdutoForm.Designer.cs"));

        Assert.Contains("Func<IWin32Window, ModoEntradaMaterial, DadosLoteEntrada?>", form, StringComparison.Ordinal);
        Assert.Contains("SolicitarDadosLotePadrao", form, StringComparison.Ordinal);
        Assert.Contains("using EntradaProdutoDadosLoteForm form = new(modoEntrada)", form, StringComparison.Ordinal);
        Assert.Contains("ConfirmarLoteOperacaoComLotes", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemOperacaoComLotes", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessoEntradaProdutoForm", dialogo, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoDadosLoteForm", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void FluxosMateriaPrimaEQuimico_DevemUsarOsMesmosControles()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm materiaPrima = CriarForm(ModoEntradaMaterial.MateriaPrima);
            using EntradaProdutoDadosLoteForm quimico = CriarForm(ModoEntradaMaterial.Quimico);

            string[] controlesMateriaPrima = EnumerarControles(materiaPrima).Select(c => c.Name).Order().ToArray();
            string[] controlesQuimico = EnumerarControles(quimico).Select(c => c.Name).Order().ToArray();

            Assert.Equal(controlesMateriaPrima, controlesQuimico);
        });

    [Fact]
    public void ReabrirMesmaInstancia_DeveLimparResultadoDialogResultStatusEFocarLote()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            PrepararParaDialogoModal(form);
            int exibicao = 0;
            Exception? erroModal = null;

            form.VisibleChanged += (_, _) =>
            {
                if (!form.Visible)
                {
                    return;
                }

                exibicao++;
                form.BeginInvoke(new MethodInvoker(() =>
                {
                    try
                    {
                        if (exibicao == 1)
                        {
                            ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-A";
                            MarcarDatasValidas(form);
                            ObterControle<Button>(form, "confirmarButton").PerformClick();
                            return;
                        }

                        Assert.Null(form.DadosConfirmados);
                        Assert.Equal(DialogResult.None, form.DialogResult);
                        Assert.Equal(string.Empty, ObterControle<Label>(form, "statusLabel").Text);
                        Assert.False(ObterControle<Label>(form, "statusLabel").Visible);
                        Assert.True(ObterControle<TextBox>(form, "numeroLoteTextBox").Focused);
                        ObterControle<Button>(form, "cancelarButton").PerformClick();
                    }
                    catch (Exception ex)
                    {
                        erroModal = ex;
                        form.Close();
                    }
                }));
            };

            DialogResult primeiroResultado = form.ShowDialog();
            if (erroModal is not null)
            {
                throw erroModal;
            }

            Assert.Equal(DialogResult.OK, primeiroResultado);
            Assert.NotNull(form.DadosConfirmados);

            DialogResult segundoResultado = form.ShowDialog();
            if (erroModal is not null)
            {
                throw erroModal;
            }

            Assert.Equal(DialogResult.Cancel, segundoResultado);
            Assert.Null(form.DadosConfirmados);
        });

    [Fact]
    public void FecharSemConfirmacao_DeveManterResultadoNuloENaoRetornarOk()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            PrepararParaDialogoModal(form);

            form.VisibleChanged += (_, _) =>
            {
                if (form.Visible)
                {
                    form.BeginInvoke(new MethodInvoker(form.Close));
                }
            };

            DialogResult resultado = form.ShowDialog();

            Assert.NotEqual(DialogResult.OK, resultado);
            Assert.Null(form.DadosConfirmados);
        });

    [Fact]
    public void FecharReaberturaSemConfirmacao_NaoDeveReaproveitarResultadoAnterior()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            PrepararParaDialogoModal(form);
            int exibicao = 0;

            form.VisibleChanged += (_, _) =>
            {
                if (!form.Visible)
                {
                    return;
                }

                exibicao++;
                form.BeginInvoke(new MethodInvoker(() =>
                {
                    if (exibicao == 1)
                    {
                        ObterControle<TextBox>(form, "numeroLoteTextBox").Text = "LOTE-B";
                        MarcarDatasValidas(form);
                        ObterControle<Button>(form, "confirmarButton").PerformClick();
                        return;
                    }

                    form.Close();
                }));
            };

            Assert.Equal(DialogResult.OK, form.ShowDialog());
            Assert.NotNull(form.DadosConfirmados);

            DialogResult segundoResultado = form.ShowDialog();

            Assert.NotEqual(DialogResult.OK, segundoResultado);
            Assert.Null(form.DadosConfirmados);
        });

    [Fact]
    public void ReabrirAposStatusInvalido_DeveLimparMensagemResultadoEDialogResult()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.Quimico);
            PrepararParaDialogoModal(form);
            int exibicao = 0;
            Exception? erroModal = null;

            form.VisibleChanged += (_, _) =>
            {
                if (!form.Visible)
                {
                    return;
                }

                exibicao++;
                form.BeginInvoke(new MethodInvoker(() =>
                {
                    try
                    {
                        if (exibicao == 1)
                        {
                            ObterControle<Button>(form, "confirmarButton").PerformClick();
                            Assert.True(ObterControle<Label>(form, "statusLabel").Visible);
                            form.Close();
                            return;
                        }

                        Assert.Equal(string.Empty, ObterControle<Label>(form, "statusLabel").Text);
                        Assert.False(ObterControle<Label>(form, "statusLabel").Visible);
                        Assert.Null(form.DadosConfirmados);
                        Assert.Equal(DialogResult.None, form.DialogResult);
                        ObterControle<Button>(form, "cancelarButton").PerformClick();
                    }
                    catch (Exception ex)
                    {
                        erroModal = ex;
                        form.Close();
                    }
                }));
            };

            Assert.NotEqual(DialogResult.OK, form.ShowDialog());
            if (erroModal is not null)
            {
                throw erroModal;
            }

            Assert.Equal(DialogResult.Cancel, form.ShowDialog());
            if (erroModal is not null)
            {
                throw erroModal;
            }
        });

    [Fact]
    public void ExibicaoReal_DeveFocarNumeroDoLote()
        => ExecutarEmSta(() =>
        {
            using EntradaProdutoDadosLoteForm form = CriarForm(ModoEntradaMaterial.MateriaPrima);
            PrepararParaDialogoModal(form);
            Exception? erroModal = null;

            form.VisibleChanged += (_, _) =>
            {
                if (form.Visible)
                {
                    form.BeginInvoke(new MethodInvoker(() =>
                    {
                        try
                        {
                            Assert.True(ObterControle<TextBox>(form, "numeroLoteTextBox").Focused);
                            ObterControle<Button>(form, "cancelarButton").PerformClick();
                        }
                        catch (Exception ex)
                        {
                            erroModal = ex;
                            form.Close();
                        }
                    }));
                }
            };

            Assert.Equal(DialogResult.Cancel, form.ShowDialog());
            if (erroModal is not null)
            {
                throw erroModal;
            }
        });

    private static EntradaProdutoDadosLoteForm CriarForm(ModoEntradaMaterial modo)
        => new(modo, new ValidadorDadosLoteEntrada(() => Hoje));

    private static void ClicarConfirmar(EntradaProdutoDadosLoteForm form)
    {
        PrepararParaClique(form);
        ObterControle<Button>(form, "confirmarButton").PerformClick();
        Application.DoEvents();
    }

    private static void ClicarCancelar(EntradaProdutoDadosLoteForm form)
    {
        PrepararParaClique(form);
        ObterControle<Button>(form, "cancelarButton").PerformClick();
        Application.DoEvents();
    }

    private static void PrepararParaClique(Form form)
    {
        if (form.Visible)
        {
            return;
        }

        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.Show();
        Application.DoEvents();
    }

    private static void PrepararParaDialogoModal(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
    }

    private static void MarcarDatasValidas(EntradaProdutoDadosLoteForm form)
        => MarcarDatas(form, Hoje, Hoje.AddDays(30));

    private static void MarcarDatas(EntradaProdutoDadosLoteForm form, DateTime fabricacao, DateTime vencimento)
    {
        DateTimePicker fabricacaoPicker = ObterControle<DateTimePicker>(form, "dataFabricacaoDateTimePicker");
        DateTimePicker vencimentoPicker = ObterControle<DateTimePicker>(form, "dataVencimentoDateTimePicker");
        fabricacaoPicker.Value = fabricacao;
        fabricacaoPicker.Checked = true;
        vencimentoPicker.Value = vencimento;
        vencimentoPicker.Checked = true;
    }

    private static string ObterTodosTextos(Control controle)
        => string.Join(" ", EnumerarControles(controle).Select(c => c.Text));

    private static IEnumerable<Control> EnumerarControles(Control controle)
    {
        yield return controle;
        foreach (Control filho in controle.Controls)
        {
            foreach (Control descendente in EnumerarControles(filho))
            {
                yield return descendente;
            }
        }
    }

    private static T ObterControle<T>(Control raiz, string nome)
        where T : Control
        => Assert.IsType<T>(raiz.Controls.Find(nome, true).Single());

    private static string LerDialogo()
    {
        string raiz = LocalizarRaizProjeto();
        return File.ReadAllText(Path.Combine(raiz, "Tela", "Processo", "EntradaProdutoDadosLoteForm.cs"))
            + File.ReadAllText(Path.Combine(raiz, "Tela", "Processo", "EntradaProdutoDadosLoteForm.Designer.cs"));
    }

    private static string LocalizarRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
            {
                return diretorio.FullName;
            }

            diretorio = diretorio.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }

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
}
