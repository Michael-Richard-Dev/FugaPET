using System.Diagnostics;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public interface IConfirmacaoProducaoSapClient
{
    bool EnvioHabilitado { get; }

    ResultadoEnvioConfirmacaoProducao ValidarProntoParaEnvio();

    Task<IReadOnlyList<OperacaoConfirmacaoSap>> ConsultarOperacoesConfirmacaoAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default);

    Task<ResultadoEnvioConfirmacaoProducao> EnviarConfirmacaoAsync(
        ConfirmacaoProducaoSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default);
}

public sealed class ConfirmacaoProducaoSapServico : IConfirmacaoProducaoSapClient
{
    public const string MensagemEscritaBloqueada = "Envio SAP bloqueado: escrita SAP desabilitada no ambiente.";
    private const string OperacaoLog = "ENVIAR_CONFIRMACAO_PRODUCAO";
    private const string EntidadeLog = "CONSUMO_MATERIAL";

    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly ILogIntegracaoSapServico _logIntegracaoSapServico;
    private readonly Lazy<ConfirmacaoProducaoSapApiClient> _cliente;

    public ConfirmacaoProducaoSapServico()
        : this(LeitorConfiguracaoSap.Carregar(), logIntegracaoSapServico: null, cliente: null)
    {
    }

    public ConfirmacaoProducaoSapServico(ConfiguracaoSap configuracaoSap)
        : this(configuracaoSap, logIntegracaoSapServico: null, cliente: null)
    {
    }

    internal ConfirmacaoProducaoSapServico(
        ConfiguracaoSap configuracaoSap,
        ILogIntegracaoSapServico? logIntegracaoSapServico,
        ConfirmacaoProducaoSapApiClient? cliente)
    {
        _configuracaoSap = configuracaoSap;
        _logIntegracaoSapServico = logIntegracaoSapServico ?? LogIntegracaoSapNuloServico.Instancia;
        _cliente = new Lazy<ConfirmacaoProducaoSapApiClient>(
            () => cliente ?? new ConfirmacaoProducaoSapApiClient(
                _configuracaoSap, FabricaHttpClientSap.Criar(_configuracaoSap)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool EnvioHabilitado => _configuracaoSap.ProductionOrderConfirmationConfigurado
                                  && _configuracaoSap.EscritaHabilitada;

    public ResultadoEnvioConfirmacaoProducao ValidarProntoParaEnvio()
    {
        if (!_configuracaoSap.ProductionOrderConfirmationConfigurado)
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(
                ConfiguracaoSap.MensagemProductionOrderConfirmationNaoConfigurado);
        }

        if (!_configuracaoSap.EscritaHabilitada)
        {
            return ResultadoEnvioConfirmacaoProducao.Falha(MensagemEscritaBloqueada);
        }

        return new ResultadoEnvioConfirmacaoProducao
        {
            Sucesso = true,
            Mensagem = "Envio por Confirmação de Produção pronto para execução."
        };
    }

    public async Task<ResultadoEnvioConfirmacaoProducao> EnviarConfirmacaoAsync(
        ConfirmacaoProducaoSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        Stopwatch cronometro = Stopwatch.StartNew();

        ResultadoEnvioConfirmacaoProducao prontidao = ValidarProntoParaEnvio();
        if (!prontidao.Sucesso)
        {
            return await BloquearAsync(chaveNegocio, correlationId, cronometro, prontidao.Mensagem);
        }

        try
        {
            ConfirmacaoProducaoSapResponse resposta =
                await _cliente.Value.EnviarConfirmacaoAsync(requisicao, correlationId.ToString(), cancellationToken);

            ResultadoEnvioConfirmacaoProducao resultado = resposta.Sucesso
                ? ResultadoEnvioConfirmacaoProducao.Ok(resposta)
                : ResultadoEnvioConfirmacaoProducao.Falha(resposta.Mensagem, resposta.StatusHttp, resposta.CorrelationId);

            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro,
                resultado.Sucesso ? "SUCESSO" : "ERRO", resultado.StatusHttp, resultado.Mensagem);
            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "CANCELADO", null, "Envio cancelado.");
            throw;
        }
        catch (Exception ex)
        {
            string mensagem = $"Etapa CLIENTE: falha tecnica ({ex.GetType().Name}).";
            await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "ERRO", null, mensagem);
            return ResultadoEnvioConfirmacaoProducao.Falha(mensagem, null, correlationId.ToString());
        }
    }

    public async Task<IReadOnlyList<OperacaoConfirmacaoSap>> ConsultarOperacoesConfirmacaoAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        ResultadoEnvioConfirmacaoProducao prontidao = ValidarProntoParaEnvio();
        if (!prontidao.Sucesso)
        {
            return [];
        }

        return await _cliente.Value.ConsultarOperacoesConfirmacaoAsync(numeroOrdem, cancellationToken);
    }

    private async Task<ResultadoEnvioConfirmacaoProducao> BloquearAsync(
        string chaveNegocio, Guid correlationId, Stopwatch cronometro, string mensagem)
    {
        await RegistrarLogAsync(chaveNegocio, correlationId, cronometro, "BLOQUEADO", null, mensagem);
        return ResultadoEnvioConfirmacaoProducao.Falha(mensagem, null, correlationId.ToString());
    }

    private async Task RegistrarLogAsync(
        string? chaveNegocio, Guid correlationId, Stopwatch cronometro,
        string situacao, int? statusHttp, string? mensagemTecnica)
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
