using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Costura mínima e testável sobre o <see cref="ProdutoAcabadoHuService"/> HOMOLOGADO (044). Expõe apenas os
/// mecanismos já existentes que a ponte 045→044 precisa reutilizar — sem nova função SQL, sem duplicar claim
/// nem autorização. O <see cref="ProdutoAcabadoHuService"/> implementa esta interface (adaptação indispensável).
/// </summary>
public interface IProdutoAcabadoHuBridgeServico
{
    bool EnvioAutorizado { get; }
    Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken cancellationToken = default);
    Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default);
    Task<ResultadoEnvioHu> EnviarAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default);
}

/// <summary>
/// Adaptador PRODUTIVO de <see cref="IProdutoAcabadoHuEnvio"/> sobre o fluxo HU 044 homologado, com a PONTE
/// 045→044 (Dédalo V6, blocker 2): após 101 confirmado, o pipeline consulta o ESTADO persistido/autoritativo da
/// HU e só POSTa quando ele permite:
/// <list type="bullet">
///   <item><b>AGUARDANDO_AUTORIZACAO_SAP</b> ⇒ autoriza UMA vez (mecanismo 044 <c>AutorizarEnvioAsync</c>) e
///   COMPROVA <b>PRONTA_PARA_ENVIO</b> relendo o estado antes de enviar;</item>
///   <item><b>PRONTA_PARA_ENVIO</b> ⇒ NÃO autoriza de novo; envia direto;</item>
///   <item>qualquer outro estado (EnviandoSap/ConfirmadaSap/ErroSap/IndeterminadoTimeout/Bloqueada/...) ⇒
///   FAIL-CLOSED, ZERO POST HU.</item>
/// </list>
/// Não reimplementa a HU (delega ao <c>EnviarAsync</c> existente); a autoridade do estado é o banco. A regra
/// 044↔045 fica AQUI (adapter/serviço), nunca na Form.
/// </summary>
public sealed class ProdutoAcabadoHuEnvioAdapter : IProdutoAcabadoHuEnvio
{
    private readonly IProdutoAcabadoHuBridgeServico _huService;

    public ProdutoAcabadoHuEnvioAdapter(IProdutoAcabadoHuBridgeServico huService)
        => _huService = huService ?? throw new ArgumentNullException(nameof(huService));

    public bool EnvioAutorizado => _huService.EnvioAutorizado;

    public async Task<StatusIntegracaoCaixa> EnviarHuAsync(long codigoCaixa, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        // Estado autoritativo persistido da HU (nunca simulado). Ausente ⇒ fail-closed (não conclui, zero POST).
        ProdutoAcabadoCaixa? caixa = await _huService.ObterPorCodigoAsync(codigoCaixa, cancellationToken);
        if (caixa is null)
        {
            return StatusIntegracaoCaixa.EnviandoSap; // estado desconhecido ⇒ bloqueia avanço, sem POST
        }

        StatusIntegracaoCaixa estado = caixa.StatusIntegracao;

        // AGUARDANDO ⇒ autoriza UMA vez e comprova PRONTA relendo o estado persistido.
        if (estado == StatusIntegracaoCaixa.AguardandoAutorizacaoSap)
        {
            bool autorizado = await _huService.AutorizarEnvioAsync(codigoCaixa, usuario, terminal, cancellationToken);
            ProdutoAcabadoCaixa? posAutorizacao = autorizado
                ? await _huService.ObterPorCodigoAsync(codigoCaixa, cancellationToken)
                : caixa;
            estado = posAutorizacao?.StatusIntegracao ?? StatusIntegracaoCaixa.EnviandoSap;

            if (estado != StatusIntegracaoCaixa.ProntaParaEnvio)
            {
                // Autorização não comprovada (estado não avançou para PRONTA) ⇒ fail-closed, ZERO POST.
                return estado;
            }
        }

        // PRONTA (já era, ou passou a ser após autorizar) ⇒ envia; devolve o estado persistido pós-envio.
        if (estado == StatusIntegracaoCaixa.ProntaParaEnvio)
        {
            ResultadoEnvioHu resultado = await _huService.EnviarAsync(codigoCaixa, usuario, terminal, cancellationToken);
            return resultado.Caixa?.StatusIntegracao ?? StatusIntegracaoCaixa.EnviandoSap;
        }

        // Qualquer outro estado (inclui ConfirmadaSap idempotente, ErroSap, IndeterminadoTimeout, Bloqueada) ⇒ ZERO POST.
        return estado;
    }
}
