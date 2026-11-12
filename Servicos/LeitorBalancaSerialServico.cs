using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Servicos;

/// <summary>
/// Leitor SERIAL de baixo nivel. NAO usar direto nas telas: ele nao conhece o cadastro
/// da balanca. Recebe SEMPRE a configuracao por parametro — quem resolve a config do
/// terminal (porta/baud/paridade) e o BalancaLeituraServico (camada Operacao).
/// Marcado como internal para sinalizar que e componente interno; nao ha mais
/// configuracao padrao fixa embutida (4800/7/Even/One foram removidos).
/// </summary>
internal sealed class LeitorBalancaSerialServico
{
    private readonly object _sincronizacao = new();
    private string? _nomePortaDetectada;

    /// <summary>
    /// Le o peso usando a configuracao informada (porta, baud, paridade, data/stop bits).
    /// </summary>
    public string LerPeso(BalancaLeituraConfiguracao configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        lock (_sincronizacao)
        {
            return LerPesoInterno(configuracao);
        }
    }

    private string LerPesoInterno(BalancaLeituraConfiguracao configuracao)
    {
        if (!string.IsNullOrWhiteSpace(configuracao.PortaSerial))
        {
            return LerPesoDaPortaConfigurada(configuracao.PortaSerial, 1200, configuracao);
        }

        if (!string.IsNullOrWhiteSpace(_nomePortaDetectada))
        {
            try
            {
                return LerPesoDaPortaConfigurada(_nomePortaDetectada, 1200, configuracao);
            }
            catch (Exception) when (PodeRetentarDescoberta())
            {
                _nomePortaDetectada = null;
            }
        }

        return DescobrirBalancaELerPeso(configuracao);
    }

    private string DescobrirBalancaELerPeso(BalancaLeituraConfiguracao configuracao)
    {
        string[] nomesPortas = ObterPortasDisponiveisSeguras();

        foreach (string nomePorta in nomesPortas)
        {
            try
            {
                string peso = LerPesoDaPortaDescoberta(nomePorta, nomesPortas, 700, configuracao);
                _nomePortaDetectada = nomePorta;
                return peso;
            }
            catch (Exception) when (PodeRetentarDescoberta())
            {
            }
        }

        RegistrarFalhaSerial("DESCOBERTA", string.Empty, nomesPortas, null);
        throw new ErroOperacionalEsperadoException("Nenhuma balan\u00e7a foi encontrada. Verifique a conex\u00e3o USB ou ajuste o cadastro da balan\u00e7a.");
    }

    private static bool PodeRetentarDescoberta()
    {
        return true;
    }

    private static string LerPesoDaPortaConfigurada(string nomePorta, int tempoLimiteMilissegundos, BalancaLeituraConfiguracao configuracao)
    {
        string portaNormalizada = NormalizarNomePorta(nomePorta);
        string[] portasDisponiveis = ObterPortasDisponiveisSeguras();
        if (!PortaExiste(portaNormalizada, portasDisponiveis))
        {
            RegistrarFalhaSerial("CONFIGURADA", portaNormalizada, portasDisponiveis, null);
            throw new ErroOperacionalEsperadoException(MensagemPortaNaoEncontrada(portaNormalizada));
        }

        return ExecutarLeituraPorta(portaNormalizada, portasDisponiveis, tempoLimiteMilissegundos, configuracao);
    }

    private static string LerPesoDaPortaDescoberta(string nomePorta, string[] portasDisponiveis, int tempoLimiteMilissegundos, BalancaLeituraConfiguracao configuracao)
    {
        string portaNormalizada = NormalizarNomePorta(nomePorta);
        if (!PortaExiste(portaNormalizada, portasDisponiveis))
        {
            RegistrarFalhaSerial("DESCOBERTA_PORTA_INEXISTENTE", portaNormalizada, portasDisponiveis, null);
            throw new ErroOperacionalEsperadoException(MensagemPortaNaoEncontrada(portaNormalizada));
        }

        return ExecutarLeituraPorta(portaNormalizada, portasDisponiveis, tempoLimiteMilissegundos, configuracao);
    }

    private static string ExecutarLeituraPorta(string nomePorta, string[] portasDisponiveis, int tempoLimiteMilissegundos, BalancaLeituraConfiguracao configuracao)
    {
        using SerialPort portaSerial = new(nomePorta, configuracao.BaudRate, configuracao.Paridade, configuracao.DataBits, configuracao.StopBits)
        {
            ReadTimeout = 500,
            Encoding = Encoding.ASCII
        };

        try
        {
            portaSerial.Open();
            portaSerial.DiscardInBuffer();
        }
        catch (Exception ex) when (EhErroSerialControlado(ex))
        {
            RegistrarFalhaSerial("ABRIR_PORTA", nomePorta, portasDisponiveis, ex);
            throw new ErroOperacionalEsperadoException(MensagemFalhaPorta(nomePorta, ex));
        }

        StringBuilder buffer = new();
        DateTime prazo = DateTime.Now.AddMilliseconds(tempoLimiteMilissegundos);
        int? ultimoValor = null;
        int contadorEstabilidade = 0;

        while (DateTime.Now < prazo)
        {
            try
            {
                string trecho = portaSerial.ReadExisting();

                if (!string.IsNullOrEmpty(trecho))
                {
                    buffer.Append(trecho);

                    int? valorAtual = TentarExtrairValorBrutoEstavel(buffer.ToString());
                    if (valorAtual.HasValue)
                    {
                        if (ultimoValor == valorAtual.Value)
                        {
                            contadorEstabilidade++;
                        }
                        else
                        {
                            ultimoValor = valorAtual.Value;
                            contadorEstabilidade = 1;
                        }

                        if (contadorEstabilidade >= 2)
                        {
                            return FormatarPeso(valorAtual.Value, configuracao.Protocolo);
                        }
                    }
                }

                Thread.Sleep(30);
            }
            catch (TimeoutException)
            {
            }
            catch (Exception ex) when (EhErroSerialControlado(ex))
            {
                RegistrarFalhaSerial("LER_PORTA", nomePorta, portasDisponiveis, ex);
                throw new ErroOperacionalEsperadoException(MensagemFalhaPorta(nomePorta, ex));
            }
        }

        string? peso = TentarExtrairPeso(buffer.ToString(), configuracao.Protocolo);
        if (!string.IsNullOrWhiteSpace(peso))
        {
            return peso;
        }

        throw new ErroOperacionalEsperadoException($"Nenhum peso foi recebido na porta {nomePorta}.");
    }

    private static string[] ObterPortasDisponiveisSeguras()
    {
        try
        {
            return SerialPort.GetPortNames()
                .Select(NormalizarNomePorta)
                .Where(nomePorta => !string.IsNullOrWhiteSpace(nomePorta))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(nomePorta => nomePorta, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (EhErroSerialControlado(ex))
        {
            RegistrarFalhaSerial("LISTAR_PORTAS", string.Empty, [], ex);
            return [];
        }
    }

    private static bool PortaExiste(string nomePorta, string[] portasDisponiveis)
        => portasDisponiveis.Any(porta => string.Equals(porta, nomePorta, StringComparison.OrdinalIgnoreCase));

    private static string NormalizarNomePorta(string? nomePorta)
        => (nomePorta ?? string.Empty).Trim().ToUpperInvariant();

    private static string MensagemPortaNaoEncontrada(string nomePorta)
        => $"Balan\u00e7a n\u00e3o encontrada na porta {nomePorta}. Verifique a conex\u00e3o USB ou ajuste o cadastro da balan\u00e7a.";

    private static string MensagemFalhaPorta(string nomePorta, Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return $"Balan\u00e7a na porta {nomePorta} est\u00e1 indispon\u00edvel ou em uso por outro aplicativo. Feche outros programas e tente novamente.";
        }

        if (ex is TimeoutException)
        {
            return $"Tempo esgotado ao comunicar com a balan\u00e7a na porta {nomePorta}. Verifique a conex\u00e3o USB.";
        }

        return MensagemPortaNaoEncontrada(nomePorta);
    }

    private static bool EhErroSerialControlado(Exception ex)
        => ex is FileNotFoundException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or TimeoutException;

    private static void RegistrarFalhaSerial(string etapa, string portaConfigurada, string[] portasDisponiveis, Exception? excecao)
    {
        string portas = portasDisponiveis.Length == 0 ? "nenhuma" : string.Join(",", portasDisponiveis);
        Trace.TraceWarning(
            "Leitura balan\u00e7a serial HML: etapa={0}; processo={1}; porta_configurada={2}; portas_disponiveis={3}; excecao={4}",
            etapa,
            "EntradaProduto/ConsumoMaterial",
            string.IsNullOrWhiteSpace(portaConfigurada) ? "<nao_informada>" : portaConfigurada,
            portas,
            excecao?.ToString() ?? "<sem_excecao>");
    }

    // internal para teste direto (InternalsVisibleTo="FugaPET_Dev.Tests"). Caminho final de buffer.
    internal static string? TentarExtrairPeso(string textoSerial, string protocolo)
    {
        string[] quadros = textoSerial
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<int> leituras = [];

        foreach (string quadro in quadros)
        {
            MatchCollection ocorrencias = Regex.Matches(quadro, @"\d{12}");
            if (ocorrencias.Count == 0)
            {
                continue;
            }

            string pesoBruto = ocorrencias
                .OrderByDescending(ocorrencia => ocorrencia.Value.Length)
                .First()
                .Value;

            string digitosPeso = pesoBruto[..6];

            if (int.TryParse(digitosPeso, NumberStyles.None, CultureInfo.InvariantCulture, out int valor))
            {
                leituras.Add(valor);
            }
        }

        if (leituras.Count == 0)
        {
            return null;
        }

        int valorEstavel = leituras
            .GroupBy(valor => valor)
            .OrderByDescending(grupo => grupo.Count())
            .ThenByDescending(grupo => grupo.Key)
            .First()
            .Key;

        return FormatarPeso(valorEstavel, protocolo);
    }

    // internal para teste direto: extrai o valor bruto (6 primeiros dígitos de um quadro de 12) do último quadro válido.
    internal static int? TentarExtrairValorBrutoEstavel(string textoSerial)
    {
        string[] quadros = textoSerial
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int indice = quadros.Length - 1; indice >= 0; indice--)
        {
            MatchCollection ocorrencias = Regex.Matches(quadros[indice], @"\d{12}");
            if (ocorrencias.Count == 0)
            {
                continue;
            }

            string pesoBruto = ocorrencias
                .OrderByDescending(ocorrencia => ocorrencia.Value.Length)
                .First()
                .Value;

            string digitosPeso = pesoBruto[..6];

            if (int.TryParse(digitosPeso, NumberStyles.None, CultureInfo.InvariantCulture, out int valor))
            {
                return valor;
            }
        }

        return null;
    }

    // internal para teste direto: formata o peso já convertido pela escala do protocolo.
    internal static string FormatarPeso(int valor, string protocolo)
    {
        decimal peso = ConverterValorBrutoParaKg(valor, protocolo);
        return peso.ToString("N2", new CultureInfo("pt-BR"));
    }

    /// <summary>
    /// Conversão CENTRALIZADA do valor bruto (6 primeiros dígitos do quadro) para KG, conforme o protocolo
    /// da balança. Única regra de divisor — não replicar em nenhuma tela.
    /// </summary>
    /// <remarks>
    /// P03 (evidência de campo): 000165 → 1,65 kg, ou seja divisor 100 (duas casas decimais).
    /// Fallback (divisor 10) é COMPATIBILIDADE TEMPORÁRIA para protocolos vazios/diferentes, mantendo o
    /// comportamento legado até haver evidência específica de cada protocolo. Não alterar sem dado de campo.
    /// </remarks>
    internal static decimal ConverterValorBrutoParaKg(int valorBruto, string protocolo)
    {
        string protocoloNormalizado = (protocolo ?? string.Empty).Trim().ToUpperInvariant();

        decimal divisor = protocoloNormalizado switch
        {
            "P03" => 100m,
            _ => 10m // fallback legado temporário
        };

        return valorBruto / divisor;
    }
}
