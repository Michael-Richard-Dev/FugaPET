using System.Text.RegularExpressions;

namespace FugaPET_Dev.Tests.Comum;

/// <summary>
/// Regra de disciplina (fitness function): telas produtivas (pasta Tela/) NAO devem usar
/// ErroUsuarioHelper.Tratar (sincrono, fire-and-forget). Fluxos criticos / seguranca / operacao /
/// impressao / balanca devem usar ErroUsuarioHelper.TratarAsync (com await), que aguarda a auditoria.
/// O Tratar sincrono fica reservado a eventos sincronos simples — documentados na allowlist abaixo.
/// Este teste impede a reintroducao silenciosa do Tratar sincrono nas telas.
/// </summary>
public sealed class ErroUsuarioHelperUsoTests
{
    // Excecoes documentadas (erro visual comum / evento sincrono simples). Vazio: nenhuma tela usa Tratar.
    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase);

    // Casa "ErroUsuarioHelper.Tratar(" mas NAO "ErroUsuarioHelper.TratarAsync(" (exige '(' apos Tratar).
    private static readonly Regex TratarSincrono = new(@"ErroUsuarioHelper\.Tratar\(", RegexOptions.Compiled);

    [Fact]
    public void TelasProdutivas_NaoDevemUsarErroUsuarioHelperTratarSincrono()
    {
        string telaDir = Path.Combine(RaizProjeto(), "Tela");
        Assert.True(Directory.Exists(telaDir), "Pasta Tela nao encontrada em: " + telaDir);

        List<string> infratores = new();
        foreach (string arquivo in Directory.EnumerateFiles(telaDir, "*.cs", SearchOption.AllDirectories))
        {
            string nome = Path.GetFileName(arquivo);
            if (Permitidos.Contains(nome))
            {
                continue;
            }

            if (TratarSincrono.IsMatch(File.ReadAllText(arquivo)))
            {
                infratores.Add(nome);
            }
        }

        Assert.True(infratores.Count == 0,
            "Telas usando ErroUsuarioHelper.Tratar (sincrono) em vez de TratarAsync:" +
            Environment.NewLine + string.Join(Environment.NewLine, infratores.Distinct().OrderBy(x => x)));
    }

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
