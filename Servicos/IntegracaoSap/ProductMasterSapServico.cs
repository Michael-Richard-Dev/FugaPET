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

    public ProductMasterSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductMasterSapServico(
        ConfiguracaoSap configuracao,
        Func<string, CancellationToken, Task<string?>>? obterJsonOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
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
