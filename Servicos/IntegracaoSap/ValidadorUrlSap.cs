namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal static class ValidadorUrlSap
{
    public static Uri ValidarBaseUrl(ConfiguracaoSap configuracao)
    {
        if (!Uri.TryCreate(configuracao.BaseUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException("A URL base do SAP e invalida.");
        }

        ValidarHttpsSemCredenciais(uri);

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("A URL base do SAP nao pode conter query string ou fragmento.");
        }

        if (!HostPermitido(uri, configuracao.HostsPermitidos))
        {
            throw new InvalidOperationException("O host da URL base do SAP nao esta na allowlist.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    public static Uri ValidarDestino(
        Uri destino,
        Uri baseUri,
        IReadOnlyList<string> hostsPermitidos)
    {
        ValidarHttpsSemCredenciais(destino);

        if (!HostPermitido(destino, hostsPermitidos)
            || !string.Equals(destino.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(destino.IdnHost, baseUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            || destino.Port != baseUri.Port
            || !CaminhoDentroDaBase(destino, baseUri))
        {
            throw new InvalidOperationException("A requisicao SAP tentou acessar uma origem nao autorizada.");
        }

        return destino;
    }

    private static void ValidarHttpsSemCredenciais(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A integracao SAP exige HTTPS.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("A URL SAP nao pode conter credenciais.");
        }
    }

    private static bool HostPermitido(Uri uri, IReadOnlyList<string> hostsPermitidos)
        => hostsPermitidos.Any(host =>
            string.Equals(
                uri.IdnHost.TrimEnd('.'),
                host.Trim().TrimEnd('.'),
                StringComparison.OrdinalIgnoreCase));

    private static bool CaminhoDentroDaBase(Uri destino, Uri baseUri)
    {
        string caminhoBase = baseUri.AbsolutePath.TrimEnd('/') + "/";
        string caminhoDestino = destino.AbsolutePath;
        return caminhoDestino.StartsWith(caminhoBase, StringComparison.Ordinal);
    }
}
