using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Servico REAL do GET de administracao de lote por material × centro no SAP
/// (API_PRODUCT_SRV / A_ProductPlant, OData V2, Basic Auth). Reaproveita a base-URL/config do Product
/// Master (mesma API_PRODUCT_SRV), valida host/base com <see cref="ValidadorUrlSap"/>, faz o GET (sem
/// POST/PATCH/CSRF) e parseia com <see cref="ProductPlantSapApiClient"/>. Nunca lanca: erros viram null
/// + diagnostico SANITIZADO. Nunca expoe Authorization/senha/token.
/// </summary>
internal sealed class ProductPlantSapServico : IProductPlantSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real (exercita parse/erro sem rede).
    private readonly Func<string, string, CancellationToken, Task<string?>>? _obterJsonOverride;

    public ProductPlantSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductPlantSapServico(
        ConfiguracaoSap configuracao,
        Func<string, string, CancellationToken, Task<string?>>? obterJsonOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
    }

    public bool Configurado => _configuracao.ProductMasterConfigurado;

    public async Task<ProdutoCentroSapMestre?> ObterProdutoCentroAsync(
        string material,
        string centro,
        CancellationToken cancellationToken = default)
    {
        string mat = (material ?? string.Empty).Trim();
        string plant = (centro ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mat) || string.IsNullOrWhiteSpace(plant))
        {
            return null;
        }

        if (_obterJsonOverride is null && !_configuracao.ProductMasterConfigurado)
        {
            RegistrarDiagnostico($"Material {mat} centro {plant}: A_ProductPlant nao configurado. Administracao de lote nao consultada.");
            return null;
        }

        try
        {
            string? json = await ObterJsonProdutoCentroAsync(mat, plant, cancellationToken);
            SapProductPlantDto? dto = ProductPlantSapApiClient.ParsearProdutoCentro(json);
            if (dto is null)
            {
                RegistrarDiagnostico($"Material {mat} centro {plant}: A_ProductPlant sem registro utilizavel.");
                return null;
            }

            ProdutoCentroSapMestre mestre = ProdutoCentroSapMestre.DeDto(dto);
            RegistrarDiagnostico(
                $"Material {mat} centro {plant}: IsBatchManagementRequired="
                + (mestre.IsBatchManagementRequired?.ToString() ?? "indeterminado") + "; origem A_ProductPlant.");
            return mestre;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico(
                $"Material {mat} centro {plant}: GET A_ProductPlant HTTP status {((int?)ex.StatusCode)?.ToString() ?? "indefinido"}: {ex.GetType().Name}.");
            return null;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Material {mat} centro {plant}: GET A_ProductPlant etapa CLIENTE: {ex.GetType().Name}.");
            return null;
        }
    }

    private async Task<string?> ObterJsonProdutoCentroAsync(string material, string centro, CancellationToken cancellationToken)
    {
        if (_obterJsonOverride is not null)
        {
            return await _obterJsonOverride(material, centro, cancellationToken);
        }

        Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
            _configuracao.ProductMasterBaseUrlEfetiva,
            _configuracao.HostsPermitidos);

        Uri url = ProductPlantSapApiClient.MontarUrlProdutoCentro(baseUri, material, centro, _configuracao.SapClient);

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
        => System.Diagnostics.Trace.TraceInformation($"[SAP][ProductPlant] {mensagem}");
}
