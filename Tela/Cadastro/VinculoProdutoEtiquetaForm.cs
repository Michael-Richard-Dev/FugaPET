using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Tela.Controls;

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

        // A moldura (barra de titulo propria, rodape, cartoes, grade e botoes) vem do helper que as
        // telas de cadastro montadas em codigo compartilham. Feito ANTES de montar o layout porque a
        // faixa da marca passa a ser tambem a barra de titulo.
        Comum.ShellCadastroFluidoHelper.PrepararFormulario(this);

        MontarLayout();
        Shown += async (_, _) => await CarregarAsync();
        KeyDown += AoTeclar;
    }

    private float Fator => DeviceDpi / 96f;

    private int Escalar(int valor) => (int)Math.Round(valor * Fator);

    private void MontarLayout()
    {
        // Duas linhas: o conteudo e a linha de situacao. O FILTRO saiu daqui e foi para DENTRO do
        // cartao da lista, como a busca das outras telas de cadastro — solto sobre o fundo ele era o
        // que mais denunciava que esta tela vinha de outra geracao.
        TableLayoutPanel raiz = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(Escalar(12)),
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        raiz.Controls.Add(MontarConteudo(), 0, 0);

        // A ORDEM DE INSERCAO IMPORTA com Dock: o ultimo a entrar fica mais perto da borda. Por isso
        // a raiz (Fill) entra primeiro e as faixas depois — assim o conteudo ocupa o que sobra entre
        // a faixa da marca no topo e o rodape do sistema embaixo, sem nenhuma das duas por cima.
        // As faixas ficam FORA do TableLayoutPanel porque vao de borda a borda, e a raiz tem recuo.
        Controls.Add(raiz);
        Controls.Add(Comum.ShellCadastroFluidoHelper.CriarRodape(Escalar));
        Controls.Add(Comum.ShellCadastroFluidoHelper.CriarBarraDeTitulo(
            this,
            Escalar,
            $"Vínculo de Produto e Etiqueta  ·  centro {_centro}",
            "Define a etiqueta e o processo de pesagem de cada produto acabado",
            Properties.Resources.etiqueta_24x_white));
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
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, Escalar(8), Escalar(8), 0),
        };

        _filtro.Width = Escalar(280);
        _filtro.Font = new Font("Segoe UI", Font.Size);
        _filtro.Margin = new Padding(0, Escalar(6), Escalar(16), 0);
        _filtro.TextChanged += (_, _) => AplicarFiltro();

        // O botao SINCRONIZA; abrir a tela apenas LE o catalogo local. Era o contrario ate
        // 18/11/2026, e abrir custava 51 consultas ao SAP.
        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_atualizar, Escalar, Font, "Atualizar do SAP (F5)", principal: false);
        _atualizar.Dock = DockStyle.None;
        _atualizar.AutoSize = false;
        _atualizar.Width = Escalar(170);
        _atualizar.Margin = new Padding(0, Escalar(2), 0, 0);
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
        // LARGURA ABSOLUTA para a coluna da decisao, nao AutoSize: com AutoSize o cartao decidia a
        // largura da coluna e a coluna decidia a do cartao, e o resultado media mais que a tela —
        // "Salvar vínculo" e "Este produto não leva etiqueta" saiam cortados na borda direita.
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Escalar(360)));

        // O cartao da lista leva o filtro no topo e a grade embaixo.
        TableLayoutPanel listaComFiltro = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        listaComFiltro.RowCount = 3;
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listaComFiltro.Controls.Add(MontarCabecalho(), 0, 0);
        listaComFiltro.Controls.Add(MontarGrade(), 0, 1);
        // A contagem fica DENTRO do cartao, embaixo da lista, como o "Exibindo N de N" das outras
        // telas de cadastro. Solta sobre o fundo ela ficava orfa.
        listaComFiltro.Controls.Add(MontarLinhaDeSituacao(), 0, 2);

        conteudo.Controls.Add(
            Comum.ShellCadastroFluidoHelper.EmCartao(Escalar, Font, listaComFiltro, "Produtos do centro"), 0, 0);
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

        Comum.ShellCadastroFluidoHelper.EstilizarGrade(_grade, Escalar, Font);

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
        // A borda quadrada de um pixel (BorderStyle.FixedSingle) saiu: o cartao arredondado vem do
        // EmCartao, igual ao da lista e ao das telas com Designer.
        TableLayoutPanel painel = new()
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
        };
        // SEM ESTA LINHA a coluna nasce AutoSize e se dimensiona pelo filho mais largo, em vez de
        // dar a largura do cartao a todos. Foi assim que os campos vazaram a borda na tela irma.
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // TODOS OS FILHOS NA MESMA COLUNA VISUAL. MEDIDO em 26/11/2026: as caixas tinham 290px
        // cravados enquanto os botoes (Dock=Top) iam a 316, e os rotulos herdavam a margem PADRAO do
        // TableLayoutPanel (3px) enquanto as caixas tinham margem 0 — resultado: nada se alinhava com
        // nada dentro do cartao. Agora todo filho ocupa a largura da coluna e tem margem horizontal
        // zero; so a vertical varia.
        _rotuloProduto.Text = "Selecione um produto na lista";
        _rotuloProduto.AutoSize = false;
        _rotuloProduto.Dock = DockStyle.Top;
        _rotuloProduto.Height = Escalar(38);
        _rotuloProduto.TextAlign = ContentAlignment.TopLeft;
        _rotuloProduto.Font = new Font("Segoe UI", Font.Size + 1f, FontStyle.Bold);
        _rotuloProduto.Margin = new Padding(0, 0, 0, Escalar(8));

        Label rotuloEtiqueta = Comum.ShellCadastroFluidoHelper.CriarRotuloDoCampo(Escalar, Font, "Etiqueta:");

        // FONTE EXPLICITA: sem isto as caixas herdam a Cascadia Code do Form e o valor sai em
        // monoespacada no meio de um cartao todo em Segoe UI.
        _etiquetas.DropDownStyle = ComboBoxStyle.DropDownList;
        _etiquetas.Dock = DockStyle.Top;
        _etiquetas.Font = new Font("Segoe UI", Font.Size);
        _etiquetas.Margin = new Padding(0, Escalar(2), 0, Escalar(10));

        _semEtiqueta.Text = "Este produto não leva etiqueta";
        _semEtiqueta.Font = new Font("Segoe UI", 9f);
        _semEtiqueta.AutoSize = false;
        _semEtiqueta.Dock = DockStyle.Top;
        _semEtiqueta.Height = Escalar(24);
        _semEtiqueta.Margin = new Padding(0, 0, 0, Escalar(12));
        _semEtiqueta.CheckedChanged += (_, _) => _etiquetas.Enabled = !_semEtiqueta.Checked;

        Label rotuloProcesso = Comum.ShellCadastroFluidoHelper.CriarRotuloDoCampo(Escalar, Font, "Processo de pesagem:");

        _processos.DropDownStyle = ComboBoxStyle.DropDownList;
        _processos.Dock = DockStyle.Top;
        _processos.Font = new Font("Segoe UI", Font.Size);
        _processos.Margin = new Padding(0, Escalar(2), 0, Escalar(6));
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
        _avisoProcesso.Dock = DockStyle.Top;
        // Cinza, não o laranja de alerta: o texto passou a DESCREVER o processo, não a avisar de uma
        // limitação. Manter a cor de alarme faria uma escolha normal parecer arriscada.
        _avisoProcesso.ForeColor = Color.FromArgb(0x6B, 0x72, 0x80);
        _avisoProcesso.Font = new Font("Segoe UI", 9f);
        _avisoProcesso.Margin = new Padding(0, 0, 0, Escalar(12));

        // OS DOIS BOTOES no padrao MEDIDO das telas de cadastro: o de gravar e laranja solido com
        // texto branco, e o destrutivo e branco com borda cinza e texto laranja. Era o visual padrao
        // do Windows que fazia estes dois parecerem de outro sistema.
        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_salvar, Escalar, Font, "Salvar vínculo", principal: true);
        _salvar.Margin = new Padding(0, 0, 0, Escalar(6));
        _salvar.Click += async (_, _) => await SalvarAsync();

        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_remover, Escalar, Font, "Remover vínculo", principal: false);
        _remover.Margin = new Padding(0);
        _remover.Click += async (_, _) => await RemoverAsync();

        painel.Controls.AddRange(
        [
            _rotuloProduto, rotuloEtiqueta, _etiquetas, _semEtiqueta,
            rotuloProcesso, _processos, _avisoProcesso, _salvar, _remover,
        ]);

        AtualizarAvisoProcesso();
        HabilitarDecisao(false);

        // O painel para de mandar na largura: quem manda e a coluna (absoluta), e o cartao a
        // preenche. Assim os rotulos e os botoes tem a largura real para desenhar.
        painel.AutoSize = false;
        painel.Dock = DockStyle.Fill;

        Control cartao = Comum.ShellCadastroFluidoHelper.EmCartao(Escalar, Font, painel, "Decisão do produto");
        cartao.Margin = new Padding(Escalar(16), 0, 0, 0);
        return cartao;
    }

    /// <summary>
    /// A LINHA DE SITUACAO da tela — o que a ultima operacao respondeu. Nao confundir com o rodape
    /// do sistema (usuario, terminal, empresa, banco, hora, data), que entra separado e vem do
    /// <see cref="Tela.Comum.ShellProcessoPadraoHelper"/> para esta tela nao ganhar uma copia dele.
    /// </summary>
    private Control MontarLinhaDeSituacao()
    {
        _situacao.Dock = DockStyle.Fill;
        _situacao.AutoSize = false;
        _situacao.Height = Escalar(22);
        _situacao.TextAlign = ContentAlignment.MiddleLeft;
        _situacao.ForeColor = Color.FromArgb(0x47, 0x55, 0x69);
        _situacao.Font = new Font("Segoe UI", 9f);
        _situacao.Margin = new Padding(0, Escalar(8), 0, 0);
        return _situacao;
    }

    /// <summary>
    /// Explica o que a escolha faz na pesagem.
    /// <para>
    /// Até 19/11/2026 este aviso dizia que o envio único não estava implementado, o que era verdade e
    /// deixou de ser. Aviso que descreve o comportamento tem de morrer junto com o comportamento que
    /// descreve: um alerta desatualizado é pior que nenhum, porque o operador aprende a não ler.
    /// </para>
    /// </summary>
    private void AtualizarAvisoProcesso()
        => _avisoProcesso.Text = ProcessoSelecionado() == ProcessoPesagemProduto.EnvioUnico
            ? "Envio único: as caixas são pesadas sem ir ao SAP, e um botão envia todas no fim — "
              + "uma Handling Unit por caixa, como no caixa a caixa. O envio para na primeira que não confirmar."
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
