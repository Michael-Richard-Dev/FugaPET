namespace FugaPET_Dev.Tests.Cadastro;

public sealed class EtiquetaRepositorioTests
{
    private static readonly string Raiz = LocalizarRaizProjeto();
    private static readonly string Repositorio = File.ReadAllText(Path.Combine(Raiz, "AcessoDados", "Repositorio", "EtiquetaRepositorio.cs"));
    private static readonly string DiretorioGaia034 = Path.Combine(
        Raiz,
        "BancoDados",
        "001_incrementais",
        "034_etiqueta_regras_banco_GAIA");

    [Fact]
    public void AtualizarAsync_NaoAlteraSituacaoEtiqueta()
    {
        string atualizar = ExtrairEntre(Repositorio, "public virtual Task<int> AtualizarAsync", "public virtual Task<int> ReativarAsync");

        Assert.DoesNotContain("situacao_etiqueta", atualizar, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExisteCodigoInternoAsync_EhGlobalIncluindoAtivosEInativos()
    {
        string metodo = ExtrairEntre(Repositorio, "public virtual async Task<bool> ExisteCodigoInternoAsync", "public virtual async Task<long> InserirAsync");

        Assert.Contains("upper(trim(codigo_interno)) = upper(trim(@codigo_interno))", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_etiqueta = true", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@codigo_interno", metodo, StringComparison.Ordinal);
        Assert.Contains("@ignorar_codigo", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcluirAsync_BloqueiaProdutoEtiquetaAtivoComNotExists()
    {
        string metodo = ExtrairEntre(Repositorio, "public virtual async Task<int> ExcluirAsync", "public virtual Task<int> AtualizarAsync");

        Assert.Contains("AND NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM produto_etiqueta pe", metodo, StringComparison.Ordinal);
        Assert.Contains("pe.situacao_produto_etiqueta = true", metodo, StringComparison.Ordinal);
        Assert.Contains("@codigo_etiqueta", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ReativarAsync_ExigeModeloAtivoENaoPermiteCodigoDuplicado()
    {
        string metodo = ExtrairEntre(Repositorio, "public virtual Task<int> ReativarAsync", "private static void PreencherParametros");

        Assert.Contains("EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("m.situacao_modelo_etiqueta = true", metodo, StringComparison.Ordinal);
        Assert.Contains("AND NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("upper(trim(outra.codigo_interno)) = upper(trim(etiqueta.codigo_interno))", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultasPermanecemParametrizadas()
    {
        Assert.Contains("ParametroTexto(\"@codigo_interno\"", Repositorio, StringComparison.Ordinal);
        Assert.Contains("ParametroLongo(\"@codigo_etiqueta\"", Repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"SELECT", Repositorio, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+ codigo", Repositorio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gaia034_ArquivosDaPropostaForamCriadosSemExecucaoAutomatica()
    {
        string[] arquivos =
        [
            "034_etiqueta_PREFLIGHT_GAIA.sql",
            "034_etiqueta_regras_banco_PROPOSTA_GAIA.sql",
            "034_etiqueta_regras_banco_ROLLBACK_GAIA.sql",
            "034_etiqueta_validacao_GAIA.sql",
            "README_034_ETIQUETA_GAIA.txt"
        ];

        foreach (string arquivo in arquivos)
        {
            string conteudo = File.ReadAllText(Path.Combine(DiretorioGaia034, arquivo));
            Assert.Contains("PROPOSTA - NAO EXECUTAR", conteudo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Gaia034_PropostaContemUnicidadeGlobalValidacoesEProtecaoDeInativacao()
    {
        string proposta = File.ReadAllText(Path.Combine(DiretorioGaia034, "034_etiqueta_regras_banco_PROPOSTA_GAIA.sql"));

        Assert.Contains("GROUP BY upper(trim(codigo_interno))", proposta, StringComparison.Ordinal);
        Assert.Contains("ck_etiqueta_codigo_tamanho", proposta, StringComparison.Ordinal);
        Assert.Contains("char_length(trim(codigo_interno)) BETWEEN 1 AND 80", proposta, StringComparison.Ordinal);
        Assert.Contains("char_length(trim(nome_etiqueta)) BETWEEN 2 AND 80", proposta, StringComparison.Ordinal);
        Assert.Contains("char_length(trim(descricao_etiqueta)) <= 255", proposta, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX uq_etiqueta_codigo_interno_global", proposta, StringComparison.Ordinal);
        Assert.Contains("ON desenvolvimento.etiqueta (upper(trim(codigo_interno)))", proposta, StringComparison.Ordinal);
        Assert.Contains("produto_etiqueta pe", proposta, StringComparison.Ordinal);
        Assert.Contains("pe.situacao_produto_etiqueta = true", proposta, StringComparison.Ordinal);
    }

    [Fact]
    public void Gaia034_ValidacaoConfereIndiceGlobalERegrasDeDados()
    {
        string validacao = File.ReadAllText(Path.Combine(DiretorioGaia034, "034_etiqueta_validacao_GAIA.sql"));

        Assert.Contains("uq_etiqueta_codigo_interno_global", validacao, StringComparison.Ordinal);
        Assert.Contains("indexdef NOT ILIKE '%WHERE%'", validacao, StringComparison.Ordinal);
        Assert.Contains("ck_etiqueta_codigo_tamanho", validacao, StringComparison.Ordinal);
        Assert.Contains("ck_etiqueta_nome_tamanho", validacao, StringComparison.Ordinal);
        Assert.Contains("ck_etiqueta_descricao_tamanho", validacao, StringComparison.Ordinal);
        Assert.Contains("trg_bloqueia_inativar_etiqueta_com_produto_ativo", validacao, StringComparison.Ordinal);
        Assert.Contains("HAVING count(*) > 1", validacao, StringComparison.Ordinal);
    }

    private static string ExtrairEntre(string fonte, string inicio, string fim)
    {
        int indiceInicio = fonte.IndexOf(inicio, StringComparison.Ordinal);
        int indiceFim = fonte.IndexOf(fim, indiceInicio + inicio.Length, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0 && indiceFim > indiceInicio);
        return fonte[indiceInicio..indiceFim];
    }

    private static string LocalizarRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            string projeto = Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj");
            if (File.Exists(projeto)) return diretorio.FullName;
            diretorio = diretorio.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
