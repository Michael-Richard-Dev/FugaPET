using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Tela.Cadastro;

/// <summary>
/// Vincula cada produto acabado do centro a uma etiqueta e a um processo de pesagem.
/// <para>
/// A tela e montada EM CODIGO, com layout fluido e tamanhos derivados do DPI. Nao ha Designer e nao
/// ha pixel cravado de proposito: as telas antigas quebram quando instaladas em maquina com outra
/// resolucao justamente porque a fonte acompanha o DPI e a geometria cravada nao.
/// </para>
/// <para>
/// Permissao: entra por ETIQUETA/ETIQUETA. Nao criei rotina propria porque isso exigiria semear
/// permissao no banco antes de a tela poder ser aberta, e quem administra etiqueta e quem decide
/// este vinculo. Se virar perfil separado, a rotina nova entra aqui.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiquetaForm : Form
{
    private readonly VinculoProdutoEtiquetaController _controller;

    private readonly DataGridView _grade = new();
    private readonly TextBox _filtro = new();
    private readonly Label _rotuloProduto = new();
    private readonly ComboBox _etiquetas = new();
    private readonly CheckBox _semEtiqueta = new();
    private readonly ComboBox _processos = new();
    private readonly Label _avisoProcesso = new();
    private readonly Button _salvar = new();
    private readonly Button _remover = new();
    private readonly Button _atualizar = new();
    private readonly Label _situacao = new();

    private IReadOnlyList<LinhaVinculoProduto> _linhas = [];
    private IReadOnlyList<EtiquetaCadastro> _etiquetasDisponiveis = [];
    private string _centro = VinculoProdutoEtiqueta.CentroPadrao;

    public VinculoProdutoEtiquetaForm(VinculoProdutoEtiquetaController? controller = null)
    {
        _controller = controller ?? FabricaControladoresCadastro.CriarVinculoProdutoEtiquetaController();

        Text = $"Vínculo de Produto e Etiqueta — centro {_centro}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(Escalar(900), Escalar(560));
        KeyPreview = true;

        MontarLayout();
        Shown += async (_, _) => await CarregarAsync();
        KeyDown += AoTeclar;
    }

    private float Fator => DeviceDpi / 96f;

    private int Escalar(int valor) => (int)Math.Round(valor * Fator);

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

        // A faixa da marca fica FORA do TableLayoutPanel, ancorada no topo do formulario: ela precisa
        // ir de borda a borda, enquanto a raiz tem recuo interno.
        Controls.Add(raiz);
        Controls.Add(MontarFaixaDaMarca());
        raiz.Padding = new Padding(Escalar(12), Escalar(12), Escalar(12), Escalar(12));
    }

    /// <summary>
    /// Faixa da marca no topo, no padrao FugaPET das demais telas: fundo laranja, logo branco a
    /// esquerda e o titulo da tela ao lado.
    /// <para>
    /// O logo vem de <see cref="Tela.Comum.IdentidadeVisualFugaPet"/>, que ja sabe cair no recurso
    /// embutido quando o arquivo nao foi distribuido — uma tela sem logo e feia, uma tela que nao
    /// abre por falta de imagem e um chamado.
    /// </para>
    /// </summary>
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
            Text = $"Vínculo de Produto e Etiqueta  ·  centro {_centro}",
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

        Label rotulo = new()
        {
            Text = "Filtrar por código ou descrição:",
            AutoSize = true,
            Margin = new Padding(0, Escalar(8), Escalar(8), 0),
        };

        _filtro.Width = Escalar(280);
        _filtro.Margin = new Padding(0, Escalar(4), Escalar(16), 0);
        _filtro.TextChanged += (_, _) => AplicarFiltro();

        // O botao SINCRONIZA; abrir a tela apenas LE o catalogo local. Era o contrario ate
        // 18/11/2026, e abrir custava 51 consultas ao SAP.
        _atualizar.Text = "Atualizar do SAP (F5)";
        _atualizar.AutoSize = true;
        _atualizar.Margin = new Padding(0, Escalar(3), 0, 0);
        _atualizar.Click += async (_, _) => await SincronizarAsync();

        faixa.Controls.AddRange([rotulo, _filtro, _atualizar]);
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
            Coluna("Descrição", 38),
            Coluna("Tipo", 8),
            Coluna("Etiqueta", 20),
            Coluna("Processo", 16),
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

        Label rotuloEtiqueta = new() { Text = "Etiqueta:", AutoSize = true };
        _etiquetas.DropDownStyle = ComboBoxStyle.DropDownList;
        _etiquetas.Width = Escalar(290);
        _etiquetas.Margin = new Padding(0, Escalar(4), 0, Escalar(8));

        _semEtiqueta.Text = "Este produto não leva etiqueta";
        _semEtiqueta.AutoSize = true;
        _semEtiqueta.Margin = new Padding(0, 0, 0, Escalar(12));
        _semEtiqueta.CheckedChanged += (_, _) => _etiquetas.Enabled = !_semEtiqueta.Checked;

        Label rotuloProcesso = new() { Text = "Processo de pesagem:", AutoSize = true };
        _processos.DropDownStyle = ComboBoxStyle.DropDownList;
        _processos.Width = Escalar(290);
        _processos.Margin = new Padding(0, Escalar(4), 0, Escalar(4));
        _processos.Items.AddRange(
        [
            VinculoProdutoEtiqueta.RotuloDoProcesso(ProcessoPesagemProduto.CaixaACaixa),
            VinculoProdutoEtiqueta.RotuloDoProcesso(ProcessoPesagemProduto.EnvioUnico),
        ]);
        _processos.SelectedIndex = 0;
        _processos.SelectedIndexChanged += (_, _) => AtualizarAvisoProcesso();

        // O aviso nao e decoracao: o envio unico ainda nao existe no sistema, e cadastrar a intencao
        // sem dizer isso faria o usuario supor que a pesagem ja se comporta assim.
        _avisoProcesso.AutoSize = true;
        _avisoProcesso.MaximumSize = new Size(Escalar(290), 0);
        _avisoProcesso.ForeColor = Color.FromArgb(0xB4, 0x50, 0x09);
        _avisoProcesso.Margin = new Padding(0, 0, 0, Escalar(12));

        _salvar.Text = "Salvar vínculo";
        _salvar.Width = Escalar(290);
        _salvar.Margin = new Padding(0, 0, 0, Escalar(6));
        _salvar.Click += async (_, _) => await SalvarAsync();

        _remover.Text = "Remover vínculo";
        _remover.Width = Escalar(290);
        _remover.Click += async (_, _) => await RemoverAsync();

        painel.Controls.AddRange(
        [
            _rotuloProduto, rotuloEtiqueta, _etiquetas, _semEtiqueta,
            rotuloProcesso, _processos, _avisoProcesso, _salvar, _remover,
        ]);

        AtualizarAvisoProcesso();
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

    private void AtualizarAvisoProcesso()
        => _avisoProcesso.Text = ProcessoSelecionado() == ProcessoPesagemProduto.EnvioUnico
            ? "Atenção: o envio único ainda não está implementado na pesagem. "
              + "A escolha fica registrada, mas hoje não há caminho de envio para ela."
            : string.Empty;

    private ProcessoPesagemProduto ProcessoSelecionado()
        => _processos.SelectedIndex == 1
            ? ProcessoPesagemProduto.EnvioUnico
            : ProcessoPesagemProduto.CaixaACaixa;

    /// <summary>
    /// Busca no SAP o que o catalogo local ainda nao tem e recarrega a tela.
    /// <para>
    /// Separado de <see cref="CarregarAsync"/> de proposito: carregar le o banco e e instantaneo;
    /// sincronizar vai a rede e pode demorar. Misturar os dois faria toda abertura pagar o preco da
    /// rede, que e o que esta mudanca veio corrigir.
    /// </para>
    /// </summary>
    private async Task SincronizarAsync()
    {
        _atualizar.Enabled = false;
        UseWaitCursor = true;
        _situacao.Text = "Consultando o SAP...";
        try
        {
            Modelo.Cadastro.ResumoSincronizacaoCatalogo resumo =
                await _controller.SincronizarCatalogoAsync(_centro);

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
        UseWaitCursor = true;
        try
        {
            _etiquetasDisponiveis = await _controller.ListarEtiquetasDisponiveisAsync();
            _etiquetas.Items.Clear();
            foreach (EtiquetaCadastro etiqueta in _etiquetasDisponiveis)
            {
                _etiquetas.Items.Add(etiqueta.NomeEtiqueta);
            }

            ResultadoListagemVinculo resultado = await _controller.ListarAsync(_centro);
            _linhas = resultado.Linhas;
            AplicarFiltro();

            int configurados = _linhas.Count(l => l.Configurado);
            string resumo = $"{_linhas.Count} produto(s) · {configurados} configurado(s) · "
                          + $"{_linhas.Count - configurados} sem decisão";

            _situacao.Text = resultado.CatalogoDisponivel
                ? resumo
                : $"{resultado.MensagemCatalogo}  Mostrando apenas os vínculos já gravados ({_linhas.Count}).";

            // Sem etiqueta elegivel, o combo fica vazio e o operador nao teria como saber por que.
            // Dizer isso aqui e melhor do que deixa-lo procurar a etiqueta que nunca vai aparecer.
            if (_etiquetasDisponiveis.Count == 0)
            {
                _situacao.Text += "  Nenhuma etiqueta do tipo "
                    + $"{Servicos.Cadastro.VinculoProdutoEtiquetaServico.TipoEtiquetaSaidaProdutoAcabado}"
                    + " está cadastrada e ativa: só é possível marcar \"não leva etiqueta\".";
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
        IEnumerable<LinhaVinculoProduto> visiveis = termo.Length == 0
            ? _linhas
            : _linhas.Where(l =>
                l.Produto.Contains(termo, StringComparison.OrdinalIgnoreCase)
                || l.DescricaoParaExibir.Contains(termo, StringComparison.OrdinalIgnoreCase));

        _grade.Rows.Clear();
        foreach (LinhaVinculoProduto linha in visiveis)
        {
            int indice = _grade.Rows.Add(
                linha.Produto,
                linha.DescricaoParaExibir,
                linha.TipoProdutoSap ?? linha.Vinculo?.TipoProduto ?? string.Empty,
                linha.SituacaoParaExibir,
                linha.Vinculo is null
                    ? string.Empty
                    : VinculoProdutoEtiqueta.RotuloDoProcesso(linha.Vinculo.ProcessoPesagem));

            _grade.Rows[indice].Tag = linha;

            if (!linha.PresenteNoSap)
            {
                _grade.Rows[indice].DefaultCellStyle.ForeColor = Color.FromArgb(0xB4, 0x50, 0x09);
                _grade.Rows[indice].Cells[1].ToolTipText =
                    "Vínculo gravado para um produto que o SAP não listou nesta consulta.";
            }
        }

        MostrarSelecionado();
    }

    private LinhaVinculoProduto? LinhaSelecionada()
        => _grade.CurrentRow?.Tag as LinhaVinculoProduto;

    private void MostrarSelecionado()
    {
        LinhaVinculoProduto? linha = LinhaSelecionada();
        if (linha is null)
        {
            _rotuloProduto.Text = "Selecione um produto na lista";
            HabilitarDecisao(false);
            return;
        }

        _rotuloProduto.Text = string.IsNullOrWhiteSpace(linha.DescricaoParaExibir)
            ? linha.Produto
            : $"{linha.Produto} — {linha.DescricaoParaExibir}";

        _semEtiqueta.Checked = linha.Vinculo?.SemEtiqueta ?? false;
        _etiquetas.Enabled = !_semEtiqueta.Checked;

        int indice = linha.Vinculo?.CodigoEtiqueta is long codigo
            ? IndiceDaEtiqueta(codigo)
            : -1;
        _etiquetas.SelectedIndex = indice;

        _processos.SelectedIndex = linha.Vinculo?.ProcessoPesagem == ProcessoPesagemProduto.EnvioUnico ? 1 : 0;

        HabilitarDecisao(true);
        _remover.Enabled = linha.Configurado;
    }

    private int IndiceDaEtiqueta(long codigo)
    {
        for (int i = 0; i < _etiquetasDisponiveis.Count; i++)
        {
            if (_etiquetasDisponiveis[i].CodigoEtiqueta == codigo) return i;
        }
        return -1;
    }

    private void HabilitarDecisao(bool habilitado)
    {
        _etiquetas.Enabled = habilitado && !_semEtiqueta.Checked;
        _semEtiqueta.Enabled = habilitado;
        _processos.Enabled = habilitado;
        _salvar.Enabled = habilitado;
        _remover.Enabled = habilitado;
    }

    private async Task SalvarAsync()
    {
        LinhaVinculoProduto? linha = LinhaSelecionada();
        if (linha is null) return;

        VinculoProdutoEtiqueta vinculo = new()
        {
            Centro = _centro,
            Produto = linha.Produto,
            DescricaoProduto = linha.DescricaoParaExibir,
            TipoProduto = linha.TipoProdutoSap ?? linha.Vinculo?.TipoProduto,
            SemEtiqueta = _semEtiqueta.Checked,
            CodigoEtiqueta = _semEtiqueta.Checked || _etiquetas.SelectedIndex < 0
                ? null
                : _etiquetasDisponiveis[_etiquetas.SelectedIndex].CodigoEtiqueta,
            ProcessoPesagem = ProcessoSelecionado(),
        };

        ResultadoOperacao resultado = await _controller.SalvarAsync(vinculo);
        _situacao.Text = resultado.Mensagem;

        if (resultado.Sucesso)
        {
            await CarregarAsync();
        }
        else
        {
            MessageBox.Show(this, resultado.Mensagem, "Vínculo de produto",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RemoverAsync()
    {
        LinhaVinculoProduto? linha = LinhaSelecionada();
        if (linha?.Vinculo is null) return;

        DialogResult confirmacao = MessageBox.Show(
            this,
            $"Remover o vínculo do produto {linha.Produto}?\n\n"
            + "Ele volta a ficar sem decisão de etiqueta e de processo.",
            "Remover vínculo",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes) return;

        ResultadoOperacao resultado = await _controller.RemoverAsync(_centro, linha.Produto);
        _situacao.Text = resultado.Mensagem;
        if (resultado.Sucesso) await CarregarAsync();
    }

    private async void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            // F5 faz o mesmo que o botao: SINCRONIZA. O rotulo do botao promete isso.
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
