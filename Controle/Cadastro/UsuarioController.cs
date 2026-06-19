using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class UsuarioController
{
    private readonly UsuarioServico _usuarioServico;
    private readonly UsuarioPerfilServico _usuarioPerfilServico;
    private readonly UsuarioSetorServico _usuarioSetorServico;

    public UsuarioController(
        UsuarioServico usuarioServico,
        UsuarioPerfilServico usuarioPerfilServico,
        UsuarioSetorServico usuarioSetorServico)
    {
        _usuarioServico = usuarioServico;
        _usuarioPerfilServico = usuarioPerfilServico;
        _usuarioSetorServico = usuarioSetorServico;
    }

    public Task<IReadOnlyList<UsuarioCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _usuarioServico.ListarAsync(cancellationToken);

    public Task<UsuarioCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioServico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(UsuarioCadastro usuario, string senhaPlana, CancellationToken cancellationToken = default)
        => _usuarioServico.InserirAsync(usuario, senhaPlana, cancellationToken);

    public Task<ResultadoOperacao> InserirComVinculosAsync(
        UsuarioCadastro usuario,
        string senhaPlana,
        long idPerfilAcesso,
        long idSetor,
        CancellationToken cancellationToken = default)
        => _usuarioServico.InserirComVinculosAsync(usuario, senhaPlana, idPerfilAcesso, idSetor, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(UsuarioCadastro usuario, CancellationToken cancellationToken = default)
        => _usuarioServico.AtualizarAsync(usuario, cancellationToken);

    public Task<ResultadoOperacao> AtualizarComVinculosAsync(
        UsuarioCadastro usuario,
        long idPerfilAcesso,
        long idSetor,
        CancellationToken cancellationToken = default)
        => _usuarioServico.AtualizarComVinculosAsync(usuario, idPerfilAcesso, idSetor, cancellationToken);

    public Task<ResultadoOperacao> AlterarSenhaAsync(long idUsuario, string novaSenhaPlana, bool exigirTrocaNoProximoLogin = false, CancellationToken cancellationToken = default)
        => _usuarioServico.AlterarSenhaAsync(idUsuario, novaSenhaPlana, exigirTrocaNoProximoLogin, cancellationToken);

    public Task<ResultadoOperacao> AlterarSenhaAsync(long idUsuario, string novaSenhaPlana, CancellationToken cancellationToken = default)
        => _usuarioServico.AlterarSenhaAsync(idUsuario, novaSenhaPlana, cancellationToken);

    public Task<ResultadoOperacao> AlterarSenhaPropriaAsync(long idUsuario, string novaSenhaPlana, CancellationToken cancellationToken = default)
        => _usuarioServico.AlterarSenhaPropriaAsync(idUsuario, novaSenhaPlana, cancellationToken);

    public Task<ResultadoOperacao> BloquearAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioServico.BloquearAsync(id, cancellationToken);

    public Task<ResultadoOperacao> DesbloquearAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioServico.DesbloquearAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioServico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _usuarioServico.ReativarAsync(id, cancellationToken);

    public Task<IReadOnlyList<UsuarioPerfilCadastro>> ListarPerfisAsync(long idUsuario, CancellationToken cancellationToken = default)
        => _usuarioPerfilServico.ListarPorUsuarioAsync(idUsuario, cancellationToken);

    public Task<ResultadoOperacao> VincularPerfilAsync(long idUsuario, long idPerfilAcesso, bool ativo = true, CancellationToken cancellationToken = default)
        => _usuarioPerfilServico.VincularPerfilAsync(idUsuario, idPerfilAcesso, ativo, cancellationToken);

    public Task<ResultadoOperacao> SincronizarPerfilUnicoAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
        => _usuarioPerfilServico.SincronizarPerfilUnicoAsync(idUsuario, idPerfilAcesso, cancellationToken);

    public Task<ResultadoOperacao> RemoverPerfilAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
        => _usuarioPerfilServico.RemoverPerfilAsync(idUsuario, idPerfilAcesso, cancellationToken);

    public Task<IReadOnlyList<UsuarioSetorCadastro>> ListarSetoresAsync(long idUsuario, CancellationToken cancellationToken = default)
        => _usuarioSetorServico.ListarPorUsuarioAsync(idUsuario, cancellationToken);

    public Task<ResultadoOperacao> VincularSetorAsync(long idUsuario, long idSetor, bool setorPadrao = false, bool ativo = true, CancellationToken cancellationToken = default)
        => _usuarioSetorServico.VincularSetorAsync(idUsuario, idSetor, setorPadrao, ativo, cancellationToken);

    public Task<ResultadoOperacao> DefinirSetorPadraoAsync(long idUsuario, long idSetor, CancellationToken cancellationToken = default)
        => _usuarioSetorServico.DefinirSetorPadraoAsync(idUsuario, idSetor, cancellationToken);

    public Task<ResultadoOperacao> RemoverSetorAsync(long idUsuario, long idSetor, CancellationToken cancellationToken = default)
        => _usuarioSetorServico.RemoverSetorAsync(idUsuario, idSetor, cancellationToken);
}
