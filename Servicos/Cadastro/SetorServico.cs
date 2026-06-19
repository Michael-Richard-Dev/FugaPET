using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class SetorServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Setor;
    private const string Tela = "SetorForm";

    private readonly SetorRepositorio _setorRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public SetorServico(SetorRepositorio setorRepositorio, AuditoriaServico auditoriaServico)
    {
        _setorRepositorio = setorRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _setorRepositorio.ListarAsync(cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (string.IsNullOrWhiteSpace(setor.NomeSetor))
        {
            return ResultadoOperacao.Falha("Nome do setor e obrigatorio.");
        }

        setor.NomeSetor = setor.NomeSetor.Trim();

        try
        {
            if (await _setorRepositorio.ExisteNomeAsync(setor.NomeSetor, ignorarCodigo: null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um setor ativo com este nome.");
            }

            long id = await _setorRepositorio.InserirAsync(setor, cancellationToken);
            if (id <= 0)
            {
                return ResultadoOperacao.Falha("Nao foi possivel cadastrar o setor.");
            }

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Setor '{setor.NomeSetor}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Setor cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um setor com este nome.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (setor.CodigoSetor <= 0)
        {
            return ResultadoOperacao.Falha("Id do setor invalido para edicao.");
        }

        if (string.IsNullOrWhiteSpace(setor.NomeSetor))
        {
            return ResultadoOperacao.Falha("Nome do setor e obrigatorio.");
        }

        setor.NomeSetor = setor.NomeSetor.Trim();

        try
        {
            if (await _setorRepositorio.ExisteNomeAsync(setor.NomeSetor, ignorarCodigo: setor.CodigoSetor, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro setor ativo com este nome.");
            }

            // Carrega estado anterior para emitir o evento de auditoria correto
            // (ATUALIZADO vs EXCLUIDO vs REATIVADO) coerente com o trigger do banco.
            SetorCadastro? anterior = await _setorRepositorio.ObterPorIdAsync(setor.CodigoSetor, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado para edicao.");
            }

            int atualizados = await _setorRepositorio.AtualizarAsync(setor, cancellationToken);
            if (atualizados <= 0)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado para edicao.");
            }

            string descricao = $"Setor '{setor.NomeSetor}'";
            if (anterior.SituacaoSetor && !setor.SituacaoSetor)
            {
                // Ativo -> Inativo: trigger registra DELETE_LOGICO; auditoria reflete inativacao.
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, setor.CodigoSetor, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Setor inativado com sucesso.");
            }

            if (!anterior.SituacaoSetor && setor.SituacaoSetor)
            {
                // Inativo -> Ativo: trigger registra REATIVACAO.
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, setor.CodigoSetor, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Setor reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, setor.CodigoSetor, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um setor com este nome.");
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

        if (id <= 0)
        {
            return ResultadoOperacao.Falha("Id do setor invalido para exclusao.");
        }

        try
        {
            int excluidos = await _setorRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado ou ja estava inativo.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Setor inativado com sucesso.");
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

        if (id <= 0)
        {
            return ResultadoOperacao.Falha("Id do setor invalido para reativacao.");
        }

        try
        {
            int reativados = await _setorRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado ou ja estava ativo.");
            }

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Setor reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}


