using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class PermissaoServico
{
    private const string Entidade = "PERMISSAO";
    private const string Tela = "PermissaoForm";

    private readonly PermissaoRepositorio _permissaoRepositorio;
    private readonly PerfilPermissaoRepositorio _perfilPermissaoRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public PermissaoServico(
        PermissaoRepositorio permissaoRepositorio,
        PerfilPermissaoRepositorio perfilPermissaoRepositorio,
        AuditoriaServico auditoriaServico)
    {
        _permissaoRepositorio = permissaoRepositorio;
        _perfilPermissaoRepositorio = perfilPermissaoRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<PermissaoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _permissaoRepositorio.ListarAsync(cancellationToken);

    public Task<IReadOnlyList<long>> ListarCodigosPermissaoPorPerfilAsync(
        long codigoPerfil,
        CancellationToken cancellationToken = default)
        => _perfilPermissaoRepositorio.ListarCodigosPermissaoPorPerfilAsync(codigoPerfil, cancellationToken);

    public Task<PermissaoCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _permissaoRepositorio.ObterPorIdAsync(id, cancellationToken);

    public async Task<ResultadoOperacao> InserirAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        ResultadoOperacao? validacao = ValidarBasico(permissao);
        if (validacao is not null) return validacao;

        NormalizarCampos(permissao);

        try
        {
            if (await _permissaoRepositorio.ExisteCombinacaoAsync(
                    permissao.ModuloPermissao, permissao.RotinaPermissao, permissao.AcaoPermissao, null, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe uma permissao ativa com esta combinacao de modulo, rotina e acao.");
            }

            long id = await _permissaoRepositorio.InserirAsync(permissao, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar a permissao.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(
                Entidade, id, DescricaoPermissao(permissao), Tela, cancellationToken);
            return ResultadoOperacao.Ok("Permissao cadastrada com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe uma permissao com esta combinacao.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (permissao.IdPermissao <= 0)
        {
            return ResultadoOperacao.Falha("Id da permissao invalido para edicao.");
        }

        ResultadoOperacao? validacao = ValidarBasico(permissao);
        if (validacao is not null) return validacao;

        NormalizarCampos(permissao);

        try
        {
            if (await _permissaoRepositorio.ExisteCombinacaoAsync(
                    permissao.ModuloPermissao, permissao.RotinaPermissao, permissao.AcaoPermissao, permissao.IdPermissao, cancellationToken))
            {
                return ResultadoOperacao.Falha("Ja existe outra permissao ativa com esta combinacao.");
            }

            PermissaoCadastro? anterior = await _permissaoRepositorio.ObterPorIdAsync(permissao.IdPermissao, cancellationToken);
            if (anterior is null)
            {
                return ResultadoOperacao.Falha("Permissao nao encontrada para edicao.");
            }

            int atualizados = await _permissaoRepositorio.AtualizarAsync(permissao, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Permissao nao encontrada para edicao.");

            string descricao = DescricaoPermissao(permissao);
            if (anterior.SituacaoPermissao && !permissao.SituacaoPermissao)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, permissao.IdPermissao, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Permissao inativada com sucesso.");
            }

            if (!anterior.SituacaoPermissao && permissao.SituacaoPermissao)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, permissao.IdPermissao, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Permissao reativada com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, permissao.IdPermissao, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe uma permissao com esta combinacao.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id da permissao invalido para exclusao.");

        try
        {
            PermissaoCadastro? permissao = await _permissaoRepositorio.ObterPorIdAsync(id, cancellationToken);
            if (permissao is null) return ResultadoOperacao.Falha("Permissao nao encontrada.");

            int excluidos = await _permissaoRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Permissao nao encontrada ou ja estava inativa.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, DescricaoPermissao(permissao), Tela, cancellationToken);
            return ResultadoOperacao.Ok("Permissao inativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id da permissao invalido.");

        try
        {
            int reativados = await _permissaoRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Permissao nao encontrada ou ja estava ativa.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Permissao reativada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> SincronizarPermissoesDoPerfilAsync(
        long codigoPerfil,
        IReadOnlyCollection<long> permissoesSelecionadas,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Gerenciar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (codigoPerfil <= 0)
        {
            return ResultadoOperacao.Falha("Selecione um perfil de acesso para salvar as permissoes.");
        }

        try
        {
            long[] permissoesValidas = permissoesSelecionadas
                .Where(codigo => codigo > 0)
                .Distinct()
                .ToArray();

            await _perfilPermissaoRepositorio.SincronizarPermissoesAsync(codigoPerfil, permissoesValidas, cancellationToken);
            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(
                "PERFIL_PERMISSAO",
                codigoPerfil,
                $"Permissoes sincronizadas: {permissoesValidas.Length}",
                Tela,
                cancellationToken);

            return ResultadoOperacao.Ok("Permissoes do perfil salvas com sucesso.");
        }
        catch (RemocaoPermissaoAdministrativaEssencialException)
        {
            // Tratado por TIPO (nao por ex.Message). A mensagem amigavel vem da constante canonica.
            string mensagem = PerfilPermissaoRepositorio.MensagemProtecaoPerfilAdministrador;
            long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
            if (codigoUsuario.HasValue)
            {
                await _auditoriaServico.RegistrarAcessoNegadoAsync(
                    codigoUsuario.Value,
                    mensagem,
                    Tela,
                    cancellationToken);
            }

            return ResultadoOperacao.Falha(mensagem);
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    private static ResultadoOperacao? ValidarBasico(PermissaoCadastro permissao)
    {
        if (string.IsNullOrWhiteSpace(permissao.ModuloPermissao))
            return ResultadoOperacao.Falha("Modulo da permissao e obrigatorio.");
        if (string.IsNullOrWhiteSpace(permissao.RotinaPermissao))
            return ResultadoOperacao.Falha("Rotina da permissao e obrigatoria.");
        if (string.IsNullOrWhiteSpace(permissao.AcaoPermissao))
            return ResultadoOperacao.Falha("Acao da permissao e obrigatoria.");
        return null;
    }

    private static void NormalizarCampos(PermissaoCadastro permissao)
    {
        permissao.ModuloPermissao = permissao.ModuloPermissao.Trim().ToUpperInvariant();
        permissao.RotinaPermissao = permissao.RotinaPermissao.Trim().ToUpperInvariant();
        permissao.AcaoPermissao = permissao.AcaoPermissao.Trim().ToUpperInvariant();
    }

    private static string DescricaoPermissao(PermissaoCadastro p)
        => $"{p.ModuloPermissao}.{p.RotinaPermissao}.{p.AcaoPermissao}";
}


