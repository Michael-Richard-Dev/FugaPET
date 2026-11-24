using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// PESAGEM AUTOMÁTICA DE CAIXAS (pedido do usuário em 24/11/2026).
/// <para>
/// Hoje, com a balança ligada, o peso só entra depois de o operador clicar em LER PESO. Formando
/// caixa atrás de caixa, esse clique é a parte mais repetida do turno. Ligado o modo automático, a
/// tela passa a observar a balança sozinha: quando o peso para por 2 segundos acima do mínimo, a
/// pesagem é gerada e a tela só avisa que concluiu; quando a balança volta ao zero, ela rearma para
/// a próxima caixa.
/// </para>
/// <para>
/// <b>A DECISÃO de quando pesar NÃO mora aqui</b> — mora em <see cref="CicloPesagemAutomatica"/>,
/// que não conhece tela nem balança e por isso é provado em teste, inclusive nos casos caros de
/// reproduzir com uma caixa na mão. Este arquivo cuida só de: ligar e desligar, ler a balança em
/// laço, e levar a decisão ao registro que já existia.
/// </para>
/// <para>
/// Arquivo PARCIAL separado de propósito: a tela de Produto Acabado já é grande, e um recurso novo
/// inteiro dentro dela tornaria mais difícil enxergar onde ele começa e termina.
/// </para>
/// </summary>
public partial class ProcessoProdutoAcabadoForm
{
    private readonly CicloPesagemAutomatica _cicloAutomatico = new();
    private CancellationTokenSource? _cancelamentoAutomatico;
    private ActionPillButton? _pesagemAutomaticaButton;
    private TaraCadastro? _taraDoCicloAutomatico;

    /// <summary>
    /// Intervalo entre leituras. Menor que a janela de estabilidade de propósito: com 2 segundos de
    /// janela e leituras a cada 400 ms, sobram amostras de folga mesmo se alguma leitura demorar.
    /// </summary>
    private static readonly TimeSpan IntervaloEntreLeituras = TimeSpan.FromMilliseconds(400);

    private bool PesagemAutomaticaLigada => _cancelamentoAutomatico is not null;

    /// <summary>
    /// Cria o botão COPIANDO a geometria dos que já existem, e nunca com pixel cravado.
    /// <para>
    /// O passo entre os botões e a altura vêm lidos dos controles vivos, que o DPI já ajustou. Um
    /// Location escrito à mão aqui funcionaria nesta máquina e quebraria na de outra resolução — é a
    /// causa raiz conhecida de tela quebrada neste sistema.
    /// </para>
    /// <para>
    /// Se o botão não couber na coluna, ele vai para o FIM da pilha em vez de sumir: um recurso
    /// invisível é pior do que um recurso fora de ordem.
    /// </para>
    /// </summary>
    private void MontarBotaoPesagemAutomatica()
    {
        int passo = leituraManualButton.Top - lerEtiquetaButton.Top;
        if (passo <= 0)
        {
            passo = lerEtiquetaButton.Height + 8;
        }

        ActionPillButton botao = new()
        {
            BackColor = leituraManualButton.BackColor,
            Font = leituraManualButton.Font,
            IconFontFamily = "Segoe MDL2 Assets",
            IconGlyph = "",          // sincronizar: o ciclo que se repete sozinho
            KeyHint = "F11",
            Name = "pesagemAutomaticaButton",
            Size = leituraManualButton.Size,
            PrimaryText = TextoBotaoAutomatico(false),
        };

        // Entra logo depois de LER PESO, que e o comando que ele substitui, empurrando os de baixo.
        ActionPillButton[] abaixo = [leituraManualButton, excluirUltimaButton, excluirCodigoButton];
        int fundoDepoisDoDeslocamento = abaixo[^1].Bottom + passo;

        if (fundoDepoisDoDeslocamento <= sidePanel.ClientSize.Height - 8)
        {
            botao.Location = new Point(leituraManualButton.Left, leituraManualButton.Top);
            foreach (ActionPillButton outro in abaixo)
            {
                outro.Top += passo;
            }
        }
        else
        {
            botao.Location = new Point(excluirCodigoButton.Left, excluirCodigoButton.Top + passo);
        }

        botao.Click += async (_, _) => await AlternarPesagemAutomaticaAsync();
        sidePanel.Controls.Add(botao);
        botao.BringToFront();
        _pesagemAutomaticaButton = botao;

        AtualizarBotaoPesagemAutomatica();
    }

    private static string TextoBotaoAutomatico(bool ligada)
        => ligada ? "PARAR AUTOMÁTICO" : "PESAGEM AUTOMÁTICA";

    private void AtualizarBotaoPesagemAutomatica()
    {
        if (_pesagemAutomaticaButton is null)
        {
            return;
        }

        bool ligada = PesagemAutomaticaLigada;
        _pesagemAutomaticaButton.PrimaryText = TextoBotaoAutomatico(ligada);
        _pesagemAutomaticaButton.Invalidate();

        // Com o automatico ligado, os comandos manuais saem de cena: os dois caminhos registrando a
        // mesma caixa dariam pesagem dobrada, e o operador nao teria como saber qual foi.
        lerEtiquetaButton.Enabled = !ligada;
        leituraManualButton.Enabled = !ligada;
    }

    private async Task AlternarPesagemAutomaticaAsync()
    {
        if (PesagemAutomaticaLigada)
        {
            PararPesagemAutomatica("Pesagem automática desligada.");
            return;
        }

        await LigarPesagemAutomaticaAsync();
    }

    /// <summary>
    /// Confere TUDO antes de entrar no laço — permissão, OP, balança e tara.
    /// <para>
    /// Antes, e não a cada caixa, porque o modo automático existe justamente para não interromper o
    /// operador: uma caixa de 12 kg parando para perguntar a tara seria pior do que o clique que o
    /// modo veio substituir. Se alguma dessas coisas mudar, o modo desliga e avisa.
    /// </para>
    /// </summary>
    private async Task LigarPesagemAutomaticaAsync()
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Executar, "ler peso de produto acabado"))
        {
            return;
        }

        if (!ValidarPodePesar() || !GarantirBalancaProdutoAcabadoConfigurada())
        {
            return;
        }

        TaraCadastro? tara = await GarantirTaraCaixaSelecionadaAsync();
        if (tara is null)
        {
            return;
        }

        _taraDoCicloAutomatico = tara;
        _cicloAutomatico.Reiniciar();
        _cancelamentoAutomatico = new CancellationTokenSource();
        AtualizarBotaoPesagemAutomatica();

        statusLabel.Text = "Pesagem automática ligada. Coloque a caixa na balança.";

        // Sem await: o laco vive enquanto o modo estiver ligado, e a tela segue respondendo.
        _ = ObservarBalancaAsync(_cancelamentoAutomatico.Token);
    }

    private void PararPesagemAutomatica(string mensagem)
    {
        CancellationTokenSource? fonte = _cancelamentoAutomatico;
        _cancelamentoAutomatico = null;

        if (fonte is not null)
        {
            try { fonte.Cancel(); } catch (ObjectDisposedException) { }
            fonte.Dispose();
        }

        _taraDoCicloAutomatico = null;
        _cicloAutomatico.Reiniciar();

        if (!IsDisposed && !Disposing)
        {
            AtualizarBotaoPesagemAutomatica();
            statusLabel.Text = mensagem;
        }
    }

    /// <summary>
    /// O laço: lê a balança, entrega a leitura ao ciclo e registra quando ele mandar.
    /// </summary>
    private async Task ObservarBalancaAsync(CancellationToken cancelamento)
    {
        try
        {
            while (!cancelamento.IsCancellationRequested)
            {
                ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync(cancelamento);

                if (cancelamento.IsCancellationRequested || !PodeSeguirNoAutomatico())
                {
                    return;
                }

                if (!leitura.Sucesso || !TryParsePesoKg(leitura.Peso, out decimal pesoBrutoKg))
                {
                    // Falha de leitura DESLIGA o modo, em vez de insistir em silencio. Um laco que
                    // erra calado deixaria o operador colocando caixa atras de caixa sem nada
                    // acontecer, achando que o sistema esta pesando.
                    PararPesagemAutomatica(
                        string.IsNullOrWhiteSpace(leitura.Mensagem)
                            ? "Pesagem automática desligada: falha ao ler a balança."
                            : $"Pesagem automática desligada: {leitura.Mensagem}");
                    return;
                }

                DecisaoPesagemAutomatica decisao = _cicloAutomatico.Observar(pesoBrutoKg, DateTimeOffset.Now);
                statusLabel.Text = decisao.Mensagem;

                if (decisao.PesoParaRegistrar is decimal peso && _taraDoCicloAutomatico is TaraCadastro tara)
                {
                    await RegistrarCaixaProdutoAcabadoAsync(peso, tara.PesoKg, "BALANCA", leitura.CodigoBalanca);

                    if (!PodeSeguirNoAutomatico())
                    {
                        return;
                    }
                }

                await Task.Delay(IntervaloEntreLeituras, cancelamento);
            }
        }
        catch (OperationCanceledException)
        {
            // Desligar o modo ou fechar a tela cancela o laco: saida normal.
        }
        catch (Exception erro)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[PesagemAutomatica] Laco encerrado por {erro.GetType().Name}.");
            PararPesagemAutomatica("Pesagem automática desligada por falha inesperada.");
        }
    }

    /// <summary>
    /// A tela ainda está em condição de pesar sozinha? Verificado A CADA volta, porque entre uma
    /// leitura e outra a tela pode ter sido fechada, a leitura encerrada ou a OP trocada.
    /// </summary>
    private bool PodeSeguirNoAutomatico()
        => !IsDisposed && !Disposing && PesagemAutomaticaLigada;
}
