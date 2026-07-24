using System.IO.Compression;
using System.Security.Cryptography;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Higienização do pacote Gaia 039: ON_ERROR_STOP por BYTES, ausência de BOM, timeouts operacionais
/// e equivalência SHA-256 entre o ZIP canônico e a pasta extraída.
/// </summary>
public sealed class Pacote039HigienizacaoTests
{
    private const string NomePacote = "039_controle_apontamentos_producao_GAIA";

    private static readonly string[] ArquivosSql =
    [
        "039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql",
        "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql",
        "039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql",
        "039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql"
    ];

    private static readonly string[] ArquivosCanonicos =
    [
        "039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql",
        "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql",
        "039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql",
        "039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql",
        "README_039_CONTROLE_APONTAMENTOS_GAIA.txt"
    ];

    // ---------- §5: ON_ERROR_STOP por bytes ----------

    [Theory]
    [InlineData("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql")]
    public void Sql_PrimeiroByte_DeveSerABarraDoSet(string arquivo)
    {
        byte[] bytes = File.ReadAllBytes(CaminhoNaPasta(arquivo));

        Assert.NotEmpty(bytes);
        // 0x5C = '\' de "\set". Nada de comentário nem BOM antes.
        Assert.Equal(0x5C, bytes[0]);
    }

    [Theory]
    [InlineData("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql")]
    public void Sql_NaoDeveTerBom(string arquivo)
    {
        byte[] bytes = File.ReadAllBytes(CaminhoNaPasta(arquivo));

        Assert.True(bytes.Length >= 3);
        // BOM UTF-8 = EF BB BF.
        bool temBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.False(temBom, $"{arquivo} não pode ter BOM.");
    }

    [Theory]
    [InlineData("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql")]
    public void Sql_DeveConterADiretivaOnErrorStop(string arquivo)
    {
        string conteudo = File.ReadAllText(CaminhoNaPasta(arquivo));

        Assert.Contains(@"\set ON_ERROR_STOP on", conteudo, StringComparison.Ordinal);
        // E ela é a PRIMEIRA linha do arquivo.
        string primeiraLinha = conteudo.Split('\n')[0].TrimEnd('\r');
        Assert.Equal(@"\set ON_ERROR_STOP on", primeiraLinha);
    }

    // ---------- §6: timeouts ----------

    [Theory]
    [InlineData("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_ROLLBACK_GAIA.sql")]
    public void PropostaERollback_DevemTerTimeoutsLocaisAposBegin(string arquivo)
    {
        string conteudo = File.ReadAllText(CaminhoNaPasta(arquivo));

        Assert.Contains("SET LOCAL lock_timeout = '5s';", conteudo, StringComparison.Ordinal);
        Assert.Contains("SET LOCAL statement_timeout = '60s';", conteudo, StringComparison.Ordinal);

        // Os timeouts vêm DEPOIS do BEGIN (senão o LOCAL não valeria).
        int begin = conteudo.IndexOf("BEGIN;", StringComparison.Ordinal);
        int lockTimeout = conteudo.IndexOf("SET LOCAL lock_timeout", StringComparison.Ordinal);
        Assert.True(begin >= 0 && lockTimeout > begin);
    }

    [Theory]
    [InlineData("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql")]
    [InlineData("039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql")]
    public void PreflightEValidacao_NaoPodemFicarSemLimite(string arquivo)
    {
        string conteudo = File.ReadAllText(CaminhoNaPasta(arquivo));

        Assert.Contains("SET lock_timeout = '5s';", conteudo, StringComparison.Ordinal);
        Assert.Contains("SET statement_timeout =", conteudo, StringComparison.Ordinal);
    }

    // ---------- §7: contagem calculada ----------

    [Fact]
    public void Validacao_NaoDeveTerContagemFixaDivergente()
    {
        string validacao = File.ReadAllText(CaminhoNaPasta("039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql"));

        Assert.DoesNotContain("VALIDACAO OK: 7 permissoes", validacao, StringComparison.Ordinal);
        // A quantidade é calculada e exibida.
        Assert.Contains("RAISE NOTICE 'VALIDACAO OK: % permissoes ativas e vinculadas ao perfil Administrador.', v_total;",
            validacao, StringComparison.Ordinal);
    }

    // ---------- §4: comentário obsoleto do término ----------

    [Fact]
    public void Proposta_NaoDeveAfirmarQueTerminoAceitaEmAndamento()
    {
        string proposta = File.ReadAllText(CaminhoNaPasta("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));

        // O comentário precisa refletir a transição ESTRITA realmente implementada.
        Assert.Contains("o UPDATE de termino exige", proposta, StringComparison.Ordinal);
        Assert.Contains("status = 'AGUARDANDO_FINALIZACAO' (transicao ESTRITA", proposta, StringComparison.Ordinal);
    }

    // ---------- §8: preflight estrutural completo ----------

    [Fact]
    public void Preflight_DeveValidarIdentityPkSequenceTamanhoDefaultsConstraintsIndicesEFk()
    {
        string preflight = File.ReadAllText(CaminhoNaPasta("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql"));

        // Identity + PK + sequence
        Assert.Contains("is_identity='YES' AND identity_generation='BY DEFAULT'", preflight, StringComparison.Ordinal);
        Assert.Contains("pc.contype = 'p'", preflight, StringComparison.Ordinal);
        Assert.Contains("pg_get_serial_sequence", preflight, StringComparison.Ordinal);

        // Tamanho exato de varchar
        Assert.Contains("character_maximum_length", preflight, StringComparison.Ordinal);

        // Defaults
        Assert.Contains("column_default", preflight, StringComparison.Ordinal);

        // Constraints por definição normalizada (não só nome)
        Assert.Contains("pg_get_constraintdef", preflight, StringComparison.Ordinal);
        foreach (string constraint in new[]
                 {
                     "ck_operacao_config_tipo_processo", "ck_operacao_config_operacao_nao_vazia",
                     "ck_apontamento_status", "ck_apontamento_resultado_operacional",
                     "ck_apontamento_termino_completo", "ck_apontamento_ordem_cronologica"
                 })
        {
            Assert.Contains(constraint, preflight, StringComparison.Ordinal);
        }

        // Índices (colunas/unicidade/predicado)
        foreach (string indice in new[]
                 {
                     "uq_operacao_config_ativa", "uq_apontamento_ativo_por_operacao",
                     "uq_apontamento_idempotency_inicio", "uq_apontamento_idempotency_termino"
                 })
        {
            Assert.Contains(indice, preflight, StringComparison.Ordinal);
        }

        Assert.Contains("CREATE UNIQUE INDEX' in v_def", preflight, StringComparison.Ordinal);
        Assert.Contains("predicado parcial", preflight, StringComparison.Ordinal);

        // FK evento -> apontamento com ON DELETE RESTRICT. A versão aprovada valida pelo CATÁLOGO
        // (conkey/confkey/confdeltype='r'), o que é mais rigoroso do que casar texto de pg_get_constraintdef.
        Assert.Contains("pc.contype = 'f'", preflight, StringComparison.Ordinal);
        Assert.Contains("pc.confdeltype = 'r'", preflight, StringComparison.Ordinal);
        Assert.Contains("pc.conkey = ARRAY[v_local_attnum]", preflight, StringComparison.Ordinal);
        Assert.Contains("pc.confkey = ARRAY[v_ref_attnum]", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void Preflight_DeveClassificarOEstadoDoPacote()
    {
        string preflight = File.ReadAllText(CaminhoNaPasta("039_controle_apontamentos_DEV_PREFLIGHT_GAIA.sql"));

        Assert.Contains("PACOTE_NAO_APLICADO", preflight, StringComparison.Ordinal);
        Assert.Contains("PACOTE_PARCIAL_INCOMPATIVEL", preflight, StringComparison.Ordinal);
        Assert.Contains("PACOTE_COMPLETAMENTE_APLICADO", preflight, StringComparison.Ordinal);
        // Estrutura parcial ABORTA.
        Assert.Contains(
            "RAISE EXCEPTION 'PREFLIGHT: PACOTE_PARCIAL_INCOMPATIVEL", preflight, StringComparison.Ordinal);
    }

    // ---------- §9: proposta segura ----------

    [Fact]
    public void Proposta_DeveSerTransacionalSemDeleteEComAsTresPermissoes()
    {
        string proposta = File.ReadAllText(CaminhoNaPasta("039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql"));

        Assert.Contains("BEGIN;", proposta, StringComparison.Ordinal);
        Assert.Contains("COMMIT;", proposta, StringComparison.Ordinal);
        Assert.Contains(@"\set ON_ERROR_STOP on", proposta, StringComparison.Ordinal); // aborta no 1º erro
        Assert.DoesNotContain("GRANT DELETE", proposta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", proposta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", proposta, StringComparison.OrdinalIgnoreCase);
        // Não apaga dados: nenhum DELETE FROM nas tabelas do módulo.
        Assert.DoesNotContain("DELETE FROM desenvolvimento.operacao_producao", proposta, StringComparison.Ordinal);
    }

    // ---------- §10: ZIP canônico ----------

    [Fact]
    public void ZipCanonico_DeveExistirForaDaPastaExtraida()
    {
        Assert.True(File.Exists(CaminhoZip()), $"ZIP canônico não encontrado: {CaminhoZip()}");

        // O ZIP é IRMÃO da pasta, não está DENTRO dela.
        string pastaDoZip = Path.GetFullPath(Path.GetDirectoryName(CaminhoZip())!);
        string pastaPacote = Path.GetFullPath(PastaPacote());
        Assert.NotEqual(pastaPacote, pastaDoZip);
        Assert.Equal(Path.GetFullPath(Path.Combine(pastaPacote, "..")), pastaDoZip);

        // E não há nenhum .zip dentro da pasta extraída.
        Assert.Empty(Directory.GetFiles(pastaPacote, "*.zip", SearchOption.AllDirectories));

        // ZIP canônico ÚNICO para o 039 (nenhuma variante duplicada ao lado).
        string[] zips039 = Directory.GetFiles(pastaDoZip, "039*.zip");
        Assert.Single(zips039);
    }

    [Fact]
    public void ZipCanonico_DeveConterExatamenteOsCincoArquivos()
    {
        using ZipArchive zip = ZipFile.OpenRead(CaminhoZip());

        string[] entradas = zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        string[] esperados = ArquivosCanonicos.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(esperados, entradas); // nem a menos, nem a mais
    }

    [Fact]
    public void ZipCanonico_DeveSerByteAByteIgualAPastaPorSha256()
    {
        string temporario = Path.Combine(Path.GetTempPath(), "fugapet_039_" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(CaminhoZip(), temporario);

            foreach (string arquivo in ArquivosCanonicos)
            {
                string naPasta = CaminhoNaPasta(arquivo);
                string extraido = Path.Combine(temporario, arquivo);

                Assert.True(File.Exists(extraido), $"Arquivo ausente no ZIP: {arquivo}");
                Assert.Equal(Sha256(naPasta), Sha256(extraido));
            }

            // Nenhum arquivo adicional no ZIP.
            string[] extraidos = Directory.GetFiles(temporario, "*", SearchOption.AllDirectories)
                .Select(c => Path.GetRelativePath(temporario, c))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(ArquivosCanonicos.OrderBy(n => n, StringComparer.Ordinal).ToArray(), extraidos);
        }
        finally
        {
            if (Directory.Exists(temporario))
            {
                Directory.Delete(temporario, recursive: true);
            }
        }
    }

    [Fact]
    public void PastaDoPacote_DeveConterExatamenteOsCincoArquivos()
    {
        string[] naPasta = Directory.GetFiles(PastaPacote())
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ArquivosCanonicos.OrderBy(n => n, StringComparer.Ordinal).ToArray(), naPasta);
    }

    [Fact]
    public void TodosOsSqlDoPacote_EstaoCobertosPelosTestesDeBytes()
        => Assert.Equal(
            ArquivosSql.OrderBy(n => n, StringComparer.Ordinal),
            Directory.GetFiles(PastaPacote(), "*.sql")
                .Select(Path.GetFileName)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal));

    // ---------- Apoio ----------

    private static string Sha256(string caminho)
    {
        using FileStream stream = File.OpenRead(caminho);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string PastaPacote()
        => Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", NomePacote);

    private static string CaminhoNaPasta(string arquivo) => Path.Combine(PastaPacote(), arquivo);

    private static string CaminhoZip()
        => Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", NomePacote + ".zip");

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
