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

        // A barra de titulo do Windows sai e entra a do sistema, como nas demais telas. Feito ANTES
        // de montar o layout porque a faixa da marca passa a ser tambem a barra de titulo.
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(0xF7, 0xF8, 0xFA);
        Comum.IconeJanelaHelper.AplicarIconePadrao(this);

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
            RowCount = 2,
            Padding = new Padding(Escalar(12)),
        };
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(MontarConteudo(), 0, 0);
        raiz.Controls.Add(MontarLinhaDeSituacao(), 0, 1);

        // A ORDEM DE INSERCAO IMPORTA com Dock: o ultimo a entrar fica mais perto da borda. Por isso
        // a raiz (Fill) entra primeiro e as faixas depois — assim o conteudo ocupa o que sobra entre
        // a faixa da marca no topo e o rodape do sistema embaixo, sem nenhuma das duas por cima.
        // As faixas ficam FORA do TableLayoutPanel porque vao de borda a borda, e a raiz tem recuo.
        Controls.Add(raiz);

        Panel rodapeDoSistema = new() { Dock = DockStyle.Bottom, Height = Escalar(38) };
        rodapeDoSistema.Controls.Add(Comum.ShellProcessoPadraoHelper.CriarRodapePadrao());
        Controls.Add(rodapeDoSistema);

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

        // Divisor fino entre o logo e o assunto da tela, como nas demais.
        Panel divisor = new()
        {
            Dock = DockStyle.Left,
            // Escalar(1) e nao 1: o teste da tela exige que todo tamanho passe pela escala, e num
            // monitor de DPI alto um fio de 1px some.
            Width = Escalar(1),
            BackColor = Color.FromArgb(0xE8, 0xAE, 0x8C),
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        // Icone da tela e titulo com subtitulo, como nas demais telas de cadastro. O icone reusa o
        // mesmo recurso da tela de Etiqueta, que e o assunto desta aqui.
        PictureBox icone = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Left,
            Image = Properties.Resources.etiqueta_24x_white,
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false,
            Width = Escalar(30),
            Margin = new Padding(0),
        };

        Panel bloco = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        Label titulo = new()
        {
            Dock = DockStyle.Top,
            Text = $"Vínculo de Produto e Etiqueta  ·  centro {_centro}",
            ForeColor = Color.White,
            AutoSize = false,
            Height = Escalar(26),
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(Escalar(12), 0, 0, 0),
            Font = new Font(Font.FontFamily, Font.Size + 3f, FontStyle.Bold),
        };

        Label subtitulo = new()
        {
            Dock = DockStyle.Top,
            Text = "Define a etiqueta e o processo de pesagem de cada produto acabado",
            ForeColor = Color.White,
            AutoSize = false,
            Height = Escalar(18),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(Escalar(12), 0, 0, 0),
            Font = new Font(Font.FontFamily, Font.Size - 0.5f),
        };

        bloco.Controls.Add(subtitulo);
        bloco.Controls.Add(titulo);

        // OS TRES BOTOES DE JANELA, porque a barra do Windows saiu. Eles sao REPOSICIONADOS a cada
        // mudanca de largura, nunca cravados: foi posicao cravada para 1366 que deixou a tela Campos
        // da Etiqueta sem saida — os botoes dela terminavam a 1100px fora do visivel.
        Label minimizar = CriarBotaoDeJanela("–");
        minimizar.Click += (_, _) => WindowState = FormWindowState.Minimized;

        Label maximizar = CriarBotaoDeJanela("□");
        maximizar.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

        Label fechar = CriarBotaoDeJanela("×");
        fechar.Click += (_, _) => Close();

        void Reposicionar()
        {
            // Sem SetBounds: o teste Tela_NaoUsaGeometriaCravadaEmPixel proibe a chamada nesta tela,
            // e com razao — e por ali que entra geometria cravada. Aqui tudo sai de variaveis.
            int largura = Escalar(46);
            int altura = faixa.ClientSize.Height;

            foreach (Label botao in new[] { minimizar, maximizar, fechar })
            {
                botao.Width = largura;
                botao.Height = altura;
                botao.Top = 0;
            }

            fechar.Left = faixa.ClientSize.Width - largura;
            maximizar.Left = fechar.Left - largura;
            minimizar.Left = maximizar.Left - largura;
            // O titulo para antes dos botoes, para o texto nunca correr por baixo deles.
            bloco.Padding = new Padding(0, 0, (largura * 3) + Escalar(8), 0);
        }

        faixa.SizeChanged += (_, _) => Reposicionar();

        faixa.Controls.Add(bloco);
        faixa.Controls.Add(icone);
        faixa.Controls.Add(divisor);
        faixa.Controls.Add(logo);
        faixa.Controls.Add(minimizar);
        faixa.Controls.Add(maximizar);
        faixa.Controls.Add(fechar);
        Reposicionar();

        // Realce ao passar o mouse e arraste pela faixa, os mesmos das outras telas.
        Comum.BarraDeTituloHelper.AplicarRealceDosBotoes(faixa, minimizar, maximizar, fechar);
        Comum.BarraDeTituloHelper.PermitirArrastarPeloCabecalho(this, faixa, bloco, titulo, subtitulo, logo);
        return faixa;
    }

    /// <summary>
    /// Um botao de janela da faixa: so a aparencia. A POSICAO e o tamanho saem do
    /// <c>Reposicionar</c> da faixa, que roda a cada mudanca de largura.
    /// </summary>
    private Label CriarBotaoDeJanela(string glifo) => new()
    {
        BackColor = Color.Transparent,
        Cursor = Cursors.Hand,
        Font = new Font(Font.FontFamily, Font.Size + 3f),
        ForeColor = Color.White,
        Text = glifo,
        TextAlign = ContentAlignment.MiddleCenter,
    };

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
        EstilizarBotao(_atualizar, "Atualizar do SAP (F5)", principal: false);
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
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listaComFiltro.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listaComFiltro.Controls.Add(MontarCabecalho(), 0, 0);
        listaComFiltro.Controls.Add(MontarGrade(), 0, 1);

        conteudo.Controls.Add(EmCartao(listaComFiltro, "Produtos do centro"), 0, 0);
        conteudo.Controls.Add(MontarPainelDecisao(), 1, 0);
        return conteudo;
    }

    /// <summary>
    /// Envolve um conteudo no cartao branco arredondado das telas de cadastro, com um titulo em
    /// cima. E o mesmo <see cref="RoundedPanel"/> que Setor, Cargo e companhia usam — aqui so vem
    /// montado em codigo, porque esta tela nao tem Designer.
    /// </summary>
    private Control EmCartao(Control conteudo, string titulo)
    {
        RoundedPanel cartao = new()
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(Escalar(14), Escalar(12), Escalar(14), Escalar(14)),
            ShadowBlur = 0,
            ShadowOffsetY = 0,
        };

        Label rotulo = new()
        {
            Dock = DockStyle.Top,
            Text = titulo,
            AutoSize = false,
            Height = Escalar(26),
            ForeColor = Color.FromArgb(0x0F, 0x17, 0x2A),
            Font = new Font("Segoe UI", Font.Size + 1.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        conteudo.Dock = DockStyle.Fill;
        cartao.Controls.Add(conteudo);
        cartao.Controls.Add(rotulo);
        return cartao;
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

        // APARENCIA no padrao das listas de cadastro: fundo branco, cabecalho claro com texto
        // escuro, linha selecionada em laranja claro e separador fino. Sem isso a grade entrega o
        // cinza padrao do WinForms, que e o que fazia esta tela parecer de outro sistema.
        _grade.BackgroundColor = Color.White;
        _grade.BorderStyle = BorderStyle.None;
        _grade.EnableHeadersVisualStyles = false;
        _grade.GridColor = Color.FromArgb(0xE2, 0xE8, 0xF0);
        _grade.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grade.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grade.ColumnHeadersHeight = Escalar(34);
        _grade.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        _grade.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
            ForeColor = Color.FromArgb(0x0F, 0x17, 0x2A),
            Font = new Font("Segoe UI", Font.Size, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(Escalar(8), 0, 0, 0),
            SelectionBackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
            SelectionForeColor = Color.FromArgb(0x0F, 0x17, 0x2A),
        };
        _grade.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0x33, 0x3D, 0x4D),
            Font = new Font("Segoe UI", Font.Size),
            Padding = new Padding(Escalar(8), 0, 0, 0),
            SelectionBackColor = Color.FromArgb(0xFD, 0xE4, 0xD3),
            SelectionForeColor = Color.FromArgb(0x0F, 0x17, 0x2A),
        };
        _grade.RowTemplate.Height = Escalar(30);

        _grade.SelectionChanged += (_, _) => MostrarSelecionado();
        return _grade;
    }

    private static DataGridViewTextBoxColumn Coluna(string titulo, int peso) => new()
    {
        HeaderText = titulo,
        FillWeight = peso,
        SortMode = DataGridViewColumnSortMode.NotSortable,
    };

    /// <summary>
    /// Aparencia dos botoes de acao, nas cores MEDIDAS nas telas de cadastro em 26/11/2026:
    /// gravar = #C84E0A com texto branco; destrutivo = branco, borda #CBD5E1 e texto #C84E0A.
    /// </summary>
    private void EstilizarBotao(Button botao, string texto, bool principal)
    {
        botao.Text = texto;
        botao.Dock = DockStyle.Top;
        botao.Height = Escalar(34);
        botao.FlatStyle = FlatStyle.Flat;
        botao.Cursor = Cursors.Hand;
        botao.Font = new Font("Segoe UI", Font.Size, FontStyle.Bold);
        botao.UseVisualStyleBackColor = false;

        if (principal)
        {
            botao.BackColor = Color.FromArgb(0xC8, 0x4E, 0x0A);
            botao.ForeColor = Color.White;
            botao.FlatAppearance.BorderColor = Color.FromArgb(0xC8, 0x4E, 0x0A);
        }
        else
        {
            botao.BackColor = Color.White;
            botao.ForeColor = Color.FromArgb(0xC8, 0x4E, 0x0A);
            botao.FlatAppearance.BorderColor = Color.FromArgb(0xCB, 0xD5, 0xE1);
        }
    }

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
        // Cinza, não o laranja de alerta: o texto passou a DESCREVER o processo, não a avisar de uma
        // limitação. Manter a cor de alarme faria uma escolha normal parecer arriscada.
        _avisoProcesso.ForeColor = Color.FromArgb(0x6B, 0x72, 0x80);
        _avisoProcesso.Margin = new Padding(0, 0, 0, Escalar(12));

        // OS DOIS BOTOES no padrao MEDIDO das telas de cadastro: o de gravar e laranja solido com
        // texto branco, e o destrutivo e branco com borda cinza e texto laranja. Era o visual padrao
        // do Windows que fazia estes dois parecerem de outro sistema.
        EstilizarBotao(_salvar, "Salvar vínculo", principal: true);
        _salvar.Margin = new Padding(0, 0, 0, Escalar(6));
        _salvar.Click += async (_, _) => await SalvarAsync();

        EstilizarBotao(_remover, "Remover vínculo", principal: false);
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

        Control cartao = EmCartao(painel, "Decisão do produto");
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
        _situacao.AutoSize = true;
        _situacao.Margin = new Padding(0, Escalar(4), 0, 0);
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
