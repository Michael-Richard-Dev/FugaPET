using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: serviço REAL do GET da descrição do material no SAP
/// (API_PRODUCT_SRV / A_ProductDescription, OData V2, Basic Auth). Monta a URL validada pelo
/// <see cref="ValidadorUrlSap"/>, faz o GET (sem POST/PATCH/CSRF), parseia <c>d.results</c> pelo
/// <see cref="ProductMasterSapApiClient"/> e escolhe o melhor idioma (PT→P→EN→primeira). Nunca lança:
/// erros viram null + diagnóstico SANITIZADO (etapa/status/tipo — sem Authorization/senha/cookie).
/// </summary>
internal sealed class ProductDescriptionSapServico : IProductDescriptionSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real (para exercitar parse/escolha/erro sem rede).
    private readonly Func<string, CancellationToken, Task<string?>>? _obterJsonOverride;

    public ProductDescriptionSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductDescriptionSapServico(
        ConfiguracaoSap configuracao,
        Func<string, CancellationToken, Task<string?>>? obterJsonOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
    }

    public bool EhSimulado => false;
    public bool Configurado => _configuracao.ProductMasterConfigurado;

    public async Task<ProdutoSapMestre?> ObterDescricaoAsync(
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
            RegistrarDiagnostico($"Produto {codigo}: Product Master não configurado. Descrição não consultada.");
            return null;
        }

        try
        {
            string? json = await ObterJsonDescricaoAsync(codigo, cancellationToken);
            IReadOnlyList<SapProductDescriptionDto> descricoes = ProductMasterSapApiClient.ParsearDescricoes(json);

            // Ajuste 4: só considera descrições do MESMO produto (defensivo) antes de escolher o idioma.
            List<SapProductDescriptionDto> doProduto = [.. descricoes.Where(d =>
                string.Equals((d.Product ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(d.Product))];
            IEnumerable<SapProductDescriptionDto> candidatas = doProduto.Count > 0 ? doProduto : descricoes;

            SapProductDescriptionDto? melhor = ProductMasterSapApiClient.EscolherMelhorDescricao(candidatas);
            if (melhor is null || string.IsNullOrWhiteSpace(melhor.ProductDescription))
            {
                RegistrarDiagnostico(
                    $"Produto {codigo}: A_ProductDescription retornou {descricoes.Count} descrição(ões), nenhuma utilizável. Fallback.");
                return null;
            }

            // ProductType NÃO é consultado aqui (A_Product é outra etapa) → Consultado=false, só descrição preenchida.
            ProdutoSapMestre mestre = new()
            {
                CodigoProduto = codigo,
                DescricaoProdutoSap = melhor.ProductDescription.Trim(),
                IdiomaDescricaoSap = (melhor.Language ?? string.Empty).Trim(),
                Consultado = false
            };

            RegistrarDiagnostico(
                $"Produto {codigo}: descrições retornadas {descricoes.Count}; idioma escolhido "
                + $"{(string.IsNullOrWhiteSpace(mestre.IdiomaDescricaoSap) ? "(vazio)" : mestre.IdiomaDescricaoSap)}; "
                + $"ProductDescription escolhido: {mestre.DescricaoProdutoSap}; origem: A_ProductDescription.");
            return mestre;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico(
                $"Produto {codigo}: GET A_ProductDescription HTTP status {((int?)ex.StatusCode)?.ToString() ?? "indefinido"}: {ex.GetType().Name}. Fallback.");
            return null;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Produto {codigo}: GET A_ProductDescription etapa CLIENTE: {ex.GetType().Name}. Fallback.");
            return null;
        }
    }

    private async Task<string?> ObterJsonDescricaoAsync(string codigo, CancellationToken cancellationToken)
    {
        if (_obterJsonOverride is not null)
        {
            return await _obterJsonOverride(codigo, cancellationToken);
        }

        Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
            _configuracao.ProductMasterBaseUrlEfetiva,
            _configuracao.HostsPermitidos);

        // Ajuste 2/4: filtra por Product (sem Language) — o idioma é escolhido no código para permitir fallback.
        Uri url = ProductMasterSapApiClient.MontarUrlDescricaoProduto(
            baseUri, codigo, idioma: null, sapClient: _configuracao.SapClient);

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
        => System.Diagnostics.Trace.TraceInformation($"[SAP][ProductDescription] {mensagem}");
}
