using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Paginacao do catalogo de produtos do centro (18/11/2026).
/// <para>
/// O servico paginava por <c>__next</c>. Esta instalacao do Gateway NAO devolve esse link: manda
/// exatamente o <c>$top</c> e nada mais. No mandante 130 o centro 3007 tem 1521 materiais, os 200
/// primeiros sao ROH/VERP e os produtos acabados comecam em 4000000 — a tela mostrava ZERO produto
/// e relatava SUCESSO, que e o pior jeito de errar. No DEV passou porque o centro cabia numa pagina.
/// </para>
/// </summary>
public sealed class CatalogoProdutoCentroPaginacao065Tests
{
    private const string Base = "https://sap.exemplo.com/API_PRODUCT_SRV/";

    [Fact]
    public void Url_PrimeiraPagina_NaoLevaSkip()
    {
        Uri url = CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(new Uri(Base), "3007", "130", 200);

        Assert.Contains("$top=200", url.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("$skip", url.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Url_PaginaSeguinte_LevaSkip()
    {
        Uri url = CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(new Uri(Base), "3007", "130", 200, 200);

        Assert.Contains("$top=200", url.ToString(), StringComparison.Ordinal);
        Assert.Contains("$skip=200", url.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// O caso que quebrou: tres paginas cheias e uma parcial, SEM __next em nenhuma. Antes da
    /// correcao o servico parava na primeira e devolvia 200 codigos.
    /// </summary>
    [Fact]
    public async Task SemNext_PercorreAtePaginaIncompleta()
    {
        List<string> pedidas = [];
        ICatalogoProdutoCentroSapServico servico = CriarServico(pedidas, (url, chamada) =>
        {
            // 0,1,2 cheias (200); a terceira traz 53 e encerra. Os FERT so aparecem na ultima.
            int quantidade = chamada < 3 ? 200 : 53;
            bool acabado = chamada == 3;
            return PaginaCentro(quantidade, chamada * 200, acabado);
        });

        ResultadoCatalogoProdutoCentro resultado = await servico.ListarProdutosAcabadosAsync("3007");

        Assert.True(resultado.Sucesso);
        Assert.Equal(4, pedidas.Count);
        Assert.DoesNotContain("$skip", pedidas[0], StringComparison.Ordinal);
        Assert.Contains("$skip=200", pedidas[1], StringComparison.Ordinal);
        Assert.Contains("$skip=400", pedidas[2], StringComparison.Ordinal);
        Assert.Contains("$skip=600", pedidas[3], StringComparison.Ordinal);
        Assert.Equal(53, resultado.Produtos.Count);
    }

    /// <summary>Pagina incompleta logo na primeira: uma consulta so, sem $skip.</summary>
    [Fact]
    public async Task PrimeiraPaginaIncompleta_NaoPedeMais()
    {
        List<string> pedidas = [];
        ICatalogoProdutoCentroSapServico servico = CriarServico(pedidas, (url, chamada) => PaginaCentro(10, 0, true));

        ResultadoCatalogoProdutoCentro resultado = await servico.ListarProdutosAcabadosAsync("3007");

        Assert.True(resultado.Sucesso);
        Assert.Single(pedidas);
        Assert.Equal(10, resultado.Produtos.Count);
    }

    /// <summary>
    /// Catalogo maior que o teto de paginas NAO vira lista parcial com cara de sucesso: devolve
    /// falha, porque apresentar parte do catalogo como se fosse o catalogo e o defeito original.
    /// </summary>
    [Fact]
    public async Task AcimaDoTeto_FalhaEmVezDeDevolverParcial()
    {
        List<string> pedidas = [];
        ICatalogoProdutoCentroSapServico servico = CriarServico(pedidas, (url, chamada) => PaginaCentro(200, chamada * 200, false));

        ResultadoCatalogoProdutoCentro resultado = await servico.ListarProdutosAcabadosAsync("3007");

        Assert.False(resultado.Sucesso);
        Assert.Equal(MotivoCatalogoIndisponivel.FalhaNaConsulta, resultado.Motivo);
        Assert.Empty(resultado.Produtos);
    }

    // ------------------------------------------------------------------
    private static ICatalogoProdutoCentroSapServico CriarServico(
        List<string> pedidas, Func<Uri, int, string> resposta)
    {
        ConfiguracaoSap configuracao = new()
        {
            ProductBaseUrl = Base,
            Usuario = "u",
            Senha = "s",
            SapClient = "130",
            HostsPermitidos = ["sap.exemplo.com"],
        };

        int chamadasDoCentro = 0;
        return new CatalogoProdutoCentroSapServico(configuracao, (url, _) =>
        {
            string texto = url.ToString();
            if (texto.Contains("A_ProductPlant", StringComparison.Ordinal))
            {
                pedidas.Add(texto);
                return Task.FromResult<string?>(resposta(url, chamadasDoCentro++));
            }
            if (texto.Contains("A_ProductDescription", StringComparison.Ordinal))
            {
                return Task.FromResult<string?>("""{"d":{"results":[]}}""");
            }
            return Task.FromResult<string?>(MestresDoFiltro(texto));
        });
    }

    private static string PaginaCentro(int quantidade, int inicio, bool contemAcabados)
    {
        StringBuilder json = new("""{"d":{"results":[""");
        for (int i = 0; i < quantidade; i++)
        {
            if (i > 0) json.Append(',');
            // Os codigos de produto acabado so entram na ultima pagina, como no caso real.
            long codigo = contemAcabados ? 4000000 + i : 1000000 + inicio + i;
            json.Append($$"""{"Product":"{{codigo}}","Plant":"3007","IsBatchManagementRequired":true}""");
        }
        json.Append("]}}");
        return json.ToString();
    }

    /// <summary>Mestre: tudo que comeca com 4 e FERT; o resto e ROH, como no centro real.</summary>
    private static string MestresDoFiltro(string url)
    {
        string decodificada = Uri.UnescapeDataString(url);
        List<string> codigos = [];
        int i = 0;
        while ((i = decodificada.IndexOf("Product eq '", i, StringComparison.Ordinal)) >= 0)
        {
            i += "Product eq '".Length;
            int fim = decodificada.IndexOf('\'', i);
            if (fim < 0) break;
            codigos.Add(decodificada[i..fim]);
            i = fim;
        }

        StringBuilder json = new("""{"d":{"results":[""");
        for (int k = 0; k < codigos.Count; k++)
        {
            if (k > 0) json.Append(',');
            string tipo = codigos[k].StartsWith('4') ? "FERT" : "ROH";
            json.Append($$"""{"Product":"{{codigos[k]}}","ProductType":"{{tipo}}","BaseUnit":"KG"}""");
        }
        json.Append("]}}");
        return json.ToString();
    }
}
