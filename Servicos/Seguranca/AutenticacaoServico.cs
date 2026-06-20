using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.Seguranca;

public sealed class AutenticacaoServico
{
    private const string VariavelSenhaAdminLocal = "FUGAPET_DEV_ADMIN_LOCAL_SENHA";

    private readonly UsuarioRepositorio _usuarioRepositorio;
    private readonly UsuarioPerfilRepositorio _usuarioPerfilRepositorio;
    private readonly PerfilAcessoRepositorio _perfilAcessoRepositorio;
    private readonly PerfilPermissaoRepositorio _perfilPermissaoRepositorio;
    private readonly UsuarioSetorRepositorio _usuarioSetorRepositorio;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly SenhaServico _senhaServico;

    public AutenticacaoServico(
        UsuarioRepositorio usuarioRepositorio,
        UsuarioPerfilRepositorio usuarioPerfilRepositorio,
        PerfilAcessoRepositorio perfilAcessoRepositorio,
        PerfilPermissaoRepositorio perfilPermissaoRepositorio,
        UsuarioSetorRepositorio usuarioSetorRepositorio,
        AuditoriaServico auditoriaServico,
        SenhaServico senhaServico)
    {
        _usuarioRepositorio = usuarioRepositorio;
        _usuarioPerfilRepositorio = usuarioPerfilRepositorio;
        _perfilAcessoRepositorio = perfilAcessoRepositorio;
        _perfilPermissaoRepositorio = perfilPermissaoRepositorio;
        _usuarioSetorRepositorio = usuarioSetorRepositorio;
        _auditoriaServico = auditoriaServico;
        _senhaServico = senhaServico;
    }

    public async Task<ResultadoAutenticacao> AutenticarAsync(string login, string senha, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
        {
            return ResultadoAutenticacao.Falha("Informe usuario e senha.");
        }

        if (!EstadoIntegracaoBanco.Habilitado)
        {
            // Sem banco, o login local offline exige demonstracao segura e explicita.
            if (!EstadoIntegracaoBanco.PodeUsarDadosSimulados)
            {
                return ResultadoAutenticacao.Falha(
                    "Sistema sem conexao com o banco de dados. Login bloqueado — acione o suporte.");
            }

            if (login.Equals("admin", StringComparison.OrdinalIgnoreCase) && SenhaAdminLocalConfere(senha))
            {
                return ResultadoAutenticacao.Ok(new SessaoUsuarioAplicacao
                {
                    Login = "admin",
                    Nome = "Administrador Local",
                    PerfisCodigo = ["ADMIN"],
                    Permissoes = [],
                    IntegracaoBancoHabilitada = false,
                    DeveTrocarSenha = false
                });
            }

            return ResultadoAutenticacao.Falha("Usuario ou senha invalidos.");
        }

        Modelo.Cadastro.UsuarioCadastro? usuario = await _usuarioRepositorio.ObterPorLoginAsync(login, cancellationToken);
        if (usuario is null)
        {
            await _auditoriaServico.RegistrarLoginFalhaAsync(login, codigoUsuario: null, motivo: "Login inexistente.", cancellationToken);
            return ResultadoAutenticacao.Falha("Usuario ou senha invalidos.");
        }

        // Bloqueia se inativo ou bloqueado (situacao_usuario / bloqueado_usuario).
        if (!usuario.SituacaoUsuario)
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(usuario.IdUsuario, "Usuario inativo (situacao_usuario = false).", tela: "LoginForm", cancellationToken);
            return ResultadoAutenticacao.Falha("Usuario bloqueado ou inativo.");
        }

        if (usuario.BloqueadoUsuario)
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(usuario.IdUsuario, "Usuario bloqueado (bloqueado_usuario = true).", tela: "LoginForm", cancellationToken);
            return ResultadoAutenticacao.Falha("Usuario bloqueado ou inativo.");
        }

        // Valida senha_hash (BCrypt) via SenhaServico.
        if (!_senhaServico.Verificar(senha, usuario.SenhaHash))
        {
            await _auditoriaServico.RegistrarLoginFalhaAsync(login, usuario.IdUsuario, motivo: "Senha invalida.", cancellationToken);
            return ResultadoAutenticacao.Falha("Usuario ou senha invalidos.");
        }

        var vinculosPerfil = await _usuarioPerfilRepositorio.ListarPorUsuarioAsync(usuario.IdUsuario, cancellationToken);
        var perfisAtivos = (await _perfilAcessoRepositorio.ListarAsync(cancellationToken))
            .Where(p => p.Ativo)
            .ToDictionary(p => p.Id, p => p.Codigo, comparer: EqualityComparer<long>.Default);

        List<string> codigosPerfil = [];
        foreach (var vinculo in vinculosPerfil.Where(v => v.Ativo))
        {
            if (perfisAtivos.TryGetValue(vinculo.IdPerfilAcesso, out string? codigo) && !string.IsNullOrWhiteSpace(codigo))
            {
                codigosPerfil.Add(codigo);
            }
        }

        IReadOnlyList<PermissaoSessaoAplicacao> permissoes =
            await _perfilPermissaoRepositorio.ListarPermissoesAtivasPorUsuarioAsync(usuario.IdUsuario, cancellationToken);

        long? idSetorPadrao = usuario.IdSetorPadrao;
        if (!idSetorPadrao.HasValue)
        {
            var setores = await _usuarioSetorRepositorio.ListarPorUsuarioAsync(usuario.IdUsuario, cancellationToken);
            idSetorPadrao = setores.FirstOrDefault(s => s.Ativo && s.SetorPadrao)?.IdSetor;
        }

        // Atualiza ultimo_login_em e registra auditoria de sucesso.
        try
        {
            await _usuarioRepositorio.AtualizarUltimoLoginAsync(usuario.IdUsuario, cancellationToken);
        }
        catch
        {
            // Falha em registrar ultimo_login nao impede acesso.
        }

        await _auditoriaServico.RegistrarLoginSucessoAsync(usuario.IdUsuario, usuario.LoginUsuario, cancellationToken);

        return ResultadoAutenticacao.Ok(new SessaoUsuarioAplicacao
        {
            IdUsuario = usuario.IdUsuario,
            Login = usuario.LoginUsuario,
            Nome = usuario.NomeUsuario,
            IdSetorPadrao = idSetorPadrao,
            PerfisCodigo = codigosPerfil.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true,
            DeveTrocarSenha = usuario.DeveTrocarSenha
        });
    }

    private static bool SenhaAdminLocalConfere(string senhaDigitada)
    {
        string? senhaLocal = ObterSenhaAdminLocalConfigurada();
        return !string.IsNullOrWhiteSpace(senhaLocal)
            && string.Equals(senhaDigitada, senhaLocal, StringComparison.Ordinal);
    }

    private static string? ObterSenhaAdminLocalConfigurada()
        => Environment.GetEnvironmentVariable(VariavelSenhaAdminLocal)
            ?? Environment.GetEnvironmentVariable(VariavelSenhaAdminLocal, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(VariavelSenhaAdminLocal, EnvironmentVariableTarget.Machine);
}
