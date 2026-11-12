using System.Diagnostics;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Envio real e GOVERNADO do consumo 261 ao SAP. Reaplica as travas de configuracao + escrita
/// (FUGAPET_SAP_WRITE_ENABLED) ANTES de qualquer chamada (sem CSRF/POST quando bloqueado) e delega ao
/// <see cref="ConsumoMaterialSap261ApiClient"/> (HTTPS + allowlist + CSRF + CookieContainer). Registra
/// o resultado sanitizado em <c>log_integracao_sap</c> (operacao ENVIAR_CONSUMO_MATERIAL_261).
/// </summary>
public sealed class ConsumoMaterialSap261Servico : IConsumoMaterialSap261Servico
{
    public const string MensagemEscritaBloqueada = "Envio SAP bloqueado: escrita SAP desabilitada no ambiente.";
    private const string OperacaoLog = "ENVIAR_CONSUMO_MATERIAL_261";
    private const string EntidadeLog = "CONSUMO_MATERIAL";

    public const string MensagemCapabilityAusente =
        "Envio SAP 261 não autorizado: habilitação de escrita (capability 261) ausente para este lançamento.";

    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly ILogIntegracaoSapServico _logIntegracaoSapServico;
    private readonly Lazy<ConsumoMaterialSap261ApiClient> _cliente;
    private readonly IRuntimeSapWriteCapability261Service _capability261;

    public ConsumoMaterialSap261Servico()
        : this(LeitorConfiguracaoSap.Carregar(), logIntegracaoSapServico: null, cliente: null)
    {
    }

    public ConsumoMaterialSap261Servico(ConfiguracaoSap configuracaoSap)
        : this(configuracaoSap, logIntegracaoSapServico: null, cliente: null)
    {
    }

    internal ConsumoMaterialSap261Servico(
        ConfiguracaoSap configuracaoSap,
        ILogIntegracaoSapServico? logIntegracaoSapServico,
        ConsumoMaterialSap261ApiClient? cliente,
        IRuntimeSapWriteCapability261Service? capability261 = null)
    {
        _configuracaoSap = configuracaoSap;
        _logIntegracaoSapServico = logIntegracaoSapServico ?? LogIntegracaoSapNuloServico.Instancia;
        _capability261 = capability261 ?? RuntimeSapWriteCapability261.Instancia;
        _cliente = new Lazy<ConsumoMaterialSap261ApiClient>(
            () => cliente ?? new ConsumoMaterialSap261ApiClient(
                _configuracaoSap, FabricaHttpClientSap.Criar(_configuracaoSap)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool EhSimulado => false;
    public bool Configurado => _configuracaoSap.MaterialDocumentConfigurado;

    public ResultadoEnvioConsumoSap261 ValidarProntoParaEnvio()
    {
        // GATE 101E-P2: prontidão ESTRUTURAL apenas. NÃO bloqueia por EscritaHabilitada (o gate final de escrita é
        // avaliado no writer, imediatamente antes do HTTP) e NÃO consome capability.
        if (!_configuracaoSap.MaterialDocumentConfigurado)
        {
            return ResultadoEnvioConsumoSap261.Falha(_configuracaoSap.MensagemMaterialDocumentAusente());
        }

        return new ResultadoEnvioConsumoSap261
        {
            Sucesso = true,
            Mensagem = "Envio SAP 261 pronto para execução."
        };
    }

    public async Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao,
        long codigoLancamento,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        Stopwatch cronometro = Stopwatch.StartNew();

        ResultadoEnvioConsumoSap261 prontidao = ValidarProntoParaEnvio();
        if (!prontidao.Sucesso)
        {
            return await BloquearAsync(chaveNegocio, correlationId, cronometro, prontidao.Mensagem);
        }

        // ===== FINAL WRITE GATE (imediatamente antes do boundary HTTP) =====
        // ALLOW = env write=true OR capability261.TryAdquirir261(codigoLancamento). Short-circuit: quando
        // EscritaHabilitada=true a capability NÃO é consumida. No Q (env=false), só a capability bound ao MESMO
        // codigo_lancamento autoriza — ausente/expirada/PK divergente ⇒ ZERO HTTP.
        bool capabilityConsumida = false;
        if (!_configuracaoSap.EscritaHabilitada)
        {
            capabilityConsumida = _capability261.TryAdquirir261(codigoLancamento);
            if (!capabilityConsumida)
            {
                await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "BLOQUEADO", null, MensagemCapabilityAusente);
                return ResultadoEnvioConsumoSap261.NaoAutorizado(MensagemCapabilityAusente);
            }
        }

        try
        {
            string payloadJson = ConsumoMaterialSapPayloadBuilder.SerializarPreview(requisicao);
            await RegistrarLogAsync(
                chaveNegocio,
                correlationId,
                cronometro,
                "PARCIAL",
                null,
                MontarDiagnosticoPayload261(requisicao, payloadJson),
                finalizarCronometro: false);

            ResultadoEnvioConsumoSap261 resultado =
                await _cliente.Value.EnviarConsumo261Async(requisicao, correlationId.ToString(), cancellationToken);

            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro,
                resultado.Sucesso ? "SUCESSO" : "ERRO", resultado.StatusHttp, resultado.Mensagem);
            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // GATE 101E-P2 §13: cancelamento APÓS a aquisição — não é possível provar que nenhum POST ocorreu.
            // Marca reconciliação (idempotente, sem refund/rearm) antes de propagar. DB permanece ENVIANDO_SAP.
            if (capabilityConsumida)
            {
                _capability261.MarcarReconciliacao(codigoLancamento);
            }
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "CANCELADO", null, "Envio cancelado.");
            throw;
        }
        catch (Exception ex)
        {
            // Falha técnica/transporte APÓS a aquisição (StatusHttp null ⇒ ResultadoIndeterminado): o POST PODE ter
            // ocorrido. Marca reconciliação; o serviço mantém ENVIANDO_SAP (sem MarcarFalhaSap, sem PENDENTE).
            if (capabilityConsumida)
            {
                _capability261.MarcarReconciliacao(codigoLancamento);
            }
            string mensagem = $"Etapa CLIENTE: falha tecnica ({ex.GetType().Name}).";
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "ERRO", null, mensagem);
            return ResultadoEnvioConsumoSap261.Falha(mensagem, null, correlationId.ToString());
        }
    }

    private async Task<ResultadoEnvioConsumoSap261> BloquearAsync(
        string chaveNegocio, Guid correlationId, Stopwatch cronometro, string mensagem)
    {
        await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "BLOQUEADO", null, mensagem);
        return ResultadoEnvioConsumoSap261.Falha(mensagem, null, correlationId.ToString());
    }

    private async Task RegistrarLogAsync(
        string? chaveNegocio, Guid correlationId, Stopwatch cronometro,
        string situacao, int? statusHttp, string? mensagemTecnica, bool finalizarCronometro = true)
    {
        if (finalizarCronometro)
        {
            cronometro.Stop();
        }

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

    private static string MontarDiagnosticoPayload261(
        ConsumoMaterialSap261Request requisicao,
        string payloadJson)
    {
        IEnumerable<string> itens = requisicao.ToMaterialDocumentItem.Select(item =>
            "Movimento: 261"
            + $" | OP: {item.ManufacturingOrder}"
            + $" | Reserva: {item.Reservation}"
            + $" | Item reserva: {item.ReservationItem}"
            + $" | Material: {item.Material}"
            + $" | Centro: {item.Plant}"
            + $" | Depósito: {item.StorageLocation}"
            + $" | Lote: {item.Batch}"
            + $" | Quantidade: {item.QuantityInEntryUnit}"
            + $" | Unidade: {item.EntryUnit}");

        return "Payload SAP 261 enviado."
            + Environment.NewLine
            + string.Join(Environment.NewLine, itens)
            + Environment.NewLine
            + "Payload JSON:"
            + Environment.NewLine
            + payloadJson;
    }
}
