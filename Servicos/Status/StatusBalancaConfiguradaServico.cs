using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Status;
using FugaPET_Dev.Servicos.Terminal;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusBalancaConfiguradaServico
{
    private readonly BalancaRepositorio _balancaRepositorio;

    public StatusBalancaConfiguradaServico()
        : this(CriarRepositorioPadrao())
    {
    }

    public StatusBalancaConfiguradaServico(BalancaRepositorio balancaRepositorio)
    {
        _balancaRepositorio = balancaRepositorio;
    }

    public async Task<ItemStatusIndustrial> ObterStatusAsync(CancellationToken cancellationToken = default)
    {
        ContextoTerminalLocal contextoTerminal = EstadoTerminalLocalAtual.Contexto;
        long? codigoBalancaPadrao = contextoTerminal.IdBalancaPadrao;

        if (!codigoBalancaPadrao.HasValue || codigoBalancaPadrao.Value <= 0)
        {
            return new ItemStatusIndustrial
            {
                Nome = "Balança Configurada",
                Identificador = "-",
                Ambiente = contextoTerminal.NomeTerminal,
                Habilitado = false,
                Online = false,
                Situacao = "Não configurada",
                Mensagem = "Terminal local sem balança padrão configurada.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }

        try
        {
            BalancaCadastro? balanca = await _balancaRepositorio.ObterPorIdAsync(
                codigoBalancaPadrao.Value,
                cancellationToken);

            if (balanca is null)
            {
                return CriarStatusNaoEncontrada(contextoTerminal, codigoBalancaPadrao.Value);
            }

            return new ItemStatusIndustrial
            {
                Nome = "Balança Configurada",
                Identificador = balanca.NomeBalanca,
                Ambiente = ObterAmbienteBalanca(balanca),
                Habilitado = balanca.SituacaoBalanca,
                Online = balanca.SituacaoBalanca,
                Situacao = balanca.SituacaoBalanca ? "Configurada" : "Inativa",
                Mensagem = balanca.SituacaoBalanca
                    ? $"Balança padrão {balanca.NomeBalanca} vinculada ao terminal {contextoTerminal.NomeTerminal}."
                    : $"Balança padrão {balanca.NomeBalanca} está cadastrada, porém inativa.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }
        catch (Exception ex)
        {
            // Detalhe tecnico vai para o log de diagnostico; o operador ve mensagem generica.
            System.Diagnostics.Trace.TraceError($"StatusBalancaConfiguradaServico: falha ao validar balanca. {ex}");
            return new ItemStatusIndustrial
            {
                Nome = "Balança Configurada",
                Identificador = $"Código {codigoBalancaPadrao.Value}",
                Ambiente = contextoTerminal.NomeTerminal,
                Habilitado = false,
                Online = false,
                Situacao = "Indisponível",
                Mensagem = "Não foi possível validar a balança configurada. Acione o suporte.",
                AtualizadoEm = DateTimeOffset.Now
            };
        }
    }

    private static ItemStatusIndustrial CriarStatusNaoEncontrada(
        ContextoTerminalLocal contextoTerminal,
        long codigoBalancaPadrao)
    {
        return new ItemStatusIndustrial
        {
            Nome = "Balança Configurada",
            Identificador = $"Código {codigoBalancaPadrao}",
            Ambiente = contextoTerminal.NomeTerminal,
            Habilitado = false,
            Online = false,
            Situacao = "Não encontrada",
            Mensagem = $"Balança padrão código {codigoBalancaPadrao} não encontrada no banco.",
            AtualizadoEm = DateTimeOffset.Now
        };
    }

    private static string ObterAmbienteBalanca(BalancaCadastro balanca)
    {
        string tipoConexao = string.IsNullOrWhiteSpace(balanca.TipoConexao)
            ? "Conexão não informada"
            : balanca.TipoConexao.Trim().ToUpperInvariant();

        if (tipoConexao == "TCP_IP" && !string.IsNullOrWhiteSpace(balanca.EnderecoIp))
        {
            return balanca.PortaTcp.HasValue
                ? $"{tipoConexao} {balanca.EnderecoIp}:{balanca.PortaTcp.Value}"
                : $"{tipoConexao} {balanca.EnderecoIp}";
        }

        if (tipoConexao == "SERIAL" && !string.IsNullOrWhiteSpace(balanca.PortaSerial))
        {
            return $"{tipoConexao} {balanca.PortaSerial}";
        }

        return tipoConexao;
    }

    private static BalancaRepositorio CriarRepositorioPadrao()
    {
        ConfiguracaoBancoPostgreSql configuracao = LeitorConfiguracaoBancoPostgreSql.Carregar();
        return new BalancaRepositorio(new FabricaConexaoPostgreSql(configuracao));
    }
}
