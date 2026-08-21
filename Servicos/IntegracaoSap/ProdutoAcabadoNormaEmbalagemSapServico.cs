using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Resposta HTTP crua da consulta da norma (costura de teste): status + corpo, ou timeout.</summary>
internal sealed record RespostaHttpNorma(int? StatusCode, string? Corpo, bool Timeout = false);

/// <summary>
/// Serviço REAL do GET da norma de embalagem do Produto Acabado (somente leitura, contrato INT012). Usa
/// EXCLUSIVAMENTE as credenciais/allowlist PRÓPRIAS da embalagem (Packaging*), nunca as das APIs standard.
/// Consulta por FILTRO (nunca por chave). Nunca lança: cada erro vira um <see cref="CenarioConsultaNormaEmbalagem"/>
/// tipado + diagnóstico SANITIZADO. Nunca expõe Authorization/usuário/senha/cookie/token/material sensível.
/// </summary>
internal sealed class ProdutoAcabadoNormaEmbalagemSapServico : IProdutoAcabadoNormaEmbalagemSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui o envio HTTP real (exercita classificação/parse sem rede).
    private readonly Func<HttpRequestMessage, CancellationToken, Task<RespostaHttpNorma>>? _envioOverride;

    public ProdutoAcabadoNormaEmbalagemSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProdutoAcabadoNormaEmbalagemSapServico(
        ConfiguracaoSap configuracao,
        Func<HttpRequestMessage, CancellationToken, Task<RespostaHttpNorma>>? envioOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _envioOverride = envioOverride;
    }

    public bool Configurado => _configuracao.PackagingConfigurado;

    public async Task<ResultadoConsultaNormaEmbalagemSap> ObterNormaAsync(
        string material,
        CancellationToken cancellationToken = default)
    {
        string mat = (material ?? string.Empty).Trim();
        string correlationId = Guid.NewGuid().ToString("N")[..12];

        // 1. Config: URL/allowlist ausentes → NaoConfigurada; URL+allowlist ok mas sem credencial própria → CredencialAusente.
        if (string.IsNullOrWhiteSpace(_configuracao.PackagingBaseUrl)
            || _configuracao.PackagingHostsPermitidos.Count == 0)
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: norma não configurada (URL/allowlist ausentes).");
            return ResultadoConsultaNormaEmbalagemSap.DeNaoConfigurada();
        }

        if (string.IsNullOrWhiteSpace(_configuracao.PackagingUsuario)
            || string.IsNullOrWhiteSpace(_configuracao.PackagingSenha))
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: credenciais próprias da embalagem ausentes.");
            return ResultadoConsultaNormaEmbalagemSap.DeCredencialAusente();
        }

        // 2. URL base válida (HTTPS, sem credenciais embutidas) + host na allowlist PRÓPRIA da embalagem.
        if (!Uri.TryCreate(_configuracao.PackagingBaseUrl, UriKind.Absolute, out Uri? baseUri)
            || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: URL da embalagem inválida (exige HTTPS sem credenciais).");
            return ResultadoConsultaNormaEmbalagemSap.DeNaoConfigurada();
        }

        Uri destino = ProdutoAcabadoNormaEmbalagemSapApiClient.MontarUrlConsulta(
            baseUri, mat, packagingInstruction: null, sapClientOpcional: _configuracao.PackagingSapClientOpcional);
        string urlSanitizada = SanitizarUrl(destino);

        if (!HostPermitido(destino, _configuracao.PackagingHostsPermitidos))
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: host {destino.IdnHost} fora da allowlist da embalagem.");
            return ResultadoConsultaNormaEmbalagemSap.DeHostNaoPermitido(urlSanitizada, correlationId);
        }

        // 3. Requisição GET com Authorization Basic derivado SOMENTE das credenciais próprias da embalagem.
        using HttpRequestMessage requisicao = new(HttpMethod.Get, destino);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", CredencialBasicPackaging());
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        RespostaHttpNorma resposta;
        try
        {
            resposta = await Enviar(requisicao, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultadoConsultaNormaEmbalagemSap.DeCancelada(urlSanitizada);
        }
        catch (OperationCanceledException)
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: timeout no GET da norma.");
            return ResultadoConsultaNormaEmbalagemSap.DeTimeout(urlSanitizada, correlationId);
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: falha HTTP no GET da norma: {ex.GetType().Name}.");
            return ResultadoConsultaNormaEmbalagemSap.DeErroHttp((int?)ex.StatusCode ?? 0, urlSanitizada, correlationId);
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"[{correlationId}] Material {mat}: erro técnico no GET da norma: {ex.GetType().Name}.");
            return ResultadoConsultaNormaEmbalagemSap.DeErroHttp(0, urlSanitizada, correlationId);
        }

        return Classificar(mat, resposta, urlSanitizada, correlationId);
    }

    private static ResultadoConsultaNormaEmbalagemSap Classificar(
        string material, RespostaHttpNorma resposta, string urlSanitizada, string correlationId)
    {
        if (resposta.Timeout)
        {
            return ResultadoConsultaNormaEmbalagemSap.DeTimeout(urlSanitizada, correlationId);
        }

        int status = resposta.StatusCode ?? 0;
        switch (status)
        {
            case 401:
                return ResultadoConsultaNormaEmbalagemSap.DeErroAutenticacao(status, urlSanitizada, correlationId);
            case 403:
                return ResultadoConsultaNormaEmbalagemSap.DeErroAutorizacao(status, urlSanitizada, correlationId);
            case 404:
                return ResultadoConsultaNormaEmbalagemSap.DeNaoEncontrada(status, urlSanitizada, correlationId);
        }

        if (status is < 200 or >= 300)
        {
            return ResultadoConsultaNormaEmbalagemSap.DeErroHttp(status, urlSanitizada, correlationId);
        }

        // 2xx: corpo vazio = sem cadastro; JSON inválido = resposta inválida; coleção vazia = sem cadastro.
        if (string.IsNullOrWhiteSpace(resposta.Corpo))
        {
            return ResultadoConsultaNormaEmbalagemSap.DeNaoEncontrada(status, urlSanitizada, correlationId);
        }

        if (!JsonValido(resposta.Corpo))
        {
            RegistrarDiagnostico($"[{correlationId}] Material {material}: resposta 2xx com JSON inválido.");
            return ResultadoConsultaNormaEmbalagemSap.DeRespostaInvalida(status, urlSanitizada, correlationId);
        }

        ConsultaNormaEmbalagemSapResponse? norma =
            ProdutoAcabadoNormaEmbalagemSapApiClient.Parsear(resposta.Corpo);
        return norma is null
            ? ResultadoConsultaNormaEmbalagemSap.DeNaoEncontrada(status, urlSanitizada, correlationId)
            : ResultadoConsultaNormaEmbalagemSap.DeEncontrada(norma, status, urlSanitizada, correlationId);
    }

    private async Task<RespostaHttpNorma> Enviar(HttpRequestMessage requisicao, CancellationToken cancellationToken)
    {
        if (_envioOverride is not null)
        {
            return await _envioOverride(requisicao, cancellationToken);
        }

        using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
        using HttpResponseMessage resposta = await httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return new RespostaHttpNorma((int)resposta.StatusCode, corpo);
    }

    private string CredencialBasicPackaging()
        => Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_configuracao.PackagingUsuario}:{_configuracao.PackagingSenha}"));

    private static bool JsonValido(string corpo)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(corpo);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HostPermitido(Uri uri, IReadOnlyList<string> hostsPermitidos)
        => hostsPermitidos.Any(host =>
            string.Equals(
                uri.IdnHost.TrimEnd('.'),
                host.Trim().TrimEnd('.'),
                StringComparison.OrdinalIgnoreCase));

    /// <summary>URL para diagnóstico: só esquema+host+caminho (sem query). Nunca contém segredo.</summary>
    private static string SanitizarUrl(Uri uri)
        => uri.GetLeftPart(UriPartial.Path);

    internal static void RegistrarDiagnostico(string mensagem)
        => System.Diagnostics.Trace.TraceInformation($"[SAP][NormaEmbalagem] {mensagem}");
}
