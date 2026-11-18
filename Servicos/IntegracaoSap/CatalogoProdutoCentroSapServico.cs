using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Entrada unica da listagem de produtos ACABADOS de um centro. Somente leitura.</summary>
public interface ICatalogoProdutoCentroSapServico
{
    bool Configurado { get; }

    Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
        string centro, CancellationToken cancellationToken = default);
}

/// <summary>
/// Monta a lista de produtos acabados de um centro a partir de TRES consultas OData, porque nesta
/// instalacao nenhuma entidade sozinha traz o conjunto:
/// <list type="number">
/// <item>A_ProductPlant filtrado por <c>Plant</c> — quais materiais existem no centro;</item>
/// <item>A_Product em lotes — o tipo de material, que e o que separa acabado do resto;</item>
/// <item>A_ProductDescription em lotes — o nome que o operador reconhece.</item>
/// </list>
/// <para>
/// A ORDEM importa: o filtro de produto acabado (<c>ProductType = FERT</c>) acontece depois da
/// etapa 2, entao a etapa 3 ja pede descricao so do que vai aparecer na tela.
/// </para>
/// <para>
/// Nunca lanca para o chamador e nunca devolve lista vazia disfarcando falha: envelope ilegivel,
/// HTTP de erro ou excecao viram <see cref="ResultadoCatalogoProdutoCentro.Falha"/>. Confundir
/// "nao consegui perguntar" com "o centro nao tem produto" faria o usuario cadastrar vinculo em
/// cima de uma lista incompleta.
/// </para>
/// </summary>
public sealed class CatalogoProdutoCentroSapServico : ICatalogoProdutoCentroSapServico
{
    /// <summary>Tipo de material do SAP para produto acabado — a mesma letra usada no resto do dominio.</summary>
    public const string TipoProdutoAcabado = "FERT";

    private const string IdiomaDescricao = "PT";
    private const int TamanhoPagina = 200;

    /// <summary>
    /// Teto de paginas percorridas. O SAP devolve <c>__next</c> ate acabar; um teto evita que um
    /// filtro errado vire laco infinito segurando a tela. Com 200 por pagina, cobre 4 mil materiais.
    /// </summary>
    private const int MaximoPaginas = 20;

    private readonly ConfiguracaoSap _configuracao;
    private readonly Func<Uri, CancellationToken, Task<string?>>? _obterJsonOverride;

    public CatalogoProdutoCentroSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal CatalogoProdutoCentroSapServico(
        ConfiguracaoSap configuracao,
        Func<Uri, CancellationToken, Task<string?>>? obterJsonOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
    }

    public bool Configurado => _configuracao.ProductMasterConfigurado;

    public async Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        if (!Configurado)
        {
            return ResultadoCatalogoProdutoCentro.NaoConfigurado();
        }

        string plant = (centro ?? string.Empty).Trim();
        if (plant.Length == 0)
        {
            return ResultadoCatalogoProdutoCentro.Falha("Centro não informado.");
        }

        try
        {
            Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
                _configuracao.ProductMasterBaseUrlEfetiva, _configuracao.HostsPermitidos);

            IReadOnlyList<string>? codigos = await ListarCodigosDoCentroAsync(baseUri, plant, cancellationToken);
            if (codigos is null)
            {
                return ResultadoCatalogoProdutoCentro.Falha(
                    $"O SAP não devolveu a lista de materiais do centro {plant} de forma interpretável.");
            }
            if (codigos.Count == 0)
            {
                return ResultadoCatalogoProdutoCentro.Ok([]);
            }

            IReadOnlyList<ProdutoMestreSapCatalogo>? mestres = await ObterMestresAsync(baseUri, codigos, cancellationToken);
            if (mestres is null)
            {
                return ResultadoCatalogoProdutoCentro.Falha(
                    "O SAP não devolveu os dados mestres dos materiais de forma interpretável.");
            }

            List<ProdutoMestreSapCatalogo> acabados = [.. mestres.Where(EhProdutoAcabado)];
            if (acabados.Count == 0)
            {
                return ResultadoCatalogoProdutoCentro.Ok([]);
            }

            IReadOnlyDictionary<string, string> descricoes =
                await ObterDescricoesAsync(baseUri, [.. acabados.Select(m => m.Produto)], cancellationToken);

            List<ProdutoAcabadoDoCentro> produtos = [.. acabados
                .Select(m => new ProdutoAcabadoDoCentro
                {
                    Produto = m.Produto,
                    Descricao = descricoes.TryGetValue(m.Produto, out string? d) ? d : null,
                    TipoMaterial = m.TipoMaterial,
                    UnidadeBase = m.UnidadeBase,
                    MarcadoParaEliminacao = m.MarcadoParaEliminacao == true,
                })
                .OrderBy(p => p.Produto, StringComparer.Ordinal)];

            return ResultadoCatalogoProdutoCentro.Ok(produtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            // Diagnostico SANITIZADO: so o tipo da excecao. Mensagem crua de HttpRequestException
            // pode carregar URL interna, e isso vai parar na tela do operador.
            RegistrarDiagnostico($"Centro {plant}: {erro.GetType().Name}.");
            return ResultadoCatalogoProdutoCentro.Falha(
                $"Não foi possível consultar o SAP ({erro.GetType().Name}).");
        }
    }

    /// <summary>
    /// Percorre as paginas de A_ProductPlant. Devolve null quando QUALQUER pagina veio ilegivel —
    /// uma lista parcial aqui viraria "produto nao existe no centro" na tela.
    /// </summary>
    private async Task<IReadOnlyList<string>?> ListarCodigosDoCentroAsync(
        Uri baseUri, string plant, CancellationToken cancellationToken)
    {
        List<string> codigos = [];
        Uri? url = CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(
            baseUri, plant, _configuracao.SapClient, TamanhoPagina);

        for (int pagina = 0; pagina < MaximoPaginas && url is not null; pagina++)
        {
            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);
            PaginaCatalogoProdutoSap resultado = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(corpo);
            if (!resultado.Valida)
            {
                RegistrarDiagnostico($"Pagina {pagina} do centro {plant} invalida: {resultado.Motivo}.");
                return null;
            }

            codigos.AddRange(resultado.Itens.Select(i => i.Produto));

            url = string.IsNullOrWhiteSpace(resultado.ProximaPagina)
                ? null
                : ValidadorUrlSap.ValidarDestino(new Uri(resultado.ProximaPagina!), baseUri, _configuracao.HostsPermitidos);
        }

        return codigos.Distinct(StringComparer.Ordinal).ToArray();
    }

    private async Task<IReadOnlyList<ProdutoMestreSapCatalogo>?> ObterMestresAsync(
        Uri baseUri, IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        List<ProdutoMestreSapCatalogo> mestres = [];
        foreach (IReadOnlyList<string> lote in CatalogoProdutoSapApiClient.PartirEmLotes(codigos))
        {
            Uri url = CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(baseUri, lote, _configuracao.SapClient);
            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);

            IReadOnlyList<ProdutoMestreSapCatalogo> parcial =
                CatalogoProdutoSapApiClient.InterpretarProdutosMestre(corpo);

            // Lote que volta vazio com codigos pedidos e sinal de envelope ilegivel, nao de materiais
            // sem cadastro: A_Product sempre devolve linha para codigo que existe no centro.
            if (parcial.Count == 0)
            {
                RegistrarDiagnostico($"Lote de {lote.Count} materiais sem mestre interpretavel.");
                return null;
            }

            mestres.AddRange(parcial);
        }

        return mestres;
    }

    /// <summary>
    /// Descricoes em lote. Diferente das duas etapas anteriores, a falta aqui NAO invalida a
    /// listagem: um produto sem nome ainda pode ser vinculado pelo codigo, e barrar a tela inteira
    /// por causa do rotulo seria pior do que mostrar o codigo sozinho.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ObterDescricoesAsync(
        Uri baseUri, IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        Dictionary<string, string> todas = new(StringComparer.Ordinal);
        foreach (IReadOnlyList<string> lote in CatalogoProdutoSapApiClient.PartirEmLotes(codigos))
        {
            Uri url = CatalogoProdutoSapApiClient.MontarUrlDescricoesLote(
                baseUri, lote, IdiomaDescricao, _configuracao.SapClient);

            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);
            foreach (KeyValuePair<string, string> item in CatalogoProdutoSapApiClient.InterpretarDescricoes(corpo))
            {
                todas.TryAdd(item.Key, item.Value);
            }
        }

        return todas;
    }

    private static bool EhProdutoAcabado(ProdutoMestreSapCatalogo mestre)
        => string.Equals(mestre.TipoMaterial?.Trim(), TipoProdutoAcabado, StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ObterJsonAsync(Uri url, Uri baseUri, CancellationToken cancellationToken)
    {
        if (_obterJsonOverride is not null)
        {
            return await _obterJsonOverride(url, cancellationToken);
        }

        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_configuracao.Usuario}:{_configuracao.Senha}"));

        using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
        using HttpRequestMessage requisicao = new(
            HttpMethod.Get,
            ValidadorUrlSap.ValidarDestino(url, baseUri, _configuracao.HostsPermitidos));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage resposta = await httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return resposta.IsSuccessStatusCode ? corpo : null;
    }

    internal static void RegistrarDiagnostico(string mensagem)
        => System.Diagnostics.Trace.TraceInformation($"[SAP][CatalogoProdutoCentro] {mensagem}");
}
