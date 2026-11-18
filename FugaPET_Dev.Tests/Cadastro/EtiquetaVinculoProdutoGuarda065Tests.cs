using Xunit;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Inativar etiqueta vinculada a produto (18/11/2026).
/// <para>
/// A guarda existia e lia <c>produto_etiqueta</c> — uma tabela que, nesta data, esta VAZIA e que
/// nenhum codigo do sistema escreve. Na pratica ela nunca barrou nada. Com a tela de Vinculo de
/// Produto (incremento 065), o vinculo real passou a viver em <c>produto_etiqueta_vinculo</c>, e
/// sem incluir essa tabela dava para inativar uma etiqueta com produtos apontando para ela.
/// </para>
/// </summary>
public sealed class EtiquetaVinculoProdutoGuarda065Tests
{
    /// <summary>A inativacao e recusada pelo proprio UPDATE quando ha vinculo ativo na tabela nova.</summary>
    [Fact]
    public void Inativacao_BarraPelaTabelaDeVinculoNova()
    {
        string metodo = Corpo("ExcluirAsync(");

        Assert.Contains("FROM produto_etiqueta_vinculo pv", metodo, StringComparison.Ordinal);
        Assert.Contains("pv.situacao = true", metodo, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", metodo, StringComparison.Ordinal);
    }

    /// <summary>A guarda antiga continua, para o caso de a tabela voltar a ser usada.</summary>
    [Fact]
    public void Inativacao_MantemAGuardaAntiga()
    {
        string metodo = Corpo("ExcluirAsync(");

        Assert.Contains("FROM produto_etiqueta pe", metodo, StringComparison.Ordinal);
        Assert.Contains("pe.situacao_produto_etiqueta = true", metodo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O pre-check que alimenta a mensagem precisa enxergar o MESMO que o UPDATE enxerga. Se contar
    /// menos, a tela diz que pode inativar e o banco recusa sem explicar.
    /// </summary>
    [Fact]
    public void ResumoDeDependencias_ContaAsDuasTabelas()
    {
        string metodo = Corpo("ObterResumoDependenciasAtivasAsync(");

        Assert.Contains("FROM produto_etiqueta\n", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM produto_etiqueta_vinculo", metodo, StringComparison.Ordinal);
        Assert.Contains("situacao = true", metodo, StringComparison.Ordinal);
    }

    private static string Corpo(string assinatura)
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "EtiquetaRepositorio.cs"));

        int inicio = fonte.LastIndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"metodo {assinatura} nao encontrado");
        int fim = fonte.IndexOf("public ", inicio + assinatura.Length, StringComparison.Ordinal);
        return fim < 0 ? fonte[inicio..] : fonte[inicio..fim];
    }

    private static string RaizProjeto()
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return dir;
    }
}
