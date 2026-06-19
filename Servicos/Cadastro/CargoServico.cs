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

        if (string.IsNullOrWhiteSpace(cargo.NomeCargo))
        {
            return ResultadoOperacao.Falha("Nome do cargo e obrigatorio.");
        }

        cargo.NomeCargo = cargo.NomeCargo.Trim();

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

        if (string.IsNullOrWhiteSpace(cargo.NomeCargo))
        {
            return ResultadoOperacao.Falha("Nome do cargo e obrigatorio.");
        }

        cargo.NomeCargo = cargo.NomeCargo.Trim();

        try
        {
            if (await _cargoRepositorio.ExisteNomeAsync(cargo.NomeCargo, cargo.IdCargo, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro cargo ativo com este nome.");
            }

            // Carrega estado anterior para emitir o evento de auditoria correto
            // (ATUALIZADO vs EXCLUIDO vs REATIVADO) coerente com o trigger do banco.
            CargoCadastro? anterior = await _cargoRepositorio.ObterPorIdAsync(cargo.IdCargo, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Cargo nao encontrado para edicao.");
            }

            int atualizados = await _cargoRepositorio.AtualizarAsync(cargo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Cargo nao encontrado para edicao.");

            string descricao = $"Cargo '{cargo.NomeCargo}'";
            if (anterior.SituacaoCargo && !cargo.SituacaoCargo)
            {
                // Ativo -> Inativo: trigger registra DELETE_LOGICO.
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, cargo.IdCargo, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Cargo inativado com sucesso.");
            }

            if (!anterior.SituacaoCargo && cargo.SituacaoCargo)
            {
                // Inativo -> Ativo: trigger registra REATIVACAO.
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, cargo.IdCargo, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Cargo reativado com sucesso.");
            }

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
            int excluidos = await _cargoRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Cargo nao encontrado ou ja estava inativo.");

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
            int reativados = await _cargoRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Cargo nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Cargo reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}


