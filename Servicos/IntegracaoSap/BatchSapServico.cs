using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 126F: entrada unica do GET de LOTES no SAP (API_BATCH_SRV, colecao <c>Batch</c>, OData V2,
/// Basic Auth). Le as datas de fabricacao e validade que vao na etiqueta do palete.
/// <para>
/// SOMENTE LEITURA: nao faz POST/PATCH, nao busca CSRF e nao altera nada no SAP.
/// </para>
/// <para>
/// NUNCA LANCA para o chamador: indisponibilidade, 404, credencial recusada ou corpo malformado
/// viram null/vazio mais diagnostico SANITIZADO (etapa, status, tipo da excecao — nunca
/// Authorization, senha ou cookie). A etiqueta e efeito local e nao pode deixar de sair porque o
/// SAP nao respondeu.
/// </para>
/// </summary>
public interface IBatchSapServico
{
    bool Configurado { get; }

    /// <summary>Datas do lote, ou null quando nao configurado, indisponivel ou inexistente.</summary>
    Task<LoteSapDatas?> ObterDatasAsync(string material, string lote, CancellationToken cancellationToken = default);
}

internal sealed class BatchSapServico : IBatchSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real para exercitar montagem/parse/erro sem rede.
    private readonly Func<string, string, CancellationToken, Task<string?>>? _obterJsonOverride;

    public BatchSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal BatchSapServico(
        ConfiguracaoSap configuracao,
        Func<string, string, CancellationToken, Task<string?>>? obterJsonOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _obterJsonOverride = obterJsonOverride;
    }

    public bool Configurado => _configuracao.BatchConfigurado;

    public async Task<LoteSapDatas?> ObterDatasAsync(
        string material, string lote, CancellationToken cancellationToken = default)
    {
        string codigoMaterial = (material ?? string.Empty).Trim();
        string codigoLote = (lote ?? string.Empty).Trim();

        if (codigoMaterial.Length == 0 || codigoLote.Length == 0)
        {
            return null;
        }

        if (_obterJsonOverride is null && !_configuracao.BatchConfigurado)
        {
            RegistrarDiagnostico($"Lote {codigoLote}: servico de lotes nao configurado. Datas nao consultadas.");
            return null;
        }

        try
        {
            string? json = await ObterJsonAsync(codigoMaterial, codigoLote, cancellationToken);
            IReadOnlyList<LoteSapDatas> lotes = BatchSapApiClient.Interpretar(json);

            // Defensivo: so aceita linha do lote PEDIDO. Um filtro mal interpretado pelo servico nao
            // pode fazer a etiqueta carregar a data de outro lote.
            string alvo = LoteSapDatas.NormalizarLote(codigoLote);
            foreach (LoteSapDatas candidato in lotes)
            {
                if (LoteSapDatas.NormalizarLote(candidato.Lote) == alvo)
                {
                    RegistrarDiagnostico(
                        $"Lote {codigoLote}: datas obtidas (fabricacao="
                        + $"{(candidato.DataFabricacao?.ToString("yyyy-MM-dd") ?? "null")}, validade="
                        + $"{(candidato.DataVencimento?.ToString("yyyy-MM-dd") ?? "null")}).");
                    return candidato;
                }
            }

            RegistrarDiagnostico(
                $"Lote {codigoLote}: retorno com {lotes.Count} linha(s), nenhuma do lote pedido. Sem datas.");
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RegistrarDiagnostico(
                $"Lote {codigoLote}: GET Batch HTTP status "
                + $"{((int?)ex.StatusCode)?.ToString() ?? "indefinido"}: {ex.GetType().Name}. Sem datas.");
            return null;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Lote {codigoLote}: GET Batch etapa CLIENTE: {ex.GetType().Name}. Sem datas.");
            return null;
        }
    }

    private async Task<string?> ObterJsonAsync(string material, string lote, CancellationToken cancellationToken)
    {
        if (_obterJsonOverride is not null)
        {
            return await _obterJsonOverride(material, lote, cancellationToken);
        }

        Uri baseUri = ValidadorUrlSap.ValidarBaseUrl(
            _configuracao.BatchBaseUrlEfetiva,
            _configuracao.HostsPermitidos);

        Uri url = new(
            BatchSapApiClient.MontarUrl(
                baseUri.AbsoluteUri, material, lote, _configuracao.SapClient),
            UriKind.Absolute);

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
        => System.Diagnostics.Trace.TraceInformation($"[SAP][Batch] {mensagem}");
}
