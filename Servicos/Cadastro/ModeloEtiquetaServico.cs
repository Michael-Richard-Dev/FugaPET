using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class ModeloEtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.ModeloEtiqueta;
    private const string Tela = "ModeloEtiquetaForm";

    private readonly ModeloEtiquetaRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public ModeloEtiquetaServico(ModeloEtiquetaRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<ModeloEtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarAsync(cancellationToken);

    public Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarBasico(modelo);
        if (validacao is not null) return validacao;

        modelo.NomeModeloEtiqueta = modelo.NomeModeloEtiqueta.Trim();

        try
        {
            if (await _repositorio.ExisteNomeVersaoAsync(modelo.NomeModeloEtiqueta, modelo.Versao, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um modelo ativo com este nome e versao.");
            }

            long id = await _repositorio.InserirAsync(modelo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o modelo de etiqueta.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Modelo '{modelo.NomeModeloEtiqueta}' v{modelo.Versao}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Modelo de etiqueta cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um modelo com este nome e versao.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (modelo.CodigoModeloEtiqueta <= 0)
            return ResultadoOperacao.Falha("Id do modelo invalido para edicao.");

        ResultadoOperacao? validacao = ValidarBasico(modelo);
        if (validacao is not null) return validacao;

        modelo.NomeModeloEtiqueta = modelo.NomeModeloEtiqueta.Trim();

        try
        {
            if (await _repositorio.ExisteNomeVersaoAsync(modelo.NomeModeloEtiqueta, modelo.Versao, modelo.CodigoModeloEtiqueta, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro modelo ativo com este nome e versao.");
            }

            ModeloEtiquetaCadastro? anterior = await _repositorio.ObterPorIdAsync(modelo.CodigoModeloEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Modelo nao encontrado para edicao.");

            int atualizados = await _repositorio.AtualizarAsync(modelo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Modelo nao encontrado para edicao.");

            string descricao = $"Modelo '{modelo.NomeModeloEtiqueta}' v{modelo.Versao}";
            if (anterior.SituacaoModeloEtiqueta && !modelo.SituacaoModeloEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, modelo.CodigoModeloEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Modelo inativado com sucesso.");
            }

            if (!anterior.SituacaoModeloEtiqueta && modelo.SituacaoModeloEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, modelo.CodigoModeloEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Modelo reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, modelo.CodigoModeloEtiqueta, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um modelo com este nome e versao.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do modelo invalido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Modelo nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Modelo inativado com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            // FK RESTRICT: ha etiquetas vinculadas a este modelo.
            return ResultadoOperacao.Falha("Nao e possivel inativar: existem etiquetas vinculadas a este modelo.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do modelo invalido.");

        try
        {
            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Modelo nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Modelo reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(ModeloEtiquetaCadastro modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo.NomeModeloEtiqueta))
            return ResultadoOperacao.Falha("Nome do modelo e obrigatorio.");
        if (modelo.Versao <= 0)
            return ResultadoOperacao.Falha("Versao deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(modelo.ConteudoZpl))
            return ResultadoOperacao.Falha("Conteudo ZPL e obrigatorio.");
        if (modelo.LarguraMm.HasValue && modelo.LarguraMm.Value < 0)
            return ResultadoOperacao.Falha("Largura nao pode ser negativa.");
        if (modelo.AlturaMm.HasValue && modelo.AlturaMm.Value < 0)
            return ResultadoOperacao.Falha("Altura nao pode ser negativa.");
        if (modelo.Dpi.HasValue && modelo.Dpi.Value <= 0)
            return ResultadoOperacao.Falha("DPI deve ser maior que zero.");
        return null;
    }
}


