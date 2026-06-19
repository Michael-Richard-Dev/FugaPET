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

    private readonly BalancaRepositorio _balancaRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public BalancaServico(BalancaRepositorio balancaRepositorio, AuditoriaServico auditoriaServico)
    {
        _balancaRepositorio = balancaRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<BalancaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _balancaRepositorio.ListarAsync(cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarBasico(balanca);
        if (validacao is not null) return validacao;

        balanca.NomeBalanca = balanca.NomeBalanca.Trim();

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

        ResultadoOperacao? validacao = ValidarBasico(balanca);
        if (validacao is not null) return validacao;

        balanca.NomeBalanca = balanca.NomeBalanca.Trim();

        try
        {
            if (await _balancaRepositorio.ExisteNomeNoSetorAsync(balanca.NomeBalanca, balanca.CodigoSetor, balanca.CodigoBalanca, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outra balanca ativa com este nome no mesmo setor.");
            }

            // Carrega estado anterior para emitir o evento de auditoria correto
            // (ATUALIZADO vs EXCLUIDO vs REATIVADO) coerente com o trigger do banco.
            BalancaCadastro? anterior = await _balancaRepositorio.ObterPorIdAsync(balanca.CodigoBalanca, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Balanca nao encontrada para edicao.");
            }

            int atualizados = await _balancaRepositorio.AtualizarAsync(balanca, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Balanca nao encontrada para edicao.");

            string descricao = $"Balanca '{balanca.NomeBalanca}'";
            if (anterior.SituacaoBalanca && !balanca.SituacaoBalanca)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, balanca.CodigoBalanca, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Balanca inativada com sucesso.");
            }

            if (!anterior.SituacaoBalanca && balanca.SituacaoBalanca)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, balanca.CodigoBalanca, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Balanca reativada com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, balanca.CodigoBalanca, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
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
            int excluidos = await _balancaRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Balanca nao encontrada ou ja estava inativa.");

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
            int reativados = await _balancaRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Balanca nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Balanca reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(BalancaCadastro balanca)
    {
        if (balanca.CodigoSetor <= 0) return ResultadoOperacao.Falha("Setor da balanca e obrigatorio.");
        if (string.IsNullOrWhiteSpace(balanca.NomeBalanca)) return ResultadoOperacao.Falha("Nome da balanca e obrigatorio.");
        if (string.IsNullOrWhiteSpace(balanca.TipoConexao)) return ResultadoOperacao.Falha("Tipo de conexao da balanca e obrigatorio.");
        if (!TiposConexaoValidos.Contains(balanca.TipoConexao.Trim().ToUpperInvariant()))
        {
            return ResultadoOperacao.Falha($"Tipo de conexao invalido. Use: {string.Join(", ", TiposConexaoValidos)}.");
        }
        if (balanca.PortaTcp.HasValue && (balanca.PortaTcp.Value < 1 || balanca.PortaTcp.Value > 65535))
        {
            return ResultadoOperacao.Falha("Porta TCP deve estar entre 1 e 65535.");
        }
        return null;
    }
}


