using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class PerfilAcessoServico
{
    private const string Entidade = "PERFIL_ACESSO";
    private const string Tela = "PerfilAcessoForm";

    private readonly PerfilAcessoRepositorio _perfilAcessoRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public PerfilAcessoServico(PerfilAcessoRepositorio perfilAcessoRepositorio, AuditoriaServico auditoriaServico)
    {
        _perfilAcessoRepositorio = perfilAcessoRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<PerfilAcessoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _perfilAcessoRepositorio.ListarAsync(cancellationToken);

    public Task<PerfilAcessoCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _perfilAcessoRepositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (string.IsNullOrWhiteSpace(perfil.NomePerfilAcesso))
        {
            return ResultadoOperacao.Falha("Nome do perfil e obrigatorio.");
        }

        perfil.NomePerfilAcesso = perfil.NomePerfilAcesso.Trim();

        try
        {
            if (await _perfilAcessoRepositorio.ExisteNomeAsync(perfil.NomePerfilAcesso, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um perfil ativo com este nome.");
            }

            long id = await _perfilAcessoRepositorio.InserirAsync(perfil, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o perfil de acesso.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Perfil '{perfil.NomePerfilAcesso}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Perfil de acesso cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um perfil com este nome.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (perfil.IdPerfilAcesso <= 0)
        {
            return ResultadoOperacao.Falha("Id do perfil invalido para edicao.");
        }

        if (string.IsNullOrWhiteSpace(perfil.NomePerfilAcesso))
        {
            return ResultadoOperacao.Falha("Nome do perfil e obrigatorio.");
        }

        perfil.NomePerfilAcesso = perfil.NomePerfilAcesso.Trim();

        try
        {
            if (await _perfilAcessoRepositorio.ExisteNomeAsync(perfil.NomePerfilAcesso, perfil.IdPerfilAcesso, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro perfil ativo com este nome.");
            }

            PerfilAcessoCadastro? anterior = await _perfilAcessoRepositorio.ObterPorIdAsync(perfil.IdPerfilAcesso, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Perfil de acesso nao encontrado para edicao.");
            }

            // Perfis de sistema nao podem ser desativados pela UI normal.
            if (anterior.PerfilSistema && anterior.SituacaoPerfilAcesso && !perfil.SituacaoPerfilAcesso)
            {
                return ResultadoOperacao.Falha("Perfil de sistema nao pode ser inativado.");
            }

            // Nao permite remover a flag perfil_sistema sem permissao especial (evita perda acidental).
            if (anterior.PerfilSistema && !perfil.PerfilSistema)
            {
                return ResultadoOperacao.Falha("Nao e possivel remover a flag de perfil de sistema.");
            }

            int atualizados = await _perfilAcessoRepositorio.AtualizarAsync(perfil, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Perfil de acesso nao encontrado para edicao.");

            string descricao = $"Perfil '{perfil.NomePerfilAcesso}'";
            if (anterior.SituacaoPerfilAcesso && !perfil.SituacaoPerfilAcesso)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, perfil.IdPerfilAcesso, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Perfil inativado com sucesso.");
            }

            if (!anterior.SituacaoPerfilAcesso && perfil.SituacaoPerfilAcesso)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, perfil.IdPerfilAcesso, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Perfil reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, perfil.IdPerfilAcesso, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um perfil com este nome.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do perfil invalido para exclusao.");

        try
        {
            PerfilAcessoCadastro? perfil = await _perfilAcessoRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (perfil is null) return ResultadoOperacao.Falha("Perfil de acesso nao encontrado.");
            if (perfil.PerfilSistema) return ResultadoOperacao.Falha("Perfil de sistema nao pode ser inativado.");
            if (!perfil.SituacaoPerfilAcesso) return ResultadoOperacao.Falha("Perfil ja esta inativo.");

            int excluidos = await _perfilAcessoRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Perfil de acesso nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, $"Perfil '{perfil.NomePerfilAcesso}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Perfil de acesso inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do perfil invalido para reativacao.");

        try
        {
            int reativados = await _perfilAcessoRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Perfil nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Perfil reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}


