using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class CargoServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Cargo;
    private const string Tela = "CargoForm";

    private readonly CargoRepositorio _cargoRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public CargoServico(CargoRepositorio cargoRepositorio, AuditoriaServico auditoriaServico)
    {
        _cargoRepositorio = cargoRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<CargoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _cargoRepositorio.ListarAsync(cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarENormalizar(cargo);
        if (validacao is not null) return validacao;

        try
        {
            if (await _cargoRepositorio.ExisteNomeAsync(cargo.NomeCargo, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um cargo ativo com este nome.");
            }

            long id = await _cargoRepositorio.InserirAsync(cargo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o cargo.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Cargo '{cargo.NomeCargo}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Cargo cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um cargo com este nome.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (cargo.IdCargo <= 0)
        {
            return ResultadoOperacao.Falha("Id do cargo invalido para edicao.");
        }

        ResultadoOperacao? validacao = ValidarENormalizar(cargo);
        if (validacao is not null) return validacao;

        try
        {
            CargoCadastro? anterior = await _cargoRepositorio.ObterPorIdAsync(cargo.IdCargo, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Cargo nao encontrado para edicao.");
            }

            // AtualizarAsync edita apenas dados cadastrais; status so muda por Inativar/Reativar.
            if (anterior.SituacaoCargo && !cargo.SituacaoCargo)
            {
                return ResultadoOperacao.Falha(
                    "A inativação do cargo deve ser feita pela ação Inativar, pois exige validação de dependências.");
            }

            if (!anterior.SituacaoCargo && cargo.SituacaoCargo)
            {
                return ResultadoOperacao.Falha("A reativação do cargo deve ser feita pela ação Reativar.");
            }

            if (await _cargoRepositorio.ExisteNomeAsync(cargo.NomeCargo, cargo.IdCargo, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro cargo ativo com este nome.");
            }

            int atualizados = await _cargoRepositorio.AtualizarAsync(cargo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Cargo nao encontrado para edicao.");

            string descricao = $"Cargo '{cargo.NomeCargo}'";
            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, cargo.IdCargo, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um cargo com este nome.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do cargo invalido para exclusao.");

        try
        {
            ResumoDependenciasCargo dependencias = await _cargoRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
            if (dependencias.PossuiDependenciasAtivas)
            {
                return ResultadoOperacao.Falha(dependencias.ObterMensagemBloqueio());
            }

            int excluidos = await _cargoRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0)
            {
                ResumoDependenciasCargo dependenciasConcorrentes = await _cargoRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
                if (dependenciasConcorrentes.PossuiDependenciasAtivas)
                {
                    return ResultadoOperacao.Falha(dependenciasConcorrentes.ObterMensagemBloqueio());
                }

                return ResultadoOperacao.Falha("Cargo nao encontrado ou ja estava inativo.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Cargo inativado com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do cargo invalido para reativacao.");

        try
        {
            CargoCadastro? cargo = await _cargoRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (cargo is null)
            {
                return ResultadoOperacao.Falha("Cargo nao encontrado para reativacao.");
            }

            if (cargo.SituacaoCargo)
            {
                return ResultadoOperacao.Falha("Cargo ja esta ativo.");
            }

            if (await _cargoRepositorio.ExisteNomeAsync(cargo.NomeCargo, ignorarCodigo: cargo.IdCargo, cancellationToken))
            {
                return ResultadoOperacao.Falha("Já existe um cargo ativo com este nome. Não é possível reativar este cargo.");
            }

            int reativados = await _cargoRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Cargo nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Cargo reativado com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Já existe um cargo ativo com este nome. Não é possível reativar este cargo.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarENormalizar(CargoCadastro cargo)
    {
        cargo.NomeCargo = cargo.NomeCargo?.Trim() ?? string.Empty;
        cargo.DescricaoCargo = cargo.DescricaoCargo?.Trim() ?? string.Empty;

        if (cargo.NomeCargo.Length < CargoCadastro.TamanhoMinimoNome
            || cargo.NomeCargo.Length > CargoCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome do cargo deve ter entre 2 e 80 caracteres.");
        }

        if (cargo.DescricaoCargo.Length > CargoCadastro.TamanhoMaximoDescricao)
        {
            return ResultadoOperacao.Falha("Descrição do cargo deve ter no máximo 255 caracteres.");
        }

        return null;
    }
}


