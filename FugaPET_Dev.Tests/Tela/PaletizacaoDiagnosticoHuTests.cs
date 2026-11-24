using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Guarda as decisões ESTRUTURAIS do diagnóstico de HU na Paletização (23/11/2026), que são
/// invisíveis no comportamento e só reaparecem como defeito muito depois.
/// </summary>
public sealed class PaletizacaoDiagnosticoHuTests
{
    private static string LerFonte(params string[] partes)
        => File.ReadAllText(Path.Combine(new[] { RaizProjeto() }.Concat(partes).ToArray()));

    /// <summary>
    /// O diagnóstico NÃO pode ler `hu_palete`: verificado em 23/11/2026 que o papel da aplicação não
    /// tem SELECT nessa tabela em NENHUM dos três ambientes. Como eu investigo como superusuário, uma
    /// consulta a ela passaria nos meus testes e falharia na mão do operador. O número do palete vem
    /// de `hu_palete_item`, que o papel lê.
    /// </summary>
    [Fact]
    public void DiagnosticoNaoLeTabelaQueOPapelDaAplicacaoNaoEnxerga()
    {
        string repo = LerFonte("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");

        int inicio = repo.IndexOf("DiagnosticarHandlingUnitAsync", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "o metodo de diagnostico precisa existir");

        int fim = repo.IndexOf("ObterAtivaPorTerminalAsync", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "ancora de fim nao encontrada");

        string corpo = repo[inicio..fim];
        Assert.Contains("hu_palete_item", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM hu_palete\n", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN hu_palete ", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O vínculo de palete só conta quando ATIVO e em VINCULADO/ENVIADO_SAP/CONFIRMADO_SAP — é o que
    /// `vw_hu_caixas_sem_palete` faz. Sem esses filtros, um vínculo cancelado faria o diagnóstico
    /// dizer "já está no palete N" de uma caixa que a tela acabou de recusar por outro motivo.
    /// </summary>
    [Fact]
    public void DiagnosticoEspelhaAsCondicoesDaView()
    {
        string repo = LerFonte("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");
        int inicio = repo.IndexOf("DiagnosticarHandlingUnitAsync", StringComparison.Ordinal);
        int fim = repo.IndexOf("ObterAtivaPorTerminalAsync", inicio, StringComparison.Ordinal);
        string corpo = repo[inicio..fim];

        Assert.Contains("situacao_hu_palete_item IS TRUE", corpo, StringComparison.Ordinal);
        Assert.Contains("'VINCULADO','ENVIADO_SAP','CONFIRMADO_SAP'", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O diagnóstico roda DEPOIS da recusa, nunca antes: ele explica, não decide. Se passasse a
    /// decidir, passariam a existir DUAS regras de elegibilidade, que um dia divergiriam.
    /// </summary>
    [Fact]
    public void TelaSoDiagnosticaQuandoABuscaNaoTrouxeNada()
    {
        string form = LerFonte("Tela", "Processo", "PaletizacaoForm.cs");

        int busca = form.IndexOf("ListarCaixasPorHandlingUnitsAsync([hu])", StringComparison.Ordinal);
        Assert.True(busca >= 0, "a busca por HU precisa existir");

        int guarda = form.IndexOf("if (caixas.Count == 0)", busca, StringComparison.Ordinal);
        int diagnostico = form.IndexOf("DiagnosticarHandlingUnitAsync", busca, StringComparison.Ordinal);

        Assert.True(guarda > busca, "o diagnostico precisa estar sob a guarda de resultado vazio");
        Assert.True(diagnostico > guarda, "o diagnostico vem DEPOIS da guarda, nao antes da busca");
    }

    /// <summary>A mensagem genérica que escondia os quatro casos não pode voltar.</summary>
    [Fact]
    public void AMensagemGenericaNaoVolta()
    {
        string form = LerFonte("Tela", "Processo", "PaletizacaoForm.cs");

        Assert.DoesNotContain("não encontrada.\"", form, StringComparison.Ordinal);
    }

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
