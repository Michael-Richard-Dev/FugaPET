using System.Net;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE R6: servico SOMENTE LEITURA do historico de documentos de material do pedido+item
/// (API_MATERIAL_DOCUMENT_SRV / A_MaterialDocumentItem, OData V2). Faz GET, segue a paginacao
/// <c>d.__next</c> ate o fim e delega classificacao ao <see cref="ClassificadorHistoricoMaterialSap"/>.
/// <para>
/// FAIL-CLOSED: qualquer erro HTTP, envelope invalido, nextLink de destino divergente, excesso de
/// paginas (protecao de loop) ou repeticao de pagina ⇒ <c>PaginacaoCompleta=false</c>, o que BLOQUEIA.
/// </para>
/// <para>
/// ABRANGENCIA: <c>AbrangenciaComprovada</c> e SEMPRE false aqui. A consulta prova o que encontrou,
/// NAO a cobertura do historico — zero documentos nunca implica P5=FALSE. A atestacao de cobertura
/// e decisao empresarial externa (ver retorno do gate).
/// </para>
/// Nenhuma escrita: o helper rejeita localmente qualquer metodo diferente de GET antes do SendAsync.
/// </summary>
public sealed class HistoricoMaterialDocumentSapServico : IHistoricoMaterialDocumentSapServico
{
    /// <summary>Teto de paginas por consulta (protecao contra loop/volume anormal).</summary>
    public const int MaxPaginas = 50;

    private readonly Uri? _baseUri;
    private readonly string _sapClient;
    private readonly Func<HttpClient> _fabricaHttpClient;
    private readonly string? _materialEsperado;
    private readonly string? _centroEsperado;
    private readonly Action<string>? _registrarDiagnostico;

    public HistoricoMaterialDocumentSapServico(
        ConfiguracaoSap configuracao,
        string? materialEsperado = null,
        string? centroEsperado = null,
        Func<HttpClient>? fabricaHttpClient = null,
        Action<string>? registrarDiagnostico = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _materialEsperado = materialEsperado;
        _centroEsperado = centroEsperado;
        _registrarDiagnostico = registrarDiagnostico;
        _sapClient = (configuracao.SapClient ?? string.Empty).Trim();
        _fabricaHttpClient = fabricaHttpClient ?? (() => FabricaHttpClientSap.Criar(configuracao));

        // Destino validado pelo MESMO contrato das demais integracoes (https + allowlist + sem credencial
        // na URL). Invalido ⇒ servico indisponivel (nunca "sem historico").
        try
        {
            _baseUri = string.IsNullOrWhiteSpace(configuracao.MaterialDocumentBaseUrl)
                ? null
                : ValidadorUrlSap.ValidarBaseUrl(
                    configuracao.MaterialDocumentBaseUrl, configuracao.HostsPermitidos);
        }
        catch (InvalidOperationException)
        {
            _baseUri = null;
        }

        Disponivel = _baseUri is not null
            && _sapClient.Length > 0
            && !string.IsNullOrWhiteSpace(configuracao.Usuario)
            && !string.IsNullOrWhiteSpace(configuracao.Senha)
            && configuracao.HostsPermitidos.Count > 0;
    }

    public bool Disponivel { get; }

    public async Task<ResultadoHistoricoMaterialSap> ConsultarPorPedidoItemAsync(
        string numeroPedido,
        string numeroItemPedido,
        CancellationToken cancellationToken = default)
    {
        if (!Disponivel || _baseUri is null)
        {
            return ResultadoHistoricoMaterialSap.Indisponivel(
                "integracao de historico de documentos de material nao configurada (URL, allowlist, "
                + "mandante ou credenciais ausentes)");
        }

        string pedido = (numeroPedido ?? string.Empty).Trim();
        string item = HistoricoMaterialDocumentSapApiClient.NormalizarItemPedido(numeroItemPedido);
        if (pedido.Length == 0 || item.Length == 0)
        {
            return ResultadoHistoricoMaterialSap.Falha(
                "pedido ou item do pedido ausentes para consultar o historico");
        }

        List<MovimentoHistoricoMaterial> movimentos = [];
        HashSet<string> paginasVisitadas = new(StringComparer.Ordinal);
        Uri destino = HistoricoMaterialDocumentSapApiClient.MontarUrlHistoricoPorPedido(
            _baseUri, pedido, _sapClient);

        using HttpClient http = _fabricaHttpClient();

        for (int pagina = 1; pagina <= MaxPaginas; pagina++)
        {
            if (!paginasVisitadas.Add(destino.ToString()))
            {
                return ResultadoHistoricoMaterialSap.Falha(
                    "paginacao do historico repetiu uma pagina (possivel loop); leitura abortada");
            }

            string corpo;
            try
            {
                using HttpRequestMessage requisicao = CriarRequisicaoGet(destino);
                using HttpResponseMessage resposta = await http.SendAsync(requisicao, cancellationToken);
                if (resposta.StatusCode != HttpStatusCode.OK)
                {
                    return ResultadoHistoricoMaterialSap.Falha(
                        $"consulta do historico devolveu HTTP {(int)resposta.StatusCode}");
                }

                corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Cancelamento sobe: tratado pela fronteira do R3B.
            }
            catch (Exception ex)
            {
                return ResultadoHistoricoMaterialSap.Falha(
                    "falha tecnica na consulta do historico (" + ex.GetType().Name + ")");
            }

            PaginaHistoricoMaterial interpretada =
                HistoricoMaterialDocumentSapApiClient.InterpretarPagina(corpo, pedido, item);
            if (!interpretada.Valida)
            {
                return ResultadoHistoricoMaterialSap.Falha(
                    "envelope do historico invalido: " + interpretada.Motivo);
            }

            movimentos.AddRange(interpretada.Movimentos);

            if (string.IsNullOrWhiteSpace(interpretada.ProximaPagina))
            {
                _registrarDiagnostico?.Invoke(
                    $"Historico 101/102 pedido {pedido} item {item}: {pagina} pagina(s), "
                    + $"{movimentos.Count} movimento(s) do item. Abrangencia NAO atestada.");

                // PAGINACAO completa; ABRANGENCIA permanece NAO comprovada (decisao empresarial externa).
                return ClassificadorHistoricoMaterialSap.Classificar(
                    movimentos,
                    paginacaoCompleta: true,
                    abrangenciaComprovada: false,
                    materialEsperado: _materialEsperado,
                    centroEsperado: _centroEsperado);
            }

            Uri? proxima = ResolverProximaPagina(interpretada.ProximaPagina!);
            if (proxima is null)
            {
                return ResultadoHistoricoMaterialSap.Falha(
                    "o __next do historico foi rejeitado por seguranca (host, porta, servico ou mandante "
                    + "divergentes do destino autorizado)");
            }

            destino = proxima;
        }

        return ResultadoHistoricoMaterialSap.Falha(
            $"paginacao do historico excedeu o teto de {MaxPaginas} paginas; leitura nao concluida");
    }

    /// <summary>Somente GET: qualquer outro metodo e rejeitado LOCALMENTE antes de qualquer SendAsync.</summary>
    private static HttpRequestMessage CriarRequisicaoGet(Uri destino)
    {
        HttpRequestMessage requisicao = new(HttpMethod.Get, destino);
        if (requisicao.Method != HttpMethod.Get)
        {
            throw new InvalidOperationException("Consulta de historico aceita exclusivamente GET.");
        }

        return requisicao;
    }

    /// <summary>
    /// Resolve o <c>__next</c> exigindo o MESMO destino autorizado: esquema, host, porta e caminho do
    /// servico da base. Tambem exige que o mandante (sap-client), se presente, seja o configurado.
    /// Qualquer divergencia devolve null (fail-closed).
    /// </summary>
    internal Uri? ResolverProximaPagina(string proximaPagina)
        => ResolverProximaPagina(proximaPagina, _baseUri, _sapClient);

    internal static Uri? ResolverProximaPagina(string proximaPagina, Uri? baseUri, string sapClient)
    {
        if (baseUri is null || string.IsNullOrWhiteSpace(proximaPagina))
        {
            return null;
        }

        if (!Uri.TryCreate(proximaPagina.Trim(), UriKind.Absolute, out Uri? destino))
        {
            string relativo = proximaPagina.Trim().TrimStart('/');
            string baseSemBarra = baseUri.ToString().TrimEnd('/');
            if (!Uri.TryCreate($"{baseSemBarra}/{relativo}", UriKind.Absolute, out destino))
            {
                return null;
            }
        }

        bool mesmoDestino =
            string.Equals(destino.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(destino.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && destino.Port == baseUri.Port
            && destino.AbsolutePath.StartsWith(
                baseUri.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        if (!mesmoDestino || !string.Equals(destino.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Mandante: quando a URL o declara, precisa ser o configurado.
        string consulta = destino.Query;
        int indice = consulta.IndexOf("sap-client=", StringComparison.OrdinalIgnoreCase);
        if (indice >= 0 && sapClient.Length > 0)
        {
            string resto = consulta[(indice + "sap-client=".Length)..];
            int fim = resto.IndexOf('&');
            string valor = (fim >= 0 ? resto[..fim] : resto).Trim();
            if (!string.Equals(valor, sapClient, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return destino;
    }
}
