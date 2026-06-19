using System.Drawing.Printing;
using FugaPET_Dev.Modelo.Status;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Terminal;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusImpressoraConfiguradaServico
{
    private readonly ServicoImpressoraZebra _servicoImpressoraZebra = new();

    public ItemStatusIndustrial ObterStatus()
    {
        ContextoTerminalLocal contextoTerminal = EstadoTerminalLocalAtual.Contexto;
        string impressoraPadrao = contextoTerminal.ImpressoraPadrao.Trim();

        if (string.IsNullOrWhiteSpace(impressoraPadrao))
        {
            string? impressoraDetectada = _servicoImpressoraZebra.ObterImpressoraZebraInstaladaPreferencial();
            if (!string.IsNullOrWhiteSpace(impressoraDetectada))
            {
                return new ItemStatusIndustrial
                {
                    Nome = "Impressora Configurada",
                    Identificador = impressoraDetectada,
                    Ambiente = contextoTerminal.NomeTerminal,
                    Habilitado = true,
                    Online = true,
                    Situacao = "Autodetectada",
                    Mensagem = $"Impressora Zebra {impressoraDetectada} autodetectada no Windows.",
                    AtualizadoEm = DateTimeOffset.Now
                };
            }

            return new ItemStatusIndustrial
            {
                Nome = "Impressora Configurada",
                Identificador = "-",
                Ambiente = contextoTerminal.NomeTerminal,
                Habilitado = false,
                Online = false,
                Situacao = "Não configurada",
                Mensagem = "Terminal local sem impressora padrão configurada.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }

        try
        {
            bool instalada = _servicoImpressoraZebra.ImpressoraInstalada(impressoraPadrao);
            string? impressoraDetectada = instalada
                ? null
                : _servicoImpressoraZebra.ObterImpressoraZebraInstaladaPreferencial();

            return new ItemStatusIndustrial
            {
                Nome = "Impressora Configurada",
                Identificador = instalada ? impressoraPadrao : impressoraDetectada ?? impressoraPadrao,
                Ambiente = contextoTerminal.NomeTerminal,
                Habilitado = instalada || !string.IsNullOrWhiteSpace(impressoraDetectada),
                Online = instalada || !string.IsNullOrWhiteSpace(impressoraDetectada),
                Situacao = instalada ? "Configurada" : !string.IsNullOrWhiteSpace(impressoraDetectada) ? "Autodetectada" : "Não encontrada",
                Mensagem = instalada
                    ? $"Impressora padrão {impressoraPadrao} configurada no terminal {contextoTerminal.NomeTerminal}."
                    : !string.IsNullOrWhiteSpace(impressoraDetectada)
                        ? $"Impressora configurada {impressoraPadrao} não encontrada; usando Zebra autodetectada {impressoraDetectada}."
                    : $"Impressora padrão {impressoraPadrao} não encontrada nas impressoras instaladas do Windows.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }
        catch (Exception ex)
        {
            // Detalhe tecnico vai para o log de diagnostico; o operador ve mensagem generica.
            System.Diagnostics.Trace.TraceError($"StatusImpressoraConfiguradaServico: falha ao validar impressora. {ex}");
            return new ItemStatusIndustrial
            {
                Nome = "Impressora Configurada",
                Identificador = impressoraPadrao,
                Ambiente = contextoTerminal.NomeTerminal,
                Habilitado = false,
                Online = false,
                Situacao = "Indisponível",
                Mensagem = "Não foi possível validar a impressora configurada. Acione o suporte.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }
    }
}
