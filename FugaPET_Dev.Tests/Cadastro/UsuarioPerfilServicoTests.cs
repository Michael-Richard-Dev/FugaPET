using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Testa as guardas de validacao de UsuarioPerfilServico que rodam ANTES de tocar o
/// repositorio/auditoria — por isso e seguro injetar null! nas dependencias: os caminhos
/// exercitados retornam Falha antes de qualquer acesso a banco.
/// </summary>
public sealed class UsuarioPerfilServicoTests
{
    [Theory]
    [InlineData(0L, 5L)]
    [InlineData(-1L, 5L)]
    public async Task SincronizarPerfilUnicoAsync_DeveRejeitarUsuarioInvalido(long idUsuario, long idPerfilAcesso)
    {
        UsuarioPerfilServico servico = new(null!, null!);

        ResultadoOperacao resultado = await servico.SincronizarPerfilUnicoAsync(idUsuario, idPerfilAcesso);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Usuario invalido", resultado.Mensagem);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-3L)]
    public async Task SincronizarPerfilUnicoAsync_DeveRejeitarPerfilInvalido(long idPerfilAcesso)
    {
        UsuarioPerfilServico servico = new(null!, null!);

        ResultadoOperacao resultado = await servico.SincronizarPerfilUnicoAsync(10, idPerfilAcesso);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Perfil de acesso invalido", resultado.Mensagem);
    }

    [Fact]
    public async Task VincularPerfilAsync_DeveDelegarGuardas_DoSincronizarPerfilUnico()
    {
        UsuarioPerfilServico servico = new(null!, null!);

        ResultadoOperacao resultado = await servico.VincularPerfilAsync(idUsuario: 0, idPerfilAcesso: 5);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Usuario invalido", resultado.Mensagem);
    }

    [Theory]
    [InlineData(0L, 5L)]
    [InlineData(10L, 0L)]
    public async Task RemoverPerfilAsync_DeveRejeitarDadosInvalidos(long idUsuario, long idPerfilAcesso)
    {
        UsuarioPerfilServico servico = new(null!, null!);

        ResultadoOperacao resultado = await servico.RemoverPerfilAsync(idUsuario, idPerfilAcesso);

        Assert.False(resultado.Sucesso);
        Assert.Contains("invalidos", resultado.Mensagem);
    }
}
