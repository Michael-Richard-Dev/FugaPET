namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 099B — regressão do TÉRMINO (EVENTO 02) do Controle de Apontamentos.
///
/// Defeito: a coluna terminado_em é timestamptz; pelo caminho object de ExecuteScalar o provider Npgsql devolve
/// um DateTime encaixotado, e o unboxing direto (DateTimeOffset)object lançava InvalidCastException — abortando a
/// transação de conclusão (rollback), o que deixava status=AGUARDANDO_FINALIZACAO, terminado_em/idempotency_key_termino
/// NULL. A leitura correta usa GetFieldValue&lt;DateTimeOffset&gt; (mesmo padrão do mapeador desta classe), sem
/// conversão dependente de locale, e mantém o fail-closed (0 linhas / DBNull ⇒ null).
///
/// A conversão CLR só se manifesta contra um Postgres real; como o gate proíbe DB, a garantia é por source-scan
/// determinístico do método real do repositório (mesmo padrão de WC01/WC11). Os cenários de orquestração de
/// término (A–H) permanecem cobertos pelos testes de serviço com repositório fake.
/// </summary>
public sealed class ControleApontamentosTerminoTimestamp099BTests
{
    [Fact]
    public void Termino_NaoUsaUnboxingFragilDeDateTimeOffset()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<DateTimeOffset?> TentarConcluirApontamentoAsync");

        // O cast frágil que causava InvalidCastException não pode mais existir.
        Assert.DoesNotContain("(DateTimeOffset)terminadoEm", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteScalarAsync", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Termino_LeTimestampComProviderTypeSafe()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<DateTimeOffset?> TentarConcluirApontamentoAsync");

        // Leitura type-safe do timestamptz, coerente com o mapeador (GetFieldValue<DateTimeOffset>).
        Assert.Contains("GetFieldValue<DateTimeOffset>(0)", metodo, StringComparison.Ordinal);
        Assert.Contains("RETURNING terminado_em", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Termino_MantemFailClosedSemLinhaOuDBNull()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<DateTimeOffset?> TentarConcluirApontamentoAsync");

        // Sem linha (0 atualizadas) OU DBNull ⇒ null (nunca marca conclusão falsamente).
        Assert.Contains("IsDBNullAsync(0", metodo, StringComparison.Ordinal);
        Assert.Contains("return null;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Termino_PreservaContratoAtomicoEIdempotente()
    {
        string metodo = ExtrairMetodo(
            LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs"),
            "public async Task<DateTimeOffset?> TentarConcluirApontamentoAsync");

        // Transição estrita (só de AGUARDANDO_FINALIZACAO), término/idempotência na MESMA transação/UPDATE,
        // e o evento de auditoria SUCESSO_TERMINO segue na mesma transação.
        Assert.Contains("status = 'CONCLUIDA'", metodo, StringComparison.Ordinal);
        Assert.Contains("AND status = 'AGUARDANDO_FINALIZACAO'", metodo, StringComparison.Ordinal);
        Assert.Contains("idempotency_key_termino = @idempotency_key_termino", metodo, StringComparison.Ordinal);
        Assert.Contains("codigo_barras_termino = @codigo_barras_termino", metodo, StringComparison.Ordinal);
        Assert.Contains("InserirEventoAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", metodo, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            string candidato = Path.Combine(dir, "FugaPET_Dev.csproj");
            if (File.Exists(candidato))
            {
                return File.ReadAllText(Path.Combine(new[] { dir }.Concat(partes).ToArray()));
            }

            dir = Directory.GetParent(dir)?.FullName;
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
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
            {
                profundidade++;
            }
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte[inicio..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Fim do método não encontrado: {assinatura}");
    }
}
