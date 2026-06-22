using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Terminal;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class SetorServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Setor;
    private const string Tela = "SetorForm";

    private readonly SetorRepositorio _setorRepositorio;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly Func<long?> _obterSetorTerminalLocal;

    public SetorServico(
        SetorRepositorio setorRepositorio,
        AuditoriaServico auditoriaServico,
        Func<long?>? obterSetorTerminalLocal = null)
    {
        _setorRepositorio = setorRepositorio;
        _auditoriaServico = auditoriaServico;
        _obterSetorTerminalLocal = obterSetorTerminalLocal
            ?? (() => EstadoTerminalLocalAtual.Contexto.IdSetorPadrao);
    }

    public Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _setorRepositorio.ListarAsync(cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarENormalizar(setor);
        if (validacao is not null) return validacao;

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

        ResultadoOperacao? validacao = ValidarENormalizar(setor);
        if (validacao is not null) return validacao;

        try
        {
            SetorCadastro? anterior = await _setorRepositorio.ObterPorIdAsync(setor.CodigoSetor, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado para edicao.");
            }

            if (anterior.SituacaoSetor && !setor.SituacaoSetor)
            {
                return ResultadoOperacao.Falha(
                    "A inativação do setor deve ser feita pela ação Inativar, pois exige validação de dependências.");
            }

            if (!anterior.SituacaoSetor && setor.SituacaoSetor)
            {
                return ResultadoOperacao.Falha("A reativação do setor deve ser feita pela ação Reativar.");
            }

            if (await _setorRepositorio.ExisteNomeAsync(setor.NomeSetor, ignorarCodigo: setor.CodigoSetor, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro setor ativo com este nome.");
            }

            int atualizados = await _setorRepositorio.AtualizarAsync(setor, cancellationToken);
            if (atualizados <= 0)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado para edicao.");
            }

            string descricao = $"Setor '{setor.NomeSetor}'";
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
            if (_obterSetorTerminalLocal() == id)
            {
                return ResultadoOperacao.Falha(
                    "Nao e possivel inativar este setor porque ele esta configurado como setor padrao deste terminal. Altere a configuracao local antes de continuar.");
            }

            ResumoDependenciasSetor dependencias = await _setorRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
            if (dependencias.PossuiDependenciasAtivas)
            {
                return ResultadoOperacao.Falha(dependencias.ObterMensagemBloqueio());
            }

            int excluidos = await _setorRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                ResumoDependenciasSetor dependenciasConcorrentes = await _setorRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
                if (dependenciasConcorrentes.PossuiDependenciasAtivas)
                {
                    return ResultadoOperacao.Falha(dependenciasConcorrentes.ObterMensagemBloqueio());
                }

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
            SetorCadastro? setor = await _setorRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (setor is null)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado para reativacao.");
            }

            if (setor.SituacaoSetor)
            {
                return ResultadoOperacao.Falha("Setor ja esta ativo.");
            }

            if (await _setorRepositorio.ExisteNomeAsync(setor.NomeSetor, ignorarCodigo: setor.CodigoSetor, cancellationToken))
            {
                return ResultadoOperacao.Falha("Já existe um setor ativo com este nome. Não é possível reativar este setor.");
            }

            int reativados = await _setorRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0)
            {
                return ResultadoOperacao.Falha("Setor nao encontrado ou ja estava ativo.");
            }

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Setor reativado com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Já existe um setor ativo com este nome. Não é possível reativar este setor.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarENormalizar(SetorCadastro setor)
    {
        setor.NomeSetor = setor.NomeSetor?.Trim() ?? string.Empty;
        setor.DescricaoSetor = setor.DescricaoSetor?.Trim() ?? string.Empty;

        if (setor.NomeSetor.Length < SetorCadastro.TamanhoMinimoNome
            || setor.NomeSetor.Length > SetorCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome do setor deve ter entre 2 e 80 caracteres.");
        }

        if (setor.DescricaoSetor.Length > SetorCadastro.TamanhoMaximoDescricao)
        {
            return ResultadoOperacao.Falha("Descrição do setor deve ter no máximo 255 caracteres.");
        }

        return null;
    }
}
