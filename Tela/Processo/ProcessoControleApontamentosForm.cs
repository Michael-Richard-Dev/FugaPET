using System.Runtime.InteropServices;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// Controle de Apontamentos: lê o código impresso na OP, identifica OP/operação/evento, valida e
/// registra início/término, direcionando para a tela operacional configurada.
///
/// A View NÃO consulta SAP nem PostgreSQL: tudo passa pelo <see cref="ProcessoControleApontamentosController"/>.
/// Usuário e estação são automáticos (sessão + terminal) — nunca digitados.
/// </summary>
public partial class ProcessoControleApontamentosForm : Form
{
    internal const string MensagemSemSessao =
        "Não há sessão de usuário autenticada. Faça login novamente para registrar apontamentos.";

    private readonly ProcessoControleApontamentosController _controller;

    private string _usuarioSessao = string.Empty;
    private long? _idUsuarioSessao;
    private long? _idSetorSessao;
    private string _estacao = string.Empty;

    // Bloqueia nova leitura enquanto a anterior está sendo processada (leitor dispara em rajada).
    private bool _processandoLeitura;
    private OrdemProducaoSap? _ordemAtual;

    // Rodapé padrão (relógio + células de identificação), igual às demais telas de Processo.
    private Label? _footerHoraLabel;
    private Label? _footerDataLabel;
    private System.Windows.Forms.Timer? _footerClockTimer;

    public ProcessoControleApontamentosForm()
        : this(new ProcessoControleApontamentosController())
    {
    }

    internal ProcessoControleApontamentosForm(ProcessoControleApontamentosController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        InitializeComponent();
        IconeJanelaHelper.AplicarIconePadrao(this);
        ConfigurarCamposContexto();
        ConfigurarGridOperacoes();
        ConfigurarEventos();
        ResolverUsuarioEEstacao();
        ConfigurarRodape();
        AtualizarStatusSap();
    }

    // ---------- Usuário e estação: automáticos ----------

    /// <summary>
    /// Usuário vem da sessão autenticada; estação vem do contexto do terminal (hostname/cadastro),
    /// com Environment.MachineName apenas como fallback. Nada é digitado pelo operador.
    /// </summary>
    private void ResolverUsuarioEEstacao()
    {
        SessaoUsuarioAplicacao? sessao = EstadoSessaoUsuarioAtual.SessaoAtual;
        if (sessao is not null)
        {
            _idUsuarioSessao = sessao.IdUsuario;
            _idSetorSessao = sessao.IdSetorPadrao;
            _usuarioSessao = !string.IsNullOrWhiteSpace(sessao.Login) ? sessao.Login : sessao.Nome;
        }

        ContextoTerminalLocal? terminal = null;
        try
        {
            terminal = EstadoTerminalLocalAtual.ObterContextoAtualizado();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[Apontamento] Falha ao resolver o terminal: {ex.GetType().Name}. Usando fallback.");
        }

        _estacao = PrimeiroPreenchido(
            terminal?.NomeTerminal,
            terminal?.MaquinaWindows,
            Environment.MachineName);

        usuarioValueLabel.Text = string.IsNullOrWhiteSpace(_usuarioSessao) ? "(sem sessão)" : _usuarioSessao;
        estacaoValueLabel.Text = _estacao;

        if (string.IsNullOrWhiteSpace(_usuarioSessao))
        {
            // Sem sessão: a tela abre para consulta, mas nenhuma leitura é processada.
            codigoLeituraTextBox.Enabled = false;
            statusLabel.Text = MensagemSemSessao;
            DefinirInstrucao(MensagemSemSessao);
        }
    }

    private static string PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))?.Trim() ?? string.Empty;

    // ---------- Configuração visual ----------

    private void ConfigurarCamposContexto()
    {
        ConfigurarParCampo(opCaptionLabel, opValueLabel, "OP", 18, 12, 130);
        ConfigurarParCampo(produtoCaptionLabel, produtoValueLabel, "PRODUTO", 168, 12, 150);
        ConfigurarParCampo(itemCaptionLabel, itemValueLabel, "ITEM", 338, 12, 80);
        ConfigurarParCampo(loteCaptionLabel, loteValueLabel, "LOTE", 438, 12, 130);
        ConfigurarParCampo(usuarioCaptionLabel, usuarioValueLabel, "USUÁRIO", 588, 12, 160);
        ConfigurarParCampo(estacaoCaptionLabel, estacaoValueLabel, "ESTAÇÃO", 768, 12, 180);

        // Status compacto em UMA linha horizontal (antes era uma pilha vertical no painel lateral).
        ConfigurarParCampo(operacaoAtualCaptionLabel, operacaoAtualValueLabel, "OPERAÇÃO ATUAL", 18, 34, 280);
        ConfigurarParCampo(proximaOperacaoCaptionLabel, proximaOperacaoValueLabel, "PRÓXIMA OPERAÇÃO", 320, 34, 280);
        ConfigurarParCampo(eventoCaptionLabel, eventoValueLabel, "EVENTO INTERPRETADO", 620, 34, 300);
        ConfigurarParCampo(statusApontamentoCaptionLabel, statusApontamentoValueLabel, "STATUS DO APONTAMENTO", 940, 34, 260);
    }

    private void ConfigurarGridOperacoes()
    {
        operacoesGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
        operacoesGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(75, 85, 99);
        operacoesGridView.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        operacoesGridView.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        operacoesGridView.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        operacoesGridView.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
        operacoesGridView.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        operacoesGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(254, 226, 226);
        operacoesGridView.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
        operacoesGridView.GridColor = Color.FromArgb(241, 245, 249);
    }

    internal const string MensagemAguardeLeitura = "Aguarde a conclusão da leitura antes de sair.";

    private void ConfigurarEventos()
    {
        Load += (_, _) => DevolverFocoParaLeitor();
        ConfigurarCabecalhoJanela();
        codigoLeituraTextBox.KeyDown += CodigoLeituraTextBox_KeyDown;
        KeyDown += ProcessoControleApontamentosForm_KeyDown;
        // Cobre Alt+F4 e o fechamento pelo Windows, não só o X da tela.
        FormClosing += ProcessoControleApontamentosForm_FormClosing;
    }

    // ---------- Barra de título padrão (idêntica às demais telas de Processo) ----------

    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ConfigurarCabecalhoJanela()
    {
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;

        // "Os três riscos" (menu) retorna à tela de Processos: fecha o diálogo, que devolve o controle
        // ao painel (que reexibe Processo de Produção). Mesma proteção de leitura em andamento.
        menuHeaderLabel.Click += CloseWindowLabel_Click;

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => AlternarEstadoJanela();
        closeWindowLabel.Click += CloseWindowLabel_Click;

        ConfigurarHoverBotaoTitulo(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigurarHoverBotaoTitulo(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigurarHoverBotaoTitulo(closeWindowLabel, Color.FromArgb(184, 18, 32));
    }

    private void CustomTitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        // Durante o processamento da leitura a tela não se move (mesma proteção do fechamento).
        if (_processandoLeitura || e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void AlternarEstadoJanela()
        => WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

    private static void ConfigurarHoverBotaoTitulo(Label botao, Color corHover)
    {
        Color corNormal = botao.BackColor;
        botao.MouseEnter += (_, _) => botao.BackColor = corHover;
        botao.MouseLeave += (_, _) => botao.BackColor = corNormal;
    }

    private void CloseWindowLabel_Click(object? sender, EventArgs e)
    {
        if (_processandoLeitura)
        {
            statusLabel.Text = MensagemAguardeLeitura;
            return;
        }

        Close();
    }

    /// <summary>
    /// Enquanto uma leitura está sendo processada, a tela NÃO fecha (X, Alt+F4, Esc ou Windows).
    /// Concluída a leitura, o fechamento volta a ser permitido.
    /// </summary>
    private void ProcessoControleApontamentosForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_processandoLeitura)
        {
            e.Cancel = true;
            statusLabel.Text = MensagemAguardeLeitura;
            MessageBox.Show(
                MensagemAguardeLeitura, "Controle de Apontamentos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _footerClockTimer?.Stop();
        _footerClockTimer?.Dispose();
    }

    private void ProcessoControleApontamentosForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && !_processandoLeitura)
        {
            Close();
        }
    }

    private async void CodigoLeituraTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        // O leitor age como teclado e termina com Enter: consome a tecla para não emitir o "beep".
        e.SuppressKeyPress = true;
        e.Handled = true;
        await ProcessarLeituraAsync(codigoLeituraTextBox.Text);
    }

    // ---------- Fluxo da leitura ----------

    private async Task ProcessarLeituraAsync(string codigoLido)
    {
        // Enquanto uma leitura está em processamento, novas leituras são ignoradas.
        if (_processandoLeitura)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(codigoLido))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_usuarioSessao))
        {
            MessageBox.Show(MensagemSemSessao, "Controle de Apontamentos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _processandoLeitura = true;
        codigoLeituraTextBox.Enabled = false;
        statusLabel.Text = "Processando leitura...";
        try
        {
            // Eco imediato do que foi interpretado (sem SAP), para o operador ver o evento lido.
            CodigoBarrasOperacao interpretado = _controller.Interpretar(codigoLido);
            AtualizarEventoInterpretado(interpretado);

            ResultadoLeituraApontamento resultado = await _controller.ProcessarLeituraAsync(
                codigoLido, _usuarioSessao, _estacao, ConfirmarComOperador, CancellationToken.None);

            await AplicarResultadoAsync(resultado);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[Apontamento] Falha inesperada ao processar leitura: {ex.GetType().Name}");
            statusLabel.Text = "Não foi possível processar a leitura. Acione o suporte.";
            MessageBox.Show(statusLabel.Text, "Controle de Apontamentos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _processandoLeitura = false;
            codigoLeituraTextBox.Enabled = !string.IsNullOrWhiteSpace(_usuarioSessao);
            codigoLeituraTextBox.Clear();
            DevolverFocoParaLeitor();
        }
    }

    /// <summary>Confirmação do operador ANTES de efetivar início/retomada/término (chamada pelo serviço).</summary>
    private bool ConfirmarComOperador(ConfirmacaoApontamento confirmacao)
    {
        (string titulo, string mensagem) = confirmacao.Tipo switch
        {
            TipoConfirmacaoApontamento.Inicio => (
                "Iniciar operação",
                "Deseja iniciar esta operação?\r\n\r\n"
                + $"OP: {confirmacao.Ordem?.NumeroOrdem}\r\n"
                + $"Operação: {confirmacao.Operacao?.Operacao}\r\n"
                + $"Descrição: {DescricaoOuTraco(confirmacao.Operacao?.Descricao)}\r\n"
                + $"Usuário: {_usuarioSessao}\r\n"
                + $"Estação: {_estacao}"),

            TipoConfirmacaoApontamento.Retomada => (
                "Retomar operação",
                "Esta operação já está em andamento. Deseja retomá-la?\r\n\r\n"
                + $"OP: {confirmacao.Ordem?.NumeroOrdem}\r\n"
                + $"Operação: {confirmacao.Operacao?.Operacao}\r\n"
                + $"Descrição: {DescricaoOuTraco(confirmacao.Operacao?.Descricao)}\r\n"
                + $"Iniciada por: {DescricaoOuTraco(confirmacao.Ativo?.UsuarioInicio)}\r\n"
                + $"Estação de início: {DescricaoOuTraco(confirmacao.Ativo?.EstacaoInicio)}\r\n"
                + $"Início original: {FormatarDataHora(confirmacao.Ativo?.IniciadoEm)}\r\n\r\n"
                + "O horário de início original será mantido."),

            _ => (
                "Finalizar operação",
                "Deseja finalizar esta operação?\r\n\r\n"
                + $"OP: {DescricaoOuTraco(confirmacao.Ativo?.NumeroOrdem)}\r\n"
                + $"Operação: {DescricaoOuTraco(confirmacao.Ativo?.Operacao)}\r\n"
                + $"Início: {FormatarDataHora(confirmacao.Ativo?.IniciadoEm)}\r\n"
                + $"Usuário responsável: {DescricaoOuTraco(confirmacao.Ativo?.UsuarioInicio)}")
        };

        return MessageBox.Show(mensagem, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private async Task AplicarResultadoAsync(ResultadoLeituraApontamento resultado)
    {
        // A OP consultada alimenta o contexto; o grid vem do estado PERSISTIDO da OP.
        if (resultado.Ordem is not null)
        {
            _ordemAtual = resultado.Ordem;
            PreencherContextoOrdem(resultado.Ordem);
        }

        PreencherGridOperacoes(resultado);

        statusLabel.Text = resultado.Mensagem;
        AtualizarStatusApontamento(resultado);

        switch (resultado.Cenario)
        {
            case CenarioLeituraApontamento.SucessoInicio when resultado.Contexto is not null:
                DefinirInstrucao(
                    "Operação iniciada. A tela do processo será aberta. "
                    + "Ao concluir, leia o código de TÉRMINO desta operação.");
                await AbrirTelaDestinoAsync(resultado.Contexto, resultado.Codigo!);
                break;

            case CenarioLeituraApontamento.RetomadaDisponivel when resultado.Contexto is not null:
                DefinirInstrucao(
                    "Retomando a operação em andamento. O início original foi preservado. "
                    + "Ao concluir, leia o código de TÉRMINO.");
                await AbrirTelaDestinoAsync(resultado.Contexto, resultado.Codigo!);
                break;

            case CenarioLeituraApontamento.OperacaoJaAguardandoTermino:
                DefinirInstrucao("A atividade já foi concluída. Leia o código de TÉRMINO desta operação.");
                MessageBox.Show(resultado.Mensagem, "Controle de Apontamentos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case CenarioLeituraApontamento.SucessoTermino:
                DefinirInstrucao("Operação concluída. Leia o código de início da próxima operação.");
                MessageBox.Show(resultado.Mensagem, "Controle de Apontamentos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case CenarioLeituraApontamento.ConfirmacaoPendente:
                DefinirInstrucao("Leitura cancelada pelo operador. Nenhum apontamento foi registrado.");
                break;

            case CenarioLeituraApontamento.EstruturaNaoAplicada:
                DefinirInstrucao(
                    ProcessoControleApontamentosServico.MensagemEstruturaNaoAplicada
                    + "\r\n\r\nA leitura e a consulta da OP funcionam; o início operacional está bloqueado.");
                MessageBox.Show(
                    ProcessoControleApontamentosServico.MensagemEstruturaNaoAplicada,
                    "Controle de Apontamentos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioLeituraApontamento.OperacaoAutomatica:
                DefinirInstrucao(
                    "Esta operação é automática no SAP. Leia o código de início de uma operação manual.");
                MessageBox.Show(resultado.Mensagem, "Controle de Apontamentos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case CenarioLeituraApontamento.ContratoRoteiroNaoResolvido:
                DefinirInstrucao(
                    "Não foi possível resolver o roteiro da operação no SAP. O apontamento está bloqueado.");
                MessageBox.Show(resultado.Mensagem, "Controle de Apontamentos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;

            default:
                DefinirInstrucao(resultado.Mensagem);
                MessageBox.Show(resultado.Mensagem, "Controle de Apontamentos",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
        }
    }

    /// <summary>
    /// Abre a tela operacional configurada. O destino vem SEMPRE da configuração (TipoProcesso), nunca da
    /// descrição da operação. Ao voltar, o apontamento só avança para AGUARDANDO_FINALIZACAO se a atividade
    /// tiver sido concluída — fechar a tela não conclui nada. Task (não async void) + tratamento seguro.
    /// </summary>
    private async Task AbrirTelaDestinoAsync(ContextoApontamentoProcesso contexto, CodigoBarrasOperacao codigo)
    {
        ResultadoExecucaoProcesso execucao = ResultadoExecucaoProcesso.NaoConcluido;

        try
        {
            execucao = contexto.TipoProcesso switch
            {
                TipoProcessoOperacao.ConsumoMateriaPrima => AbrirConsumo(contexto, ModoConsumoMaterial.MateriaPrima),
                TipoProcessoOperacao.ConsumoQuimicos => AbrirConsumo(contexto, ModoConsumoMaterial.Quimico),
                TipoProcessoOperacao.ConsumoSemiAcabado => AbrirConsumoSemiAcabado(contexto),
                TipoProcessoOperacao.ResultadoApontamento => AbrirResultadoApontamento(contexto),
                _ => AvisarDestinoNaoConectado(contexto)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[Apontamento] Falha ao abrir a tela de destino: {ex.GetType().Name}");
            MessageBox.Show(
                "Não foi possível abrir a tela do processo. O apontamento continua EM ANDAMENTO e pode ser retomado.",
                "Controle de Apontamentos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        bool liberouTermino;
        try
        {
            liberouTermino = await _controller.RegistrarConclusaoOperacionalAsync(
                contexto.CodigoApontamento, execucao, _usuarioSessao, _estacao, codigo, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[Apontamento] Falha ao registrar a conclusão operacional: {ex.GetType().Name}");
            liberouTermino = false;
        }

        if (liberouTermino)
        {
            statusApontamentoValueLabel.Text = "AGUARDANDO TÉRMINO";
            statusApontamentoValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
            statusLabel.Text = "Atividade concluída. Leia o código de término para finalizar a operação.";
            DefinirInstrucao("Atividade concluída. Leia o código de TÉRMINO desta operação para finalizá-la.");
        }
        else
        {
            statusApontamentoValueLabel.Text = "EM ANDAMENTO";
            statusApontamentoValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
            statusLabel.Text = "Operação em andamento. Conclua a atividade antes de ler o código de término.";
            DefinirInstrucao(
                execucao.Resultado is ResultadoExecucaoProcessoApontamento.ErroSap
                    or ResultadoExecucaoProcessoApontamento.DivergenciaSap
                    ? "A atividade terminou com pendência no SAP. O término está bloqueado até a regularização."
                    : "A atividade não foi concluída. O apontamento continua EM ANDAMENTO e pode ser retomado "
                      + "lendo novamente o código de início.");
        }

        DevolverFocoParaLeitor();
    }

    private ResultadoExecucaoProcesso AvisarDestinoNaoConectado(ContextoApontamentoProcesso contexto)
    {
        MessageBox.Show(
            $"O tipo de processo '{contexto.TipoProcesso}' ainda não possui tela conectada nesta versão.",
            "Controle de Apontamentos",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return ResultadoExecucaoProcesso.NaoConcluido;
    }

    private ResultadoExecucaoProcesso AbrirConsumo(ContextoApontamentoProcesso contexto, ModoConsumoMaterial modo)
    {
        Hide();
        try
        {
            using ProcessoConsumoMaterialForm form = new(modo, contexto);
            form.ShowDialog(this);
            return form.ResultadoExecucaoApontamento;
        }
        finally
        {
            Show();
        }
    }

    private ResultadoExecucaoProcesso AbrirConsumoSemiAcabado(ContextoApontamentoProcesso contexto)
    {
        Hide();
        try
        {
            using ProcessoConsumoSemiAcabadoForm form = new(contexto);
            form.ShowDialog(this);
            return form.ResultadoExecucaoApontamento;
        }
        finally
        {
            Show();
        }
    }
    private ResultadoExecucaoProcesso AbrirResultadoApontamento(ContextoApontamentoProcesso contexto)
    {
        Hide();
        try
        {
            using ProcessoResultadoApontamentoForm form = new(contexto);
            form.ShowDialog(this);
            return form.ResultadoExecucaoApontamento;
        }
        finally
        {
            Show();
        }
    }

    // ---------- Preenchimento da tela ----------

    private void PreencherContextoOrdem(OrdemProducaoSap ordem)
    {
        ItemOrdemProducaoSap? item = ordem.Itens.FirstOrDefault();
        opValueLabel.Text = DescricaoOuTraco(ordem.NumeroOrdem);
        produtoValueLabel.Text = DescricaoOuTraco(item?.Material ?? ordem.MaterialProduzido);
        itemValueLabel.Text = DescricaoOuTraco(item?.ItemOrdem);
        loteValueLabel.Text = DescricaoOuTraco(item?.Lote ?? ordem.Lote);
    }

    /// <summary>
    /// Monta o grid com o estado COMPLETO da OP: cada operação recebe o tipo de processo/tela da sua
    /// configuração e o status/usuário/horários do seu apontamento PERSISTIDO — não só a operação lida.
    /// A ordem é a sequência TÉCNICA (Sequencia → Operacao → Suboperacao), nunca texto puro.
    /// </summary>
    private void PreencherGridOperacoes(ResultadoLeituraApontamento resultado)
    {
        if (_ordemAtual is null)
        {
            return;
        }

        operacoesGridView.Rows.Clear();

        foreach (OperacaoOrdemProducaoSap operacao in
                 ProcessoControleApontamentosServico.OrdenarTecnicamente(_ordemAtual))
        {
            OperacaoProducaoApontamento? apontamento = LocalizarApontamento(resultado.ApontamentosDaOrdem, operacao);

            bool ehOperacaoLida = resultado.Operacao is not null
                && string.Equals(resultado.Operacao.Operacao, operacao.Operacao, StringComparison.Ordinal)
                && string.Equals(resultado.Operacao.Sequencia, operacao.Sequencia, StringComparison.Ordinal);

            // Grade enxuta: Seleção (marca a operação atual/lida), Apontamento/Batida, Data início e Hora início.
            int indice = operacoesGridView.Rows.Add(
                ehOperacaoLida,
                $"{operacao.Operacao} · {DescricaoOuTraco(operacao.Descricao)}",
                FormatarData(apontamento?.IniciadoEm),
                FormatarHora(apontamento?.IniciadoEm));

            operacoesGridView.Rows[indice].Tag = operacao;
            if (ehOperacaoLida)
            {
                operacoesGridView.Rows[indice].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                operacoesGridView.Rows[indice].DefaultCellStyle.Font =
                    new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            }
        }

        AtualizarOperacaoAtualEProxima(_ordemAtual, resultado.Operacao);
    }

    private static OperacaoProducaoApontamento? LocalizarApontamento(
        IReadOnlyList<OperacaoProducaoApontamento> apontamentos, OperacaoOrdemProducaoSap operacao)
        => apontamentos
            .Where(a => string.Equals(a.Sequencia, operacao.Sequencia, StringComparison.Ordinal)
                        && string.Equals(a.Operacao, operacao.Operacao, StringComparison.Ordinal)
                        && string.Equals(a.Suboperacao ?? string.Empty, operacao.Suboperacao ?? string.Empty,
                            StringComparison.Ordinal))
            .OrderByDescending(a => a.CodigoApontamento)
            .FirstOrDefault();

    /// <summary>Configuração aplicável à operação, pela mesma regra de especificidade do repository (por dados).</summary>
    private ConfiguracaoOperacaoProcesso? LocalizarConfiguracao(
        IReadOnlyList<ConfiguracaoOperacaoProcesso> configuracoes, OperacaoOrdemProducaoSap operacao)
    {
        if (_ordemAtual is null)
        {
            return null;
        }

        return configuracoes
            .Where(c => string.Equals(c.OperacaoSap, operacao.Operacao, StringComparison.Ordinal)
                        && Curinga(c.Centro, _ordemAtual.Centro)
                        && Curinga(c.TipoOrdem, _ordemAtual.TipoOrdem)
                        && Curinga(c.SequenciaSap, operacao.Sequencia)
                        && Curinga(c.SuboperacaoSap, operacao.Suboperacao)
                        && Curinga(c.CentroTrabalho, operacao.CentroTrabalho))
            .OrderByDescending(Especificidade)
            .ThenBy(c => c.CodigoConfiguracao)
            .FirstOrDefault();
    }

    private static bool Curinga(string configurado, string? real)
        => configurado.Length == 0 || string.Equals(configurado, real ?? string.Empty, StringComparison.Ordinal);

    private static int Especificidade(ConfiguracaoOperacaoProcesso c)
        => (c.Centro.Length > 0 ? 1 : 0)
           + (c.TipoOrdem.Length > 0 ? 1 : 0)
           + (c.SequenciaSap.Length > 0 ? 1 : 0)
           + (c.SuboperacaoSap.Length > 0 ? 1 : 0)
           + (c.CentroTrabalho.Length > 0 ? 1 : 0);

    private void AtualizarOperacaoAtualEProxima(OrdemProducaoSap ordem, OperacaoOrdemProducaoSap? atual)
    {
        if (atual is null)
        {
            operacaoAtualValueLabel.Text = "-";
            proximaOperacaoValueLabel.Text = "-";
            destaqueValueLabel.Text = "-";
            return;
        }

        operacaoAtualValueLabel.Text = $"{atual.Operacao} · {DescricaoOuTraco(atual.Descricao)}";

        // Faixa de destaque: operação/processo atual em evidência (descrição + centro de trabalho),
        // no espírito do legado ("MISTURA DE RECEITA - MISTURADOR"), com o visual atual do FugaPET.
        string descricaoDestaque = DescricaoOuTraco(atual.Descricao);
        destaqueValueLabel.Text = string.IsNullOrWhiteSpace(atual.CentroTrabalho)
            ? descricaoDestaque
            : $"{descricaoDestaque} — {atual.CentroTrabalho.Trim()}";

        // A próxima operação sai da sequência TÉCNICA (mesma regra usada na validação da anterior).
        OperacaoOrdemProducaoSap? proxima = ProcessoControleApontamentosServico.ObterProximaOperacao(ordem, atual);
        proximaOperacaoValueLabel.Text = proxima is null
            ? "-"
            : $"{proxima.Operacao} · {DescricaoOuTraco(proxima.Descricao)}";
    }

    private void AtualizarEventoInterpretado(CodigoBarrasOperacao codigo)
    {
        if (!codigo.Valido)
        {
            eventoValueLabel.Text = "INVÁLIDO";
            eventoValueLabel.ForeColor = Color.FromArgb(220, 53, 69);
            return;
        }

        eventoValueLabel.Text = codigo.TipoEvento == TipoEventoOperacao.Inicio
            ? $"INÍCIO · OP {codigo.OrdemProducao} · OP.{codigo.Operacao}"
            : $"TÉRMINO · OP {codigo.OrdemProducao} · OP.{codigo.Operacao}";
        eventoValueLabel.ForeColor = Color.FromArgb(17, 24, 39);
    }

    private void AtualizarStatusApontamento(ResultadoLeituraApontamento resultado)
    {
        (string texto, Color cor) = resultado.Cenario switch
        {
            CenarioLeituraApontamento.SucessoInicio => ("EM ANDAMENTO", Color.FromArgb(34, 166, 82)),
            CenarioLeituraApontamento.RetomadaDisponivel => ("RETOMADA", Color.FromArgb(34, 166, 82)),
            CenarioLeituraApontamento.SucessoTermino => ("CONCLUÍDA", Color.FromArgb(34, 166, 82)),
            CenarioLeituraApontamento.OperacaoJaAguardandoTermino => ("AGUARDANDO TÉRMINO", Color.FromArgb(217, 119, 6)),
            CenarioLeituraApontamento.EstruturaNaoAplicada => ("ESTRUTURA PENDENTE", Color.FromArgb(217, 119, 6)),
            CenarioLeituraApontamento.MapeamentoNaoConfigurado => ("SEM DESTINO", Color.FromArgb(217, 119, 6)),
            CenarioLeituraApontamento.OperacaoAnteriorNaoConcluida => ("SEQUÊNCIA", Color.FromArgb(217, 119, 6)),
            CenarioLeituraApontamento.OperacaoAmbigua => ("AMBÍGUA", Color.FromArgb(220, 53, 69)),
            CenarioLeituraApontamento.ConfiguracaoAmbigua => ("CONFIG. AMBÍGUA", Color.FromArgb(220, 53, 69)),
            CenarioLeituraApontamento.TerminoAmbiguo => ("AMBÍGUA", Color.FromArgb(220, 53, 69)),
            CenarioLeituraApontamento.OperacaoEmAndamentoPorOutroUsuario => ("OUTRO USUÁRIO", Color.FromArgb(220, 53, 69)),
            CenarioLeituraApontamento.ConfirmacaoPendente => ("CANCELADA", Color.FromArgb(75, 85, 99)),
            _ => ("BLOQUEADA", Color.FromArgb(220, 53, 69))
        };

        statusApontamentoValueLabel.Text = texto;
        statusApontamentoValueLabel.ForeColor = cor;
    }

    private void AtualizarStatusSap()
    {
        // Rótulo informativo do cabeçalho; o estado real é sempre o do resultado da leitura.
        sapStatusLabel.Text = "SAP: Ordem de Produção (somente leitura)";
    }

    // ---------- Rodapé padrão (Usuário · Terminal · Empresa · Banco · Hora · Data · Status) ----------

    private void ConfigurarRodape()
    {
        footerPanel.BackColor = Color.FromArgb(248, 250, 253);
        footerPanel.Height = 38;
        footerPanel.Controls.Clear(); // o statusLabel será recolocado na célula de status

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 7,
            RowCount = 1,
            Margin = new Padding(0)
        };
        foreach (float peso in new[] { 15F, 14F, 22F, 19F, 7F, 9F, 14F })
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, peso));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        string terminal = string.IsNullOrWhiteSpace(_estacao) ? Environment.MachineName : _estacao;
        layout.Controls.Add(CriarCelulaRodape("", UsuarioLogadoUiHelper.ObterTextoUsuarioRodape(), out _), 0, 0);
        layout.Controls.Add(CriarCelulaRodape("", $"Terminal:  {terminal}", out _), 1, 0);
        layout.Controls.Add(CriarCelulaRodape("", "Empresa:  FUGA COUROS S.A.", out _), 2, 0);
        layout.Controls.Add(CriarCelulaRodape("", RodapeBancoHelper.ObterTextoBancoDados(), out _), 3, 0);
        layout.Controls.Add(CriarCelulaRodape("", "--:--", out _footerHoraLabel), 4, 0);
        layout.Controls.Add(CriarCelulaRodape("", "--/--/----", out _footerDataLabel), 5, 0);

        // Célula de status: mantém o statusLabel operacional (feedback de leitura) visível no rodapé.
        Panel celulaStatus = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Padding = new Padding(8, 0, 6, 0);
        statusLabel.AutoEllipsis = true;
        celulaStatus.Controls.Add(statusLabel);
        layout.Controls.Add(celulaStatus, 6, 0);

        footerPanel.Controls.Add(layout);

        UpdateFooterDateTime();
        _footerClockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _footerClockTimer.Tick += (_, _) => UpdateFooterDateTime();
        _footerClockTimer.Start();
    }

    private static Panel CriarCelulaRodape(string glyph, string texto, out Label textoLabel)
    {
        Panel celula = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };

        Panel divisor = new() { Dock = DockStyle.Right, Width = 1, BackColor = Color.FromArgb(214, 219, 226) };
        Label icone = new()
        {
            Dock = DockStyle.Left,
            Width = 28,
            Font = new Font("Segoe MDL2 Assets", 10F),
            ForeColor = Color.FromArgb(212, 37, 49),
            Text = glyph,
            TextAlign = ContentAlignment.MiddleCenter
        };
        textoLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(98, 108, 124),
            Padding = new Padding(2, 0, 0, 0),
            Text = texto,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        celula.Controls.Add(textoLabel);
        celula.Controls.Add(icone);
        celula.Controls.Add(divisor);
        return celula;
    }

    private void UpdateFooterDateTime()
    {
        System.Globalization.CultureInfo ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        DateTime agora = DateTime.Now;
        if (_footerDataLabel is not null)
        {
            _footerDataLabel.Text = agora.ToString("dd/MM/yyyy", ptBr);
        }

        if (_footerHoraLabel is not null)
        {
            _footerHoraLabel.Text = agora.ToString("HH:mm", ptBr);
        }
    }

    private void DefinirInstrucao(string texto) => instrucaoLabel.Text = texto;

    private void DevolverFocoParaLeitor()
    {
        if (codigoLeituraTextBox.Enabled && codigoLeituraTextBox.CanFocus)
        {
            codigoLeituraTextBox.Focus();
        }
    }

    private static string DescricaoOuTraco(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? "-" : valor.Trim();

    // Horários vêm do banco como DateTimeOffset; exibidos no fuso local da estação.
    private static string FormatarDataHora(DateTimeOffset? data)
        => data?.LocalDateTime.ToString("dd/MM/yyyy HH:mm") ?? "-";

    private static string FormatarDataHoraCurta(DateTimeOffset? data)
        => data?.LocalDateTime.ToString("dd/MM HH:mm") ?? "-";

    private static string FormatarData(DateTimeOffset? data)
        => data?.LocalDateTime.ToString("dd/MM/yyyy") ?? "-";

    private static string FormatarHora(DateTimeOffset? data)
        => data?.LocalDateTime.ToString("HH:mm") ?? "-";

    private static string FormatarDuracao(TimeSpan? duracao)
        => duracao is { } d ? $"{(int)d.TotalHours:00}:{d.Minutes:00}" : "-";
}
