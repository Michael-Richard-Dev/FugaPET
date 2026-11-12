using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Terminal;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class BalancaLeituraServico
{
    private readonly BalancaRepositorio _balancaRepositorio;
    private readonly LeitorBalancaSerialServico _leitorBalanca;

    public BalancaLeituraServico()
        : this(new BalancaRepositorio(new FabricaConexaoPostgreSql()), new LeitorBalancaSerialServico())
    {
    }

    // internal: expoe LeitorBalancaSerialServico (tipo interno); usado para composicao/testes.
    internal BalancaLeituraServico(BalancaRepositorio balancaRepositorio, LeitorBalancaSerialServico leitorBalanca)
    {
        _balancaRepositorio = balancaRepositorio;
        _leitorBalanca = leitorBalanca;
    }

    /// <summary>
    /// Leitura assincrona real: resolve a config da balanca padrao do terminal e executa
    /// a leitura serial (bloqueante) fora da thread de UI. Nunca use .GetAwaiter().GetResult()
    /// na thread da interface — chame com await.
    /// </summary>
    public async Task<ResultadoLeituraPeso> LerPesoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            BalancaLeituraConfiguracao configuracao = await ObterConfiguracaoBalancaPadraoAsync(cancellationToken);
            ResultadoLeituraPeso? falhaPortaConfigurada = ValidarPortaConfiguradaDisponivel(configuracao);
            if (falhaPortaConfigurada is not null)
            {
                return falhaPortaConfigurada;
            }

            // A leitura serial e bloqueante (SerialPort + loop): roda em thread de fundo.
            // Falhas operacionais esperadas sao capturadas dentro da propria tarefa para
            // evitar que o depurador pare como excecao sem tratamento do usuario.
            return await Task.Run(() => LerPesoComFalhaOperacionalControlada(configuracao), cancellationToken);
        }
        catch (ErroOperacionalEsperadoException ex)
        {
            // Mensagens curadas: balanca nao configurada/encontrada, tipo invalido, sem peso, etc.
            return ResultadoLeituraPeso.Falha(ex.Message);
        }
        catch (Exception)
        {
            return ResultadoLeituraPeso.Falha("Nao foi possivel concluir a leitura da balanca. Acione o suporte.");
        }
    }

    private ResultadoLeituraPeso LerPesoComFalhaOperacionalControlada(BalancaLeituraConfiguracao configuracao)
    {
        try
        {
            return ResultadoLeituraPeso.Ok(_leitorBalanca.LerPeso(configuracao)) with
            {
                CodigoBalanca = configuracao.CodigoBalanca
            };
        }
        catch (ErroOperacionalEsperadoException ex)
        {
            return ResultadoLeituraPeso.Falha(ex.Message);
        }
    }

    private static ResultadoLeituraPeso? ValidarPortaConfiguradaDisponivel(BalancaLeituraConfiguracao configuracao)
    {
        string portaConfigurada = (configuracao.PortaSerial ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(portaConfigurada))
        {
            return null;
        }

        string[] portasDisponiveis;
        try
        {
            portasDisponiveis = SerialPort.GetPortNames()
                .Select(porta => (porta ?? string.Empty).Trim().ToUpperInvariant())
                .Where(porta => !string.IsNullOrWhiteSpace(porta))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(porta => porta, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or TimeoutException)
        {
            RegistrarFalhaPortaConfigurada("LISTAR_PORTAS", portaConfigurada, [], ex);
            return ResultadoLeituraPeso.Falha("N\u00e3o foi poss\u00edvel consultar as portas seriais da m\u00e1quina. Verifique a conex\u00e3o USB ou acione o suporte.");
        }

        if (portasDisponiveis.Any(porta => string.Equals(porta, portaConfigurada, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        RegistrarFalhaPortaConfigurada("PORTA_CONFIGURADA_INEXISTENTE", portaConfigurada, portasDisponiveis, null);
        return ResultadoLeituraPeso.Falha($"Balan\u00e7a n\u00e3o encontrada na porta {portaConfigurada}. Verifique a conex\u00e3o USB ou ajuste o cadastro da balan\u00e7a.");
    }

    private static void RegistrarFalhaPortaConfigurada(string etapa, string portaConfigurada, string[] portasDisponiveis, Exception? excecao)
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

    /// <summary>
    /// Aquece a leitura (descoberta de porta) sem bloquear; ignora falhas esperadas.
    /// </summary>
    public Task AquecerAsync(CancellationToken cancellationToken = default)
        => LerPesoAsync(cancellationToken);

    private async Task<BalancaLeituraConfiguracao> ObterConfiguracaoBalancaPadraoAsync(CancellationToken cancellationToken = default)
    {
        long? codigoBalancaPadrao = EstadoTerminalLocalAtual.Contexto.IdBalancaPadrao;
        if (!codigoBalancaPadrao.HasValue || codigoBalancaPadrao.Value <= 0)
        {
            throw new ErroOperacionalEsperadoException("Balanca padrao nao configurada para este terminal.");
        }

        BalancaCadastro? balanca = await _balancaRepositorio.ObterPorIdAsync(codigoBalancaPadrao.Value, cancellationToken);
        if (balanca is null || !balanca.SituacaoBalanca)
        {
            throw new ErroOperacionalEsperadoException("Balanca padrao do terminal nao foi encontrada ou esta inativa.");
        }

        if (!TipoConexaoSerial(balanca.TipoConexao))
        {
            throw new ErroOperacionalEsperadoException("A leitura automatica esta disponivel apenas para balanca serial/USB configurada.");
        }

        return new BalancaLeituraConfiguracao
        {
            CodigoBalanca = balanca.CodigoBalanca,
            PortaSerial = balanca.PortaSerial,
            BaudRate = balanca.BaudRate ?? 4800,
            DataBits = balanca.DataBits ?? 7,
            Paridade = ConverterParidade(balanca.Paridade),
            StopBits = ConverterStopBits(balanca.StopBits),
            // Protocolo transportado ao leitor: define a escala decimal do valor bruto (ex.: P03 divide por 100).
            Protocolo = balanca.Protocolo?.Trim().ToUpperInvariant() ?? string.Empty
        };
    }

    private static bool TipoConexaoSerial(string tipoConexao)
    {
        return string.Equals(tipoConexao, "SERIAL", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tipoConexao, "USB", StringComparison.OrdinalIgnoreCase);
    }

    private static Parity ConverterParidade(string paridade)
    {
        string valor = RemoverAcentos(paridade).Trim().ToUpperInvariant();

        return valor switch
        {
            "NONE" or "NENHUMA" or "SEM" => Parity.None,
            "ODD" or "IMPAR" => Parity.Odd,
            "EVEN" or "PAR" => Parity.Even,
            "MARK" => Parity.Mark,
            "SPACE" => Parity.Space,
            _ => Parity.Even
        };
    }

    private static StopBits ConverterStopBits(decimal? stopBits)
    {
        return stopBits switch
        {
            1 => StopBits.One,
            1.5m => StopBits.OnePointFive,
            2 => StopBits.Two,
            _ => StopBits.One
        };
    }

    private static string RemoverAcentos(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        string normalizado = valor.Normalize(NormalizationForm.FormD);
        char[] caracteres = normalizado
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(caracteres).Normalize(NormalizationForm.FormC);
    }
}
