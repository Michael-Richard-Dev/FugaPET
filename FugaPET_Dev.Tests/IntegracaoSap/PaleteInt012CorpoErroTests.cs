using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 126H: o corpo de uma resposta nao-2xx do INT012 era lido e DESCARTADO — o operador via
/// apenas "INT012 retornou HTTP 500", sem a causa. Caso real no DEV110 em 16/11/2026.
/// <para>
/// O corpo vem de servico externo: e dado, nunca instrucao, e vai para a tela achatado, com campos
/// sensiveis redigidos e truncado.
/// </para>
/// </summary>
public sealed class PaleteInt012CorpoErroTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    public void CorpoVazioNaoViraTextoNenhum(string? corpo)
        => Assert.Equal(string.Empty, ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro(corpo));

    [Fact]
    public void AchataQuebrasDeLinhaEEspacosRepetidos()
    {
        string resumo = ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro(
            "{\n  \"error\":  \"HU creation failed\",\r\n\t\"detail\": \"plant 3007\"\n}");

        Assert.DoesNotContain('\n', resumo);
        Assert.DoesNotContain('\r', resumo);
        Assert.DoesNotContain("  ", resumo, StringComparison.Ordinal);
        Assert.Contains("HU creation failed", resumo, StringComparison.Ordinal);
        Assert.Contains("plant 3007", resumo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O servico pode ecoar o que recebeu, cabecalhos inclusive. Credencial nunca chega a uma
    /// mensagem de tela nem a um log.
    /// </summary>
    [Theory]
    [InlineData("""{ "Authorization": "Basic YWRtaW46c2VucmE=", "erro": "x" }""", "YWRtaW46c2VucmE=")]
    [InlineData("""{ "password": "SEGREDO123", "erro": "x" }""", "SEGREDO123")]
    [InlineData("""{ "senha": "SEGREDO123" }""", "SEGREDO123")]
    [InlineData("""{ "client_secret": "abc.def-ghi", "erro": "x" }""", "abc.def-ghi")]
    [InlineData("""{ "access_token": "eyJhbGciOiJIUzI1NiJ9" }""", "eyJhbGciOiJIUzI1NiJ9")]
    [InlineData("""{ "Cookie": "SESSION=9f8e7d6c5b4a3210" }""", "9f8e7d6c5b4a3210")]
    [InlineData("Authorization=Bearer eyJhbGciOiJIUzI1NiJ9 erro=x", "eyJhbGciOiJIUzI1NiJ9")]
    public void RedigeCampoSensivel(string corpo, string segredo)
    {
        string resumo = ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro(corpo);

        Assert.DoesNotContain(segredo, resumo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDIGIDO]", resumo, StringComparison.Ordinal);
    }

    /// <summary>Redigir nao pode engolir a mensagem de erro util que vem ao lado.</summary>
    [Fact]
    public void RedigirPreservaORestanteDaMensagem()
    {
        string resumo = ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro(
            """{ "password": "SEGREDO123", "error": "Packaging material PALLET01 not found in plant 3007" }""");

        Assert.DoesNotContain("SEGREDO123", resumo, StringComparison.Ordinal);
        Assert.Contains("Packaging material PALLET01 not found in plant 3007", resumo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uma stack trace de CPI pode ter dezenas de milhares de caracteres. A linha de status nao
    /// pode virar isso.
    /// </summary>
    [Fact]
    public void TruncaCorpoLongoEDeclaraOTruncamento()
    {
        string resumo = ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro(new string('x', 5000));

        Assert.EndsWith("... (truncado)", resumo, StringComparison.Ordinal);
        Assert.True(
            resumo.Length <= ProdutoAcabadoPaleteInt012Gateway.LimiteCorpoErro + 20,
            $"resumo ficou com {resumo.Length} caracteres");
    }

    /// <summary>Corpo dentro do limite sai inteiro, sem o rotulo de truncamento.</summary>
    [Fact]
    public void CorpoCurtoNaoEMarcadoComoTruncado()
    {
        string resumo = ProdutoAcabadoPaleteInt012Gateway.ResumirCorpoErro("""{"error":"HU exists"}""");

        Assert.DoesNotContain("truncado", resumo, StringComparison.Ordinal);
        Assert.Contains("HU exists", resumo, StringComparison.Ordinal);
    }
}
