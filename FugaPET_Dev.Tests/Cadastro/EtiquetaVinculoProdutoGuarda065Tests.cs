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

    /// <summary>
    /// A tabela ANTIGA saiu do codigo em 18/11/2026, por decisao do usuario: estava vazia, ninguem
    /// escrevia nela, e no QAS110/QAS130 o papel de aplicacao nem tinha privilegio de leitura — a
    /// consulta respondia "permission denied", de modo que inativar etiqueta ja falhava la.
    /// <para>
    /// Este teste impede que ela volte por distracao. A tabela segue existindo no banco; o que nao
    /// pode voltar e a DEPENDENCIA do codigo nela.
    /// </para>
    /// </summary>
    [Fact]
    public void Inativacao_NaoDependeMaisDaTabelaAntiga()
    {
        string metodo = Corpo("ExcluirAsync(");

        Assert.DoesNotContain("FROM produto_etiqueta pe", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_produto_etiqueta", metodo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O pre-check que alimenta a mensagem precisa enxergar o MESMO que o UPDATE enxerga. Se contar
    /// menos, a tela diz que pode inativar e o banco recusa sem explicar.
    /// </summary>
    [Fact]
    public void ResumoDeDependencias_ContaAMesmaTabelaQueOUpdate()
    {
        string metodo = Corpo("ObterResumoDependenciasAtivasAsync(");

        Assert.Contains("FROM produto_etiqueta_vinculo", metodo, StringComparison.Ordinal);
        Assert.Contains("situacao = true", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_produto_etiqueta", metodo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Corpo do metodo, com as quebras de linha NORMALIZADAS.
    /// <para>
    /// A normalizacao importa: a arvore do DEV usa LF e as do QAS110/QAS130 usam CRLF. Sem ela, uma
    /// assercao que inclua "\n" passa aqui e falha no porte — foi o que aconteceu em 18/11/2026.
    /// </para>
    /// </summary>
    private static string Corpo(string assinatura)
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "EtiquetaRepositorio.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

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
