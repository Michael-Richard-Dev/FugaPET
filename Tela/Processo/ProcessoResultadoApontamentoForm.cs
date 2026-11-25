using System.Globalization;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// Tela real de Resultado do Apontamento para operações intermediárias. Não usa SAP, balança, consumo ou impressão.
/// </summary>
public partial class ProcessoResultadoApontamentoForm : Form
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string MensagemNenhumResultadoAInformar =
        "Nenhum resultado a informar para esta operação.";

    private readonly ContextoApontamentoProcesso _contexto;
    private readonly ResultadoApontamentoController _controller;
    private readonly List<ResultadoApontamentoItem> _itens = new();
    private ResultadoPersistidoApontamento? _resultadoPersistido;
    private bool _gravacaoEmAndamento;
    private bool _definicoesCarregadasComPerfilResolvido;
    private bool ResultadoPersistidoAtivo => _resultadoPersistido is not null;

    internal ResultadoExecucaoProcesso ResultadoExecucaoApontamento { get; private set; }
        = ResultadoExecucaoProcesso.NaoConcluido;

    public ProcessoResultadoApontamentoForm(ContextoApontamentoProcesso contexto)
        : this(contexto, new ResultadoApontamentoController())
    {
    }

    internal ProcessoResultadoApontamentoForm(
        ContextoApontamentoProcesso contexto,
        ResultadoApontamentoController controller)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        InitializeComponent();
        IconeJanelaHelper.AplicarIconePadrao(this);
        ConfigurarEventos();
        PreencherContexto();
        ConfigurarGridResultados();


    }

    private void ConfigurarEventos()
    {
        Load += async (_, _) => await CarregarDefinicoesAsync();
        KeyDown += ProcessoResultadoApontamentoForm_KeyDown;
        FormClosing += ProcessoResultadoApontamentoForm_FormClosing;
        closeWindowLabel.Click += (_, _) => FecharSePermitido();

        // Realce no botao de fechar e arraste pelo cabecalho — ver Comum/BarraDeTituloHelper.
        // Esta tela tem SO o botao de fechar; nao se inventam aqui os de minimizar e maximizar,
        // que ela nunca teve. O arraste tambem faltava: era a unica das nove sem ele.
        FugaPET_Dev.Tela.Comum.BarraDeTituloHelper.AplicarRealceDosBotoes(headerPanel, closeWindowLabel);
        FugaPET_Dev.Tela.Comum.BarraDeTituloHelper.PermitirArrastarPeloCabecalho(
            this, headerPanel, headerTitleLabel, headerSubtitleLabel);
        fecharButton.Click += (_, _) => FecharSePermitido();
        gravarResultadoButton.Click += async (_, _) =>
        {
            if (ResultadoPersistidoAtivo)
            {
                FinalizarResultadoPersistido();
                return;
            }

            await GravarResultadoAsync();
        };
        resultadosGridView.CellValidating += ResultadosGridView_CellValidating;
        resultadosGridView.CellEndEdit += ResultadosGridView_CellEndEdit;
        resultadosGridView.DataError += (_, e) => e.ThrowException = false;
    }

    private void PreencherContexto()
    {
        headerSubtitleLabel.Text = MontarSubtitulo(_contexto);
        opValueLabel.Text = TextoOuTraco(_contexto.NumeroOrdem);
        itemValueLabel.Text = TextoOuTraco(_contexto.ItemOrdem);
        produtoValueLabel.Text = TextoOuTraco(_contexto.Produto);
        sequenciaValueLabel.Text = TextoOuTraco(_contexto.Sequencia);
        operacaoValueLabel.Text = TextoOuTraco(_contexto.Operacao);
        descricaoOperacaoValueLabel.Text = TextoOuTraco(_contexto.DescricaoOperacao);
        centroTrabalhoValueLabel.Text = TextoOuTraco(_contexto.CentroTrabalho);
        usuarioValueLabel.Text = TextoOuTraco(_contexto.Usuario);
        estacaoValueLabel.Text = TextoOuTraco(_contexto.Estacao);
        inicioValueLabel.Text = _contexto.IniciadoEm == default
            ? "-"
            : _contexto.IniciadoEm.ToString("dd/MM/yyyy HH:mm:ss", CulturaPtBr);
        statusValueLabel.Text = "AGUARDANDO RESULTADO";
        atalhoValueLabel.Text = "F10 gravar · Esc fechar";
        instrucaoLabel.Text = "Digite o resultado da operação e pressione F10 para gravar localmente.";
        string banco = _controller.ObterIdentificacaoBanco();
        statusLabel.Text = $"Usuário: {TextoOuTraco(_contexto.Usuario)} | Terminal: {TextoOuTraco(_contexto.Estacao)} | Banco: {TextoOuTraco(banco)}";
    }

    private void ConfigurarGridResultados()
    {
        resultadosGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
        resultadosGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
        resultadosGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        resultadosGridView.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        resultadosGridView.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
        resultadosGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(254, 226, 226);
        resultadosGridView.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        resultadosGridView.GridColor = Color.FromArgb(241, 245, 249);
        colReferencia.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colResultado.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colTipo.ReadOnly = true;
        colMedida.ReadOnly = true;
        colReferencia.ReadOnly = true;
        colResultado.ReadOnly = false;
    }

    private async Task CarregarDefinicoesAsync()
    {
        try
        {
            IReadOnlyList<ResultadoApontamentoItem> definicoes =
                await _controller.ListarDefinicoesAsync(_contexto, CancellationToken.None);

            ReiniciarEstadoCorrecao();
            _itens.Clear();
            _itens.AddRange(definicoes.OrderBy(item => item.OrdemExibicao));
            _definicoesCarregadasComPerfilResolvido = _contexto.CodigoPerfilResultado is long;
            PreencherGrid();

            ResultadoApontamentoPersistidoRecovery recovery =
                await _controller.ObterResultadoPersistidoDoApontamentoAsync(
                    _contexto,
                    _itens,
                    CancellationToken.None);

            if (recovery.Estado == ResultadoApontamentoPersistidoRecoveryEstado.Recuperado
                && recovery.Resultado is not null)
            {
                AplicarResultadoPersistido(recovery.Resultado);
                return;
            }

            if (recovery.Estado == ResultadoApontamentoPersistidoRecoveryEstado.Falha)
            {
                _resultadoPersistido = null;
                gravarResultadoButton.Enabled = false;
                colResultado.ReadOnly = true;
                resultadosGridView.ReadOnly = true;
                AtualizarVisualBotaoGravar();
                ResultadoExecucaoApontamento = ResultadoExecucaoProcesso.NaoConcluido;
                statusValueLabel.Text = "RECOVERY BLOQUEADO";
                statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
                atalhoValueLabel.Text = "Esc fechar";
                instrucaoLabel.Text = recovery.Mensagem;
                statusLabel.Text = recovery.Mensagem;
                return;
            }

            AtualizarEstadoDefinicoes();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[ResultadoApontamento] Falha ao carregar definições: {ex.GetType().Name}");
            _itens.Clear();
            _definicoesCarregadasComPerfilResolvido = false;
            resultadosGridView.Rows.Clear();
            gravarResultadoButton.Enabled = false;
            AtualizarVisualBotaoGravar();
            ResultadoExecucaoApontamento = ResultadoExecucaoProcesso.NaoConcluido;
            statusValueLabel.Text = "ERRO AO CARREGAR";
            statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
            atalhoValueLabel.Text = "Esc fechar";
            instrucaoLabel.Text = "Não foi possível carregar os itens de resultado desta operação.";
            statusLabel.Text = "Não foi possível carregar os itens de resultado desta operação.";
            MessageBox.Show(
                "Não foi possível carregar os itens de resultado desta operação.",
                "Resultado do Apontamento",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void PreencherGrid()
    {
        resultadosGridView.Rows.Clear();
        foreach (ResultadoApontamentoItem item in _itens.OrderBy(i => i.OrdemExibicao))
        {
            int indice = resultadosGridView.Rows.Add(
                item.Tipo,
                item.Medida,
                FormatarDecimal(item.Referencia),
                item.Resultado is null ? string.Empty : FormatarDecimal(item.Resultado.Value));
            resultadosGridView.Rows[indice].Tag = item;
        }
    }

    private void AtualizarEstadoDefinicoes()
    {
        _resultadoPersistido = null;
        resultadosGridView.ReadOnly = false;
        colResultado.ReadOnly = false;
        gravarResultadoButton.Text = "GRAVAR RESULTADO  F10";
        bool possuiDefinicoes = _itens.Count > 0;
        bool permiteHeaderOnly = !possuiDefinicoes && _definicoesCarregadasComPerfilResolvido;
        gravarResultadoButton.Enabled = possuiDefinicoes || permiteHeaderOnly;
        AtualizarVisualBotaoGravar();
        fecharButton.Enabled = true;
        if (permiteHeaderOnly)
        {
            statusLabel.Text = MensagemNenhumResultadoAInformar;
            statusValueLabel.Text = "SEM RESULTADO";
            statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
            atalhoValueLabel.Text = "F10 gravar · Esc fechar";
            instrucaoLabel.Text = MensagemNenhumResultadoAInformar;
            return;
        }

        if (!possuiDefinicoes)
        {
            statusLabel.Text = "Nenhum item de resultado está configurado para esta operação.";
            statusValueLabel.Text = "SEM DEFINIÇÃO";
            statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
            atalhoValueLabel.Text = "Esc fechar";
            instrucaoLabel.Text = "Nenhum item de resultado está configurado para esta operação.";
            return;
        }

        statusValueLabel.Text = "AGUARDANDO RESULTADO";
        statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
        atalhoValueLabel.Text = "F10 gravar · Esc fechar";
        instrucaoLabel.Text = "Digite o resultado da operação e pressione F10 para gravar localmente.";
        FocarPrimeiroResultado();
    }

    private void AplicarResultadoPersistido(ResultadoPersistidoApontamento resultado)
    {
        _resultadoPersistido = resultado;
        _itens.Clear();
        _itens.AddRange(resultado.Itens.OrderBy(item => item.OrdemExibicao));
        PreencherGrid();

        // 072: a grade continua EDITÁVEL. Travá-la aqui era o que fazia o primeiro valor gravado
        // virar definitivo — o operador digitava outro e nada acontecia. Agora ele digita por cima,
        // a tela confirma uma vez que é correção, e o F10 grava um registro novo que passa a valer.
        colResultado.ReadOnly = false;
        resultadosGridView.ReadOnly = false;
        gravarResultadoButton.Text = "FINALIZAR";
        gravarResultadoButton.Enabled = true;
        fecharButton.Enabled = true;
        AtualizarVisualBotaoGravar();
        statusValueLabel.Text = "RESULTADO JÁ REGISTRADO";
        statusValueLabel.ForeColor = Color.FromArgb(34, 166, 82);
        atalhoValueLabel.Text = "Finalizar · Esc fechar";
        instrucaoLabel.Text = "Resultado já registrado localmente; finalize para reconciliar o apontamento sem nova gravação.";
        statusLabel.Text = "Resultado já registrado localmente. Use CORRIGIR RESULTADO para informar outro valor.";
    }

    private void FocarPrimeiroResultado()
    {
        if (resultadosGridView.Rows.Count == 0)
        {
            return;
        }

        BeginInvoke(() =>
        {
            resultadosGridView.CurrentCell = resultadosGridView.Rows[0].Cells[colResultado.Index];
            resultadosGridView.BeginEdit(true);
        });
    }

    private async Task GravarResultadoAsync()
    {
        if (ResultadoPersistidoAtivo)
        {
            statusValueLabel.Text = "RESULTADO JÁ REGISTRADO";
            statusLabel.Text = "Use FINALIZAR para reconciliar o resultado persistido sem nova gravação.";
            return;
        }

        if (_gravacaoEmAndamento)
        {
            return;
        }

        if (!AtualizarItensDoGrid())
        {
            return;
        }

        try
        {
            DefinirOperacaoEmAndamento(true);
            ResultadoOperacao resultado = await _controller.RegistrarAsync(_contexto, _itens, CancellationToken.None);
            if (!resultado.Sucesso)
            {
                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, "Resultado do Apontamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultadoExecucaoApontamento = new ResultadoExecucaoProcesso(
                ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente,
                resultado.IdGerado,
                resultado.Mensagem,
                indicadorConfirmadoSap: false);
            statusValueLabel.Text = "RESULTADO GRAVADO";
            statusValueLabel.ForeColor = Color.FromArgb(34, 166, 82);
            statusLabel.Text = resultado.Mensagem;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ResultadoApontamento] Falha controlada ao gravar resultado: {ex.GetType().Name}");
            ResultadoExecucaoApontamento = ResultadoExecucaoProcesso.NaoConcluido;
            DialogResult = DialogResult.None;
            const string mensagem = "Não foi possível gravar o resultado da operação. O apontamento permanece em andamento e pode ser retomado.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Resultado do Apontamento", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            DefinirOperacaoEmAndamento(false);
        }
    }

    private bool AtualizarItensDoGrid()
    {
        resultadosGridView.EndEdit();

        foreach (DataGridViewRow row in resultadosGridView.Rows)
        {
            if (row.Tag is not ResultadoApontamentoItem item)
            {
                continue;
            }

            string texto = Convert.ToString(row.Cells[colResultado.Index].Value, CulturaPtBr) ?? string.Empty;
            if (!TentarConverterResultadoItem(item, texto, out decimal? valor))
            {
                resultadosGridView.CurrentCell = row.Cells[colResultado.Index];
                statusLabel.Text = "Informe um resultado numérico válido no padrão pt-BR.";
                MessageBox.Show(
                    "Informe um resultado numérico válido no padrão pt-BR.",
                    "Resultado do Apontamento",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            item.Resultado = valor;
            row.Cells[colResultado.Index].Value = valor is null ? string.Empty : FormatarDecimal(valor.Value);
        }

        string? erro = Servicos.Processo.ResultadoApontamentoServico.ValidarItens(
            _itens,
            _definicoesCarregadasComPerfilResolvido);
        if (erro is not null)
        {
            statusLabel.Text = erro;
            MessageBox.Show(erro, "Resultado do Apontamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void ResultadosGridView_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.ColumnIndex != colResultado.Index || e.RowIndex < 0)
        {
            return;
        }

        ResultadoApontamentoItem? item = resultadosGridView.Rows[e.RowIndex].Tag as ResultadoApontamentoItem;
        if (item is not null && !TentarConverterResultadoItem(item, Convert.ToString(e.FormattedValue, CulturaPtBr), out _))
        {
            e.Cancel = true;
            resultadosGridView.Rows[e.RowIndex].ErrorText = "Informe um número válido no padrão pt-BR.";
            statusLabel.Text = "Resultado inválido. Use números no padrão pt-BR, por exemplo 5,5.";
            return;
        }

        // 072: editar um resultado JÁ REGISTRADO é uma correção, e o operador precisa saber disso
        // antes de seguir. A pergunta vem aqui, na validação, porque é o último ponto em que dá
        // para recusar a mudança e deixar a célula como estava.
        if (!ConfirmarCorrecaoDoResultadoPersistido())
        {
            e.Cancel = true;
            resultadosGridView.CancelEdit();
        }
    }

    private void ResultadosGridView_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            resultadosGridView.Rows[e.RowIndex].ErrorText = string.Empty;
        }
    }

    private void ProcessoResultadoApontamentoForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F10)
        {
            e.SuppressKeyPress = true;
            if (ResultadoPersistidoAtivo)
            {
                statusValueLabel.Text = "RESULTADO JÁ REGISTRADO";
                statusLabel.Text = "Use FINALIZAR para reconciliar o resultado persistido sem nova gravação.";
                return;
            }

            if ((!_definicoesCarregadasComPerfilResolvido && _itens.Count == 0) || !gravarResultadoButton.Enabled || _gravacaoEmAndamento)
            {
                statusValueLabel.Text = _itens.Count == 0 ? "SEM DEFINIÇÃO" : statusValueLabel.Text;
                statusLabel.Text = _itens.Count == 0
                    ? "Nenhum item de resultado está configurado para esta operação."
                    : statusLabel.Text;
                return;
            }

            _ = GravarResultadoAsync();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            FecharSePermitido();
        }
    }

    private void ProcessoResultadoApontamentoForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_gravacaoEmAndamento)
        {
            e.Cancel = true;
            statusLabel.Text = "Aguarde a gravação do resultado antes de fechar.";
        }
    }

    private void FecharSePermitido()
    {
        if (_gravacaoEmAndamento)
        {
            statusLabel.Text = "Aguarde a gravação do resultado antes de fechar.";
            return;
        }

        Close();
    }

    private void FinalizarResultadoPersistido()
    {
        if (_resultadoPersistido is null || _gravacaoEmAndamento)
        {
            return;
        }

        ResultadoExecucaoApontamento = new ResultadoExecucaoProcesso(
            ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente,
            _resultadoPersistido.CodigoResultado,
            string.IsNullOrWhiteSpace(_resultadoPersistido.MensagemResumo)
                ? "Resultado já registrado localmente."
                : _resultadoPersistido.MensagemResumo,
            indicadorConfirmadoSap: false);
        statusValueLabel.Text = "RESULTADO RECONCILIADO";
        statusValueLabel.ForeColor = Color.FromArgb(34, 166, 82);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void DefinirOperacaoEmAndamento(bool emAndamento)
    {
        _gravacaoEmAndamento = emAndamento;
        bool podeGravar = ResultadoPersistidoAtivo
            || _itens.Count > 0
            || (_itens.Count == 0 && _definicoesCarregadasComPerfilResolvido);
        gravarResultadoButton.Enabled = !emAndamento && podeGravar;
        fecharButton.Enabled = !emAndamento;
        resultadosGridView.Enabled = !emAndamento;
        AtualizarVisualBotaoGravar();
    }


    private void AtualizarVisualBotaoGravar()
    {
        if (gravarResultadoButton.Enabled)
        {
            gravarResultadoButton.BackColor = CorAcento;
            gravarResultadoButton.ForeColor = Color.White;
            return;
        }

        gravarResultadoButton.BackColor = Color.FromArgb(229, 231, 235);
        gravarResultadoButton.ForeColor = Color.FromArgb(107, 114, 128);
    }
    internal static bool TentarConverterResultadoItem(ResultadoApontamentoItem item, string? texto, out decimal? resultado)
    {
        resultado = null;
        if (string.IsNullOrWhiteSpace(texto) && !item.Obrigatorio)
        {
            return true;
        }

        if (!TentarConverterResultado(texto, out decimal valor))
        {
            return false;
        }

        resultado = valor;
        return true;
    }

    internal static bool TentarConverterResultado(string? texto, out decimal resultado)
        => decimal.TryParse(
               texto?.Trim(),
               NumberStyles.Number,
               CulturaPtBr,
               out resultado)
           && resultado >= 0m;

    internal static string FormatarDecimal(decimal valor)
        => valor.ToString("0.###", CulturaPtBr);

    internal static string MontarSubtitulo(ContextoApontamentoProcesso contexto)
        => $"OP {contexto.NumeroOrdem} | Operação {contexto.Operacao} - {contexto.DescricaoOperacao}";

    private static string TextoOuTraco(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();
}









