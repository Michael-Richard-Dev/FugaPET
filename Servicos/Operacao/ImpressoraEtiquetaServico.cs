using FugaPET_Dev.Controle;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Camada operacional de impressao de etiquetas. Prioriza a impressora cadastrada
/// no terminal local; se nao houver configuracao valida, tenta detectar uma Zebra
/// instalada na maquina de forma controlada.
/// </summary>
public sealed class ImpressoraEtiquetaServico
{
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
        _servicoImpressoraZebra.GarantirImpressoraDisponivel(await ObterImpressoraPadraoTerminalAsync());
    }

    public async Task AquecerAsync()
    {
        _servicoImpressoraZebra.AquecerImpressora(await ObterImpressoraPadraoTerminalAsync());
    }

    public async Task ImprimirEtiquetaProducaoAsync(DadosEtiquetaProducao etiqueta)
    {
        ExigirPermissaoImpressao(PermissoesSistema.Acoes.Imprimir);
        _servicoImpressoraZebra.ImprimirEtiquetaProducao(await ObterImpressoraPadraoTerminalAsync(), etiqueta);
    }
    public async Task ImprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
    {
        ExigirPermissaoImpressao(PermissoesSistema.Acoes.Imprimir);
        _servicoImpressoraZebra.ImprimirEtiquetaMateriaPrima(await ObterImpressoraPadraoTerminalAsync(), etiqueta);
    }

    public async Task ReimprimirEtiquetaMateriaPrimaAsync(DadosEtiquetaMateriaPrima etiqueta)
    {
        ExigirPermissaoImpressao(PermissoesSistema.Acoes.Reimprimir);
        _servicoImpressoraZebra.ImprimirEtiquetaMateriaPrima(
            await ObterImpressoraPadraoTerminalAsync(),
            etiqueta);
    }

    private static void ExigirPermissaoImpressao(string acao)
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(acao))
        {
            throw new ErroOperacionalEsperadoException(
                AutorizacaoServico.MensagemSemPermissao(
                    PermissoesSistema.Modulos.Etiqueta,
                    PermissoesSistema.Rotinas.ImpressaoEtiqueta,
                    acao));
        }
    }

    private async Task<string> ObterImpressoraPadraoTerminalAsync()
    {
        string impressoraConfigurada = EstadoTerminalLocalAtual.Contexto.ImpressoraPadrao.Trim();
        if (!string.IsNullOrWhiteSpace(impressoraConfigurada)
            && _servicoImpressoraZebra.ImpressoraInstalada(impressoraConfigurada))
        {
            return impressoraConfigurada;
        }

        string? impressoraDetectada = _servicoImpressoraZebra.ObterImpressoraZebraInstaladaPreferencial();
        if (!string.IsNullOrWhiteSpace(impressoraDetectada))
        {
            return impressoraDetectada;
        }

        await RegistrarBloqueioSemImpressoraAsync(impressoraConfigurada);
        throw new ErroOperacionalEsperadoException(MensagemImpressoraNaoConfigurada);
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
}

