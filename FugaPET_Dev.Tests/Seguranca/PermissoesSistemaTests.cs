using System.Reflection;
using System.Text.RegularExpressions;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Garante que PermissoesSistema espelha o banco: cada (modulo, rotina, acao) seedado nos
/// scripts SQL (baseline + incrementais) deve existir como constante em
/// PermissoesSistema.Modulos / .Rotinas / .Acoes — ou estar explicitamente listado como legado.
/// Se alguem adicionar uma permissao no SQL e esquecer a constante, este teste quebra.
/// </summary>
public sealed class PermissoesSistemaTests
{
    [Fact]
    public void Modulos_DeveUsarNomesOficiaisDoBanco()
    {
        Assert.Equal("SEGURANCA", PermissoesSistema.Modulos.Seguranca);
        Assert.Equal("PROCESSO_PRODUCAO", PermissoesSistema.Modulos.ProcessoProducao);
        Assert.Equal("ETIQUETA", PermissoesSistema.Modulos.Etiqueta);
        Assert.Equal("HISTORICO", PermissoesSistema.Modulos.Historico);
        Assert.Equal("INTEGRACAO_SAP", PermissoesSistema.Modulos.IntegracaoSap);
    }

    [Fact]
    public void Rotinas_DeveUsarPerfilAcessoPermissaoEMapeamentoOficiais()
    {
        Assert.Equal("PERFIL_ACESSO", PermissoesSistema.Rotinas.PerfilAcesso);
        Assert.Equal("PERMISSAO", PermissoesSistema.Rotinas.Permissao);
        Assert.Equal("MAPEAMENTO_CAMPO_ETIQUETA", PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta);
    }

    // Valores seedados no SQL que NAO devem (ainda) virar constante. Mantido vazio de proposito:
    // qualquer permissao nova no SQL precisa de constante OU de uma entrada documentada aqui.
    // Falso positivo do extrator: tupla de configuracao do ambiente na baseline de desenvolvimento.
    private static readonly HashSet<string> ModulosLegado = new(["AMBIENTE_BANCO"], StringComparer.Ordinal);
    private static readonly HashSet<string> RotinasLegado = new(["DESENVOLVIMENTO"], StringComparer.Ordinal);
    private static readonly HashSet<string> AcoesLegado = new(["TEXTO"], StringComparer.Ordinal);

    // Tupla de permissao: 3 tokens MAIUSCULOS + uma descricao que termina em ponto.
    // O ".'" final evita falsos positivos como listas IN ('BALANCA','TARA','TIPO_TARA','PRODUTO_REFERENCIA').
    private static readonly Regex TuplaPermissao = new(
        @"\(\s*'([A-Z][A-Z0-9_]*)'\s*,\s*'([A-Z][A-Z0-9_]*)'\s*,\s*'([A-Z][A-Z0-9_]*)'\s*,\s*'[^']*\.'",
        RegexOptions.Compiled);

    [Fact]
    public void TodaPermissaoSeedadaNoSql_DeveExistirComoConstante()
    {
        IReadOnlyList<(string Modulo, string Rotina, string Acao, string Arquivo)> seed = LerPermissoesDoSql();

        Assert.True(seed.Count >= 40,
            $"Esperava encontrar as permissoes seedadas no SQL, mas extrai apenas {seed.Count}. " +
            "Verifique a localizacao de BancoDados e o padrao do INSERT de permissao.");

        HashSet<string> modulos = ValoresConstantes(typeof(PermissoesSistema.Modulos));
        HashSet<string> rotinas = ValoresConstantes(typeof(PermissoesSistema.Rotinas));
        HashSet<string> acoes = ValoresConstantes(typeof(PermissoesSistema.Acoes));

        List<string> faltando = new();
        foreach ((string modulo, string rotina, string acao, string arquivo) in seed)
        {
            if (!modulos.Contains(modulo) && !ModulosLegado.Contains(modulo))
                faltando.Add($"Modulo '{modulo}' (em {arquivo})");

            if (!rotinas.Contains(rotina) && !RotinasLegado.Contains(rotina))
                faltando.Add($"Rotina '{rotina}' (em {arquivo})");

            if (!acoes.Contains(acao) && !AcoesLegado.Contains(acao))
                faltando.Add($"Acao '{acao}' (em {arquivo})");
        }

        Assert.True(faltando.Count == 0,
            "Permissoes seedadas no SQL sem constante em PermissoesSistema:" +
            Environment.NewLine + string.Join(Environment.NewLine, faltando.Distinct().OrderBy(x => x)));
    }

    private static HashSet<string> ValoresConstantes(Type tipo)
    {
        return tipo
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<(string, string, string, string)> LerPermissoesDoSql()
    {
        string bancoDados = LocalizarPastaBancoDados();
        Assert.False(string.IsNullOrEmpty(bancoDados),
            "Nao encontrei a pasta BancoDados a partir de " + AppContext.BaseDirectory);

        List<(string, string, string, string)> tuplas = new();
        foreach (string arquivo in Directory.EnumerateFiles(bancoDados, "*.sql", SearchOption.AllDirectories))
        {
            string conteudo = File.ReadAllText(arquivo);
            string nome = Path.GetFileName(arquivo);
            foreach (Match m in TuplaPermissao.Matches(conteudo))
            {
                tuplas.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, nome));
            }
        }

        return tuplas;
    }

    private static string LocalizarPastaBancoDados()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidato = Path.Combine(dir.FullName, "BancoDados");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            dir = dir.Parent;
        }

        return string.Empty;
    }
}
