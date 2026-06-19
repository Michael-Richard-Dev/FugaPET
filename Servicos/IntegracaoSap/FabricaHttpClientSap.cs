using System.Net;
using System.Net.Security;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal static class FabricaHttpClientSap
{
    public static HttpClient Criar(ConfiguracaoSap configuracao)
    {
        HttpClientHandler handler = CriarHandler();

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, configuracao.TimeoutSegundos))
        };
    }

    internal static bool CertificadoValido(SslPolicyErrors erros)
        => erros == SslPolicyErrors.None;

    internal static HttpClientHandler CriarHandler()
        => new()
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback =
                (_, _, _, erros) => CertificadoValido(erros)
        };
}
