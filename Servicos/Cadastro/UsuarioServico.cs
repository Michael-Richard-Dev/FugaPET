using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class UsuarioServico
{
    private const int TamanhoMinimoSenha = 6;
    private const string Entidade = "USUARIO";
    private const string Tela = "CadastroUsuarioForm";

    private readonly UsuarioRepositorio _usuarioRepositorio;
    private readonly SenhaServico _senhaServico;
    private readonly AuditoriaServico _auditoriaServico;

    public UsuarioServico(
        UsuarioRepositorio usuarioRepositorio,
        SenhaServico senhaServico,
        AuditoriaServico auditoriaServico)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _senhaServico = senhaServico;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<UsuarioCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _usuarioRepositorio.ListarAsync(cancellationToken);

    public Task<UsuarioCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioRepositorio.ObterPorIdAsync(id, cancellationToken);

    public Task<UsuarioEdicaoAgregado?> ObterEdicaoAgregadaAsync(
        long id,
        CancellationToken cancellationToken = default)
        => _usuarioRepositorio.ObterEdicaoAgregadaAsync(id, cancellationToken);

    /// <summary>
    /// Cadastra o usuario recebendo a senha em TEXTO PURO. O hash BCrypt e gerado
    /// aqui, no servico, via SenhaServico. A camada de tela nunca deve montar SenhaHash.
    /// </summary>
    public async Task<ResultadoOperacao> InserirAsync(UsuarioCadastro usuario, string senhaPlana, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        try
        {
            if (string.IsNullOrWhiteSpace(usuario.NomeUsuario))
                return ResultadoOperacao.Falha("Nome do usuario e obrigatorio.");

            if (string.IsNullOrWhiteSpace(usuario.LoginUsuario))
                return ResultadoOperacao.Falha("Login do usuario e obrigatorio.");

            if (string.IsNullOrWhiteSpace(senhaPlana))
                return ResultadoOperacao.Falha("Senha do usuario e obrigatoria.");

            if (senhaPlana.Length < TamanhoMinimoSenha)
                return ResultadoOperacao.Falha($"Senha deve ter ao menos {TamanhoMinimoSenha} caracteres.");

            usuario.NomeUsuario = usuario.NomeUsuario.Trim();
            usuario.LoginUsuario = usuario.LoginUsuario.Trim();

            // ExisteLoginAsync e GerarHash ficam DENTRO do try: qualquer falha inesperada
            // (ex.: banco indisponivel) vira ResultadoOperacao amigavel, nao excecao na tela.
            if (await _usuarioRepositorio.ExisteLoginAsync(usuario.LoginUsuario, null, cancellationToken))
                return ResultadoOperacao.Falha("Ja existe um usuario ativo com este login.");

            // Gera o hash BCrypt a partir da senha pura - nunca armazenar texto puro.
            usuario.SenhaHash = _senhaServico.GerarHash(senhaPlana);

            long id = await _usuarioRepositorio.InserirAsync(usuario, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o usuario.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Usuario '{usuario.LoginUsuario}'", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Usuario cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um usuario com este login ou e-mail.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> InserirComVinculosAsync(
        UsuarioCadastro usuario,
        string senhaPlana,
        long idPerfilAcesso,
        long idSetor,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Criar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (usuario.IdCargo is null or <= 0)
            return ResultadoOperacao.Falha("Cargo do usuario e obrigatorio.");

        if (usuario.IdSetorPadrao is null or <= 0)
            return ResultadoOperacao.Falha("Setor padrao do usuario e obrigatorio.");

        if (idPerfilAcesso <= 0)
            return ResultadoOperacao.Falha("Perfil de acesso do usuario e obrigatorio.");

        if (idSetor <= 0)
            return ResultadoOperacao.Falha("Setor vinculado do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(usuario.NomeUsuario))
            return ResultadoOperacao.Falha("Nome do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(usuario.LoginUsuario))
            return ResultadoOperacao.Falha("Login do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(senhaPlana))
            return ResultadoOperacao.Falha("Senha do usuario e obrigatoria.");

        if (senhaPlana.Length < TamanhoMinimoSenha)
            return ResultadoOperacao.Falha($"Senha deve ter ao menos {TamanhoMinimoSenha} caracteres.");

        usuario.NomeUsuario = usuario.NomeUsuario.Trim();
        usuario.LoginUsuario = usuario.LoginUsuario.Trim();

        try
        {
            // ExisteLoginAsync (acesso a banco) dentro do try: falha inesperada vira falha amigavel.
            if (await _usuarioRepositorio.ExisteLoginAsync(usuario.LoginUsuario, null, cancellationToken))
                return ResultadoOperacao.Falha("Ja existe um usuario ativo com este login.");

            usuario.SenhaHash = _senhaServico.GerarHash(senhaPlana);
            usuario.IdSetorPadrao = idSetor;

            long id = await _usuarioRepositorio.InserirComVinculosAsync(usuario, idPerfilAcesso, idSetor, cancellationToken);
            if (id <= 0) return ResultadoOperacao.Falha("Nao foi possivel cadastrar o usuario.");

            await _auditoriaServico.RegistrarCadastroCriadoAsync(Entidade, id, $"Usuario '{usuario.LoginUsuario}' com perfil e setor padrao", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Usuario cadastrado com sucesso.", id);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um usuario com este login, e-mail, perfil ou setor vinculado.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarAsync(UsuarioCadastro usuario, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (usuario.IdUsuario <= 0)
            return ResultadoOperacao.Falha("Id do usuario invalido para edicao.");

        if (string.IsNullOrWhiteSpace(usuario.NomeUsuario))
            return ResultadoOperacao.Falha("Nome do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(usuario.LoginUsuario))
            return ResultadoOperacao.Falha("Login do usuario e obrigatorio.");

        usuario.NomeUsuario = usuario.NomeUsuario.Trim();
        usuario.LoginUsuario = usuario.LoginUsuario.Trim();

        try
        {
            // ExisteLoginAsync e ObterPorIdAsync (acesso a banco) dentro do try.
            if (await _usuarioRepositorio.ExisteLoginAsync(usuario.LoginUsuario, usuario.IdUsuario, cancellationToken))
                return ResultadoOperacao.Falha("Ja existe outro usuario ativo com este login.");

            UsuarioCadastro? anterior = await _usuarioRepositorio.ObterPorIdAsync(usuario.IdUsuario, cancellationToken);
            if (anterior is null)
                return ResultadoOperacao.Falha("Usuario nao encontrado para edicao.");

            int atualizados = await _usuarioRepositorio.AtualizarAsync(usuario, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado para edicao.");

            string descricao = $"Usuario '{usuario.LoginUsuario}'";
            if (anterior.SituacaoUsuario && !usuario.SituacaoUsuario)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Usuario inativado com sucesso.");
            }

            if (!anterior.SituacaoUsuario && usuario.SituacaoUsuario)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Usuario reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um usuario com este login ou e-mail.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AtualizarComVinculosAsync(
        UsuarioCadastro usuario,
        long idPerfilAcesso,
        long idSetor,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (usuario.IdUsuario <= 0)
            return ResultadoOperacao.Falha("Id do usuario invalido para edicao.");

        if (usuario.IdCargo is null or <= 0)
            return ResultadoOperacao.Falha("Cargo do usuario e obrigatorio.");

        if (idSetor <= 0)
            return ResultadoOperacao.Falha("Setor padrao do usuario e obrigatorio.");

        if (idPerfilAcesso <= 0)
            return ResultadoOperacao.Falha("Perfil de acesso do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(usuario.NomeUsuario))
            return ResultadoOperacao.Falha("Nome do usuario e obrigatorio.");

        if (string.IsNullOrWhiteSpace(usuario.LoginUsuario))
            return ResultadoOperacao.Falha("Login do usuario e obrigatorio.");

        usuario.NomeUsuario = usuario.NomeUsuario.Trim();
        usuario.LoginUsuario = usuario.LoginUsuario.Trim();
        usuario.IdSetorPadrao = idSetor;

        try
        {
            // ExisteLoginAsync e ObterPorIdAsync (acesso a banco) dentro do try.
            if (await _usuarioRepositorio.ExisteLoginAsync(usuario.LoginUsuario, usuario.IdUsuario, cancellationToken))
                return ResultadoOperacao.Falha("Ja existe outro usuario ativo com este login.");

            UsuarioCadastro? anterior = await _usuarioRepositorio.ObterPorIdAsync(usuario.IdUsuario, cancellationToken);
            if (anterior is null)
                return ResultadoOperacao.Falha("Usuario nao encontrado para edicao.");

            int atualizados = await _usuarioRepositorio.AtualizarComVinculosAsync(usuario, idPerfilAcesso, idSetor, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado para edicao.");

            string descricao = $"Usuario '{usuario.LoginUsuario}' com perfil e setor padrao";
            if (anterior.SituacaoUsuario && !usuario.SituacaoUsuario)
            {
                await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Usuario inativado com sucesso.");
            }

            if (!anterior.SituacaoUsuario && usuario.SituacaoUsuario)
            {
                await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
                return ResultadoOperacao.Ok("Usuario reativado com sucesso.");
            }

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, usuario.IdUsuario, descricao, Tela, cancellationToken);
            return ResultadoOperacao.Ok("Edicao concluida com sucesso.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return ResultadoOperacao.Falha("Ja existe um usuario com este login, e-mail, perfil ou setor vinculado.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return ResultadoOperacao.Falha("Cargo, perfil ou setor informado nao existe.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    /// <summary>
    /// Redefine a senha do usuario. Recebe senha em texto puro; gera o hash via SenhaServico.
    /// </summary>
    public async Task<ResultadoOperacao> AlterarSenhaAsync(long idUsuario, string novaSenhaPlana, bool exigirTrocaNoProximoLogin = false, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (idUsuario <= 0)
            return ResultadoOperacao.Falha("Id do usuario invalido para alteracao de senha.");

        if (string.IsNullOrWhiteSpace(novaSenhaPlana))
            return ResultadoOperacao.Falha("Nova senha e obrigatoria.");

        if (novaSenhaPlana.Length < TamanhoMinimoSenha)
            return ResultadoOperacao.Falha($"Senha deve ter ao menos {TamanhoMinimoSenha} caracteres.");

        string hash = _senhaServico.GerarHash(novaSenhaPlana);

        try
        {
            int atualizados = await _usuarioRepositorio.AtualizarSenhaAsync(idUsuario, hash, exigirTrocaNoProximoLogin, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado para alteracao de senha.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, idUsuario, "Senha redefinida", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Senha alterada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> AlterarSenhaPropriaAsync(long idUsuario, string novaSenhaPlana, CancellationToken cancellationToken = default)
    {
        long? usuarioLogado = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (!usuarioLogado.HasValue || usuarioLogado.Value != idUsuario)
            return ResultadoOperacao.Falha("Voce só pode alterar a senha do proprio usuario logado.");

        if (idUsuario <= 0)
            return ResultadoOperacao.Falha("Id do usuario invalido para alteracao de senha.");

        if (string.IsNullOrWhiteSpace(novaSenhaPlana))
            return ResultadoOperacao.Falha("Nova senha e obrigatoria.");

        if (novaSenhaPlana.Length < TamanhoMinimoSenha)
            return ResultadoOperacao.Falha($"Senha deve ter ao menos {TamanhoMinimoSenha} caracteres.");

        string hash = _senhaServico.GerarHash(novaSenhaPlana);

        try
        {
            int atualizados = await _usuarioRepositorio.AtualizarSenhaAsync(idUsuario, hash, deveTrocarSenha: false, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado para alteracao de senha.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, idUsuario, "Senha alterada pelo proprio usuario", "TrocaSenhaObrigatoriaForm", cancellationToken);
            return ResultadoOperacao.Ok("Senha alterada com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, "TrocaSenhaObrigatoriaForm", cancellationToken);
        }
    }
    public Task<ResultadoOperacao> AlterarSenhaAsync(long idUsuario, string novaSenhaPlana, CancellationToken cancellationToken = default)
        => AlterarSenhaAsync(idUsuario, novaSenhaPlana, false, cancellationToken);

    public async Task<ResultadoOperacao> BloquearAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Bloquear, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do usuario invalido para bloqueio.");

        long? operador = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (operador.HasValue && operador.Value == id)
            return ResultadoOperacao.Falha("Voce nao pode bloquear o proprio usuario em uso.");

        try
        {
            int atualizados = await _usuarioRepositorio.BloquearAsync(id, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado ou ja estava bloqueado.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, id, "Usuario bloqueado", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Usuario bloqueado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> DesbloquearAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Desbloquear, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do usuario invalido para desbloqueio.");

        try
        {
            int atualizados = await _usuarioRepositorio.DesbloquearAsync(id, cancellationToken);
            if (atualizados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado ou ja estava desbloqueado.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, id, "Usuario desbloqueado", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Usuario desbloqueado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Excluir, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do usuario invalido para exclusao.");

        // Impede o usuario logado de inativar a si mesmo.
        long? operador = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (operador.HasValue && operador.Value == id)
            return ResultadoOperacao.Falha("Voce nao pode inativar o proprio usuario em uso.");

        try
        {
            int excluidos = await _usuarioRepositorio.ExcluirAsync(id, cancellationToken);
            if (excluidos <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado ou ja estava inativo.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Usuario inativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        ResultadoOperacao? bloqueio = await AutorizacaoCadastroServico.BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Editar, _auditoriaServico, Tela, cancellationToken);
        if (bloqueio is not null) return bloqueio;

        if (id <= 0) return ResultadoOperacao.Falha("Id do usuario invalido para reativacao.");

        try
        {
            int reativados = await _usuarioRepositorio.ReativarAsync(id, cancellationToken);
            if (reativados <= 0) return ResultadoOperacao.Falha("Usuario nao encontrado ou ja estava ativo.");

            await _auditoriaServico.RegistrarCadastroReativadoAsync(Entidade, id, tela: Tela, cancellationToken: cancellationToken);
            return ResultadoOperacao.Ok("Usuario reativado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}

