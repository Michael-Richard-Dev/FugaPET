using System.Diagnostics;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Implementacao real do envio de Entrada de Produto ao SAP via Material Document (movimento 101).
/// Reaplica as travas de configuracao/escrita antes de delegar ao <see cref="MaterialDocumentSapApiClient"/>
/// (HTTPS + allowlist + CSRF) e registra o resultado sanitizado em <c>log_integracao_sap</c>
/// (operacao CRIAR_DOCUMENTO_MATERIAL_101, entidade DOCUMENTO_MATERIAL). Nunca expoe segredo/payload.
/// </summary>
public sealed class MaterialDocumentSapServico : IMaterialDocumentSapServico
{
    private const string EntidadeLog = "DOCUMENTO_MATERIAL";
    private const string OperacaoLog = "CRIAR_DOCUMENTO_MATERIAL_101";

    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly Lazy<MaterialDocumentSapApiClient> _cliente;
    private readonly ILogIntegracaoSapServico _logIntegracaoSapServico;
    private readonly IRuntimeSapWriteCapabilityService _capability;
    // Seam de dispatch HTTP (o cliente real é sealed). Default = cliente real; testes contam dispatches no boundary.
    private readonly Func<MaterialDocumentSapRequest, CancellationToken, Task<ResultadoMaterialDocumentSap>> _dispatchHttp;

    public MaterialDocumentSapServico()
        : this(LeitorConfiguracaoSap.Carregar(), logIntegracaoSapServico: null, cliente: null)
    {
    }

    public MaterialDocumentSapServico(ConfiguracaoSap configuracaoSap)
        : this(configuracaoSap, logIntegracaoSapServico: null, cliente: null)
    {
    }

    internal MaterialDocumentSapServico(
        ConfiguracaoSap configuracaoSap,
        ILogIntegracaoSapServico? logIntegracaoSapServico,
        MaterialDocumentSapApiClient? cliente,
        IRuntimeSapWriteCapabilityService? capability = null,
        Func<MaterialDocumentSapRequest, CancellationToken, Task<ResultadoMaterialDocumentSap>>? dispatchHttp = null)
    {
        _configuracaoSap = configuracaoSap;
        _logIntegracaoSapServico = logIntegracaoSapServico ?? LogIntegracaoSapNuloServico.Instancia;
        _capability = capability ?? RuntimeSapWriteCapability.Instancia;
        _dispatchHttp = dispatchHttp
            ?? ((requisicao, cancellationToken) => _cliente.Value.CriarDocumentoMaterialAsync(requisicao, cancellationToken));
        _cliente = new Lazy<MaterialDocumentSapApiClient>(
            () => cliente ?? new MaterialDocumentSapApiClient(
                _configuracaoSap,
                FabricaHttpClientSap.Criar(_configuracaoSap)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool EhSimulado => false;

    public bool MaterialDocumentConfigurado => _configuracaoSap.MaterialDocumentConfigurado;

    public async Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        requisicao = requisicao with { Itens = requisicao.Itens.ToArray() };
        Guid correlationId = Guid.NewGuid();
        if (Entrada.P5.BloqueioP5R7.Bloqueia(requisicao))
            return ResultadoMaterialDocumentSap.Falha(null, Entrada.P5.BloqueioP5R7.Mensagem) with { PostTentado = false };
        Stopwatch cronometro = Stopwatch.StartNew();

        if (!_configuracaoSap.Configurado)
        {
            return await BloquearAsync(
                chaveNegocio, correlationId, cronometro, _configuracaoSap.MensagemConfiguracaoBaseAusente());
        }

        if (!_configuracaoSap.MaterialDocumentConfigurado)
        {
            return await BloquearAsync(
                chaveNegocio, correlationId, cronometro, _configuracaoSap.MensagemMaterialDocumentAusente());
        }

        // ENFORCEMENT POINT FINAL da capability 101 (12E-E-C). Este é o único ponto que autoriza um
        // dispatch HTTP de Material Document 101. Escopo EXCLUSIVO 101 — não afeta 261/HU/PALLET.
        //   env bootstrap true (legado/teste): preserva o comportamento coberto por testes existentes.
        //   env false (Q runtime): CONSUMO ATÔMICO one-shot ARMADA_101 -> CONSUMIDA imediatamente antes do
        //     POST. CONSUMIDA/RECONCILIACAO/DESABILITADA/expirada => TryAdquirir101 falha => ZERO POST.
        //     Segunda invocação direta do writer com CONSUMIDA é bloqueada aqui (não há novo dispatch).
        if (!_configuracaoSap.EscritaHabilitada && !_capability.TryAdquirir101())
        {
            return await BloquearAsync(
                chaveNegocio, correlationId, cronometro, ConfiguracaoSap.MensagemEscritaBloqueada);
        }

        try
        {
            // Diagnostico controlado: registra o payload ENVIADO (somente campos funcionais, sem
            // segredo) para correlacionar com o retorno do SAP. Stopwatch proprio (duracao ~0).
            // situacao = PARCIAL (e nao a etapa) para nao violar a constraint de situacao do
            // log_integracao_sap; a etapa fica na mensagem (prefixo Etapa PAYLOAD: ...).
            await RegistrarLogAsync(
                chaveNegocio,
                correlationId,
                Stopwatch.StartNew(),
                "PARCIAL",
                null,
                "Etapa PAYLOAD: " + MaterialDocumentSapApiClient.DescreverPayloadSanitizado(requisicao));

            ResultadoMaterialDocumentSap resultado =
                await _dispatchHttp(requisicao, cancellationToken);

            await RegistrarLogAsync(
                chaveNegocio,
                correlationId,
                cronometro,
                resultado.Sucesso ? "SUCESSO" : "ERRO",
                resultado.StatusHttp,
                resultado.MensagemSanitizada);

            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarLogAsync(
                chaveNegocio, correlationId, cronometro, "CANCELADO", null, "Criacao cancelada.");
            throw;
        }
        catch (MaterialDocumentClienteException ex)
        {
            // Falha na CONSTRUCAO do cliente (URL/auth), antes do HTTP: etapa + Message sanitizada.
            string mensagem = $"Etapa {ex.Etapa} (antes do HTTP): "
                + MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(
                    ex.InnerException ?? ex, SegredosSanitizacao());
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "ERRO", null, mensagem);
            return ResultadoMaterialDocumentSap.Falha(null, mensagem) with { PostTentado = false };
        }
        catch (Exception ex)
        {
            // Ultimo recurso: registra etapa CLIENTE_CONSTRUCAO + tipo + Message + inner, sanitizados
            // (sem Authorization/senha/cookie/token/payload). Nao esconde a Message util da excecao.
            string mensagem = $"Etapa {MaterialDocumentSapApiClient.EtapaClienteConstrucao}: "
                + MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(ex, SegredosSanitizacao());
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "ERRO", null, mensagem);
            return ResultadoMaterialDocumentSap.Falha(null, mensagem);
        }
    }

    // Segredos a redigir nas mensagens tecnicas (usuario, senha e a credencial Basic derivada).
    private string[] SegredosSanitizacao()
    {
        string credencial = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_configuracaoSap.Usuario}:{_configuracaoSap.Senha}"));
        return [_configuracaoSap.Usuario, _configuracaoSap.Senha, credencial];
    }

    private async Task<ResultadoMaterialDocumentSap> BloquearAsync(
        string chaveNegocio,
        Guid correlationId,
        Stopwatch cronometro,
        string mensagem)
    {
        await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "BLOQUEADO", null, mensagem);
        return ResultadoMaterialDocumentSap.Falha(null, mensagem) with { PostTentado = false };
    }

    private async Task RegistrarLogAsync(
        string? chaveNegocio,
        Guid correlationId,
        Stopwatch cronometro,
        string situacao,
        int? statusHttp,
        string? mensagemTecnica)
    {
        cronometro.Stop();
        await _logIntegracaoSapServico.RegistrarAsync(
            new RegistroLogIntegracaoSap
            {
                TipoIntegracao = "SAP_ODATA",
                Operacao = OperacaoLog,
                Entidade = EntidadeLog,
                ChaveNegocio = chaveNegocio,
                StatusHttp = statusHttp,
                DuracaoMs = cronometro.ElapsedMilliseconds,
                CorrelationId = correlationId,
                Situacao = situacao,
                Tentativa = 1,
                MensagemTecnicaSanitizada = mensagemTecnica,
                RegistradoEmUtc = DateTimeOffset.UtcNow
            },
            CancellationToken.None);
    }
}
