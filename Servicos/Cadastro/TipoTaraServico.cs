using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class TipoTaraServico
{
    private const string Entidade = PermissoesSistema.Rotinas.TipoTara;
    private const string Tela = "TipoTaraForm";

    private readonly TipoTaraRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public TipoTaraServico(TipoTaraRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<TipoTaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarAsync(cancellationToken);

    public Task<TipoTaraCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (string.IsNullOrWhiteSpace(tipo.NomeTipoTara))
        {
            return ResultadoOperacao.Falha("Nome do tipo de tara e obrigatorio.");
        }

        tipo.NomeTipoTara = tipo.NomeTipoTara.Trim();

        try
        {
            if (await _repositorio.ExisteNomeAsync(tipo.NomeTipoTara, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um tipo de tara ativo com este nome.");
            }

            long id = await _repositorio.InserirAsync(tipo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o tipo de tara.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Tipo de tara '{tipo.NomeTipoTara}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um tipo de tara com este nome.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (tipo.CodigoTipoTara <= 0)
        {
            return ResultadoOperacao.Falha("Id do tipo de tara invalido para edicao.");
        }

        if (string.IsNullOrWhiteSpace(tipo.NomeTipoTara))
        {
            return ResultadoOperacao.Falha("Nome do tipo de tara e obrigatorio.");
        }

        tipo.NomeTipoTara = tipo.NomeTipoTara.Trim();

        try
        {
            if (await _repositorio.ExisteNomeAsync(tipo.NomeTipoTara, tipo.CodigoTipoTara, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro tipo de tara ativo com este nome.");
            }

            // Carrega estado anterior para emitir o evento de auditoria correto
            // (ATUALIZADO vs EXCLUIDO vs REATIVADO) coerente com o trigger do banco.
            TipoTaraCadastro? anterior = await _repositorio.ObterPorIdAsync(tipo.CodigoTipoTara, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Tipo de tara nao encontrado para edicao.");
            }

            int atualizados = await _repositorio.AtualizarAsync(tipo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Tipo de tara nao encontrado para edicao.");

            string descricao = $"Tipo de tara '{tipo.NomeTipoTara}'";
            if (anterior.SituacaoTipoTara && !tipo.SituacaoTipoTara)
            {
                // Ativo -> Inativo: trigger registra DELETE_LOGICO.
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, tipo.CodigoTipoTara, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Tipo de tara inativado com sucesso.");
            }

            if (!anterior.SituacaoTipoTara && tipo.SituacaoTipoTara)
            {
                // Inativo -> Ativo: trigger registra REATIVACAO.
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, tipo.CodigoTipoTara, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Tipo de tara reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, tipo.CodigoTipoTara, descricao, Tela, cancellationToken);
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do tipo de tara invalido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Tipo de tara nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara inativado com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do tipo de tara invalido.");

        try
        {
            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Tipo de tara nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tipo de tara reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}


