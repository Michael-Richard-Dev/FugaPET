using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Gateway do palete via INT012 (CPI). Fail-closed: gate false / URL inválida / PackagingMaterial ausente ⇒ sem HTTP.</summary>
public interface IProdutoAcabadoPaleteInt012Gateway
{
    bool EnvioAutorizado { get; }
    Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest requisicao, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gateway INT012 testável (HttpMessageHandler injetável). Gate próprio <c>FUGAPET_SAP_PALLET_WRITE_ENABLED</c>
/// (default false) — NUNCA reutiliza FUGAPET_SAP_WRITE_ENABLED. Requisitos: HTTPS + allowlist (ValidadorUrlSap),
/// Basic Auth sem logar segredo, timeout configurável, UM POST por tentativa, ZERO retry, redirect inseguro
/// bloqueado, timeout pós-envio ⇒ IndeterminadoTimeout. Só POST no endpoint INT012 (PATCH/DELETE impossíveis).
/// PackagingMaterial ausente ⇒ NaoEnviado (DEPENDENCIA_ARES). Nenhum POST real nos testes.
/// </summary>
public sealed class ProdutoAcabadoPaleteInt012Gateway : IProdutoAcabadoPaleteInt012Gateway
{
    private readonly Uri? _destino;
    private readonly string _usuario;
    private readonly string _senha;
    private readonly Func<HttpMessageHandler>? _fabricaHandler;
    private readonly TimeSpan _timeout;

    public ProdutoAcabadoPaleteInt012Gateway(
        string? baseUrl,
        IReadOnlyList<string>? hostsPermitidos,
        string usuario,
        string senha,
        bool envioAutorizado,
        Func<HttpMessageHandler>? fabricaHandler,
        TimeSpan? timeout = null)
    {
        _usuario = usuario ?? string.Empty;
        _senha = senha ?? string.Empty;
        _fabricaHandler = fabricaHandler;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);

        _destino = envioAutorizado ? ValidarEndpoint(baseUrl, hostsPermitidos ?? []) : null;
        EnvioAutorizado = envioAutorizado;
    }

    /// <summary>HTTPS + host na allowlist + sem userinfo. Endpoint INT012 completo (path preservado); senão fail-closed.</summary>
    private static Uri? ValidarEndpoint(string? url, IReadOnlyList<string> hostsPermitidos)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || hostsPermitidos.Count == 0
            || !hostsPermitidos.Any(h => string.Equals(h?.Trim(), uri.Host, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return uri;
    }

    public bool EnvioAutorizado { get; }

    public async Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest requisicao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        if (!EnvioAutorizado || _fabricaHandler is null || _destino is null)
        {
            return ResultadoPaleteInt012.NaoEnviado(
                "Envio de palete (INT012) não autorizado nesta execução (gate FUGAPET_SAP_PALLET_WRITE_ENABLED=false ou destino inválido).");
        }

        // GATE 046-K REV2: PALLET01 e o PackagingMaterial oficial do MVP (INT012 v1.00) - ACEITO.
        // So bloqueia ausente/vazio/branco (fail-closed); sem rejeicao literal de PALLET01.
        if (string.IsNullOrWhiteSpace(requisicao.PackagingMaterial))
        {
            return ResultadoPaleteInt012.NaoEnviado(
                "PackagingMaterial ausente/vazio: palete nao enviado.");
        }

        using HttpClient http = CriarHttpClient();
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Post, _destino);
            AplicarBasicAuth(req);
            req.Content = new StringContent(
                JsonSerializer.Serialize(requisicao), Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await http.SendAsync(req, cancellationToken); // UM POST, sem retry
            int status = (int)resp.StatusCode;
            string corpo = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (status is < 200 or > 299)
            {
                // GATE 126H: o corpo da resposta era LIDO e DESCARTADO, e so o status chegava ao
                // operador. Num 500 do CPI isso nao diz nada: a causa esta no corpo, e sem ela a
                // unica saida e adivinhar. Vai sanitizado e truncado.
                string detalhe = ResumirCorpoErro(corpo);
                return ResultadoPaleteInt012.ErroHttp(
                    detalhe.Length == 0
                        ? $"INT012 retornou HTTP {status}."
                        : $"INT012 retornou HTTP {status}. Resposta: {detalhe}",
                    status);
            }

            return PaleteInt012ResponseParser.Parsear(corpo, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout APÓS possível transmissão ⇒ indeterminado, sem retry cego.
            return ResultadoPaleteInt012.Indeterminado("Timeout no POST do INT012: resultado indeterminado (sem retry).");
        }
        catch (HttpRequestException ex)
        {
            return ResultadoPaleteInt012.Indeterminado($"Falha de rede no POST do INT012 (indeterminado): {ex.GetType().Name}.");
        }
    }

    private HttpClient CriarHttpClient()
    {
        HttpMessageHandler handler = _fabricaHandler!();
        HttpClient http = new(handler, disposeHandler: true) { Timeout = _timeout };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private void AplicarBasicAuth(HttpRequestMessage req)
    {
        // Basic Auth — NUNCA logar Authorization/senha.
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_usuario}:{_senha}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    /// <summary>Tamanho maximo do trecho da resposta levado a mensagem operacional.</summary>
    internal const int LimiteCorpoErro = 400;

    /// <summary>
    /// GATE 126H: resumo SANITIZADO do corpo de uma resposta nao-2xx do INT012, para que o operador
    /// veja a causa em vez de apenas "HTTP 500".
    /// <para>
    /// A resposta vem de um servico externo e NAO e confiavel: pode ser enorme, pode vir em uma
    /// linha so, e pode ecoar o que mandamos — inclusive cabecalhos. Por isso o texto e achatado,
    /// tem os campos sensiveis redigidos e e truncado. O corpo NUNCA e tratado como instrucao, so
    /// como texto de diagnostico.
    /// </para>
    /// </summary>
    internal static string ResumirCorpoErro(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return string.Empty;
        }

        // Achata quebras de linha e espacos repetidos: a mensagem vai para uma linha de status.
        string texto = System.Text.RegularExpressions.Regex.Replace(corpo.Trim(), @"\s+", " ");

        // ORDEM IMPORTA: o esquema Basic/Bearer vem PRIMEIRO. Se a redacao de chave-valor rodasse
        // antes, ela consumiria apenas a palavra "Bearer" em `Authorization=Bearer <token>` (o valor
        // termina no espaco) e o token sobreviveria — foi o que um teste pegou.
        texto = System.Text.RegularExpressions.Regex.Replace(
            texto, "(?i)\\b(Basic|Bearer)\\s+[A-Za-z0-9+/=._-]{8,}", "$1 [REDIGIDO]");

        // Redige valores de campos sensiveis, caso o servico ecoe o que recebeu.
        texto = System.Text.RegularExpressions.Regex.Replace(
            texto,
            "(?i)(\"?(authorization|cookie|set-cookie|x-csrf-token|password|senha|client_secret|access_token)\"?\\s*[:=]\\s*)(\"[^\"]*\"|[^,;}\\s]+)",
            "$1\"[REDIGIDO]\"");

        return texto.Length <= LimiteCorpoErro
            ? texto
            : texto[..LimiteCorpoErro] + "... (truncado)";
    }
}
