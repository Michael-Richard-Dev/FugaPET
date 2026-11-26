using System.Globalization;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Tela.Cadastro;

/// <summary>
/// Define, por material do centro, quanto a pesagem do Recebimento pode divergir da quantidade do
/// pedido de compra — para mais e para menos.
/// <para>
/// Irma de <see cref="VinculoProdutoEtiquetaForm"/>, e de proposito: as duas cruzam catalogo de
/// material com decisao do usuario, e manter a mesma forma faz quem conhece uma saber operar a outra
/// sem aprender nada novo. A diferenca esta no que se decide e na lista: ali produto ACABADO, aqui
/// material que passa pelo RECEBIMENTO.
/// </para>
/// <para>
/// A tela e montada EM CODIGO, com tamanhos derivados do DPI. Nao ha Designer e nao ha pixel cravado
/// de proposito: telas antigas quebram em maquina com outra resolucao justamente porque a fonte
/// acompanha o DPI e a geometria cravada nao.
/// </para>
/// </summary>
public sealed class ToleranciaRecebimentoMaterialForm : Form
{
    private readonly ToleranciaRecebimentoMaterialController _controller;

    private readonly DataGridView _grade = new();
    private readonly TextBox _filtro = new();
    private readonly CheckBox _soNaoConfigurados = new();
    private readonly Label _rotuloMaterial = new();
    private readonly NumericUpDown _percentual = new();
    private readonly Label _exemplo = new();
    private readonly CheckBox _ativa = new();
    private readonly Button _salvar = new();
    private readonly Button _remover = new();
    private readonly Button _atualizar = new();
    private readonly Label _situacao = new();

    private IReadOnlyList<LinhaToleranciaMaterial> _linhas = [];
    private string _centro = ToleranciaRecebimentoMaterial.CentroPadrao;

    public ToleranciaRecebimentoMaterialForm(ToleranciaRecebimentoMaterialController? controller = null)
    {
        _controller = controller ?? FabricaControladoresCadastro.CriarToleranciaRecebimentoMaterialController();

        Text = $"Tolerância de Recebimento — centro {_centro}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(Escalar(900), Escalar(560));
        KeyPreview = true;

        // A moldura (barra de titulo propria, rodape, cartoes, grade e botoes) vem do helper que as
        // tres telas de cadastro montadas em codigo compartilham.
        Comum.ShellCadastroFluidoHelper.PrepararFormulario(this);

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
            RowCount = 1,
            Padding = new Padding(Escalar(12)),
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        raiz.Controls.Add(MontarConteudo(), 0, 0);

        // A ORDEM DE INSERCAO IMPORTA com Dock: a raiz (Fill) entra primeiro e as faixas depois, para
        // o conteudo ocupar o que sobra entre a barra de titulo e o rodape.
        Controls.Add(raiz);
        Controls.Add(Comum.ShellCadastroFluidoHelper.CriarRodape(Escalar));
        Controls.Add(Comum.ShellCadastroFluidoHelper.CriarBarraDeTitulo(
            this,
            Escalar,
            $"Tolerância de Recebimento  ·  centro {_centro}",
            "Percentual aceito a mais ou a menos no recebimento de cada material",
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
            Font = new Font("Segoe UI", Font.Size),
            Margin = new Padding(0, Escalar(10), Escalar(8), 0),
        };

        _filtro.Width = Escalar(280);
        _filtro.Font = new Font("Segoe UI", Font.Size);
        _filtro.Margin = new Padding(0, Escalar(6), Escalar(16), 0);
        _filtro.TextChanged += (_, _) => AplicarFiltro();

        // Atalho para o trabalho que esta tela realmente cria: como material sem tolerancia BLOQUEIA o
        // recebimento, a pergunta mais frequente aqui vai ser "o que ainda falta?".
        _soNaoConfigurados.Text = "Só os que faltam";
        _soNaoConfigurados.AutoSize = true;
        _soNaoConfigurados.Font = new Font("Segoe UI", Font.Size);
        _soNaoConfigurados.Margin = new Padding(0, Escalar(10), Escalar(16), 0);
        _soNaoConfigurados.CheckedChanged += (_, _) => AplicarFiltro();

        // O mesmo desenho do botao secundario do cartao de decisao, mas com tamanho proprio: num
        // FlowLayoutPanel o Dock=Top do helper nao vale.
        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_atualizar, Escalar, Font, "Atualizar do SAP (F5)", principal: false);
        _atualizar.Dock = DockStyle.None;
        _atualizar.AutoSize = false;
        _atualizar.Width = Escalar(170);
        _atualizar.Margin = new Padding(0, Escalar(2), 0, 0);
        _atualizar.Click += async (_, _) => await SincronizarAsync();

        faixa.Controls.AddRange([rotulo, _filtro, _soNaoConfigurados, _atualizar]);
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
        // LARGURA ABSOLUTA, nao AutoSize: com AutoSize o cartao decide a largura da coluna e a coluna
        // a do cartao, e o texto dos botoes sai cortado na borda.
        conteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Escalar(360)));

        // O cartao da lista leva o filtro em cima e a contagem embaixo, como a busca e o
        // "Exibindo N de N" das telas com Designer.
        TableLayoutPanel listaComFiltro = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
        };
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listaComFiltro.Controls.Add(MontarCabecalho(), 0, 0);
        listaComFiltro.Controls.Add(MontarGrade(), 0, 1);
        listaComFiltro.Controls.Add(MontarLinhaDeSituacao(), 0, 2);

        conteudo.Controls.Add(
            Comum.ShellCadastroFluidoHelper.EmCartao(Escalar, Font, listaComFiltro, "Materiais do centro"), 0, 0);
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
            Coluna("Material", 18),
            Coluna("Descrição", 46),
            Coluna("Tipo", 10),
            Coluna("Tolerância", 16),
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
        // A borda quadrada saiu: o cartao arredondado vem do helper. E quem manda na largura passa a
        // ser a COLUNA, nao o painel — assim rotulos, caixas e botoes tem a largura real para desenhar.
        TableLayoutPanel painel = new()
        {
            ColumnCount = 1,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        // SEM ESTA LINHA a coluna nasce AutoSize e se dimensiona pelo filho mais largo: medido em
        // 26/11/2026, os campos sairam com 359px dentro de um cartao de 316 e vazavam pela borda.
        painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _rotuloMaterial.Text = "Selecione um material na lista";
        _rotuloMaterial.AutoSize = false;
        _rotuloMaterial.Dock = DockStyle.Top;
        _rotuloMaterial.Height = Escalar(38);
        _rotuloMaterial.TextAlign = ContentAlignment.TopLeft;
        _rotuloMaterial.Font = new Font("Segoe UI", Font.Size + 1f, FontStyle.Bold);
        _rotuloMaterial.Margin = new Padding(0, 0, 0, Escalar(8));

        Label rotuloPercentual = Comum.ShellCadastroFluidoHelper.CriarRotuloDoCampo(Escalar, Font, "Tolerância (± %):");
        _percentual.Dock = DockStyle.Top;
        _percentual.Margin = new Padding(0, Escalar(2), 0, Escalar(10));
        _percentual.Font = new Font("Segoe UI", Font.Size);
        _percentual.DecimalPlaces = 2;
        _percentual.Increment = 0.5m;
        _percentual.Minimum = 0m;
        // O mesmo teto do banco (CHECK do incremento 069). Acima de 100 o limite inferior ficaria
        // negativo; deixar digitar para recusar depois seria fazer o usuario descobrir no erro.
        _percentual.Maximum = ToleranciaRecebimentoMaterial.PercentualMaximo;
        _percentual.ValueChanged += (_, _) => AtualizarExemplo();

        // O exemplo nao e enfeite: percentual e abstrato na hora de decidir, e o operador erra a ordem
        // de grandeza com facilidade. Ver o intervalo em numeros torna o engano visivel antes de salvar.
        _exemplo.AutoSize = true;
        _exemplo.Dock = DockStyle.Top;
        _exemplo.ForeColor = Color.FromArgb(0x6B, 0x72, 0x80);
        _exemplo.Font = new Font("Segoe UI", Font.Size);
        _exemplo.Margin = new Padding(0, 0, 0, Escalar(12));

        _ativa.Text = "Tolerância ativa";
        _ativa.AutoSize = false;
        _ativa.Dock = DockStyle.Top;
        _ativa.Height = Escalar(24);
        _ativa.Checked = true;
        _ativa.Font = new Font("Segoe UI", Font.Size);
        _ativa.Margin = new Padding(0, 0, 0, Escalar(12));

        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_salvar, Escalar, Font, "Salvar tolerância", principal: true);
        _salvar.Margin = new Padding(0, 0, 0, Escalar(6));
        _salvar.Click += async (_, _) => await SalvarAsync();

        Comum.ShellCadastroFluidoHelper.EstilizarBotao(_remover, Escalar, Font, "Remover tolerância", principal: false);
        _remover.Click += async (_, _) => await RemoverAsync();

        painel.Controls.AddRange(
        [
            _rotuloMaterial, rotuloPercentual, _percentual, _exemplo, _ativa, _salvar, _remover,
        ]);

        AtualizarExemplo();
        HabilitarDecisao(false);

        Control cartao = Comum.ShellCadastroFluidoHelper.EmCartao(Escalar, Font, painel, "Decisão do material");
        cartao.Margin = new Padding(Escalar(16), 0, 0, 0);
        return cartao;
    }

    /// <summary>
    /// A LINHA DE SITUACAO da tela. Nao confundir com o rodape do sistema, que vem do helper.
    /// </summary>
    private Control MontarLinhaDeSituacao()
    {
        _situacao.Dock = DockStyle.Fill;
        _situacao.AutoSize = false;
        _situacao.Height = Escalar(22);
        _situacao.TextAlign = ContentAlignment.MiddleLeft;
        _situacao.ForeColor = Color.FromArgb(0x47, 0x55, 0x69);
        _situacao.Font = new Font("Segoe UI", Font.Size);
        _situacao.Margin = new Padding(0, Escalar(8), 0, 0);
        return _situacao;
    }

    /// <summary>
    /// Traduz o percentual num intervalo concreto, e diz o que "inativa" significa de verdade.
    /// <para>
    /// A segunda parte importa: inativar NAO libera o material, BLOQUEIA — a Entrada trata inativa como
    /// nao cadastrada. Sem esse texto, "desmarcar ativa" parece o jeito natural de tirar a trava, e faz
    /// exatamente o contrario.
    /// </para>
    /// </summary>
    private void AtualizarExemplo()
    {
        decimal p = _percentual.Value;
        decimal folga = 100m * p / 100m;

        _exemplo.Text = p == 0m
            ? "Com 0%, o recebimento tem de bater exatamente com o pedido."
            : $"Num pedido de 100, aceita de {F(100m - folga)} a {F(100m + folga)}.";
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
        UseWaitCursor = true;
        try
        {
            ResultadoListagemTolerancia resultado = await _controller.ListarAsync(_centro);
            _linhas = resultado.Linhas;
            AplicarFiltro();

            int configurados = _linhas.Count - resultado.NaoConfigurados;
            string resumo = $"{_linhas.Count} material(is) · {configurados} com tolerância · "
                          + $"{resultado.NaoConfigurados} sem tolerância";

            _situacao.Text = resultado.CatalogoDisponivel
                ? resumo
                : $"{resultado.MensagemCatalogo}  Mostrando apenas as tolerâncias já gravadas ({_linhas.Count}).";

            // O numero sozinho nao diz o que esta em jogo. Material sem tolerancia nao e so "pendencia
            // de cadastro": e recebimento que vai parar na balanca.
            if (resultado.NaoConfigurados > 0)
            {
                _situacao.Text += $"  ATENÇÃO: o recebimento dos {resultado.NaoConfigurados} sem "
                                + "tolerância fica bloqueado até serem cadastrados.";
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
        IEnumerable<LinhaToleranciaMaterial> visiveis = _linhas;

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

        _grade.Rows.Clear();
        foreach (LinhaToleranciaMaterial linha in visiveis)
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
                    "Sem tolerância ativa: o recebimento deste material fica bloqueado.";
            }

            if (!linha.PresenteNoCatalogo)
            {
                _grade.Rows[indice].Cells[1].ToolTipText =
                    "Tolerância gravada para um material que o catálogo não listou nesta consulta.";
            }
        }

        MostrarSelecionado();
    }

    private LinhaToleranciaMaterial? LinhaSelecionada()
        => _grade.CurrentRow?.Tag as LinhaToleranciaMaterial;

    private void MostrarSelecionado()
    {
        LinhaToleranciaMaterial? linha = LinhaSelecionada();
        if (linha is null)
        {
            _rotuloMaterial.Text = "Selecione um material na lista";
            HabilitarDecisao(false);
            return;
        }

        _rotuloMaterial.Text = string.IsNullOrWhiteSpace(linha.DescricaoParaExibir)
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
        LinhaToleranciaMaterial? linha = LinhaSelecionada();
        if (linha is null) return;

        ToleranciaRecebimentoMaterial tolerancia = new()
        {
            Centro = _centro,
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
            MessageBox.Show(this, resultado.Mensagem, "Tolerância de recebimento",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RemoverAsync()
    {
        LinhaToleranciaMaterial? linha = LinhaSelecionada();
        if (linha?.Tolerancia is null) return;

        // A confirmacao diz a CONSEQUENCIA, nao so a acao. Remover tolerancia parece afrouxar e faz o
        // oposto: sem linha, o material para de ser recebido.
        DialogResult confirmacao = MessageBox.Show(
            this,
            $"Remover a tolerância do material {linha.Material}?\n\n"
            + "O recebimento deste material fica BLOQUEADO até a tolerância ser cadastrada de novo.",
            "Remover tolerância",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmacao != DialogResult.Yes) return;

        ResultadoOperacao resultado = await _controller.RemoverAsync(_centro, linha.Material);
        _situacao.Text = resultado.Mensagem;
        if (resultado.Sucesso) await CarregarAsync();
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
