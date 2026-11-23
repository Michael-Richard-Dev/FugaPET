using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Usuario sempre em CAIXA ALTA no login (pedido do usuario em 23/11/2026).
/// <para>
/// Sao DUAS pontas, e o teste guarda as duas: a tela force a maiuscula, e a busca do login continue
/// comparando sem diferenciar caixa. Se a segunda mudasse sozinha, a primeira trancaria do lado de
/// fora todo mundo que esta cadastrado em minusculas — e isso apareceria so na tela de login, com a
/// mensagem generica, que e o pior lugar possivel para descobrir.
/// </para>
/// </summary>
public sealed class LoginUsuarioCaixaAltaTests
{
    [Fact]
    public void Tela_ForcaCaixaAltaNoCampoDeUsuario()
    {
        string designer = LerFonte("Tela", "LoginForm.Designer.cs");

        Assert.Contains("userTextBox.CharacterCasing = CharacterCasing.Upper;", designer, StringComparison.Ordinal);
    }

    /// <summary>A senha NAO pode ser forcada: maiuscula em senha muda a senha.</summary>
    [Fact]
    public void Tela_NaoMexeNaCaixaDaSenha()
    {
        string designer = LerFonte("Tela", "LoginForm.Designer.cs");

        Assert.DoesNotContain("passwordTextBox.CharacterCasing", designer, StringComparison.Ordinal);
    }

    /// <summary>
    /// A busca do login compara com lower() DOS DOIS LADOS. E o que torna a caixa alta inofensiva
    /// para quem ja esta cadastrado em minusculas.
    /// </summary>
    [Fact]
    public void BuscaDoLogin_NaoDiferenciaCaixa()
    {
        string repositorio = LerFonte("AcessoDados", "Repositorio", "UsuarioRepositorio.cs");

        Assert.Contains(
            "lower(COALESCE(NULLIF(j ->> 'login_usuario', ''), '')) = lower(@login_usuario)",
            repositorio, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
