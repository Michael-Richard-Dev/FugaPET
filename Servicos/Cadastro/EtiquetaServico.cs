using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class EtiquetaServico
{
    public const string MensagemDuplicidadeGlobal =
        "Já existe uma etiqueta com este código interno, mesmo que esteja inativa. Localize o registro existente e utilize a ação Reativar.";
    public const string MensagemInativarPelaAcao =
        "A inativação da etiqueta deve ser feita pela ação Inativar.";
    public const string MensagemReativarPelaAcao =
        "A reativação da etiqueta deve ser feita pela ação Reativar.";
    public const string MensagemModeloInativoReativacao =
        "Não é possível reativar esta etiqueta porque o modelo vinculado está inativo.";
    public const string MensagemDependenciaProduto =
        "Não é possível inativar esta etiqueta porque existem produtos ativos vinculados a ela.";
    public const string MensagemNovaDeveSerAtiva =
        "Nova etiqueta deve ser cadastrada como Ativa. Utilize a ação Inativar após o cadastro, quando necessário.";

    private const string Entidade = PermissoesSistema.Rotinas.Etiqueta;
    private const string Tela = "EtiquetaForm";
    private static readonly HashSet<string> TiposValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "CAIXA", "PALETE", "HU", "INTERNA", "OUTRA"
    };

    private readonly EtiquetaRepositorio _etiquetaRepositorio;
    private readonly Func<long, CancellationToken, Task<ModeloEtiquetaCadastro?>> _obterModeloPorIdAsync;
    private readonly AuditoriaServico _auditoriaServico;

    public EtiquetaServico(
        EtiquetaRepositorio etiquetaRepositorio,
        ModeloEtiquetaRepositorio modeloEtiquetaRepositorio,
        AuditoriaServico auditoriaServico)
        : this(etiquetaRepositorio, modeloEtiquetaRepositorio.ObterPorIdAsync, auditoriaServico)
    {
    }

    public EtiquetaServico(
        EtiquetaRepositorio etiquetaRepositorio,
        Func<long, CancellationToken, Task<ModeloEtiquetaCadastro?>> obterModeloPorIdAsync,
        AuditoriaServico auditoriaServico)
    {
        _etiquetaRepositorio = etiquetaRepositorio;
        _obterModeloPorIdAsync = obterModeloPorIdAsync;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<EtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _etiquetaRepositorio.ListarAsync(cancellationToken);

    public Task<EtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaRepositorio.ObterPorIdAsync(id, cancellationToken);

    public Task<ResumoDependenciasEtiqueta> ObterResumoDependenciasAtivasAsync(
        long id,
        CancellationToken cancellationToken = default)
        => _etiquetaRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);

    public async Task<bool> ExisteCodigoInternoAsync(
        string codigoInterno,
        long? ignorarCodigo = null,
        CancellationToken cancellationToken = default)
    {
        string codigoNormalizado = (codigoInterno ?? string.Empty).Trim();
        return codigoNormalizado.Length > 0
            && await _etiquetaRepositorio.ExisteCodigoInternoAsync(codigoNormalizado, ignorarCodigo, cancellationToken);
    }

    public async Task<ResultadoOperacao> InserirAsync(
        EtiquetaCadastro etiqueta,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await ValidarPermissaoAsync(PermissoesSistema.Acoes.Criar, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        try
        {
            if (!etiqueta.SituacaoEtiqueta)
                return ResultadoOperacao.Falha(MensagemNovaDeveSerAtiva);

            Normalizar(etiqueta);

            ResultadoOperacao? validacao = await ValidarDadosAsync(etiqueta, cancellationToken);
            if (validacao is not null) return validacao;

            if (await _etiquetaRepositorio.ExisteCodigoInternoAsync(etiqueta.CodigoInterno, null, cancellationToken))
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);

            long id = await _etiquetaRepositorio.InserirAsync(etiqueta, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Não foi possível cadastrar a etiqueta.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(
                Entidade,
                id,
                Descrever(etiqueta),
                Tela,
                cancellationToken);

            return ResultadoOperacao.Ok("Etiqueta cadastrada com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RegistrarErroAsync("ETIQUETA_ERRO_DUPLICIDADE", ex, cancellationToken);
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            await RegistrarErroAsync("ETIQUETA_ERRO_MODELO", ex, cancellationToken);
            return ResultadoOperacao.Falha("Modelo de etiqueta informado não existe.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(
                _auditoriaServico, "ETIQUETA_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(
        EtiquetaCadastro etiqueta,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await ValidarPermissaoAsync(PermissoesSistema.Acoes.Editar, cancellationToken);
        if (bloqueio is not null) return bloqueio;
        if (etiqueta.CodigoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Identificador da etiqueta inválido para edição.");

        try
        {
            EtiquetaCadastro? anterior = await _etiquetaRepositorio.ObterPorIdAsync(etiqueta.CodigoEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Etiqueta não encontrada para edição.");
            if (anterior.SituacaoEtiqueta && !etiqueta.SituacaoEtiqueta)
                return ResultadoOperacao.Falha(MensagemInativarPelaAcao);
            if (!anterior.SituacaoEtiqueta && etiqueta.SituacaoEtiqueta)
                return ResultadoOperacao.Falha(MensagemReativarPelaAcao);

            Normalizar(etiqueta);
            ResultadoOperacao? validacao = await ValidarDadosAsync(etiqueta, cancellationToken);
            if (validacao is not null) return validacao;

            if (await _etiquetaRepositorio.ExisteCodigoInternoAsync(
                    etiqueta.CodigoInterno,
                    etiqueta.CodigoEtiqueta,
                    cancellationToken))
            {
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
            }

            int atualizados = await _etiquetaRepositorio.AtualizarAsync(etiqueta, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Etiqueta não encontrada para edição.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(
                Entidade,
                etiqueta.CodigoEtiqueta,
                Descrever(etiqueta),
                Tela,
                cancellationToken);

            return ResultadoOperacao.Ok("Etiqueta atualizada com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await RegistrarErroAsync("ETIQUETA_ERRO_DUPLICIDADE", ex, cancellationToken);
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(
                _auditoriaServico, "ETIQUETA_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await ValidarPermissaoAsync(PermissoesSistema.Acoes.Excluir, cancellationToken);
        if (bloqueio is not null) return bloqueio;
        if (id <= 0) return ResultadoOperacao.Falha("Identificador da etiqueta inválido para inativação.");

        try
        {
            EtiquetaCadastro? etiqueta = await _etiquetaRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (etiqueta is null || !etiqueta.SituacaoEtiqueta)
                return ResultadoOperacao.Falha("Etiqueta não encontrada ou já estava inativa.");

            ResumoDependenciasEtiqueta dependencias =
                await _etiquetaRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
            if (dependencias.PossuiDependenciasAtivas)
                return ResultadoOperacao.Falha(MensagemDependenciaProduto);

            int inativados = await _etiquetaRepositorio.ExcluirAsync(id, cancellationToken);
            if (inativados <= 0)
            {
                ResumoDependenciasEtiqueta dependenciasConcorrentes =
                    await _etiquetaRepositorio.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
                return dependenciasConcorrentes.PossuiDependenciasAtivas
                    ? ResultadoOperacao.Falha(MensagemDependenciaProduto)
                    : ResultadoOperacao.Falha("Etiqueta não encontrada ou já estava inativa.");
            }

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(
                Entidade, id, Descrever(etiqueta), Tela, cancellationToken);
            return ResultadoOperacao.Ok("Etiqueta inativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(
                _auditoriaServico, "ETIQUETA_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await ValidarPermissaoAsync(PermissoesSistema.Acoes.Editar, cancellationToken);
        if (bloqueio is not null) return bloqueio;
        if (id <= 0) return ResultadoOperacao.Falha("Identificador da etiqueta inválido para reativação.");

        try
        {
            EtiquetaCadastro? etiqueta = await _etiquetaRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (etiqueta is null || etiqueta.SituacaoEtiqueta)
                return ResultadoOperacao.Falha("Etiqueta não encontrada ou já estava ativa.");

            if (await _etiquetaRepositorio.ExisteCodigoInternoAsync(etiqueta.CodigoInterno, id, cancellationToken))
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);

            ModeloEtiquetaCadastro? modelo = await _obterModeloPorIdAsync(etiqueta.CodigoModeloEtiqueta, cancellationToken);
            if (modelo is null)
                return ResultadoOperacao.Falha("Não é possível reativar esta etiqueta porque o modelo vinculado não existe.");
            if (!modelo.SituacaoModeloEtiqueta)
                return ResultadoOperacao.Falha(MensagemModeloInativoReativacao);

            int reativados = await _etiquetaRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0)
                return ResultadoOperacao.Falha("Não foi possível reativar a etiqueta. Verifique o código interno e o modelo vinculado.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(
                Entidade, id, Descrever(etiqueta), Tela, cancellationToken);
            return ResultadoOperacao.Ok("Etiqueta reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(
                _auditoriaServico, "ETIQUETA_ERRO", ex, Tela, cancellationToken);
        }
    }

    private Task<ResultadoOperacao?> ValidarPermissaoAsync(string acao, CancellationToken cancellationToken)
        => AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(
            Entidade, acao, _auditoriaServico, Tela, cancellationToken);

    private async Task<ResultadoOperacao?> ValidarDadosAsync(
        EtiquetaCadastro etiqueta,
        CancellationToken cancellationToken)
    {
        if (etiqueta.CodigoInterno.Length == 0)
            return ResultadoOperacao.Falha("Código interno da etiqueta é obrigatório.");
        if (etiqueta.CodigoInterno.Length > EtiquetaCadastro.TamanhoMaximoCodigoInterno)
            return ResultadoOperacao.Falha("Código interno deve ter no máximo 80 caracteres.");
        if (etiqueta.NomeEtiqueta.Length < EtiquetaCadastro.TamanhoMinimoNome
            || etiqueta.NomeEtiqueta.Length > EtiquetaCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome da etiqueta deve ter entre 2 e 80 caracteres.");
        }
        if (!TiposValidos.Contains(etiqueta.TipoEtiqueta))
            return ResultadoOperacao.Falha("Tipo da etiqueta inválido. Selecione CAIXA, PALETE, HU, INTERNA ou OUTRA.");
        if (etiqueta.DescricaoEtiqueta.Length > EtiquetaCadastro.TamanhoMaximoDescricao)
            return ResultadoOperacao.Falha("Descrição da etiqueta deve ter no máximo 255 caracteres.");
        if (etiqueta.CodigoModeloEtiqueta <= 0)
            return ResultadoOperacao.Falha("Modelo da etiqueta é obrigatório.");

        ModeloEtiquetaCadastro? modelo = await _obterModeloPorIdAsync(etiqueta.CodigoModeloEtiqueta, cancellationToken);
        if (modelo is null)
            return ResultadoOperacao.Falha("Modelo de etiqueta informado não existe.");
        if (!modelo.SituacaoModeloEtiqueta)
            return ResultadoOperacao.Falha("Modelo de etiqueta está inativo. Selecione um modelo ativo.");

        return null;
    }

    private static void Normalizar(EtiquetaCadastro etiqueta)
    {
        etiqueta.CodigoInterno = (etiqueta.CodigoInterno ?? string.Empty).Trim();
        etiqueta.NomeEtiqueta = (etiqueta.NomeEtiqueta ?? string.Empty).Trim();
        etiqueta.TipoEtiqueta = (etiqueta.TipoEtiqueta ?? string.Empty).Trim().ToUpperInvariant();
        etiqueta.DescricaoEtiqueta = (etiqueta.DescricaoEtiqueta ?? string.Empty).Trim();
    }

    private static string Descrever(EtiquetaCadastro etiqueta)
        => $"Etiqueta '{etiqueta.NomeEtiqueta}' ({etiqueta.CodigoInterno})";

    private Task RegistrarErroAsync(string acao, Exception ex, CancellationToken cancellationToken)
        => _auditoriaServico.RegistrarErroAsync(acao, ex.ToString(), Tela, cancellationToken);
}
