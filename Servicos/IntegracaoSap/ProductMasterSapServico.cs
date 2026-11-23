using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1: serviço REAL do GET do tipo mestre do material no SAP
/// (API_PRODUCT_SRV / A_Product, OData V2, Basic Auth). Monta a URL validada pelo <see cref="ValidadorUrlSap"/>,
/// faz o GET (sem POST/PATCH/CSRF) e parseia com o <see cref="ProductMasterSapApiClient"/>. Nunca lança:
/// erros viram null + diagnóstico SANITIZADO. Reaproveita a base-URL derivada do Product Master (23... da descrição).
/// </summary>
internal sealed class ProductMasterSapServico : IProductMasterSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real (para exercitar parse/erro sem rede).
    private readonly Func<string, CancellationToken, Task<string?>>? _obterJsonOverride;

    // Costura SEPARADA para o GET dos EANs. Reusar a de cima faria um teste de produto responder também ao
    // EAN (e vice-versa), escondendo qual dos dois GETs o código realmente fez.
    private readonly Func<string, CancellationToken, Task<string?>>? _obterJsonEanOverride;

    public ProductMasterSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductMasterSapServico(
        ConfiguracaoSap configuracao,
        Func<string, CancellationToken, Task<string?>>? obterJsonOverride,
        Func<string, CancellationToken, Task<string?>>? obterJsonEanOverride = null)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
        _obterJsonEanOverride = obterJsonEanOverride;
    }

    public bool EhSimulado => false;
    public bool Configurado => _configuracao.ProductMasterConfigurado;

    public async Task<ProdutoSapMestre?> ObterProdutoAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
    {
        string codigo = (codigoProduto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        if (_obterJsonOverride is null && !_configuracao.ProductMasterConfigurado)
        {
            RegistrarDiagnostico($"Produto {codigo}: Product Master não configurado. Tipo não consultado.");
            return null;
        }

        try
        {
            string? json = await ObterJsonProdutoAsync(codigo, cancellationToken);
            SapProductMasterDto? dto = ProductMasterSapApiClient.ParsearProduto(json);
            if (dto is null || string.IsNullOrWhiteSpace(dto.ProductType))
            {
                RegistrarDiagnostico($"Produto {codigo}: A_Product sem ProductType utilizável. Item ficará Indefinido.");
                return null;
            }

            ProdutoSapMestre mestre = ProdutoSapMestre.DeDto(dto);
            RegistrarDiagnostico(
                $"Produto {codigo}: ProductType {mestre.TipoMaterialSap}; ProductGroup {mestre.GrupoMaterialSap}; "
                + $"BaseUnit {mestre.UnidadeBaseSap}; origem A_Product.");
            return mestre;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico(
                $"Produto {codigo}: GET A_Product HTTP status {((int?)ex.StatusCode)?.ToString() ?? "indefinido"}: {ex.GetType().Name}. Item Indefinido.");
            return null;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Produto {codigo}: GET A_Product etapa CLIENTE: {ex.GetType().Name}. Item Indefinido.");
            return null;
        }
    }

    /// <summary>
    /// GET dos EANs do material e escolha da linha da unidade CAIXA. Nunca lança: qualquer falha vira string
    /// vazia + diagnóstico, e a etiqueta mantém o aviso. Pedido do usuário em 23/11/2026.
    /// </summary>
    public async Task<string> ObterEanCaixaAsync(
        string codigoProduto,
        CancellationToken cancellationToken = default)
    {
        string codigo = (codigoProduto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return string.Empty;
        }

        if (_obterJsonEanOverride is null && !_configuracao.ProductMasterConfigurado)
        {
            RegistrarDiagnostico($"Produto {codigo}: Product Master não configurado. EAN da caixa não consultado.");
            return string.Empty;
        }

        try
        {
            string? json = await ObterJsonEansAsync(codigo, cancellationToken);
            IReadOnlyList<SapProdutoEanDto> eans = ProductMasterSapApiClient.ParsearEans(json);
            string ean = ProductMasterSapApiClient.EscolherEanDaCaixa(eans);

            if (string.IsNullOrWhiteSpace(ean))
            {
                // Distinguir "material sem EAN nenhum" de "tem EAN, mas não em CX" é o que permite ao
                // usuário saber se falta cadastrar a linha ou se o problema é a unidade.
                string unidades = eans.Count == 0
                    ? "nenhuma linha de EAN"
                    : "unidades " + string.Join("/", eans.Select(e => e.AlternativeUnit));
                RegistrarDiagnostico(
                    $"Produto {codigo}: sem EAN na unidade {ProductMasterSapApiClient.UnidadeCaixa} ({unidades}). Etiqueta sem barra de EAN.");
                return string.Empty;
            }

            RegistrarDiagnostico(
                $"Produto {codigo}: EAN da caixa {ean} (unidade {ProductMasterSapApiClient.UnidadeCaixa}); origem A_ProductUnitsOfMeasureEAN.");
            return ean;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico(
                $"Produto {codigo}: GET EAN HTTP status {((int?)ex.StatusCode)?.ToString() ?? "indefinido"}: {ex.GetType().Name}. Etiqueta sem barra de EAN.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Produto {codigo}: GET EAN etapa CLIENTE: {ex.GetType().Name}. Etiqueta sem barra de EAN.");
            return string.Empty;
        }
    }

    private async Task<string?> ObterJsonEansAsync(string codigo, CancellationToken cancellationToken)
    {
        if (_obterJsonEanOverride is not null)
        {
            return await _obterJsonEanOverride(codigo, cancellationToken);
        }

        Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
            _configuracao.ProductMasterBaseUrlEfetiva,
            _configuracao.HostsPermitidos);

        Uri url = ProductMasterSapApiClient.MontarUrlEansProduto(baseUri, codigo, _configuracao.SapClient);
        return await EnviarGetAsync(baseUri, url, cancellationToken);
    }

    private async Task<string?> ObterJsonProdutoAsync(string codigo, CancellationToken cancellationToken)
    {
        if (_obterJsonOverride is not null)
        {
            return await _obterJsonOverride(codigo, cancellationToken);
        }

        Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
            _configuracao.ProductMasterBaseUrlEfetiva,
            _configuracao.HostsPermitidos);

        Uri url = ProductMasterSapApiClient.MontarUrlProduto(baseUri, codigo, _configuracao.SapClient);
        return await EnviarGetAsync(baseUri, url, cancellationToken);
    }

    /// <summary>
    /// GET autenticado, com a validação de destino aplicada. Extraído ao acrescentar o GET dos EANs: sem
    /// isso, o segundo GET duplicaria a montagem do Basic Auth e a revalidação do host — e uma cópia que
    /// esquecesse <see cref="ValidadorUrlSap.ValidarDestino"/> deixaria passar redirecionamento para fora
    /// dos hosts permitidos. Só GET: nada de POST/PATCH/CSRF neste serviço.
    /// </summary>
    private async Task<string?> EnviarGetAsync(Uri baseUri, Uri url, CancellationToken cancellationToken)
    {
        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_configuracao.Usuario}:{_configuracao.Senha}"));

        using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
        using HttpRequestMessage requisicao = new(
            HttpMethod.Get,
            ValidadorUrlSap.ValidarDestino(url, baseUri, _configuracao.HostsPermitidos));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage resposta = await httpClient.SendAsync(requisicao, cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return resposta.IsSuccessStatusCode ? corpo : null;
    }

    internal static void RegistrarDiagnostico(string mensagem)
        => System.Diagnostics.Trace.TraceInformation($"[SAP][ProductMaster] {mensagem}");
}
