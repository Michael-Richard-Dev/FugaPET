using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class MapeamentoCampoEtiquetaServico
{
    private const string Entidade = PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta;
    private const string Tela = "EtiquetaForm";
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

    /// <summary>
    /// Define o mapeamento do campo. Como ha no maximo 1 ativo por campo (uq_mapeamento_campo_ativo),
    /// se ja existir um ativo este e atualizado; senao cria um novo.
    /// </summary>
    public async Task<ResultadoOperacao> SalvarAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
    {
        try
        {
            ResultadoOperacao? validacao = ValidarBasico(mapa);
            if (validacao is not null) return validacao;

            mapa.OrigemDado = mapa.OrigemDado.Trim().ToUpperInvariant();

            MapeamentoCampoEtiquetaCadastro? existente = await _repositorio.ObterAtivoPorCampoAsync(mapa.CodigoCampoEtiqueta, cancellationToken);
            string acaoNecessaria = existente is null ? PermissoesSistema.Acoes.Criar : PermissoesSistema.Acoes.Editar;

            ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, acaoNecessaria, _auditoriaServico, Tela, cancellationToken);
            if (bloqueio is not null) return bloqueio;

            if (existente is not null)
            {
                // Ja existe mapeamento ativo -> atualiza (mantem 1:1).
                mapa.CodigoMapeamentoCampoEtiqueta = existente.CodigoMapeamentoCampoEtiqueta;
                int atualizados = await _repositorio.AtualizarAsync(mapa, cancellationToken);
                if (atualizados <= 0) return ResultadoOperacao.Falha("Nao foi possivel atualizar o mapeamento.");

                await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, existente.CodigoMapeamentoCampoEtiqueta, $"Mapeamento do campo {mapa.CodigoCampoEtiqueta} ({mapa.OrigemDado})", Tela, cancellationToken);
                return ResultadoOperacao.Ok("Mapeamento atualizado com sucesso.", existente.CodigoMapeamentoCampoEtiqueta);
            }

            long id = await _repositorio.InserirAsync(mapa, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o mapeamento.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Mapeamento do campo {mapa.CodigoCampoEtiqueta} ({mapa.OrigemDado})", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Mapeamento cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um mapeamento ativo para este campo.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return ResultadoOperacao.Falha("Campo de etiqueta informado nao existe.");
        }
        catch (Exception ex)
        {
            // Qualquer excecao inesperada e tratada de forma padronizada: audita o detalhe tecnico
            // e devolve mensagem amigavel, sem deixar a excecao subir para a tela.
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPodeGerenciarEtiquetaAsync(Entidade, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do mapeamento invalido.");

        try
        {
            int excluidos = await _repositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Mapeamento nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Mapeamento inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(MapeamentoCampoEtiquetaCadastro mapa)
    {
        if (mapa.CodigoCampoEtiqueta <= 0)
            return ResultadoOperacao.Falha("Campo do mapeamento e obrigatorio.");
        if (string.IsNullOrWhiteSpace(mapa.OrigemDado))
            return ResultadoOperacao.Falha("Origem do dado e obrigatoria.");
        if (!OrigensValidas.Contains(mapa.OrigemDado.Trim().ToUpperInvariant()))
            return ResultadoOperacao.Falha($"Origem invalida. Use: {string.Join(", ", OrigensValidas)}.");
        return null;
    }
}


