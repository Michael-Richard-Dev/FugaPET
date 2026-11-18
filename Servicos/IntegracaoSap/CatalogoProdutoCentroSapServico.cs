using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Leitura do catalogo de produtos de um centro no SAP. Somente leitura.</summary>
public interface ICatalogoProdutoCentroSapServico
{
    bool Configurado { get; }

    /// <summary>Tudo de uma vez: lista o centro, le o cadastro e filtra os acabados.</summary>
    Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
        string centro, CancellationToken cancellationToken = default);

    /// <summary>
    /// So os CODIGOS que o centro tem hoje. E a etapa que a sincronizacao refaz por inteiro, porque
    /// e ela que revela o que entrou e o que saiu.
    /// </summary>
    Task<ResultadoCodigosCentro> ListarCodigosDoCentroAsync(
        string centro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastro de um conjunto especifico de materiais — TODOS os tipos, nao so os acabados.
    /// <para>
    /// Trazer tambem os que nao interessam a tela e proposital: e o que permite gravar "ja perguntei
    /// por este e ele e ROH" e nunca mais perguntar. Filtrar antes de gravar faria a sincronizacao
    /// repetir a pergunta para os mesmos materiais em toda rodada.
    /// </para>
    /// </summary>
    Task<ResultadoCadastroProdutos> ObterCadastroAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken = default);
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
        ResultadoCodigosCentro codigos = await ListarCodigosDoCentroAsync(centro, cancellationToken);
        if (!codigos.Sucesso)
        {
            return codigos.Motivo == MotivoCatalogoIndisponivel.NaoConfigurado
                ? ResultadoCatalogoProdutoCentro.NaoConfigurado()
                : ResultadoCatalogoProdutoCentro.Falha(codigos.MensagemDiagnostico);
        }
        if (codigos.Codigos.Count == 0)
        {
            return ResultadoCatalogoProdutoCentro.Ok([]);
        }

        ResultadoCadastroProdutos cadastro = await ObterCadastroAsync(codigos.Codigos, cancellationToken);
        if (!cadastro.Sucesso)
        {
            return ResultadoCatalogoProdutoCentro.Falha(cadastro.MensagemDiagnostico);
        }

        return ResultadoCatalogoProdutoCentro.Ok(
            [.. cadastro.Itens.Where(EhProdutoAcabado).OrderBy(p => p.Produto, StringComparer.Ordinal)]);
    }

    public async Task<ResultadoCodigosCentro> ListarCodigosDoCentroAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        if (!Configurado)
        {
            return ResultadoCodigosCentro.Falha(
                "Integração SAP não configurada nesta estação: usuário, senha ou URL de produto ausentes.",
                MotivoCatalogoIndisponivel.NaoConfigurado);
        }

        string plant = (centro ?? string.Empty).Trim();
        if (plant.Length == 0)
        {
            return ResultadoCodigosCentro.Falha("Centro não informado.", MotivoCatalogoIndisponivel.FalhaNaConsulta);
        }

        try
        {
            Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
                _configuracao.ProductMasterBaseUrlEfetiva, _configuracao.HostsPermitidos);

            (IReadOnlyList<string>? codigos, int chamadas) =
                await PercorrerCodigosDoCentroAsync(baseUri, plant, cancellationToken);

            return codigos is null
                ? ResultadoCodigosCentro.Falha(
                    $"O SAP não devolveu a lista de materiais do centro {plant} de forma interpretável.",
                    MotivoCatalogoIndisponivel.FalhaNaConsulta, chamadas)
                : ResultadoCodigosCentro.Ok(codigos, chamadas);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            RegistrarDiagnostico($"Centro {plant}: {erro.GetType().Name}.");
            return ResultadoCodigosCentro.Falha(
                $"Não foi possível consultar o SAP ({erro.GetType().Name}).",
                MotivoCatalogoIndisponivel.FalhaNaConsulta);
        }
    }

    public async Task<ResultadoCadastroProdutos> ObterCadastroAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        if (!Configurado)
        {
            return ResultadoCadastroProdutos.Falha(
                "Integração SAP não configurada nesta estação: usuário, senha ou URL de produto ausentes.");
        }
        if (codigos.Count == 0)
        {
            return ResultadoCadastroProdutos.Ok([], 0);
        }

        try
        {
            Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
                _configuracao.ProductMasterBaseUrlEfetiva, _configuracao.HostsPermitidos);

            (IReadOnlyList<ProdutoMestreSapCatalogo>? mestres, int chamadasMestre) =
                await BuscarMestresAsync(baseUri, codigos, cancellationToken);

            if (mestres is null)
            {
                return ResultadoCadastroProdutos.Falha(
                    "O SAP não devolveu os dados mestres dos materiais de forma interpretável.", chamadasMestre);
            }

            // Descricao so faz sentido para o que a tela mostra; pedir para ROH e VERP seria gastar
            // chamada com material que nunca vai aparecer na lista.
            string[] acabados = [.. mestres.Where(EhProdutoAcabado).Select(m => m.Produto)];
            (IReadOnlyDictionary<string, string> descricoes, int chamadasDescricao) =
                acabados.Length == 0
                    ? (new Dictionary<string, string>(StringComparer.Ordinal), 0)
                    : await BuscarDescricoesAsync(baseUri, acabados, cancellationToken);

            List<ProdutoAcabadoDoCentro> itens = [.. mestres.Select(m => new ProdutoAcabadoDoCentro
            {
                Produto = m.Produto,
                Descricao = descricoes.TryGetValue(m.Produto, out string? d) ? d : null,
                TipoMaterial = m.TipoMaterial,
                UnidadeBase = m.UnidadeBase,
                MarcadoParaEliminacao = m.MarcadoParaEliminacao == true,
            })];

            return ResultadoCadastroProdutos.Ok(itens, chamadasMestre + chamadasDescricao);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            RegistrarDiagnostico($"Cadastro de {codigos.Count} materiais: {erro.GetType().Name}.");
            return ResultadoCadastroProdutos.Falha($"Não foi possível consultar o SAP ({erro.GetType().Name}).");
        }
    }

    /// <summary>
    /// Percorre as paginas de A_ProductPlant. Devolve null quando QUALQUER pagina veio ilegivel —
    /// uma lista parcial aqui viraria "produto nao existe no centro" na tela.
    /// <para>
    /// A paginacao e por <c>$skip</c>, nao por <c>__next</c>. Esta instalacao do Gateway nao devolve
    /// o link de continuacao: manda exatamente o <c>$top</c> pedido e mais nada. Quem confia so no
    /// link le a primeira pagina e para. Foi o que aconteceu em 18/11/2026 no mandante 130, onde o
    /// centro 3007 tem 1521 materiais, os 200 primeiros sao ROH/VERP e a tela mostrava ZERO produto
    /// acabado — relatando sucesso, que e o pior jeito de errar.
    /// </para>
    /// <para>
    /// O criterio de fim e a PAGINA INCOMPLETA: pagina cheia significa que pode haver mais. Quando o
    /// servico devolve o <c>__next</c>, ele tem precedencia, porque ai e a propria fonte dizendo
    /// onde continuar.
    /// </para>
    /// </summary>
    private async Task<(IReadOnlyList<string>? Codigos, int Chamadas)> PercorrerCodigosDoCentroAsync(
        Uri baseUri, string plant, CancellationToken cancellationToken)
    {
        List<string> codigos = [];
        int chamadas = 0;
        Uri? url = CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(
            baseUri, plant, _configuracao.SapClient, TamanhoPagina);
        int lidos = 0;

        for (int pagina = 0; pagina < MaximoPaginas && url is not null; pagina++)
        {
            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);
            chamadas++;
            PaginaCatalogoProdutoSap resultado = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(corpo);
            if (!resultado.Valida)
            {
                RegistrarDiagnostico($"Pagina {pagina} do centro {plant} invalida: {resultado.Motivo}.");
                return (null, chamadas);
            }

            codigos.AddRange(resultado.Itens.Select(i => i.Produto));
            lidos += resultado.Itens.Count;

            if (!string.IsNullOrWhiteSpace(resultado.ProximaPagina))
            {
                url = ValidadorUrlSap.ValidarDestino(
                    new Uri(resultado.ProximaPagina!), baseUri, _configuracao.HostsPermitidos);
            }
            else if (resultado.Itens.Count == TamanhoPagina)
            {
                url = CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(
                    baseUri, plant, _configuracao.SapClient, TamanhoPagina, lidos);
            }
            else
            {
                url = null;
            }
        }

        if (url is not null)
        {
            // Teto de paginas atingido com pagina ainda cheia: a lista esta INCOMPLETA. Devolver o
            // que se tem seria apresentar um catalogo parcial como se fosse o catalogo.
            RegistrarDiagnostico($"Centro {plant}: teto de {MaximoPaginas} paginas atingido com {lidos} materiais.");
            return (null, chamadas);
        }

        return (codigos.Distinct(StringComparer.Ordinal).ToArray(), chamadas);
    }

    private async Task<(IReadOnlyList<ProdutoMestreSapCatalogo>? Mestres, int Chamadas)> BuscarMestresAsync(
        Uri baseUri, IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        List<ProdutoMestreSapCatalogo> mestres = [];
        int chamadas = 0;
        foreach (IReadOnlyList<string> lote in CatalogoProdutoSapApiClient.PartirEmLotes(codigos))
        {
            Uri url = CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(baseUri, lote, _configuracao.SapClient);
            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);
            chamadas++;

            IReadOnlyList<ProdutoMestreSapCatalogo> parcial =
                CatalogoProdutoSapApiClient.InterpretarProdutosMestre(corpo);

            // Lote que volta vazio com codigos pedidos e sinal de envelope ilegivel, nao de materiais
            // sem cadastro: A_Product sempre devolve linha para codigo que existe no centro.
            if (parcial.Count == 0)
            {
                RegistrarDiagnostico($"Lote de {lote.Count} materiais sem mestre interpretavel.");
                return (null, chamadas);
            }

            mestres.AddRange(parcial);
        }

        return (mestres, chamadas);
    }

    /// <summary>
    /// Descricoes em lote. Diferente das duas etapas anteriores, a falta aqui NAO invalida a
    /// listagem: um produto sem nome ainda pode ser vinculado pelo codigo, e barrar a tela inteira
    /// por causa do rotulo seria pior do que mostrar o codigo sozinho.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, string> Descricoes, int Chamadas)> BuscarDescricoesAsync(
        Uri baseUri, IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        Dictionary<string, string> todas = new(StringComparer.Ordinal);
        int chamadas = 0;
        foreach (IReadOnlyList<string> lote in CatalogoProdutoSapApiClient.PartirEmLotes(codigos))
        {
            Uri url = CatalogoProdutoSapApiClient.MontarUrlDescricoesLote(
                baseUri, lote, IdiomaDescricao, _configuracao.SapClient);

            string? corpo = await ObterJsonAsync(url, baseUri, cancellationToken);
            chamadas++;
            foreach (KeyValuePair<string, string> item in CatalogoProdutoSapApiClient.InterpretarDescricoes(corpo))
            {
                todas.TryAdd(item.Key, item.Value);
            }
        }

        return (todas, chamadas);
    }

    private static bool EhProdutoAcabado(ProdutoMestreSapCatalogo mestre)
        => EhProdutoAcabado(mestre.TipoMaterial);

    private static bool EhProdutoAcabado(ProdutoAcabadoDoCentro produto)
        => EhProdutoAcabado(produto.TipoMaterial);

    private static bool EhProdutoAcabado(string? tipoMaterial)
        => string.Equals(tipoMaterial?.Trim(), TipoProdutoAcabado, StringComparison.OrdinalIgnoreCase);

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
