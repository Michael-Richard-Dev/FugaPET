using FugaPET_Dev.Controle;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Servicos.Seguranca;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Camada operacional de impressao de etiquetas. Prioriza a impressora cadastrada
/// no terminal local; se nao houver configuracao valida, tenta detectar uma Zebra
/// instalada na maquina de forma controlada.
/// </summary>
public sealed class ImpressoraEtiquetaServico
{
    private static readonly SemaphoreSlim GateImpressao = new(1, 1);
    private static readonly TimeSpan TimeoutOperacao = TimeSpan.FromSeconds(20);
    private const int TentativasImpressao = 2;

    private const string MensagemImpressoraNaoConfigurada =
        "Nenhuma impressora Zebra foi encontrada neste terminal. " +
        "Instale a Zebra no Windows, defina-a como padrao ou configure a impressora do terminal.";

    private readonly ServicoImpressoraZebra _servicoImpressoraZebra;

    public ImpressoraEtiquetaServico()
        : this(new ServicoImpressoraZebra())
    {
    }

    public ImpressoraEtiquetaServico(ServicoImpressoraZebra servicoImpressoraZebra)
    {
        _servicoImpressoraZebra = servicoImpressoraZebra;
    }

    public async Task GarantirImpressoraDisponivelAsync()
    {
        await ExecutarOperacaoZebraAsync("validar impressora", impressora =>
        {
            _servicoImpressoraZebra.GarantirImpressoraDisponivel(impressora);
            return ResultadoEnvioZebra.Vazio;
        });
    }

    public async Task AquecerAsync()
    {
        await ExecutarOperacaoZebraAsync("aquecer impressora", impressora =>
            _servicoImpressoraZebra.AquecerImpressora(impressora));
    }

    public async Task AquecerSeDisponivelAsync()
    {
        string operacao = "warm-up";
        if (!TentarObterImpressoraPadraoTerminal(out string impressora, out ErroImpressaoZebraException? erroResolucao))
        {
            await RegistrarDiagnosticoImpressaoAsync(
                operacao,
                impressora,
                ResultadoEnvioZebra.Vazio,
                tentativa: 1,
                tempoMs: 0,
                sucesso: false,
                erroResolucao);
            return;
        }

        if (!_servicoImpressoraZebra.TryImpressoraPronta(impressora, out ErroImpressaoZebraException? erroDisponibilidade))
        {
            await RegistrarDiagnosticoImpressaoAsync(
                operacao,
                impressora,
                ResultadoEnvioZebra.Vazio,
                tentativa: 1,
                tempoMs: 0,
                sucesso: false,
                erroDisponibilidade);
            return;
        }

        try
        {
            await ExecutarOperacaoZebraAsync("warm-up", impressora =>
                _servicoImpressoraZebra.AquecerImpressora(impressora));
        }
        catch (Exception ex)
        {
            await RegistrarDiagnosticoImpressaoAsync(
                operacao,
                impressora,
                ResultadoEnvioZebra.Vazio,
                tentativa: 1,
                tempoMs: 0,
                sucesso: false,
                ex);
        }
    }

    public async Task ImprimirEtiquetaProducaoAsync(DadosEtiquetaProducao etiqueta)
    {
        await ExigirPermissaoImpressaoAsync(PermissoesSistema.Acoes.Imprimir, "impressão após leitura");
        await ExecutarOperacaoZebraAsync("imprimir etiqueta de producao", impressora =>
            _servicoImpressoraZebra.ImprimirEtiquetaProducao(impressora, etiqueta));
    }
    public async Task ImprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
    {
        await ExigirPermissaoImpressaoAsync(PermissoesSistema.Acoes.Imprimir, "impressão após leitura");
        // codigo_etiqueta_padrao identifica a etiqueta padrao do terminal, mas a materia-prima
        // ainda usa layout ZPL fixo. Layout dinamico por cadastro deve ser incremento separado.
        await ExecutarOperacaoZebraAsync("imprimir etiqueta de materia-prima", impressora =>
            _servicoImpressoraZebra.ImprimirEtiquetaMateriaPrima(impressora, etiqueta));
    }

    public async Task ReimprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
    {
        await ExigirPermissaoImpressaoAsync(PermissoesSistema.Acoes.Reimprimir, "reimpressão");
        await ExecutarOperacaoZebraAsync("reimprimir etiqueta de materia-prima", impressora =>
            _servicoImpressoraZebra.ImprimirEtiquetaMateriaPrima(impressora, etiqueta));
    }

    public async Task<string> DescreverImpressoraAtualAsync()
        => await ObterImpressoraPadraoTerminalAsync();

    private async Task ExecutarOperacaoZebraAsync(string operacao, Func<string, ResultadoEnvioZebra> acao)
    {
        using CancellationTokenSource timeout = new(TimeoutOperacao);
        try
        {
            await GateImpressao.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            throw new ErroOperacionalEsperadoException(
                $"Nao foi possivel {operacao}: outra impressao Zebra esta em andamento.");
        }

        try
        {
            string impressora = await ObterImpressoraPadraoTerminalAsync();
            if (!await ValidarImpressoraProntaAntesDoRawAsync(operacao, impressora))
            {
                return;
            }

            Exception? ultimaFalha = null;
            for (int tentativa = 1; tentativa <= TentativasImpressao; tentativa++)
            {
                Stopwatch cronometro = Stopwatch.StartNew();
                try
                {
                    ResultadoEnvioZebra resultado = await Task.Run(() => acao(impressora), timeout.Token);
                    cronometro.Stop();
                    await RegistrarDiagnosticoImpressaoAsync(
                        operacao,
                        impressora,
                        resultado,
                        tentativa,
                        cronometro.ElapsedMilliseconds,
                        sucesso: true);
                    return;
                }
                catch (Exception ex) when (tentativa < TentativasImpressao && ErroTransitórioImpressao(ex))
                {
                    cronometro.Stop();
                    ultimaFalha = ex;
                    await RegistrarDiagnosticoImpressaoAsync(
                        operacao,
                        impressora,
                        ResultadoEnvioZebra.Vazio,
                        tentativa,
                        cronometro.ElapsedMilliseconds,
                        sucesso: false,
                        ex);
                    await Task.Delay(250, timeout.Token);
                }
                catch (Exception ex)
                {
                    cronometro.Stop();
                    await RegistrarDiagnosticoImpressaoAsync(
                        operacao,
                        impressora,
                        ResultadoEnvioZebra.Vazio,
                        tentativa,
                        cronometro.ElapsedMilliseconds,
                        sucesso: false,
                        ex);
                    throw;
                }
            }

            if (ultimaFalha is not null)
            {
                throw ultimaFalha;
            }
        }
        finally
        {
            GateImpressao.Release();
        }
    }

    private async Task<bool> ValidarImpressoraProntaAntesDoRawAsync(string operacao, string impressora)
    {
        if (_servicoImpressoraZebra.TryImpressoraPronta(impressora, out ErroImpressaoZebraException? erro))
        {
            return true;
        }

        erro ??= new ErroImpressaoZebraException(
            CategoriaErroImpressaoZebra.ImpressoraOfflinePausada,
            "A impressora Zebra não está pronta para impressão.");

        await RegistrarDiagnosticoImpressaoAsync(
            operacao,
            impressora,
            ResultadoEnvioZebra.Vazio,
            tentativa: 1,
            tempoMs: 0,
            sucesso: false,
            erro);

        throw new ErroOperacionalEsperadoException(erro.Message);
    }

    private static bool ErroTransitórioImpressao(Exception ex)
        => ex is IOException or TimeoutException or ErroImpressaoZebraException
            || ex.InnerException is IOException or TimeoutException or ErroImpressaoZebraException;

    private static async Task ExigirPermissaoImpressaoAsync(string acao, string operacao)
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(acao))
        {
            await RegistrarDiagnosticoImpressaoAsync(
                operacao,
                "não resolvida",
                ResultadoEnvioZebra.Vazio,
                tentativa: 1,
                tempoMs: 0,
                sucesso: false,
                new ErroImpressaoZebraException(
                    CategoriaErroImpressaoZebra.UsuarioSemPermissao,
                    "Usuário sem permissão para imprimir etiquetas."));

            throw new ErroOperacionalEsperadoException(
                AutorizacaoServico.MensagemSemPermissao(
                    PermissoesSistema.Modulos.Etiqueta,
                    PermissoesSistema.Rotinas.ImpressaoEtiqueta,
                    acao));
        }
    }

    private async Task<string> ObterImpressoraPadraoTerminalAsync()
    {
        if (TentarObterImpressoraPadraoTerminal(out string impressora, out _))
        {
            return impressora;
        }

        ContextoTerminalLocal contexto = EstadoTerminalLocalAtual.ObterContextoAtualizado();
        string impressoraConfigurada = contexto.ImpressoraPadrao.Trim();

        await RegistrarBloqueioSemImpressoraAsync(impressoraConfigurada);
        if (!string.IsNullOrWhiteSpace(impressoraConfigurada))
        {
            throw new ErroImpressaoZebraException(
                CategoriaErroImpressaoZebra.ImpressoraNaoInstalada,
                $"A impressora Zebra configurada '{impressoraConfigurada}' não está instalada neste terminal. Verifique o cadastro do terminal ou instale a impressora no Windows.");
        }

        throw new ErroImpressaoZebraException(
            CategoriaErroImpressaoZebra.ImpressoraNaoConfigurada,
            MensagemImpressoraNaoConfigurada);
    }

    private bool TentarObterImpressoraPadraoTerminal(
        out string impressora,
        out ErroImpressaoZebraException? erro)
    {
        erro = null;
        ContextoTerminalLocal contexto = EstadoTerminalLocalAtual.ObterContextoAtualizado();
        string impressoraConfigurada = contexto.ImpressoraPadrao.Trim();
        if (!string.IsNullOrWhiteSpace(impressoraConfigurada)
            && _servicoImpressoraZebra.ImpressoraInstalada(impressoraConfigurada))
        {
            impressora = impressoraConfigurada;
            return true;
        }

        string? impressoraDetectada;
        try
        {
            impressoraDetectada = _servicoImpressoraZebra.ObterImpressoraZebraInstaladaPreferencial();
        }
        catch (Exception ex)
        {
            impressora = string.IsNullOrWhiteSpace(impressoraConfigurada)
                ? "não configurada"
                : impressoraConfigurada;
            erro = ex as ErroImpressaoZebraException
                ?? new ErroImpressaoZebraException(
                    CategoriaErroImpressaoZebra.ImpressoraNaoConfigurada,
                    ex.Message,
                    innerException: ex);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(impressoraDetectada))
        {
            impressora = impressoraDetectada;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(impressoraConfigurada))
        {
            impressora = impressoraConfigurada;
            erro = new ErroImpressaoZebraException(
                CategoriaErroImpressaoZebra.ImpressoraNaoInstalada,
                $"A impressora Zebra configurada '{impressoraConfigurada}' não está instalada neste terminal. Verifique o cadastro do terminal ou instale a impressora no Windows.");
            return false;
        }

        impressora = "não configurada";
        erro = new ErroImpressaoZebraException(
            CategoriaErroImpressaoZebra.ImpressoraNaoConfigurada,
            MensagemImpressoraNaoConfigurada);
        return false;
    }

    private static async Task RegistrarBloqueioSemImpressoraAsync(string impressoraConfigurada)
    {
        // Auditoria/erro tecnico — nunca pode quebrar o fluxo nem mascarar o bloqueio.
        try
        {
            string terminal = EstadoTerminalLocalAtual.Contexto.NomeTerminal;
            string detalheConfiguracao = string.IsNullOrWhiteSpace(impressoraConfigurada)
                ? "sem impressora configurada"
                : $"impressora configurada nao instalada: '{impressoraConfigurada}'";

            await FabricaControladoresCadastro.CriarAuditoriaServico()
                .RegistrarErroAsync(
                    "IMPRESSAO_BLOQUEADA_SEM_IMPRESSORA",
                    $"Impressao bloqueada: terminal '{terminal}' {detalheConfiguracao} e sem Zebra local autodetectada.",
                    "ImpressoraEtiquetaServico");
        }
        catch
        {
            // Auditoria indisponivel nao deve impedir o bloqueio nem a mensagem ao usuario.
        }
    }

    private static async Task RegistrarDiagnosticoImpressaoAsync(
        string operacao,
        string impressora,
        ResultadoEnvioZebra resultado,
        int tentativa,
        long tempoMs,
        bool sucesso,
        Exception? excecao = null)
    {
        try
        {
            string resultadoFinal = sucesso ? "SUCESSO" : "FALHA";
            int? codigoWin32 = ObterCodigoWin32(excecao);
            string mensagemTecnica = SanitizarMensagemTecnica(excecao);
            string categoria = excecao is ErroImpressaoZebraException erroZebra
                ? erroZebra.Categoria.ToString()
                : "NENHUMA";
            string mensagem =
                $"Operacao={operacao}; Impressora='{SanitizarMensagemTecnica(impressora)}'; " +
                $"Bytes={resultado.TamanhoBytes}; Blocos={resultado.BlocosEnviados}; " +
                $"TempoMs={tempoMs}; Tentativa={tentativa}; Win32={codigoWin32?.ToString() ?? "N/A"}; " +
                $"Categoria={categoria}; Resultado={resultadoFinal}; Mensagem='{mensagemTecnica}'";

            AuditoriaServico auditoria = FabricaControladoresCadastro.CriarAuditoriaServico();
            await auditoria.RegistrarEventoOperacionalAsync(
                "IMPRESSAO_ZEBRA_DIAGNOSTICO",
                resultadoFinal,
                mensagem,
                "ImpressoraEtiquetaServico");
        }
        catch
        {
            // Auditoria indisponivel nao pode impedir a impressao nem mascarar a falha fisica.
        }
    }

    private static int? ObterCodigoWin32(Exception? excecao)
        => excecao switch
        {
            ErroImpressaoZebraException erroZebra => erroZebra.CodigoWin32,
            System.ComponentModel.Win32Exception win32 => win32.NativeErrorCode,
            _ when excecao?.InnerException is System.ComponentModel.Win32Exception win32 => win32.NativeErrorCode,
            _ => null
        };

    private static string SanitizarMensagemTecnica(Exception? excecao)
        => SanitizarMensagemTecnica(excecao?.Message);

    private static string SanitizarMensagemTecnica(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return string.Empty;
        }

        string sanitizada = mensagem
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        sanitizada = Regex.Replace(
            sanitizada,
            "(senha|password|token|cookie|csrf|authorization|basic)\\s*[:=]\\s*\\S+",
            "$1=***",
            RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));
        return sanitizada.Length <= 500 ? sanitizada : sanitizada[..500];
    }
}
