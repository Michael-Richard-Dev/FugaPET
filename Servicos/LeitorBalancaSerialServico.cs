using System.Globalization;
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
            return LerPesoDaPorta(configuracao.PortaSerial, 1200, configuracao);
        }

        if (!string.IsNullOrWhiteSpace(_nomePortaDetectada))
        {
            try
            {
                return LerPesoDaPorta(_nomePortaDetectada, 1200, configuracao);
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
        string[] nomesPortas = SerialPort.GetPortNames()
            .OrderBy(nomePorta => nomePorta, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string nomePorta in nomesPortas)
        {
            try
            {
                string peso = LerPesoDaPorta(nomePorta, 700, configuracao);
                _nomePortaDetectada = nomePorta;
                return peso;
            }
            catch (Exception) when (PodeRetentarDescoberta())
            {
            }
        }

        throw new ErroOperacionalEsperadoException("Nenhuma balanca foi encontrada. Verifique a conexao USB.");
    }

    private static bool PodeRetentarDescoberta()
    {
        return true;
    }

    private static string LerPesoDaPorta(string nomePorta, int tempoLimiteMilissegundos, BalancaLeituraConfiguracao configuracao)
    {
        using SerialPort portaSerial = new(nomePorta, configuracao.BaudRate, configuracao.Paridade, configuracao.DataBits, configuracao.StopBits)
        {
            ReadTimeout = 500,
            Encoding = Encoding.ASCII
        };

        portaSerial.Open();
        portaSerial.DiscardInBuffer();

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
        }

        string? peso = TentarExtrairPeso(buffer.ToString(), configuracao.Protocolo);
        if (!string.IsNullOrWhiteSpace(peso))
        {
            return peso;
        }

        throw new ErroOperacionalEsperadoException($"Nenhum peso foi recebido na porta {nomePorta}.");
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
