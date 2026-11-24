using System.Globalization;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Tela.Cadastro;

/// <summary>
/// Define, por produto do centro, quanto a pesagem pode divergir do previsto da ordem de produção —
/// para mais e para menos — em cada PROCESSO que deriva do Apontamento: Consumo de Material e
/// Produto Semi-Acabado.
/// <para>
/// Irmã de <see cref="ToleranciaRecebimentoMaterialForm"/>, e de propósito: as duas cruzam catálogo
/// de produto com decisão do usuário, e manter a mesma forma faz quem conhece uma saber operar a
/// outra sem aprender nada novo. A diferença é o SELETOR DE PROCESSO, que aqui faz parte da chave.
/// </para>
/// <para>
/// A tela é montada EM CÓDIGO, com tamanhos derivados do DPI. Não há Designer e não há pixel cravado
/// de propósito: telas antigas quebram em máquina com outra resolução justamente porque a fonte
/// acompanha o DPI e a geometria cravada não.
/// </para>
/// </summary>
public sealed class ToleranciaProcessoProducaoForm : Form
{
    private readonly ToleranciaProcessoProducaoController _controller;

    private readonly ComboBox _processo = new();
    private readonly DataGridView _grade = new();
    private readonly TextBox _filtro = new();
    private readonly CheckBox _soNaoConfigurados = new();
    private readonly Label _rotuloProduto = new();
    private readonly NumericUpDown _percentual = new();
    private readonly Label _exemplo = new();
    private readonly CheckBox _ativa = new();
    private readonly Button _salvar = new();
    private readonly Button _remover = new();
    private readonly Button _atualizar = new();
    private readonly Label _situacao = new();

    private IReadOnlyList<LinhaToleranciaProcesso> _linhas = [];
    private readonly string _centro = ToleranciaProcessoProducao.CentroPadrao;
    private bool _trocandoProcesso;

    public ToleranciaProcessoProducaoForm(ToleranciaProcessoProducaoController? controller = null)
    {
        _controller = controller ?? FabricaControladoresCadastro.CriarToleranciaProcessoProducaoController();

        Text = $"Tolerância de Produção — centro {_centro}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(Escalar(980), Escalar(560));
        KeyPreview = true;

        MontarLayout();
        Shown += async (_, _) => await CarregarAsync();
        KeyDown += AoTeclar;
    }

    private float Fator => DeviceDpi / 96f;

    private int Escalar(int valor) => (int)Math.Round(valor * Fator);

    private string ProcessoSelecionado
        => _processo.SelectedItem is ItemProcesso item ? item.Codigo : ProcessoToleranciaProducao.Consumo;

    private sealed record ItemProcesso(string Codigo)
    {
        public override string ToString() => ProcessoToleranciaProducao.Descrever(Codigo);
    }

    private void MontarLayout()
    {
        TableLayoutPanel raiz = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(Escalar(12)),
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(MontarCabecalho(), 0, 0);
        raiz.Controls.Add(MontarConteudo(), 0, 1);
        raiz.Controls.Add(MontarRodape(), 0, 2);

        Controls.Add(raiz);
        Controls.Add(MontarFaixaDaMarca());
    }

    private Control MontarFaixaDaMarca()
    {
        Panel faixa = new()
        {
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(0xC8, 0x4E, 0x0A),
            Height = Escalar(62),
            Padding = new Padding(Escalar(16), Escalar(8), Escalar(16), Escalar(8)),
        };

        PictureBox logo = new()
        {
            Dock = DockStyle.Left,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = Escalar(132),
            BackColor = Color.Transparent,
        };
        Comum.IdentidadeVisualFugaPet.AplicarNoCabecalho(logo);

        Label titulo = new()
        {
            Dock = DockStyle.Fill,
            Text = $"Tolerância de Produção  ·  centro {_centro}",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(Escalar(16), 0, 0, 0),
            Font = new Font(Font.FontFamily, Font.Size + 3f, FontStyle.Bold),
        };

        faixa.Controls.Add(titulo);
        faixa.Controls.Add(logo);
        return faixa;
    }

    private Control MontarCabecalho()
    {
        FlowLayoutPanel faixa = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
        };

        Label rotuloProcesso = new()
        {
            Text = "Processo:",
            AutoSize = true,
            Margin = new Padding(0, Escalar(8), Escalar(8), 0),
        };

        // O processo faz parte da CHAVE: trocar aqui nao filtra a mesma lista, carrega outra decisao.
        // Por isso ele e o primeiro controle da tela, e nao um filtro perdido no meio.
        _processo.DropDownStyle = ComboBoxStyle.DropDownList;
        _processo.Width = Escalar(220);
        _processo.Margin = new Padding(0, Escalar(4), Escalar(16), 0);
        foreach (string codigo in ProcessoToleranciaProducao.Todos)
        {
            _processo.Items.Add(new ItemProcesso(codigo));
        }

        _processo.SelectedIndex = 0;
        _processo.SelectedIndexChanged += async (_, _) =>
        {
            if (_trocandoProcesso) { return; }
            await CarregarAsync();
        };

        Label rotuloFiltro = new()
        {
            Text = "Filtrar por código ou descrição:",
            AutoSize = true,
            Margin = new Padding(0, Escalar(8), Escalar(8), 0),
        };

        _filtro.Width = Escalar(260);
        _filtro.Margin = new Padding(0, Escalar(4), Escalar(16), 0);
        _filtro.TextChanged += (_, _) => AplicarFiltro();

        // Atalho para o trabalho que esta tela realmente cria: como produto sem tolerancia BLOQUEIA a
        // pesagem, a pergunta mais frequente aqui vai ser "o que ainda falta?".
        _soNaoConfigurados.Text = "Só os que faltam";
        _soNaoConfigurados.AutoSize = true;
        _soNaoConfigurados.Margin = new Padding(0, Escalar(8), Escalar(16), 0);
        _soNaoConfigurados.CheckedChanged += (_, _) => AplicarFiltro();

        _atualizar.Text = "Atualizar do SAP (F5)";
        _atualizar.AutoSize = true;
        _atualizar.Margin = new Padding(0, Escalar(3), 0, 0);
        _atualizar.Click += async (_, _) => await SincronizarAsync();

        faixa.Controls.AddRange(
            [rotuloProcesso, _processo, rotuloFiltro, _filtro, _soNaoConfigurados, _atualizar]);
        return faixa;
    }

    private Control MontarConteudo()
    {
        TableLayoutPanel conteudo = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, Escalar(8), 0, Escalar(8)),
        };
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        conteudo.Controls.Add(MontarGrade(), 0, 0);
        conteudo.Controls.Add(MontarPainelDecisao(), 1, 0);
        return conteudo;
    }

    private Control MontarGrade()
    {
        _grade.Dock = DockStyle.Fill;
        _grade.ReadOnly = true;
        _grade.AllowUserToAddRows = false;
        _grade.AllowUserToDeleteRows = false;
        _grade.AllowUserToResizeRows = false;
        _grade.MultiSelect = false;
        _grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grade.RowHeadersVisible = false;

        _grade.Columns.AddRange(
        [
            Coluna("Produto", 18),
            Coluna("Descrição", 46),
            Coluna("Tipo", 10),
            Coluna("Tolerância", 16),
        ]);

        _grade.SelectionChanged += (_, _) => MostrarSelecionado();
        return _grade;
    }

    private static DataGridViewTextBoxColumn Coluna(string titulo, int peso) => new()
    {
        HeaderText = titulo,
        FillWeight = peso,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    private Control MontarPainelDecisao()
    {
        TableLayoutPanel painel = new()
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(Escalar(16), 0, 0, 0),
            Padding = new Padding(Escalar(12)),
            BorderStyle = BorderStyle.FixedSingle,
            Width = Escalar(320),
        };

        _rotuloProduto.Text = "Selecione um produto na lista";
        _rotuloProduto.AutoSize = true;
        _rotuloProduto.MaximumSize = new Size(Escalar(290), 0);
        _rotuloProduto.Font = new Font(Font, FontStyle.Bold);
        _rotuloProduto.Margin = new Padding(0, 0, 0, Escalar(12));

        Label rotuloPercentual = new() { Text = "Tolerância (± %):", AutoSize = true };
        _percentual.Width = Escalar(290);
        _percentual.Margin = new Padding(0, Escalar(4), 0, Escalar(8));
        _percentual.DecimalPlaces = 2;
        _percentual.Increment = 0.5m;
        _percentual.Minimum = 0m;
        // O mesmo teto do banco (CHECK do incremento 071). Acima de 100 o limite inferior ficaria
        // negativo; deixar digitar para recusar depois seria fazer o usuário descobrir no erro.
        _percentual.Maximum = ToleranciaProcessoProducao.PercentualMaximo;
        _percentual.ValueChanged += (_, _) => AtualizarExemplo();

        // O exemplo não é enfeite: percentual é abstrato na hora de decidir, e o operador erra a ordem
        // de grandeza com facilidade. Ver o intervalo em números torna o engano visível antes de salvar.
        _exemplo.AutoSize = true;
        _exemplo.MaximumSize = new Size(Escalar(290), 0);
        _exemplo.ForeColor = Color.FromArgb(0x6B, 0x72, 0x80);
        _exemplo.Margin = new Padding(0, 0, 0, Escalar(12));

        _ativa.Text = "Tolerância ativa";
        _ativa.AutoSize = true;
        _ativa.Checked = true;
        _ativa.Margin = new Padding(0, 0, 0, Escalar(12));

        _salvar.Text = "Salvar tolerância";
        _salvar.Width = Escalar(290);
        _salvar.Margin = new Padding(0, 0, 0, Escalar(6));
        _salvar.Click += async (_, _) => await SalvarAsync();

        _remover.Text = "Remover tolerância";
        _remover.Width = Escalar(290);
        _remover.Click += async (_, _) => await RemoverAsync();

        painel.Controls.AddRange(
        [
            _rotuloProduto, rotuloPercentual, _percentual, _exemplo, _ativa, _salvar, _remover,
        ]);

        AtualizarExemplo();
        HabilitarDecisao(false);
        return painel;
    }

    private Control MontarRodape()
    {
        _situacao.Dock = DockStyle.Fill;
        _situacao.AutoSize = true;
        _situacao.Margin = new Padding(0, Escalar(4), 0, 0);
        return _situacao;
    }

    /// <summary>
    /// Traduz o percentual num intervalo concreto, com a palavra do processo selecionado.
    /// <para>
    /// A referência muda de nome conforme a tela: no Consumo é a quantidade PREVISTA do componente na
    /// ordem; no Semi-Acabado é a quantidade PLANEJADA da ordem. Usar "previsto" nos dois esconderia
    /// de onde vem o número que está sendo comparado.
    /// </para>
    /// </summary>
    private void AtualizarExemplo()
    {
        decimal p = _percentual.Value;
        decimal folga = 100m * p / 100m;

        string referencia = ProcessoSelecionado == ProcessoToleranciaProducao.SemiAcabado
            ? "planejado da OP"
            : "previsto do componente";

        _exemplo.Text = p == 0m
            ? $"Com 0%, a pesagem tem de bater exatamente com o {referencia}."
            : $"Num {referencia} de 100, aceita de {F(100m - folga)} a {F(100m + folga)}.";
    }

    private static string F(decimal valor)
        => valor.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

    private async Task SincronizarAsync()
    {
        _atualizar.Enabled = false;
        UseWaitCursor = true;
        _situacao.Text = "Consultando o SAP...";
        try
        {
            ResumoSincronizacaoCatalogo resumo = await _controller.SincronizarCatalogoAsync(_centro);

            await CarregarAsync();
            _situacao.Text = $"{resumo.Mensagem}  {_situacao.Text}";

            if (!resumo.Sucesso)
            {
                MessageBox.Show(this, resumo.Mensagem, "Atualizar do SAP",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception erro)
        {
            _situacao.Text = $"Não foi possível sincronizar: {erro.GetType().Name}.";
        }
        finally
        {
            UseWaitCursor = false;
            _atualizar.Enabled = true;
        }
    }

    private async Task CarregarAsync()
    {
        string processo = ProcessoSelecionado;
        UseWaitCursor = true;
        try
        {
            ResultadoListagemToleranciaProcesso resultado = await _controller.ListarAsync(_centro, processo);
            _linhas = resultado.Linhas;
            AplicarFiltro();
            AtualizarExemplo();

            int configurados = _linhas.Count - resultado.NaoConfigurados;
            string nome = ProcessoToleranciaProducao.Descrever(processo);
            string resumo = $"{nome}: {_linhas.Count} produto(s) · {configurados} com tolerância · "
                          + $"{resultado.NaoConfigurados} sem tolerância";

            _situacao.Text = resultado.CatalogoDisponivel
                ? resumo
                : $"{resultado.MensagemCatalogo}  Mostrando apenas as tolerâncias já gravadas ({_linhas.Count}).";

            // O número sozinho não diz o que está em jogo. Produto sem tolerância não é só "pendência
            // de cadastro": é produção que vai parar na balança.
            if (resultado.NaoConfigurados > 0)
            {
                _situacao.Text += $"  ATENÇÃO: a pesagem dos {resultado.NaoConfigurados} sem tolerância "
                                + $"fica bloqueada em {nome} até serem cadastrados.";
            }
        }
        catch (Exception erro)
        {
            _situacao.Text = $"Não foi possível carregar: {erro.GetType().Name}.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AplicarFiltro()
    {
        string termo = _filtro.Text.Trim();
        IEnumerable<LinhaToleranciaProcesso> visiveis = _linhas;

        if (termo.Length > 0)
        {
            visiveis = visiveis.Where(l =>
                l.Material.Contains(termo, StringComparison.OrdinalIgnoreCase)
                || l.DescricaoParaExibir.Contains(termo, StringComparison.OrdinalIgnoreCase));
        }

        if (_soNaoConfigurados.Checked)
        {
            visiveis = visiveis.Where(l => !l.Configurado);
        }

        string nome = ProcessoToleranciaProducao.Descrever(ProcessoSelecionado);

        _grade.Rows.Clear();
        foreach (LinhaToleranciaProcesso linha in visiveis)
        {
            int indice = _grade.Rows.Add(
                linha.Material,
                linha.DescricaoParaExibir,
                linha.TipoMaterialCatalogo ?? linha.Tolerancia?.TipoMaterial ?? string.Empty,
                linha.ToleranciaParaExibir);

            _grade.Rows[indice].Tag = linha;

            if (!linha.Configurado)
            {
                _grade.Rows[indice].DefaultCellStyle.ForeColor = Color.FromArgb(0xB4, 0x50, 0x09);
                _grade.Rows[indice].Cells[3].ToolTipText =
                    $"Sem tolerância ativa: a pesagem deste produto em {nome} fica bloqueada.";
            }

            if (!linha.PresenteNoCatalogo)
            {
                _grade.Rows[indice].Cells[1].ToolTipText =
                    "Tolerância gravada para um produto que o catálogo não listou nesta consulta.";
            }
        }

        MostrarSelecionado();
    }

    private LinhaToleranciaProcesso? LinhaSelecionada()
        => _grade.CurrentRow?.Tag as LinhaToleranciaProcesso;

    private void MostrarSelecionado()
    {
        LinhaToleranciaProcesso? linha = LinhaSelecionada();
        if (linha is null)
        {
            _rotuloProduto.Text = "Selecione um produto na lista";
            HabilitarDecisao(false);
            return;
        }

        _rotuloProduto.Text = string.IsNullOrWhiteSpace(linha.DescricaoParaExibir)
            ? linha.Material
            : $"{linha.Material} — {linha.DescricaoParaExibir}";

        _percentual.Value = linha.Tolerancia?.PercentualTolerancia ?? 0m;
        _ativa.Checked = linha.Tolerancia?.Situacao ?? true;

        AtualizarExemplo();
        HabilitarDecisao(true);
        _remover.Enabled = linha.Tolerancia is not null;
    }

    private void HabilitarDecisao(bool habilitado)
    {
        _percentual.Enabled = habilitado;
        _ativa.Enabled = habilitado;
        _salvar.Enabled = habilitado;
        _remover.Enabled = habilitado;
    }

    private async Task SalvarAsync()
    {
        LinhaToleranciaProcesso? linha = LinhaSelecionada();
        if (linha is null) { return; }

        ToleranciaProcessoProducao tolerancia = new()
        {
            Centro = _centro,
            Processo = ProcessoSelecionado,
            Material = linha.Material,
            PercentualTolerancia = _percentual.Value,
            DescricaoMaterial = linha.DescricaoParaExibir,
            TipoMaterial = linha.TipoMaterialCatalogo ?? linha.Tolerancia?.TipoMaterial,
            Situacao = _ativa.Checked,
        };

        ResultadoOperacao resultado = await _controller.SalvarAsync(tolerancia);
        _situacao.Text = resultado.Mensagem;

        if (resultado.Sucesso)
        {
            await CarregarAsync();
        }
        else
        {
            MessageBox.Show(this, resultado.Mensagem, "Tolerância de produção",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RemoverAsync()
    {
        LinhaToleranciaProcesso? linha = LinhaSelecionada();
        if (linha?.Tolerancia is null) { return; }

        string nome = ProcessoToleranciaProducao.Descrever(ProcessoSelecionado);

        // A confirmação diz a CONSEQUÊNCIA, não só a ação. Remover tolerância parece afrouxar e faz o
        // oposto: sem linha, o produto para de ser pesado naquele processo.
        DialogResult confirmacao = MessageBox.Show(
            this,
            $"Remover a tolerância do produto {linha.Material} em {nome}?\n\n"
            + $"A pesagem deste produto em {nome} fica BLOQUEADA até a tolerância ser cadastrada de novo.\n\n"
            + "A tolerância dele no outro processo, se houver, não é afetada.",
            "Remover tolerância",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmacao != DialogResult.Yes) { return; }

        ResultadoOperacao resultado = await _controller.RemoverAsync(_centro, ProcessoSelecionado, linha.Material);
        _situacao.Text = resultado.Mensagem;
        if (resultado.Sucesso) { await CarregarAsync(); }
    }

    private async void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            e.Handled = true;
            await SincronizarAsync();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
