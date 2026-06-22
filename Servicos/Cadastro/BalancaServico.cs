using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class BalancaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Balanca;
    private const string Tela = "BalancaForm";
    private static readonly string[] TiposConexaoValidos = ["SERIAL", "TCP_IP", "USB", "MANUAL"];
    private static readonly string[] ParidadesValidas = ["NONE", "EVEN", "ODD", "MARK", "SPACE"];
    private static readonly decimal[] StopBitsValidos = [1m, 1.5m, 2m];
    private static readonly string[] FlowControlValidos = ["NONE", "XON_XOFF", "RTS_CTS", "DTR_DSR"];

    private readonly BalancaRepositorio _balancaRepositorio;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly Func<IReadOnlyCollection<long>> _obterBalancasPadraoTerminal;

    public BalancaServico(
        BalancaRepositorio balancaRepositorio,
        AuditoriaServico auditoriaServico,
        Func<IReadOnlyCollection<long>>? obterBalancasPadraoTerminal = null)
    {
        _balancaRepositorio = balancaRepositorio;
        _auditoriaServico = auditoriaServico;
        _obterBalancasPadraoTerminal = obterBalancasPadraoTerminal ?? ObterBalancasPadraoTerminalDaConfiguracao;
    }

    public Task<IReadOnlyList<BalancaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _balancaRepositorio.ListarAsync(cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarENormalizar(balanca);
        if (validacao is not null) return validacao;

        try
        {
            if (await _balancaRepositorio.ExisteNomeNoSetorAsync(balanca.NomeBalanca, balanca.CodigoSetor, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe uma balanca ativa com este nome no mesmo setor.");
            }

            long id = await _balancaRepositorio.InserirAsync(balanca, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar a balanca.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Balanca '{balanca.NomeBalanca}' (setor {balanca.CodigoSetor})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Balanca cadastrada com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe uma balanca com este nome no mesmo setor.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (balanca.CodigoBalanca <= 0) return ResultadoOperacao.Falha("Id da balanca invalido para edicao.");

        ResultadoOperacao? validacao = ValidarENormalizar(balanca);
        if (validacao is not null) return validacao;

        try
        {
            BalancaCadastro? anterior = await _balancaRepositorio.ObterPorIdAsync(balanca.CodigoBalanca, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Balanca nao encontrada para edicao.");
            }

            if (anterior.SituacaoBalanca && !balanca.SituacaoBalanca)
            {
                return ResultadoOperacao.Falha(
                    "A inativação da balança deve ser feita pela ação Inativar, pois exige validação de dependências.");
            }

            if (!anterior.SituacaoBalanca && balanca.SituacaoBalanca)
            {
                return ResultadoOperacao.Falha("A reativação da balança deve ser feita pela ação Reativar.");
            }

            if (await _balancaRepositorio.ExisteNomeNoSetorAsync(balanca.NomeBalanca, balanca.CodigoSetor, balanca.CodigoBalanca, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outra balanca ativa com este nome no mesmo setor.");
            }

            int atualizados = await _balancaRepositorio.AtualizarAsync(balanca, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Balanca nao encontrada para edicao.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, balanca.CodigoBalanca, $"Balanca '{balanca.NomeBalanca}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe uma balanca com este nome no mesmo setor.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id da balanca invalido para exclusao.");

        try
        {
            if (_obterBalancasPadraoTerminal().Contains(id))
            {
                return ResultadoOperacao.Falha(
                    "Nao e possivel inativar esta balanca porque ela esta configurada como balanca padrao de um terminal. Altere a configuracao do terminal antes de continuar.");
            }

            ResumoDependenciasBalanca dependencias = await _balancaRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
            if (dependencias.PossuiDependenciasAtivas)
            {
                return ResultadoOperacao.Falha(dependencias.ObterMensagemBloqueio());
            }

            int excluidos = await _balancaRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                ResumoDependenciasBalanca dependenciasConcorrentes = await _balancaRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
                if (dependenciasConcorrentes.PossuiDependenciasAtivas)
                {
                    return ResultadoOperacao.Falha(dependenciasConcorrentes.ObterMensagemBloqueio());
                }

                return ResultadoOperacao.Falha("Balanca nao encontrada ou ja estava inativa.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Balanca inativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id da balanca invalido.");

        try
        {
            BalancaCadastro? balanca = await _balancaRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (balanca is null)
            {
                return ResultadoOperacao.Falha("Balanca nao encontrada para reativacao.");
            }

            if (balanca.SituacaoBalanca)
            {
                return ResultadoOperacao.Falha("Balanca ja esta ativa.");
            }

            if (await _balancaRepositorio.ExisteNomeNoSetorAsync(balanca.NomeBalanca, balanca.CodigoSetor, balanca.CodigoBalanca, cancellationToken))
            {
                return ResultadoOperacao.Falha("Já existe uma balança ativa com este nome neste setor. Não é possível reativar esta balança.");
            }

            int reativados = await _balancaRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Balanca nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Balanca reativada com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Já existe uma balança ativa com este nome neste setor. Não é possível reativar esta balança.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarENormalizar(BalancaCadastro balanca)
    {
        balanca.NomeBalanca = balanca.NomeBalanca?.Trim() ?? string.Empty;
        balanca.IdentificacaoLocal = balanca.IdentificacaoLocal?.Trim() ?? string.Empty;
        balanca.EnderecoIp = balanca.EnderecoIp?.Trim() ?? string.Empty;
        balanca.PortaSerial = balanca.PortaSerial?.Trim() ?? string.Empty;
        balanca.TipoConexao = (balanca.TipoConexao ?? string.Empty).Trim().ToUpperInvariant();
        balanca.Paridade = (balanca.Paridade ?? string.Empty).Trim().ToUpperInvariant();
        balanca.FlowControl = (balanca.FlowControl ?? string.Empty).Trim().ToUpperInvariant();
        balanca.Protocolo = balanca.Protocolo?.Trim() ?? string.Empty;
        balanca.Observacao = balanca.Observacao?.Trim() ?? string.Empty;

        if (balanca.CodigoSetor <= 0)
            return ResultadoOperacao.Falha("Setor da balanca e obrigatorio.");

        if (balanca.NomeBalanca.Length < BalancaCadastro.TamanhoMinimoNome
            || balanca.NomeBalanca.Length > BalancaCadastro.TamanhoMaximoNome)
            return ResultadoOperacao.Falha("Nome da balança deve ter entre 2 e 80 caracteres.");

        if (balanca.IdentificacaoLocal.Length > BalancaCadastro.TamanhoMaximoIdentificacaoLocal)
            return ResultadoOperacao.Falha("Identificação do local deve ter no máximo 120 caracteres.");

        if (balanca.EnderecoIp.Length > BalancaCadastro.TamanhoMaximoEnderecoIp)
            return ResultadoOperacao.Falha("Endereço IP deve ter no máximo 45 caracteres.");

        if (balanca.PortaSerial.Length > BalancaCadastro.TamanhoMaximoPortaSerial)
            return ResultadoOperacao.Falha("Porta serial deve ter no máximo 50 caracteres.");

        if (balanca.Paridade.Length > BalancaCadastro.TamanhoMaximoParidade)
            return ResultadoOperacao.Falha("Paridade deve ter no máximo 10 caracteres.");

        if (balanca.Protocolo.Length > BalancaCadastro.TamanhoMaximoProtocolo)
            return ResultadoOperacao.Falha("Protocolo deve ter no máximo 50 caracteres.");

        if (balanca.Observacao.Length > BalancaCadastro.TamanhoMaximoObservacao)
            return ResultadoOperacao.Falha("Observação deve ter no máximo 255 caracteres.");

        if (string.IsNullOrWhiteSpace(balanca.TipoConexao))
            return ResultadoOperacao.Falha("Tipo de conexao da balanca e obrigatorio.");

        if (!TiposConexaoValidos.Contains(balanca.TipoConexao))
            return ResultadoOperacao.Falha($"Tipo de conexao invalido. Use: {string.Join(", ", TiposConexaoValidos)}.");

        if (balanca.FlowControl.Length > BalancaCadastro.TamanhoMaximoFlowControl)
            return ResultadoOperacao.Falha("Controle de fluxo deve ter no máximo 20 caracteres.");

        if (balanca.FlowControl.Length > 0 && !FlowControlValidos.Contains(balanca.FlowControl))
            return ResultadoOperacao.Falha($"Controle de fluxo invalido. Use: {string.Join(", ", FlowControlValidos)}.");

        return ValidarPorTipoConexao(balanca);
    }

    private static ResultadoOperacao? ValidarPorTipoConexao(BalancaCadastro balanca)
    {
        switch (balanca.TipoConexao)
        {
            case "SERIAL":
                if (string.IsNullOrWhiteSpace(balanca.PortaSerial))
                    return ResultadoOperacao.Falha("Porta serial é obrigatória para conexão SERIAL.");
                if (!balanca.BaudRate.HasValue || balanca.BaudRate.Value <= 0)
                    return ResultadoOperacao.Falha("Baud rate é obrigatório para conexão SERIAL.");
                if (!balanca.DataBits.HasValue || balanca.DataBits.Value <= 0)
                    return ResultadoOperacao.Falha("Data bits é obrigatório para conexão SERIAL.");
                if (string.IsNullOrWhiteSpace(balanca.Paridade))
                    return ResultadoOperacao.Falha("Paridade é obrigatória para conexão SERIAL.");
                if (!ParidadesValidas.Contains(balanca.Paridade))
                    return ResultadoOperacao.Falha($"Paridade inválida. Use: {string.Join(", ", ParidadesValidas)}.");
                if (!balanca.StopBits.HasValue)
                    return ResultadoOperacao.Falha("Stop bits é obrigatório para conexão SERIAL.");
                if (!StopBitsValidos.Contains(balanca.StopBits.Value))
                    return ResultadoOperacao.Falha("Stop bits inválido. Use: 1, 1.5 ou 2.");
                break;

            case "TCP_IP":
                if (string.IsNullOrWhiteSpace(balanca.EnderecoIp))
                    return ResultadoOperacao.Falha("Endereço IP é obrigatório para conexão TCP_IP.");
                if (!balanca.PortaTcp.HasValue)
                    return ResultadoOperacao.Falha("Porta TCP é obrigatória para conexão TCP_IP.");
                if (balanca.PortaTcp.Value < 1 || balanca.PortaTcp.Value > 65535)
                    return ResultadoOperacao.Falha("Porta TCP deve estar entre 1 e 65535.");
                break;

            case "USB":
                if (string.IsNullOrWhiteSpace(balanca.IdentificacaoLocal))
                    return ResultadoOperacao.Falha("Identificação do local é obrigatória para conexão USB.");
                break;

            case "MANUAL":
                break;
        }

        return null;
    }

    private static IReadOnlyCollection<long> ObterBalancasPadraoTerminalDaConfiguracao()
    {
        ConfiguracaoTerminalLocal configuracao = LeitorConfiguracaoTerminalLocal.Carregar();
        HashSet<long> ids = [];
        if (configuracao.Padrao.IdBalancaPadrao is long padrao) ids.Add(padrao);
        foreach (TerminalLocalItem terminal in configuracao.Terminais)
        {
            if (terminal.IdBalancaPadrao is long id) ids.Add(id);
        }

        return ids;
    }
}
