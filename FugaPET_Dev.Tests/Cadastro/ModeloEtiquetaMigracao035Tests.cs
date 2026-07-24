namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Modelo de Etiqueta: valida por source-scan o pacote FINAL de migração 035 (proposta controlada, não
/// executada): preflight detecta duplicidade, aplicar cria índice global/função/trigger/5 checks, validar
/// confere os objetos, rollback restaura o índice parcial e nenhum script apaga dados.
/// </summary>
public sealed class ModeloEtiquetaMigracao035Tests
{
    private const string Pasta = "035_modelo_etiqueta_regras_banco_FINAL_GAIA";

    [Theory]
    [InlineData("DEV")]
    [InlineData("HML")]
    public void Preflight_DetectaDuplicidadeGlobal(string amb)
    {
        string sql = LerScript($"035_modelo_etiqueta_preflight_{amb}.sql");
        Assert.Contains("GROUP BY upper(trim(nome_modelo_etiqueta)), versao", sql, StringComparison.Ordinal);
        Assert.Contains("HAVING count(*) > 1", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("HML")]
    public void Aplicar_CriaObjetosEsperadosEEhTransacionalEIdempotente(string amb)
    {
        string sql = LerScript($"035_modelo_etiqueta_aplicar_{amb}.sql");
        Assert.Contains("BEGIN;", sql, StringComparison.Ordinal);
        Assert.Contains("COMMIT;", sql, StringComparison.Ordinal);
        // Índice global (com IF NOT EXISTS) e remoção do parcial antigo.
        Assert.Contains("DROP INDEX IF EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("uq_modelo_etiqueta_nome_versao", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS uq_modelo_etiqueta_nome_versao_global", sql, StringComparison.Ordinal);
        // Função e trigger.
        Assert.Contains("CREATE OR REPLACE FUNCTION", sql, StringComparison.Ordinal);
        Assert.Contains("fn_bloqueia_inativar_modelo_com_etiqueta_ativa", sql, StringComparison.Ordinal);
        Assert.Contains("trg_bloqueia_inativar_modelo_com_etiqueta_ativa", sql, StringComparison.Ordinal);
        // 5 checks novos.
        Assert.Contains("ck_modelo_etiqueta_nome_tamanho", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_observacao_tamanho", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_dpi_positivo", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_largura_positiva", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_altura_positiva", sql, StringComparison.Ordinal);
        // Idempotência: não recria checks já existentes; preflight embutido antes de DROP/CREATE.
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM pg_constraint", sql, StringComparison.Ordinal);
        Assert.Contains("APLICAR abortado", sql, StringComparison.Ordinal);
        // Não recria versão/ZPL.
        Assert.DoesNotContain("ADD CONSTRAINT ck_modelo_etiqueta_versao", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ADD CONSTRAINT ck_modelo_etiqueta_zpl_nao_vazio", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("HML")]
    public void Validar_ConfirmaIndiceTriggerECincoChecks(string amb)
    {
        string sql = LerScript($"035_modelo_etiqueta_validar_{amb}.sql");
        Assert.Contains("uq_modelo_etiqueta_nome_versao_global", sql, StringComparison.Ordinal);
        Assert.Contains("trg_bloqueia_inativar_modelo_com_etiqueta_ativa", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_nome_tamanho", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_observacao_tamanho", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_dpi_positivo", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_largura_positiva", sql, StringComparison.Ordinal);
        Assert.Contains("ck_modelo_etiqueta_altura_positiva", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("HML")]
    public void Rollback_RestauraIndiceParcialERemoveObjetosDo035(string amb)
    {
        string sql = LerScript($"035_modelo_etiqueta_rollback_{amb}.sql");
        Assert.Contains("DROP TRIGGER IF EXISTS trg_bloqueia_inativar_modelo_com_etiqueta_ativa", sql, StringComparison.Ordinal);
        Assert.Contains("DROP FUNCTION IF EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("DROP INDEX IF EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("uq_modelo_etiqueta_nome_versao_global", sql, StringComparison.Ordinal);
        // Restaura o índice parcial anterior (WHERE situacao = true).
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS uq_modelo_etiqueta_nome_versao", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE situacao_modelo_etiqueta = true", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preflight")]
    [InlineData("aplicar")]
    [InlineData("validar")]
    [InlineData("rollback")]
    public void Scripts_NaoContemComandosDeExclusaoDeDados(string kind)
    {
        foreach (string amb in new[] { "DEV", "HML" })
        {
            string sql = LerScript($"035_modelo_etiqueta_{kind}_{amb}.sql").ToUpperInvariant();
            Assert.DoesNotContain("DELETE FROM", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("TRUNCATE", sql, StringComparison.Ordinal);
            // Não deve haver UPDATE de linhas de dados (apenas DDL). "UPDATE" só apareceria em "BEFORE UPDATE" (trigger).
            Assert.DoesNotContain("UPDATE MODELO_ETIQUETA SET", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Pacote_TemOsNoveArquivos()
    {
        string dir = Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", Pasta);
        foreach (string amb in new[] { "DEV", "HML" })
        {
            foreach (string kind in new[] { "preflight", "aplicar", "validar", "rollback" })
            {
                Assert.True(File.Exists(Path.Combine(dir, $"035_modelo_etiqueta_{kind}_{amb}.sql")),
                    $"Faltando 035_modelo_etiqueta_{kind}_{amb}.sql");
            }
        }
        Assert.True(File.Exists(Path.Combine(dir, "README_035_MODELO_ETIQUETA_FINAL_GAIA.txt")), "Faltando README.");
    }

    private static string LerScript(string nome)
        => File.ReadAllText(Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", Pasta, nome));

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
