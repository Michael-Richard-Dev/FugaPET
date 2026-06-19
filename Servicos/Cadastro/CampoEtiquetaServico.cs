using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class CampoEtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.CampoEtiqueta;
    private const string Tela = "EtiquetaForm";
    private static readonly string[] TiposDadoValidos =
        ["TEXTO", "NUMERO", "DATA", "PESO", "QRCODE", "CODIGO_BARRAS", "BOOLEANO"];

    private readonly CampoEtiquetaRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public CampoEtiquetaServico(CampoEtiquetaRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<CampoEtiquetaCadastro>> ListarPorEtiquetaAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
        => _repositorio.ListarPorEtiquetaAsync(codigoEtiqueta, cancellationToken);

    public Task<CampoEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarBasico(campo);
        if (validacao is not null) return validacao;

        campo.NomeCampo = campo.NomeCampo.Trim();
        campo.TipoDado = campo.TipoDado.Trim().ToUpperInvariant();

        try
        {
            if (await _repositorio.ExisteNomeNaEtiquetaAsync(campo.CodigoEtiqueta, campo.NomeCampo, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe um campo ativo com este nome nesta etiqueta.");
            }

            long id = await _repositorio.InserirAsync(campo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o campo.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Campo '{campo.NomeCampo}' (etiqueta {campo.CodigoEtiqueta})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Campo cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um campo com este nome nesta etiqueta.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return ResultadoOperacao.Falha("Etiqueta informada nao existe.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (campo.CodigoCampoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Id do campo invalido para edicao.");

        ResultadoOperacao? validacao = ValidarBasico(campo);
        if (validacao is not null) return validacao;

        campo.NomeCampo = campo.NomeCampo.Trim();
        campo.TipoDado = campo.TipoDado.Trim().ToUpperInvariant();

        try
        {
            if (await _repositorio.ExisteNomeNaEtiquetaAsync(campo.CodigoEtiqueta, campo.NomeCampo, campo.CodigoCampoEtiqueta, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outro campo ativo com este nome nesta etiqueta.");
            }

            CampoEtiquetaCadastro? anterior = await _repositorio.ObterPorIdAsync(campo.CodigoCampoEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Campo nao encontrado para edicao.");

            int atualizados = await _repositorio.AtualizarAsync(campo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Campo nao encontrado para edicao.");

            string descricao = $"Campo '{campo.NomeCampo}'";
            if (anterior.SituacaoCampoEtiqueta && !campo.SituacaoCampoEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, campo.CodigoCampoEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Campo inativado com sucesso.");
            }

            if (!anterior.SituacaoCampoEtiqueta && campo.SituacaoCampoEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, campo.CodigoCampoEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Campo reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, campo.CodigoCampoEtiqueta, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um campo com este nome nesta etiqueta.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do campo invalido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Campo nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Campo inativado com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id do campo invalido.");

        try
        {
            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Campo nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Campo reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(CampoEtiquetaCadastro campo)
    {
        if (campo.CodigoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Etiqueta do campo e obrigatoria.");
        if (string.IsNullOrWhiteSpace(campo.NomeCampo))
            return ResultadoOperacao.Falha("Nome do campo e obrigatorio.");
        if (string.IsNullOrWhiteSpace(campo.TipoDado))
            return ResultadoOperacao.Falha("Tipo de dado do campo e obrigatorio.");
        if (!TiposDadoValidos.Contains(campo.TipoDado.Trim().ToUpperInvariant()))
            return ResultadoOperacao.Falha($"Tipo de dado invalido. Use: {string.Join(", ", TiposDadoValidos)}.");
        if (campo.Ordem <= 0)
            return ResultadoOperacao.Falha("Ordem deve ser maior que zero.");
        if (campo.TamanhoMaximo.HasValue && campo.TamanhoMaximo.Value < 0)
            return ResultadoOperacao.Falha("Tamanho maximo nao pode ser negativo.");
        return null;
    }
}


