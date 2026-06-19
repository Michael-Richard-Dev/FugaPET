using System.Globalization;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Cadastro;

/// <summary>
/// Dialogo filho para editar os CAMPOS de uma etiqueta e o MAPEAMENTO (origem do dado)
/// de cada campo. Autossuficiente: escolhe a etiqueta por combo proprio.
/// UI construida em codigo (sem Designer) para manter o dialogo simples e isolado.
/// </summary>
public sealed class CamposEtiquetaForm : Form
{
    private static readonly string[] TiposDado =
        ["TEXTO", "NUMERO", "DATA", "PESO", "QRCODE", "CODIGO_BARRAS", "BOOLEANO"];
    private static readonly string[] OrigensDado =
        ["SISTEMA", "SAP", "USUARIO", "CALCULADO", "BALANCA", "FIXO"];

    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private readonly EtiquetaController _etiquetaController;
    private readonly CampoEtiquetaController _campoController;
    private readonly MapeamentoCampoEtiquetaController _mapeamentoController;

    private long _etiquetaPreSelecionada;
    private long _idCampoAtual;
    private long _idMapeamentoAtual;
    private IReadOnlyList<CampoEtiquetaCadastro> _campos = [];

    // Controles
    private ComboBox _cmbEtiqueta = null!;
    private DataGridView _grid = null!;
    private TextBox _txtNome = null!;
    private ComboBox _cmbTipoDado = null!;
    private TextBox _txtOrdem = null!;
    private CheckBox _chkObrigatorio = null!;
    private TextBox _txtTamanhoMax = null!;
    private TextBox _txtFormato = null!;
    private TextBox _txtDescricao = null!;
    private ComboBox _cmbSituacao = null!;
    private Button _btnNovoCampo = null!;
    private Button _btnSalvarCampo = null!;
    private Button _btnInativarCampo = null!;

    private ComboBox _cmbOrigem = null!;
    private TextBox _txtExpressao = null!;
    private TextBox _txtValorPadrao = null!;
    private CheckBox _chkObrigImpressao = null!;
    private TextBox _txtObsMapa = null!;
    private Button _btnSalvarMapa = null!;
    private Button _btnRemoverMapa = null!;
    private Label _lblMapaInfo = null!;

    public CamposEtiquetaForm(
        long etiquetaPreSelecionada = 0,
        EtiquetaController? etiquetaController = null,
        CampoEtiquetaController? campoController = null,
        MapeamentoCampoEtiquetaController? mapeamentoController = null)
    {
        _etiquetaPreSelecionada = etiquetaPreSelecionada;
        _etiquetaController = etiquetaController ?? FabricaControladoresCadastro.CriarEtiquetaController();
        _campoController = campoController ?? FabricaControladoresCadastro.CriarCampoEtiquetaController();
        _mapeamentoController = mapeamentoController ?? FabricaControladoresCadastro.CriarMapeamentoCampoEtiquetaController();

        ConstruirUi();

        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) => await CarregarEtiquetasAsync();
        }
    }

    private long EtiquetaSelecionada => TryParseLong(_cmbEtiqueta.SelectedValue);

    // ============================================================
    // Construcao da UI
    // ============================================================

    private void ConstruirUi()
    {
        Text = "Campos e Mapeamento da Etiqueta";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(960, 600);
        Size = new Size(1040, 640);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        // Topo: combo de etiqueta
        Label lblEtiqueta = new() { Text = "Etiqueta:", Location = new Point(16, 18), AutoSize = true };
        _cmbEtiqueta = new ComboBox
        {
            Location = new Point(80, 14),
            Width = 460,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbEtiqueta.SelectedIndexChanged += async (_, _) => await CarregarCamposAsync();

        // Grid de campos (esquerda)
        _grid = new DataGridView
        {
            Location = new Point(16, 52),
            Size = new Size(520, 520),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Codigo", DataPropertyName = "Codigo", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ordem", HeaderText = "Ordem", DataPropertyName = "Ordem", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nome", HeaderText = "Campo", DataPropertyName = "Nome", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tipo", HeaderText = "Tipo", DataPropertyName = "Tipo", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Situacao", HeaderText = "Situacao", DataPropertyName = "Situacao", Width = 80 });
        _grid.SelectionChanged += (_, _) => AoSelecionarCampo();

        // Painel direito: editor de campo
        GroupBox grpCampo = new()
        {
            Text = "Campo",
            Location = new Point(552, 52),
            Size = new Size(456, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _txtNome = AdicionarCampoTexto(grpCampo, "Nome *", 28);
        _cmbTipoDado = AdicionarCombo(grpCampo, "Tipo de dado *", 70, TiposDado);
        _txtOrdem = AdicionarCampoTexto(grpCampo, "Ordem *", 112);
        _txtTamanhoMax = AdicionarCampoTexto(grpCampo, "Tamanho maximo", 154);
        _txtFormato = AdicionarCampoTexto(grpCampo, "Formato de saida", 196);
        _txtDescricao = AdicionarCampoTexto(grpCampo, "Descricao", 238);

        _chkObrigatorio = new CheckBox { Text = "Obrigatorio", Location = new Point(240, 116), AutoSize = true };
        grpCampo.Controls.Add(_chkObrigatorio);

        _cmbSituacao = new ComboBox { Location = new Point(330, 24), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSituacao.Items.AddRange(["Ativo", "Inativo"]);
        _cmbSituacao.SelectedIndex = 0;
        grpCampo.Controls.Add(_cmbSituacao);

        _btnNovoCampo = new Button { Text = "Novo", Location = new Point(16, 268), Width = 90 };
        _btnSalvarCampo = new Button { Text = "Salvar", Location = new Point(116, 268), Width = 100 };
        _btnInativarCampo = new Button { Text = "Inativar", Location = new Point(226, 268), Width = 100 };
        _btnNovoCampo.Click += (_, _) => LimparEditorCampo();
        _btnSalvarCampo.Click += async (_, _) => await SalvarCampoAsync();
        _btnInativarCampo.Click += async (_, _) => await InativarCampoAsync();
        grpCampo.Controls.Add(_btnNovoCampo);
        grpCampo.Controls.Add(_btnSalvarCampo);
        grpCampo.Controls.Add(_btnInativarCampo);

        // Painel direito: mapeamento do campo selecionado
        GroupBox grpMapa = new()
        {
            Text = "Mapeamento do campo (origem do dado)",
            Location = new Point(552, 364),
            Size = new Size(456, 208),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        _lblMapaInfo = new Label { Location = new Point(16, 22), Size = new Size(424, 18), ForeColor = Color.FromArgb(100, 116, 139), Text = "Selecione um campo." };
        grpMapa.Controls.Add(_lblMapaInfo);

        _cmbOrigem = AdicionarCombo(grpMapa, "Origem *", 56, OrigensDado);
        _txtExpressao = AdicionarCampoTexto(grpMapa, "Expressao origem", 98);
        _txtValorPadrao = AdicionarCampoTexto(grpMapa, "Valor padrao", 140);
        _txtObsMapa = new TextBox { Location = new Point(240, 116), Width = 200 };
        Label lblObsMapa = new() { Text = "Observacao", Location = new Point(240, 98), AutoSize = true };
        grpMapa.Controls.Add(lblObsMapa);
        grpMapa.Controls.Add(_txtObsMapa);

        _chkObrigImpressao = new CheckBox { Text = "Obrigatorio para impressao", Location = new Point(240, 142), AutoSize = true };
        grpMapa.Controls.Add(_chkObrigImpressao);

        _btnSalvarMapa = new Button { Text = "Salvar mapeamento", Location = new Point(16, 170), Width = 160 };
        _btnRemoverMapa = new Button { Text = "Remover", Location = new Point(186, 170), Width = 100 };
        _btnSalvarMapa.Click += async (_, _) => await SalvarMapeamentoAsync();
        _btnRemoverMapa.Click += async (_, _) => await RemoverMapeamentoAsync();
        grpMapa.Controls.Add(_btnSalvarMapa);
        grpMapa.Controls.Add(_btnRemoverMapa);

        Button btnFechar = new() { Text = "Fechar", Location = new Point(16, 582), Width = 100, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnFechar.Click += (_, _) => Close();

        Controls.Add(lblEtiqueta);
        Controls.Add(_cmbEtiqueta);
        Controls.Add(_grid);
        Controls.Add(grpCampo);
        Controls.Add(grpMapa);
        Controls.Add(btnFechar);
    }

    private static TextBox AdicionarCampoTexto(Control parent, string rotulo, int y)
    {
        Label lbl = new() { Text = rotulo, Location = new Point(16, y), AutoSize = true };
        TextBox txt = new() { Location = new Point(16, y + 18), Width = 200 };
        parent.Controls.Add(lbl);
        parent.Controls.Add(txt);
        return txt;
    }

    private static ComboBox AdicionarCombo(Control parent, string rotulo, int y, string[] itens)
    {
        Label lbl = new() { Text = rotulo, Location = new Point(16, y), AutoSize = true };
        ComboBox cmb = new() { Location = new Point(16, y + 18), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        cmb.Items.AddRange(itens);
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        parent.Controls.Add(lbl);
        parent.Controls.Add(cmb);
        return cmb;
    }

    // ============================================================
    // Carregamento
    // ============================================================

    private async Task CarregarEtiquetasAsync()
    {
        try
        {
            IReadOnlyList<EtiquetaCadastro> etiquetas = await _etiquetaController.ListarAsync();
            var ativas = etiquetas.Where(e => e.SituacaoEtiqueta).ToList();
            _cmbEtiqueta.DisplayMember = nameof(EtiquetaCadastro.NomeEtiqueta);
            _cmbEtiqueta.ValueMember = nameof(EtiquetaCadastro.CodigoEtiqueta);
            _cmbEtiqueta.DataSource = ativas;

            if (_etiquetaPreSelecionada > 0)
            {
                _cmbEtiqueta.SelectedValue = _etiquetaPreSelecionada;
            }
            _cmbEtiqueta.SelectedIndexChanged -= OnEtiquetaChangedNoop;
            await CarregarCamposAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(await ErroUsuarioHelper.TratarAsync("CAMPOS_ETIQUETA_ERRO", ex, "CamposEtiquetaForm", "Não foi possível carregar as etiquetas. Acione o suporte."), "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnEtiquetaChangedNoop(object? sender, EventArgs e) { }

    private async Task CarregarCamposAsync()
    {
        long idEtiqueta = EtiquetaSelecionada;
        LimparEditorCampo();
        LimparEditorMapeamento();

        if (idEtiqueta <= 0)
        {
            _grid.DataSource = null;
            _campos = [];
            return;
        }

        try
        {
            _campos = await _campoController.ListarPorEtiquetaAsync(idEtiqueta);
            _grid.DataSource = _campos.Select(c => new CampoLinha
            {
                Codigo = c.CodigoCampoEtiqueta,
                Ordem = c.Ordem,
                Nome = c.NomeCampo,
                Tipo = c.TipoDado,
                Situacao = c.SituacaoCampoEtiqueta ? "Ativo" : "Inativo"
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(await ErroUsuarioHelper.TratarAsync("CAMPOS_ETIQUETA_ERRO", ex, "CamposEtiquetaForm", "Não foi possível carregar os campos. Acione o suporte."), "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AoSelecionarCampo()
    {
        if (_grid.CurrentRow?.Cells["Codigo"].Value is not { } valor) return;
        long codigo = TryParseLong(valor);
        CampoEtiquetaCadastro? campo = _campos.FirstOrDefault(c => c.CodigoCampoEtiqueta == codigo);
        if (campo is null) return;

        _idCampoAtual = campo.CodigoCampoEtiqueta;
        _txtNome.Text = campo.NomeCampo;
        SelecionarCombo(_cmbTipoDado, campo.TipoDado);
        _txtOrdem.Text = campo.Ordem.ToString(CultureInfo.InvariantCulture);
        _chkObrigatorio.Checked = campo.Obrigatorio;
        _txtTamanhoMax.Text = campo.TamanhoMaximo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _txtFormato.Text = campo.FormatoSaida;
        _txtDescricao.Text = campo.DescricaoCampoEtiqueta;
        _cmbSituacao.Text = campo.SituacaoCampoEtiqueta ? "Ativo" : "Inativo";

        _ = CarregarMapeamentoAsync(campo.CodigoCampoEtiqueta);
    }

    private async Task CarregarMapeamentoAsync(long idCampo)
    {
        LimparEditorMapeamento();
        try
        {
            MapeamentoCampoEtiquetaCadastro? mapa = await _mapeamentoController.ObterAtivoPorCampoAsync(idCampo);
            if (mapa is null)
            {
                _lblMapaInfo.Text = "Campo sem mapeamento ativo. Defina abaixo.";
                return;
            }

            _idMapeamentoAtual = mapa.CodigoMapeamentoCampoEtiqueta;
            SelecionarCombo(_cmbOrigem, mapa.OrigemDado);
            _txtExpressao.Text = mapa.ExpressaoOrigem;
            _txtValorPadrao.Text = mapa.ValorPadrao;
            _chkObrigImpressao.Checked = mapa.ObrigatorioParaImpressao;
            _txtObsMapa.Text = mapa.Observacao;
            _lblMapaInfo.Text = $"Mapeamento ativo (cod. {mapa.CodigoMapeamentoCampoEtiqueta}).";
        }
        catch (Exception ex)
        {
            _lblMapaInfo.Text = await ErroUsuarioHelper.TratarAsync("MAPEAMENTO_CAMPO_ERRO", ex, "CamposEtiquetaForm", "Não foi possível carregar o mapeamento. Acione o suporte.");
        }
    }

    // ============================================================
    // Acoes — Campo
    // ============================================================

    private async Task SalvarCampoAsync()
    {
        if (!GarantirBanco()) return;

        long idEtiqueta = EtiquetaSelecionada;
        if (idEtiqueta <= 0)
        {
            MessageBox.Show("Selecione uma etiqueta.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CampoEtiquetaCadastro campo = new()
        {
            CodigoCampoEtiqueta = _idCampoAtual,
            CodigoEtiqueta = idEtiqueta,
            NomeCampo = _txtNome.Text.Trim(),
            TipoDado = _cmbTipoDado.Text,
            Ordem = int.TryParse(_txtOrdem.Text.Trim(), out int ordem) ? ordem : 1,
            Obrigatorio = _chkObrigatorio.Checked,
            TamanhoMaximo = int.TryParse(_txtTamanhoMax.Text.Trim(), out int tm) ? tm : null,
            FormatoSaida = _txtFormato.Text.Trim(),
            DescricaoCampoEtiqueta = _txtDescricao.Text.Trim(),
            SituacaoCampoEtiqueta = _cmbSituacao.Text.Equals("Ativo", StringComparison.OrdinalIgnoreCase)
        };

        ResultadoOperacao resultado = _idCampoAtual > 0
            ? await _campoController.AtualizarAsync(campo)
            : await _campoController.InserirAsync(campo);

        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarCamposAsync();
        }
    }

    private async Task InativarCampoAsync()
    {
        if (!GarantirBanco()) return;
        if (_idCampoAtual <= 0)
        {
            MessageBox.Show("Selecione um campo para inativar.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show("Confirma a inativacao do campo?", "Campos da Etiqueta", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        ResultadoOperacao resultado = await _campoController.ExcluirAsync(_idCampoAtual);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso) await CarregarCamposAsync();
    }

    // ============================================================
    // Acoes — Mapeamento
    // ============================================================

    private async Task SalvarMapeamentoAsync()
    {
        if (!GarantirBanco()) return;
        if (_idCampoAtual <= 0)
        {
            MessageBox.Show("Selecione um campo para definir o mapeamento.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MapeamentoCampoEtiquetaCadastro mapa = new()
        {
            CodigoCampoEtiqueta = _idCampoAtual,
            OrigemDado = _cmbOrigem.Text,
            ExpressaoOrigem = _txtExpressao.Text.Trim(),
            ValorPadrao = _txtValorPadrao.Text.Trim(),
            ObrigatorioParaImpressao = _chkObrigImpressao.Checked,
            Observacao = _txtObsMapa.Text.Trim(),
            SituacaoMapeamentoCampoEtiqueta = true
        };

        ResultadoOperacao resultado = await _mapeamentoController.SalvarAsync(mapa);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso) await CarregarMapeamentoAsync(_idCampoAtual);
    }

    private async Task RemoverMapeamentoAsync()
    {
        if (!GarantirBanco()) return;
        if (_idMapeamentoAtual <= 0)
        {
            MessageBox.Show("Nao ha mapeamento ativo para remover neste campo.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ResultadoOperacao resultado = await _mapeamentoController.ExcluirAsync(_idMapeamentoAtual);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso && _idCampoAtual > 0) await CarregarMapeamentoAsync(_idCampoAtual);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private void LimparEditorCampo()
    {
        _idCampoAtual = 0;
        _txtNome.Text = string.Empty;
        if (_cmbTipoDado.Items.Count > 0) _cmbTipoDado.SelectedIndex = 0;
        _txtOrdem.Text = ProximaOrdemSugerida().ToString(CultureInfo.InvariantCulture);
        _chkObrigatorio.Checked = false;
        _txtTamanhoMax.Text = string.Empty;
        _txtFormato.Text = string.Empty;
        _txtDescricao.Text = string.Empty;
        _cmbSituacao.Text = "Ativo";
    }

    private void LimparEditorMapeamento()
    {
        _idMapeamentoAtual = 0;
        if (_cmbOrigem.Items.Count > 0) _cmbOrigem.SelectedIndex = 0;
        _txtExpressao.Text = string.Empty;
        _txtValorPadrao.Text = string.Empty;
        _chkObrigImpressao.Checked = false;
        _txtObsMapa.Text = string.Empty;
        _lblMapaInfo.Text = "Selecione um campo.";
    }

    private int ProximaOrdemSugerida()
        => _campos.Count == 0 ? 1 : _campos.Max(c => c.Ordem) + 1;

    private bool GarantirBanco()
    {
        if (_integracaoBancoHabilitada) return true;
        MessageBox.Show("Integracao com banco desabilitada no momento.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private static void SelecionarCombo(ComboBox combo, string valor)
    {
        int idx = combo.FindStringExact(valor);
        combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1);
    }

    private static long TryParseLong(object? value)
    {
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is string s && long.TryParse(s, out long p)) return p;
        return 0;
    }

    private sealed class CampoLinha
    {
        public long Codigo { get; init; }
        public int Ordem { get; init; }
        public string Nome { get; init; } = string.Empty;
        public string Tipo { get; init; } = string.Empty;
        public string Situacao { get; init; } = string.Empty;
    }
}
