using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class CampoEtiquetaServico
{
    public const string MensagemNovaDeveSerAtiva =
        "Novo campo deve ser cadastrado como Ativo. Utilize a ação Inativar após o cadastro, quando necessário.";

    public const string MensagemDuplicidadeGlobal =
        "Já existe um campo com este nome nesta etiqueta, mesmo que esteja inativo. Localize o registro existente e utilize a ação Reativar.";

    public const string MensagemInativarPelaAcao =
        "A inativação do campo deve ser feita pela ação Inativar.";

    public const string MensagemReativarPelaAcao =
        "A reativação do campo deve ser feita pela ação Reativar.";

    private const string Entidade = PermissoesSistema.Rotinas.CampoEtiqueta;
    private const string Tela = "CamposEtiquetaForm";
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

    public Task<CampoEtiquetaEdicaoAgregado?> ObterEdicaoAgregadaAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterEdicaoAgregadaAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (!campo.SituacaoCampoEtiqueta)
            return ResultadoOperacao.Falha(MensagemNovaDeveSerAtiva);

        Normalizar(campo);
        ResultadoOperacao? validacao = ValidarBasico(campo);
        if (validacao is not null) return validacao;

        try
        {
            if (!await _repositorio.EtiquetaEstaAtivaAsync(campo.CodigoEtiqueta, cancellationToken))
                return ResultadoOperacao.Falha("Etiqueta informada não existe ou está inativa.");

            if (await _repositorio.ExisteNomeNaEtiquetaAsync(campo.CodigoEtiqueta, campo.NomeCampo, null, cancellationToken))
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);

            long id = await _repositorio.InserirAsync(campo, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Não foi possível cadastrar o campo.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Campo '{campo.NomeCampo}' (etiqueta {campo.CodigoEtiqueta})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Campo cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return ResultadoOperacao.Falha("Etiqueta informada não existe.");
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
            return ResultadoOperacao.Falha("Identificador do campo inválido para edição.");

        Normalizar(campo);
        ResultadoOperacao? validacao = ValidarBasico(campo);
        if (validacao is not null) return validacao;

        try
        {
            CampoEtiquetaCadastro? anterior = await _repositorio.ObterPorIdAsync(campo.CodigoCampoEtiqueta, cancellationToken);
            if (anterior is null) return ResultadoOperacao.Falha("Campo não encontrado para edição.");

            if (anterior.CodigoEtiqueta != campo.CodigoEtiqueta)
                return ResultadoOperacao.Falha("Não é permitido alterar a etiqueta do campo.");

            if (anterior.SituacaoCampoEtiqueta && !campo.SituacaoCampoEtiqueta)
                return ResultadoOperacao.Falha(MensagemInativarPelaAcao);

            if (!anterior.SituacaoCampoEtiqueta && campo.SituacaoCampoEtiqueta)
                return ResultadoOperacao.Falha(MensagemReativarPelaAcao);

            if (!await _repositorio.EtiquetaEstaAtivaAsync(anterior.CodigoEtiqueta, cancellationToken))
                return ResultadoOperacao.Falha("Etiqueta vinculada ao campo não existe ou está inativa.");

            if (await _repositorio.ExisteNomeNaEtiquetaAsync(anterior.CodigoEtiqueta, campo.NomeCampo, campo.CodigoCampoEtiqueta, cancellationToken))
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);

            campo.CodigoEtiqueta = anterior.CodigoEtiqueta;
            campo.SituacaoCampoEtiqueta = anterior.SituacaoCampoEtiqueta;

            int atualizados = await _repositorio.AtualizarAsync(campo, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Campo não encontrado para edição.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, campo.CodigoCampoEtiqueta, $"Campo '{campo.NomeCampo}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edição concluída com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);
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

        if (id <= 0) return ResultadoOperacao.Falha("Identificador do campo inválido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Campo não encontrado ou já estava inativo.");

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

        if (id <= 0) return ResultadoOperacao.Falha("Identificador do campo inválido.");

        try
        {
            CampoEtiquetaCadastro? campo = await _repositorio.ObterPorIdAsync(id, cancellationToken);
            if (campo is null) return ResultadoOperacao.Falha("Campo não encontrado para reativação.");
            if (campo.SituacaoCampoEtiqueta) return ResultadoOperacao.Falha("Campo já está ativo.");

            if (!await _repositorio.EtiquetaEstaAtivaAsync(campo.CodigoEtiqueta, cancellationToken))
                return ResultadoOperacao.Falha("Não é possível reativar o campo porque a etiqueta vinculada está inativa.");

            if (await _repositorio.ExisteNomeNaEtiquetaAsync(campo.CodigoEtiqueta, campo.NomeCampo, id, cancellationToken))
                return ResultadoOperacao.Falha(MensagemDuplicidadeGlobal);

            int reativados = await _repositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Não foi possível reativar o campo. Verifique a etiqueta vinculada e duplicidade de nome.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Campo reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static void Normalizar(CampoEtiquetaCadastro campo)
    {
        campo.NomeCampo = (campo.NomeCampo ?? string.Empty).Trim();
        campo.DescricaoCampoEtiqueta = (campo.DescricaoCampoEtiqueta ?? string.Empty).Trim();
        campo.TipoDado = (campo.TipoDado ?? string.Empty).Trim().ToUpperInvariant();
        campo.FormatoSaida = (campo.FormatoSaida ?? string.Empty).Trim();
    }

    private static ResultadoOperacao? ValidarBasico(CampoEtiquetaCadastro campo)
    {
        if (campo.CodigoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Etiqueta do campo é obrigatória.");

        if (string.IsNullOrWhiteSpace(campo.NomeCampo)
            || campo.NomeCampo.Length < CampoEtiquetaCadastro.TamanhoMinimoNome
            || campo.NomeCampo.Length > CampoEtiquetaCadastro.TamanhoMaximoNome)
        {
            return ResultadoOperacao.Falha("Nome do campo deve ter entre 2 e 80 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(campo.TipoDado))
            return ResultadoOperacao.Falha("Tipo de dado do campo é obrigatório.");

        if (!TiposDadoValidos.Contains(campo.TipoDado.Trim().ToUpperInvariant()))
            return ResultadoOperacao.Falha($"Tipo de dado inválido. Use: {string.Join(", ", TiposDadoValidos)}.");

        if (campo.Ordem <= 0)
            return ResultadoOperacao.Falha("Ordem deve ser maior que zero.");

        if (campo.TamanhoMaximo.HasValue && campo.TamanhoMaximo.Value <= 0)
            return ResultadoOperacao.Falha("Tamanho máximo deve ser maior que zero quando informado.");

        if (campo.DescricaoCampoEtiqueta.Length > CampoEtiquetaCadastro.TamanhoMaximoDescricao)
            return ResultadoOperacao.Falha("Descrição do campo deve ter no máximo 255 caracteres.");

        if (campo.FormatoSaida.Length > CampoEtiquetaCadastro.TamanhoMaximoFormatoSaida)
            return ResultadoOperacao.Falha("Formato de saída deve ter no máximo 100 caracteres.");

        return null;
    }
}