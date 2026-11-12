namespace FugaPET_Dev.Tests.Processo;

public sealed class ControleApontamentosConsumoConfirmadoRecovery100FTests
{
    [Fact]
    public void Repositorio_RecuperaPorIdentidadeExplicitaEmTransacaoUnica()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<bool> TentarRecuperarConsumoConfirmadoAsync");

        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("codigo_consumo_material_lancamento = @codigo_lancamento", metodo, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", metodo, StringComparison.Ordinal);
        Assert.Contains("InserirEventoAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIMIT 1", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repositorio_PreservaTodosOsGuardsFailClosed()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<bool> TentarRecuperarConsumoConfirmadoAsync");

        string[] contratos =
        [
            "status = 'EM_ANDAMENTO'",
            "resultado_operacional IS NULL",
            "codigo_registro_processo IS NULL",
            "concluido_operacional_em IS NULL",
            "tipo_processo = 'CONSUMO_MATERIA_PRIMA'",
            "status_lancamento = 'CONFIRMADO_SAP'",
            "documento_material_sap",
            "exercicio_documento_material_sap",
            "COUNT(*)",
            "codigo_material",
            "numero_reserva",
            "item_reserva",
            "lote",
            "NOT EXISTS",
            "operacao_producao_apontamento_processo"
        ];

        foreach (string contrato in contratos)
        {
            Assert.Contains(contrato, metodo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Repositorio_NaoCriaPesagemLancamentoOuItemNoRecovery()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<bool> TentarRecuperarConsumoConfirmadoAsync");

        Assert.DoesNotContain("INSERT INTO consumo_material_lancamento", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO consumo_material_item", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO consumo_material_pesagem", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PostAsync", metodo, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return File.ReadAllText(Path.Combine(new[] { diretorio }.Concat(partes).ToArray()));
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Projeto FugaPET_Dev não localizado para source-scan.");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");

        int abre = fonte.IndexOf('{', inicio);
        Assert.True(abre >= 0, $"Corpo não encontrado: {assinatura}");

        int profundidade = 0;
        for (int indice = abre; indice < fonte.Length; indice++)
        {
            if (fonte[indice] == '{') profundidade++;
            if (fonte[indice] == '}' && --profundidade == 0) return fonte[inicio..(indice + 1)];
        }

        throw new InvalidOperationException($"Fim do método não encontrado: {assinatura}");
    }
}
