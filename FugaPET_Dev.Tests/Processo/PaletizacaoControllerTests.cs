using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Npgsql;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// INCREMENTAL 047: reúso de serviços pela Paletização. MontarPaletePorSelecao reaproveita o validador ÚNICO
/// (mesmas regras do Produto Acabado) e as leituras por HU são fail-closed sem tocar o banco quando o filtro
/// vem vazio. Sem SAP/POST.
/// </summary>
public sealed class PaletizacaoControllerTests
{
    private static ProdutoAcabadoController Controller() => new(new ProductionOrderSapFake());

    private static ProdutoAcabadoCaixa Caixa(int numero, long codigo, string op = "1001",
        StatusIntegracaoCaixa status = StatusIntegracaoCaixa.ConfirmadaSap, string hu = "HU", string material = "2000091", string lote = "L1") => new()
    {
        CodigoProdutoAcabadoCaixa = codigo,
        NumeroCaixa = numero,
        NumeroOrdemProducao = op,
        Material = material,
        Lote = lote,
        Centro = "3007",
        Deposito = "PP01",
        PesoBrutoKg = 10m,
        PesoLiquidoKg = 9m,
        TaraKg = 1m,
        StatusIntegracao = status,
        HandlingUnitExternalId = $"{hu}-{numero}"
    };

    [Fact]
    public void MontarPaletePorSelecao_CaixasValidas_AgregaPelosPesosEUsaValidador()
    {
        ProdutoAcabadoPalete palete = Controller().MontarPaletePorSelecao(
            [Caixa(2, 102), Caixa(3, 103)], [], "PALLET01");

        Assert.Equal(2, palete.Caixas.Count);
        Assert.Equal(2, palete.PrimeiraCaixa);
        Assert.Equal(3, palete.UltimaCaixa);
        Assert.Equal(20m, palete.PesoBrutoKg);
        Assert.Equal(18m, palete.PesoLiquidoKg);
        Assert.Equal(2m, palete.TaraKg);
        Assert.Equal("PALLET01", palete.PackagingMaterial);
        Assert.Equal("3007", palete.Plant);
        Assert.Equal("PP01", palete.StorageLocation);
        Assert.Contains("PLT-1001", palete.CodigoPaleteLocal, StringComparison.Ordinal);
    }

    [Fact] // regra preservada: caixa não CONFIRMADA_SAP ⇒ rejeita (validador único)
    public void MontarPaletePorSelecao_CaixaNaoConfirmada_Rejeita()
    {
        Assert.Throws<InvalidOperationException>(() => Controller().MontarPaletePorSelecao(
            [Caixa(2, 102), Caixa(3, 103, status: StatusIntegracaoCaixa.ErroSap)], [], "PALLET01"));
    }

    [Fact]
    public void MontarPaletePorSelecao_ManualDuasOpsDiferentes_Aceita()
    {
        ProdutoAcabadoPalete palete = Controller().MontarPaletePorSelecao(
            [Caixa(2, 102, op: "1001"), Caixa(3, 103, op: "2002")], [], "PALLET01");

        Assert.Equal(2, palete.Caixas.Count);
        Assert.Equal("PLT-MULTIOP-0002-0003", palete.CodigoPaleteLocal);
    }

    [Fact]
    public void MontarPaletePorSelecao_ManualDoisMateriaisELotesDiferentes_Aceita()
    {
        ProdutoAcabadoPalete palete = Controller().MontarPaletePorSelecao(
            [Caixa(2, 102, material: "MAT-A", lote: "L1"), Caixa(3, 103, material: "MAT-B", lote: "L2")], [], "PALLET01");

        Assert.Equal(2, palete.Caixas.Count);
        Assert.Contains(palete.Caixas, c => c.Material == "MAT-A" && c.Lote == "L1");
        Assert.Contains(palete.Caixas, c => c.Material == "MAT-B" && c.Lote == "L2");
    }

    [Fact]
    public void ValidadorGlobal_PreservaMesmaOpPorDefault()
    {
        FugaPET_Dev.Servicos.Operacao.ResultadoPaleteLocal resultado = FugaPET_Dev.Servicos.Operacao.ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar(
            "1001",
            [Caixa(2, 102, op: "1001"), Caixa(3, 103, op: "2002")],
            []);

        Assert.False(resultado.Sucesso);
        Assert.Contains("OP diferente", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // seleção vazia ⇒ bloqueia
    public void MontarPaletePorSelecao_SemCaixas_Rejeita()
        => Assert.Throws<InvalidOperationException>(() => Controller().MontarPaletePorSelecao([], [], "PALLET01"));

    [Fact] // leitura por HU (manual) com lista vazia ⇒ fail-closed, sem abrir conexão
    public async Task ListarPorHandlingUnits_Vazio_FailClosed_SemConexao()
    {
        ProdutoAcabadoRepositorio repo = new(new FabricaConexaoFalha());
        Assert.Empty(await repo.ListarPorHandlingUnitsAsync([]));
    }

    [Fact] // leitura por INTERVALO de HU (sequência) com HU vazia ⇒ fail-closed, sem abrir conexão
    public async Task ListarPorIntervaloHu_Vazio_FailClosed_SemConexao()
    {
        ProdutoAcabadoRepositorio repo = new(new FabricaConexaoFalha());
        Assert.Empty(await repo.ListarPorIntervaloHandlingUnitAsync("", "", null));
    }

    [Fact]
    public void Repositorio_HuExata_UsaCapabilityGaia047()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");

        Assert.Contains("fn_pa_047_caixa_buscar_hu_exata(@hu)", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("handling_unit_external_id = ANY(@hus)", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_IntervaloHu_UsaCapabilityGaia047_SemRangeLexicografico()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");

        Assert.Contains("fn_pa_047_caixa_listar_intervalo_hu(p_material => @mat, p_hu_inicial => @ini, p_hu_final => @fim)", fonte, StringComparison.Ordinal);
        Assert.Contains("cmd.Parameters.AddWithValue(\"mat\", NpgsqlDbType.Text, mat)", fonte, StringComparison.Ordinal);
        Assert.Contains("cmd.Parameters.AddWithValue(\"ini\", NpgsqlDbType.Text, ini)", fonte, StringComparison.Ordinal);
        Assert.Contains("cmd.Parameters.AddWithValue(\"fim\", NpgsqlDbType.Text, fim)", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_pa_047_caixa_listar_intervalo_hu(@ini,@fim,@mat)", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("handling_unit_external_id BETWEEN @ini AND @fim", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("string.CompareOrdinal(ini, fim)", fonte, StringComparison.Ordinal);
    }

    [Fact] // GATE 047-V: capabilities 047 projetam SOMENTE as 15 colunas do RETURNS TABLE físico (senão 42703).
    public void Repositorio_Capabilities047_ProjetamSomenteAs15ColunasRetornadas()
    {
        string fonte = LerProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");

        // As duas capabilities usam a projeção específica + hidratação restrita (nunca ColunasCaixa genérica).
        Assert.Contains("SELECT {ColunasCaixa047}", fonte, StringComparison.Ordinal);
        Assert.Contains("Hidratar047(leitor)", fonte, StringComparison.Ordinal);
        Assert.Contains("private static ProdutoAcabadoCaixa Hidratar047(", fonte, StringComparison.Ordinal);

        string projecao = ExtrairBloco(fonte, "ColunasCaixa047 = \"\"\"", "\"\"\";");

        string[] retornadas =
        [
            "codigo_hu_caixa", "handling_unit_external_id", "numero_ordem_producao", "material", "lote",
            "numero_caixa", "hu_caixa", "codigo_caixa_local", "peso_bruto", "peso_liquido", "peso_tara",
            "unidade_peso", "centro", "deposito", "status_hu_caixa"
        ];
        foreach (string coluna in retornadas)
        {
            Assert.Contains(coluna, projecao, StringComparison.Ordinal);
        }

        // Nenhuma coluna que NÃO pertence ao RETURNS TABLE das capabilities 047 pode estar na projeção.
        string[] naoRetornadas =
        [
            "item_ordem_producao", "material_embalagem", "origem_material_embalagem", "quantidade",
            "unidade_quantidade", "origem_pesagem", "codigo_balanca", "codigo_usuario", "terminal",
            "correlation_id", "request_json_sanitizado", "response_json_sanitizado", "erro_sanitizado",
            "http_status", "tentativas", "claim_token", "criado_em", "atualizado_em", "enviado_sap_em", "confirmado_sap_em"
        ];
        foreach (string coluna in naoRetornadas)
        {
            Assert.DoesNotContain(coluna, projecao, StringComparison.Ordinal);
        }
    }

    [Theory] // GATE 047-CH: regra MVP centralizada — só PALLET01 (trim, case-insensitive); vazio/NULL/outros ⇒ false.
    [InlineData("PALLET01", true)]
    [InlineData("pallet01", true)]
    [InlineData("  PALLET01  ", true)]
    [InlineData("PALLET02", false)]
    [InlineData("PALLET03", false)]
    [InlineData("PALLET05", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void PackagingMaterialMvp_AutorizaSomentePallet01(string? material, bool esperado)
        => Assert.Equal(esperado, FugaPET_Dev.Modelo.Processo.PackagingMaterialMvp.Autorizado(material));

    [Fact] // ponto único da regra MVP
    public void PackagingMaterialMvp_Permitido_EhPallet01()
        => Assert.Equal("PALLET01", FugaPET_Dev.Modelo.Processo.PackagingMaterialMvp.Permitido);

    private static string ExtrairBloco(string fonte, string inicio, string fim)
    {
        int i = fonte.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(i >= 0, $"Marcador de início não encontrado: {inicio}");
        i += inicio.Length;
        int f = fonte.IndexOf(fim, i, StringComparison.Ordinal);
        Assert.True(f > i, $"Marcador de fim não encontrado: {fim}");
        return fonte[i..f];
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
    private sealed class FabricaConexaoFalha : IFabricaConexaoBanco
    {
        public NpgsqlConnection CriarConexao() => throw NaoDeveria();
        public Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default) => throw NaoDeveria();
        public string ObterConnectionString() => throw NaoDeveria();
        private static InvalidOperationException NaoDeveria() => new("Conexão não deveria ser aberta em consulta fail-closed.");
    }

    private sealed class ProductionOrderSapFake : IProductionOrderSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;
        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoConfigurado("Fake sem consulta SAP."));
    }
}





