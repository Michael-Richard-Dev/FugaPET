using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class UsuarioPerfilServico
{
    private const string Entidade = "USUARIO_PERFIL";
    private const string Tela = "CadastroUsuarioForm";

    private readonly UsuarioPerfilRepositorio _usuarioPerfilRepositorio;
    private readonly AuditoriaServico _auditoriaServico;

    public UsuarioPerfilServico(UsuarioPerfilRepositorio usuarioPerfilRepositorio, AuditoriaServico auditoriaServico)
    {
        _usuarioPerfilRepositorio = usuarioPerfilRepositorio;
        _auditoriaServico = auditoriaServico;
    }

    public Task<IReadOnlyList<UsuarioPerfilCadastro>> ListarPorUsuarioAsync(long idUsuario, CancellationToken cancellationToken = default)
        => _usuarioPerfilRepositorio.ListarPorUsuarioAsync(idUsuario, cancellationToken);

    public async Task<ResultadoOperacao> VincularPerfilAsync(long idUsuario, long idPerfilAcesso, bool ativo = true, CancellationToken cancellationToken = default)
    {
        return await SincronizarPerfilUnicoAsync(idUsuario, idPerfilAcesso, cancellationToken);
    }

    public async Task<ResultadoOperacao> SincronizarPerfilUnicoAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
            return ResultadoOperacao.Falha("Usuario invalido para vinculo de perfil.");

        if (idPerfilAcesso <= 0)
            return ResultadoOperacao.Falha("Perfil de acesso invalido para vinculo.");

        try
        {
            long idVinculo = await _usuarioPerfilRepositorio.SincronizarPerfilUnicoAsync(idUsuario, idPerfilAcesso, cancellationToken);
            if (idVinculo <= 0) return ResultadoOperacao.Falha("Nao foi possivel sincronizar o perfil do usuario.");

            await _auditoriaServico.RegistrarCadastroAtualizadoAsync(Entidade, idVinculo, $"Perfil unico {idPerfilAcesso} sincronizado no usuario {idUsuario}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Perfil sincronizado com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }

    public async Task<ResultadoOperacao> RemoverPerfilAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0 || idPerfilAcesso <= 0)
            return ResultadoOperacao.Falha("Dados invalidos para remover vinculo de perfil.");

        try
        {
            int removidos = await _usuarioPerfilRepositorio.RemoverAsync(idUsuario, idPerfilAcesso, cancellationToken);
            if (removidos <= 0) return ResultadoOperacao.Falha("Vinculo de perfil nao encontrado para remocao.");

            await _auditoriaServico.RegistrarCadastroExcluidoAsync(Entidade, idUsuario, $"Perfil {idPerfilAcesso} removido do usuario {idUsuario}", Tela, cancellationToken);
            return ResultadoOperacao.Ok("Perfil removido com sucesso.");
        }
        catch (Exception ex)
        {
            return await TratamentoErroCadastroServico.TratarFalhaAsync(_auditoriaServico, Entidade + "_ERRO", ex, Tela, cancellationToken);
        }
    }
}
