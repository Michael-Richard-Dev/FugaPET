using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class MapeamentoCampoEtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta;
    private const string Tela = "CamposEtiquetaForm";
    private static readonly string[] OrigensValidas =
        ["SISTEMA", "SAP", "USUARIO", "CALCULADO", "BALANCA", "FIXO"];

    private readonly MapeamentoCampoEtiquetaRepositorio _repositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public MapeamentoCampoEtiquetaServico(MapeamentoCampoEtiquetaRepositorio repositorio, AuditoriaServico auditoriaServico)
    {
        _repositorio = repositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<MapeamentoCampoEtiquetaCadastro?> ObterAtivoPorCampoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
        => _repositorio.ObterAtivoPorCampoAsync(codigoCampoEtiqueta, cancellationToken);

    public Task<MapeamentoCampoEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> SalvarAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
    {
        try
        {
            Normalizar(mapa);
            ResultadoOperacao? validacao = ValidarBasico(mapa);
            if (validacao is not null) return validacao;

            if (!mapa.SituacaoMapeamentoCampoEtiqueta)
                return ResultadoOperacao.Falha("Mapeamento deve ser salvo como Ativo. Utilize a ação Inativar Mapeamento para remover o vínculo ativo.");

            if (!await _repositorio.CampoEstaAtivoAsync(mapa.CodigoCampoEtiqueta, cancellationToken))
                return ResultadoOperacao.Falha("Campo de etiqueta informado não existe ou está inativo.");

            MapeamentoCampoEtiquetaCadastro? existente = await _repositorio.ObterAtivoPorCampoAsync(mapa.CodigoCampoEtiqueta, cancellationToken);
            string acaoNecessaria = existente is null ? PermissoesSistema.Acoes.Criar : PermissoesSistema.Acoes.Editar;

            ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, acaoNecessaria, _auditoriaServico, Tela, cancellationToken);
            if (bloqueio is not null) return bloqueio;

            if (existente is not null)
            {
                mapa.CodigoMapeamentoCampoEtiqueta = existente.CodigoMapeamentoCampoEtiqueta;
                mapa.SituacaoMapeamentoCampoEtiqueta = true;

                int atualizados = await _repositorio.AtualizarAsync(mapa, cancellationToken);
                if (atualizados <= 0) return ResultadoOperacao.Falha("Não foi possível atualizar o mapeamento.");

                await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, existente.CodigoMapeamentoCampoEtiqueta, $"Mapeamento do campo {mapa.CodigoCampoEtiqueta} ({mapa.OrigemDado})", Tela, cancellationToken);
                return ResultadoOperacao.Ok("Mapeamento atualizado com sucesso.", existente.CodigoMapeamentoCampoEtiqueta);
            }

            long id = await _repositorio.InserirAsync(mapa, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Não foi possível cadastrar o mapeamento.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Mapeamento do campo {mapa.CodigoCampoEtiqueta} ({mapa.OrigemDado})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Mapeamento cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Já existe um mapeamento ativo para este campo.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return ResultadoOperacao.Falha("Campo de etiqueta informado não existe.");
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

        if (id <= 0) return ResultadoOperacao.Falha("Identificador do mapeamento inválido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Mapeamento não encontrado ou já estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Mapeamento inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static void Normalizar(MapeamentoCampoEtiquetaCadastro mapa)
    {
        mapa.OrigemDado = (mapa.OrigemDado ?? string.Empty).Trim().ToUpperInvariant();
        mapa.ExpressaoOrigem = (mapa.ExpressaoOrigem ?? string.Empty).Trim();
        mapa.ValorPadrao = mapa.ValorPadrao ?? string.Empty;
        mapa.Observacao = (mapa.Observacao ?? string.Empty).Trim();
    }

    private static ResultadoOperacao? ValidarBasico(MapeamentoCampoEtiquetaCadastro mapa)
    {
        if (mapa.CodigoCampoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Campo do mapeamento é obrigatório.");

        if (string.IsNullOrWhiteSpace(mapa.OrigemDado))
            return ResultadoOperacao.Falha("Origem do dado é obrigatória.");

        if (!OrigensValidas.Contains(mapa.OrigemDado.Trim().ToUpperInvariant()))
            return ResultadoOperacao.Falha($"Origem inválida. Use: {string.Join(", ", OrigensValidas)}.");

        if (mapa.Observacao.Length > MapeamentoCampoEtiquetaCadastro.TamanhoMaximoObservacao)
            return ResultadoOperacao.Falha("Observação do mapeamento deve ter no máximo 255 caracteres.");

        return null;
    }
}