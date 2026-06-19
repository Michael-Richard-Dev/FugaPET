using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class EtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.Etiqueta;
    private const string Tela = "EtiquetaForm";
    private static readonly string[] TiposValidos = ["CAIXA", "PALETE", "HU", "INTERNA", "OUTRA"];

    private readonly EtiquetaRepositorio _etiquetaRepositorio;
    private readonly ModeloEtiquetaRepositorio _modeloEtiquetaRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public EtiquetaServico(
        EtiquetaRepositorio etiquetaRepositorio,
        ModeloEtiquetaRepositorio modeloEtiquetaRepositorio,
        AuditoriaServico auditoriaServico)
    {
        _etiquetaRepositorio = etiquetaRepositorio;
        _modeloEtiquetaRepositorio = modeloEtiquetaRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<EtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _etiquetaRepositorio.ListarAsync(cancellationToken);

    public Task<EtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaRepositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<bool> ExisteCodigoInternoAsync(string codigoInterno, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoInterno))
        {
            return false;
        }

        return await _etiquetaRepositorio.ExisteCodigoInternoAsync(codigoInterno.Trim(), ignorarCodigo, cancellationToken);
    }

    public async Task<ResultadoOperacao> InserirAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        try
        {
            ResultadoOperacao? validacao = await ValidarAsync(etiqueta, cancellationToken);
            if (validacao is not null) return validacao;

            etiqueta.CodigoInterno = etiqueta.CodigoInterno.Trim();
            etiqueta.NomeEtiqueta = etiqueta.NomeEtiqueta.Trim();
            etiqueta.TipoEtiqueta = etiqueta.TipoEtiqueta.Trim().ToUpperInvariant();

            if (await _etiquetaRepositorio.ExisteCodigoInternoAsync(etiqueta.CodigoInterno, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe uma etiqueta ativa com este codigo interno.");
            }

            long id = await _etiquetaRepositorio.InserirAsync(etiqueta, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar a etiqueta.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Etiqueta '{etiqueta.NomeEtiqueta}' ({etiqueta.CodigoInterno})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Etiqueta cadastrada com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await _auditoriaServico.RegistrarErroAsync(Entidade + "_ERRO_DUPLICIDADE", ex.ToString(), Tela, cancellationToken);
            return ResultadoOperacao.Falha("Ja existe uma etiqueta com este codigo interno.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            await _auditoriaServico.RegistrarErroAsync(Entidade + "_ERRO_MODELO", ex.ToString(), Tela, cancellationToken);
            return ResultadoOperacao.Falha("Modelo de etiqueta informado nao existe.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (etiqueta.CodigoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Id da etiqueta invalido para edicao.");

        try
        {
            ResultadoOperacao? validacao = await ValidarAsync(etiqueta, cancellationToken);
            if (validacao is not null) return validacao;

            etiqueta.CodigoInterno = etiqueta.CodigoInterno.Trim();
            etiqueta.NomeEtiqueta = etiqueta.NomeEtiqueta.Trim();
            etiqueta.TipoEtiqueta = etiqueta.TipoEtiqueta.Trim().ToUpperInvariant();

            if (await _etiquetaRepositorio.ExisteCodigoInternoAsync(etiqueta.CodigoInterno, etiqueta.CodigoEtiqueta, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outra etiqueta ativa com este codigo interno.");
            }

            EtiquetaCadastro? anterior = await _etiquetaRepositorio.ObterPorIdAsync(etiqueta.CodigoEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Etiqueta nao encontrada para edicao.");

            int atualizados = await _etiquetaRepositorio.AtualizarAsync(etiqueta, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Etiqueta nao encontrada para edicao.");

            string descricao = $"Etiqueta '{etiqueta.NomeEtiqueta}' ({etiqueta.CodigoInterno})";
            if (anterior.SituacaoEtiqueta && !etiqueta.SituacaoEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, etiqueta.CodigoEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Etiqueta inativada com sucesso.");
            }

            if (!anterior.SituacaoEtiqueta && etiqueta.SituacaoEtiqueta)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, etiqueta.CodigoEtiqueta, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Etiqueta reativada com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, etiqueta.CodigoEtiqueta, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await _auditoriaServico.RegistrarErroAsync(Entidade + "_ERRO_DUPLICIDADE", ex.ToString(), Tela, cancellationToken);
            return ResultadoOperacao.Falha("Ja existe uma etiqueta com este codigo interno.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            await _auditoriaServico.RegistrarErroAsync(Entidade + "_ERRO_MODELO", ex.ToString(), Tela, cancellationToken);
            return ResultadoOperacao.Falha("Modelo de etiqueta informado nao existe.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da etiqueta invalido para exclusao.");

        try
        {
            int excluidos = await _etiquetaRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Etiqueta nao encontrada ou ja estava inativa.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Etiqueta inativada com sucesso.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Id da etiqueta invalido.");

        try
        {
            int reativados = await _etiquetaRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Etiqueta nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Etiqueta reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private async Task<ResultadoOperacao?> ValidarAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken)
    {
        if (etiqueta.CodigoModeloEtiqueta <= 0)
            return ResultadoOperacao.Falha("Modelo da etiqueta e obrigatorio.");
        if (string.IsNullOrWhiteSpace(etiqueta.CodigoInterno))
            return ResultadoOperacao.Falha("Codigo interno da etiqueta e obrigatorio.");
        if (string.IsNullOrWhiteSpace(etiqueta.NomeEtiqueta))
            return ResultadoOperacao.Falha("Nome da etiqueta e obrigatorio.");
        if (string.IsNullOrWhiteSpace(etiqueta.TipoEtiqueta))
            return ResultadoOperacao.Falha("Tipo da etiqueta e obrigatorio.");
        if (!TiposValidos.Contains(etiqueta.TipoEtiqueta.Trim().ToUpperInvariant()))
            return ResultadoOperacao.Falha($"Tipo invalido. Use: {string.Join(", ", TiposValidos)}.");

        // Garante que o modelo existe e esta ativo (FK e RESTRICT).
        ModeloEtiquetaCadastro? modelo = await _modeloEtiquetaRepositorio.ObterPorIdAsync(etiqueta.CodigoModeloEtiqueta, cancellationToken);
        if (modelo is null)
            return ResultadoOperacao.Falha("Modelo de etiqueta informado nao existe.");
        if (!modelo.SituacaoModeloEtiqueta)
            return ResultadoOperacao.Falha("Modelo de etiqueta esta inativo. Selecione um modelo ativo.");

        return null;
    }
}


