using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class UsuarioServicoTests : IDisposable
{
    public UsuarioServicoTests()
    {
        Environment.SetEnvironmentVariable("FUGAPET_DEV_CONEXAO_POSTGRES", "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes =
            [
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "USUARIO", Acao = "CRIAR" },
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "USUARIO", Acao = "EDITAR" }
            ],
            IntegracaoBancoHabilitada = true
        });
    }

    [Fact]
    public async Task InserirAsync_DeveValidarNomeAntesDeAcessarRepositorio()
    {
        UsuarioServico servico = new(null!, new SenhaServico(), null!);
        UsuarioCadastro usuario = new() { LoginUsuario = "usuario", SituacaoUsuario = true };

        ResultadoOperacao resultado = await servico.InserirAsync(usuario, "123456");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Nome do usuario", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveValidarSenhaMinimaAntesDeAcessarRepositorio()
    {
        UsuarioServico servico = new(null!, new SenhaServico(), null!);
        UsuarioCadastro usuario = new() { NomeUsuario = "Usuario Teste", LoginUsuario = "usuario", SituacaoUsuario = true };

        ResultadoOperacao resultado = await servico.InserirAsync(usuario, "123");

        Assert.False(resultado.Sucesso);
        Assert.Contains("Senha deve ter ao menos", resultado.Mensagem);
    }

    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }
}
