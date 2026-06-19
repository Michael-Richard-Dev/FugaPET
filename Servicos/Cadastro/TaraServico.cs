using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class TaraServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Tara;
    private const string Tela = "TaraForm";

    private readonly TaraRepositorio _taraRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public TaraServico(TaraRepositorio taraRepositorio, AuditoriaServico auditoriaServico)
    {
        _taraRepositorio = taraRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<TaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _taraRepositorio.ListarAsync(cancellationToken);

    /// <summary>Taras ativas do setor informado, para a selecao de tara da Entrada de Produto.</summary>
    public Task<IReadOnlyList<TaraCadastro>> ListarAtivasPorSetorAsync(long codigoSetor, CancellationToken cancellationToken = default)
        => _taraRepositorio.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarBasico(tara);
        if (validacao is not null) return validacao;

        tara.NomeTara = tara.NomeTara.Trim();

        try
        {
            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe uma tara ativa com este nome no mesmo setor e tipo.");
            }

            long id = await _taraRepositorio.InserirAsync(tara, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar a tara.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Tara '{tara.NomeTara}' (setor {tara.CodigoSetor}, tipo {tara.CodigoTipoTara})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Tara cadastrada com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe uma tara com este nome no mesmo setor e tipo.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (tara.CodigoTara <= 0) return ResultadoOperacao.Falha("Id da tara invalido para edicao.");

        ResultadoOperacao? validacao = ValidarBasico(tara);
        if (validacao is not null) return validacao;

        tara.NomeTara = tara.NomeTara.Trim();

        try
        {
            if (await _taraRepositorio.ExisteNomeNoSetorTipoAsync(tara.NomeTara, tara.CodigoSetor, tara.CodigoTipoTara, tara.CodigoTara, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outra tara ativa com este nome no mesmo setor e tipo.");
            }

            // Carrega estado anterior para emitir o evento de auditoria correto
            // (ATUALIZADO vs EXCLUIDO vs REATIVADO) coerente com o trigger do banco.
            TaraCadastro? anterior = await _taraRepositorio.ObterPorIdAsync(tara.CodigoTara, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Tara nao encontrada para edicao.");
            }

            int atualizados = await _taraRepositorio.AtualizarAsync(tara, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Tara nao encontrada para edicao.");

            string descricao = $"Tara '{tara.NomeTara}'";
            if (anterior.SituacaoTara && !tara.SituacaoTara)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, tara.CodigoTara, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Tara inativada com sucesso.");
            }

            if (!anterior.SituacaoTara && tara.SituacaoTara)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, tara.CodigoTara, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Tara reativada com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, tara.CodigoTara, descricao, Tela, cancellationToken);
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da tara invalido para exclusao.");

        try
        {
            int excluidos = await _taraRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Tara nao encontrada ou ja estava inativa.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tara inativada com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da tara invalido.");

        try
        {
            int reativados = await _taraRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Tara nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Tara reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(TaraCadastro tara)
    {
        if (tara.CodigoTipoTara <= 0) return ResultadoOperacao.Falha("Tipo da tara e obrigatorio.");
        if (tara.CodigoSetor <= 0) return ResultadoOperacao.Falha("Setor da tara e obrigatorio.");
        if (string.IsNullOrWhiteSpace(tara.NomeTara)) return ResultadoOperacao.Falha("Nome da tara e obrigatorio.");
        if (tara.PesoKg < 0) return ResultadoOperacao.Falha("Peso da tara nao pode ser negativo.");
        return null;
    }
}


