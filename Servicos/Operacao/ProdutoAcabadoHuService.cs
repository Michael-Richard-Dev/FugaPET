using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Orquestração da máquina de fluxo persistente da HU de caixa (Produto Acabado): PostgreSQL (Repository)
/// + gateway SAP. Responsabilidades: registrar caixa/pesagem, finalizar local, gerar preview, aguardar/
/// autorizar, CLAIM atômico (usa EXCLUSIVAMENTE o snapshot retornado), enviar UMA tentativa, classificar
/// sucesso/erro/timeout, reconciliar e cancelar/reprocessar. Nunca faz UI. Timeout ⇒ INDETERMINADO_TIMEOUT
/// (sem retry, sem 2º POST). Respostas atrasadas NUNCA alteram tentativa diferente (token+tentativa do claim).
/// </summary>
public sealed class ProdutoAcabadoHuService : IProdutoAcabadoHuBridgeServico
{
    private readonly IProdutoAcabadoRepositorio _repositorio;
    private readonly IProdutoAcabadoHandlingUnitSapServico _gateway;
    private readonly ProdutoAcabadoHandlingUnitCaixaRequestBuilder _requestBuilder;

    public ProdutoAcabadoHuService(
        IProdutoAcabadoRepositorio repositorio,
        IProdutoAcabadoHandlingUnitSapServico gateway,
        ProdutoAcabadoHandlingUnitCaixaRequestBuilder? requestBuilder = null)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _requestBuilder = requestBuilder ?? new ProdutoAcabadoHandlingUnitCaixaRequestBuilder();
    }

    public bool EnvioAutorizado => _gateway.EnvioAutorizado;

    /// <summary>
    /// Registra a caixa (INSERT EM_PESAGEM) + a pesagem, finaliza local, gera preview (request tipado),
    /// salva preview e move para AGUARDANDO_AUTORIZACAO_SAP. Garante UMA caixa ativa por terminal antes de
    /// registrar. Retorna o snapshot persistido final.
    /// </summary>
    public async Task<ResultadoFinalizacaoHu> RegistrarEFinalizarCaixaAsync(
        ProdutoAcabadoCaixa caixa, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caixa);

        ProdutoAcabadoCaixa? ativa = await _repositorio.ObterAtivaPorTerminalAsync(terminal, cancellationToken);
        if (ativa is not null)
        {
            return ResultadoFinalizacaoHu.Bloqueado(
                $"Já existe uma caixa ativa no terminal (status {MapeadorStatusHuCaixa.ParaTextoBanco(ativa.StatusIntegracao)}). "
                + "Conclua ou cancele antes de registrar outra.", ativa);
        }

        ResultadoRequestHandlingUnitCaixa request = _requestBuilder.Montar(caixa);
        if (!request.Sucesso || request.Request is null)
        {
            return ResultadoFinalizacaoHu.Bloqueado(request.Mensagem, null);
        }

        ProdutoAcabadoCaixa persistida = await _repositorio.RegistrarCaixaAsync(caixa, cancellationToken);
        long codigo = persistida.CodigoProdutoAcabadoCaixa
            ?? throw new InvalidOperationException("Caixa persistida sem código.");

        await _repositorio.RegistrarPesagemAsync(new RegistroPesagemHuCaixa
        {
            CodigoHuCaixa = codigo,
            CodigoBalanca = persistida.CodigoBalanca,
            OrigemPesagem = persistida.OrigemPesagem,
            PesoLido = persistida.PesoBrutoKg,
            PesoBruto = persistida.PesoBrutoKg,
            PesoLiquido = persistida.PesoLiquidoKg,
            PesoTara = persistida.TaraKg,
            UnidadePeso = persistida.UnidadePeso,
            CodigoUsuario = usuario
        }, cancellationToken);

        // REV4-§5/§10: TODA póscondição bool do contrato 044 é verificada; false ⇒ BLOQUEADO (nunca vira
        // sucesso lógico da camada Service). A finalização só é OK depois de comprovar o snapshot persistido.
        if (!await _repositorio.FinalizarLocalAsync(codigo, usuario, terminal, cancellationToken))
        {
            return ResultadoFinalizacaoHu.Bloqueado(
                "Não foi possível finalizar a caixa localmente (persistência não comprovada).",
                await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken) ?? persistida);
        }

        if (!await _repositorio.SalvarPreviewAsync(codigo, request.JsonSanitizado, request.Endpoint, cancellationToken))
        {
            return ResultadoFinalizacaoHu.Bloqueado(
                "Não foi possível salvar o preview da caixa (persistência não comprovada).",
                await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken) ?? persistida);
        }

        if (!await _repositorio.AguardarAutorizacaoAsync(codigo, cancellationToken))
        {
            return ResultadoFinalizacaoHu.Bloqueado(
                "Não foi possível mover a caixa para AGUARDANDO_AUTORIZACAO_SAP (persistência não comprovada).",
                await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken) ?? persistida);
        }

        // Recarrega o snapshot e só declara OK se o estado persistido for o esperado (AGUARDANDO_AUTORIZACAO_SAP)
        // ou um estado comprovadamente posterior permitido pelo contrato sob concorrência controlada (PRONTA).
        ProdutoAcabadoCaixa? snapshot = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
        if (snapshot is null
            || snapshot.StatusIntegracao is not (StatusIntegracaoCaixa.AguardandoAutorizacaoSap or StatusIntegracaoCaixa.ProntaParaEnvio))
        {
            return ResultadoFinalizacaoHu.Bloqueado(
                "Estado final da caixa não pôde ser comprovado como AGUARDANDO_AUTORIZACAO_SAP.",
                snapshot ?? persistida);
        }

        return ResultadoFinalizacaoHu.Ok(snapshot, request.JsonSanitizado);
    }

    /// <summary>fn_autorizar_envio: AGUARDANDO_AUTORIZACAO_SAP → PRONTA_PARA_ENVIO.</summary>
    public Task<bool> AutorizarEnvioAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
        => _repositorio.AutorizarEnvioAsync(codigo, usuario, terminal, cancellationToken);

    /// <summary>
    /// Envia UMA tentativa. Fail-closed: se o gateway não estiver autorizado, NÃO faz claim nem POST (a
    /// caixa permanece PRONTA_PARA_ENVIO). Caso contrário: claim atômico → usa SÓ o snapshot retornado →
    /// POST → classifica sucesso/erro/timeout com token+tentativa do claim.
    /// </summary>
    public async Task<ResultadoEnvioHu> EnviarAsync(long codigo, long usuario, string terminal, CancellationToken cancellationToken = default)
    {
        if (!_gateway.EnvioAutorizado)
        {
            ProdutoAcabadoCaixa? atual = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
            return ResultadoEnvioHu.NaoAutorizadoParaEnvio(
                "Envio da HU não autorizado nesta frente (gateway fail-closed). Caixa permanece pronta para envio.", atual);
        }

        // Defesa 4 (Hera): a camada SERVICE não depende da UI. Carrega o snapshot PERSISTIDO e valida o centro
        // PET 3007 ANTES do claim. Centro divergente ⇒ NÃO autoriza/claim/POST (CLAIM=0, POST=0, tentativa não
        // consumida). Sem tocar em funções SQL/banco.
        ProdutoAcabadoCaixa? preCheck = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
        if (preCheck is not null && !RegraCentroPetProdutoAcabado.CentroPermitido(preCheck.Centro))
        {
            return ResultadoEnvioHu.NaoAutorizadoParaEnvio(
                RegraCentroPetProdutoAcabado.MensagemCentroNaoPermitido, preCheck);
        }

        ProdutoAcabadoCaixa? snapshot = await _repositorio.ClaimEnvioAsync(codigo, usuario, terminal, cancellationToken);
        if (snapshot is null)
        {
            return ResultadoEnvioHu.ClaimNaoObtido(
                "Claim de envio não obtido (estado divergente/duplo clique). Nenhum POST foi executado.");
        }

        int tentativa = snapshot.Tentativas;
        Guid claimToken = snapshot.ClaimToken
            ?? throw new InvalidOperationException("Snapshot do claim sem claim_token.");

        ResultadoRequestHandlingUnitCaixa request = _requestBuilder.Montar(snapshot);
        if (!request.Sucesso || request.Request is null)
        {
            // Falha PRÉ-POST (request inválido): registra o erro e verifica a póscondição (§10). Nenhum POST
            // foi executado; se a persistência do erro não puder ser comprovada, retorna estado seguro.
            bool erroPersistido = await _repositorio.RegistrarErroAsync(
                codigo, tentativa, claimToken, null, null, null, request.Mensagem,
                ResultadoErroHu.ErroDefinitivo, podeReprocessar: false, cancellationToken);
            ProdutoAcabadoCaixa? frescoReq = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
            return erroPersistido
                ? ResultadoEnvioHu.Erro(request.Mensagem, frescoReq)
                : ResultadoEnvioHu.ConfirmacaoLocalNaoComprovada(
                    "Falha ao montar o request e a persistência local do erro não pôde ser comprovada. "
                    + "Não envie novamente. Verifique o estado da caixa.", null, frescoReq);
        }

        ResultadoPostHandlingUnit resultado =
            await _gateway.CriarHandlingUnitCaixaAsync(request.Request, request.Endpoint, cancellationToken);

        // REV4-§6/§8/§9/§10: cada póscondição bool do banco é capturada e comprovada no snapshot fresco.
        // Persistência não comprovada NUNCA vira sucesso lógico, NUNCA gera 2º POST/retry/reprocessamento.
        switch (resultado.Cenario)
        {
            case CenarioPostHandlingUnit.Confirmado:
            {
                string huSap = resultado.HandlingUnitExternalId ?? string.Empty;
                bool persistido = await _repositorio.RegistrarSucessoAsync(
                    codigo, tentativa, claimToken, huSap, resultado.Warehouse,
                    resultado.HttpStatus ?? 201, resultado.ResponseJsonSanitizado, resultado.SapMessagesJson,
                    resultado.Etag, resultado.CreatedByUserSap, resultado.CreationDatetimeSap, cancellationToken);
                ProdutoAcabadoCaixa? fresco = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);

                // REV5-§2: CONFIRMADO exige EXATAMENTE persistência true E snapshot fresco CONFIRMADA_SAP
                // + HU idêntica à retornada pelo SAP + HttpStatus == 201 (NULL/200/500/qualquer != 201 ⇒ NÃO).
                bool huComprovada = !string.IsNullOrWhiteSpace(huSap)
                    && persistido
                    && fresco is { StatusIntegracao: StatusIntegracaoCaixa.ConfirmadaSap }
                    && string.Equals(fresco.HandlingUnitExternalId, huSap, StringComparison.Ordinal)
                    && fresco.HttpStatus == 201;
                if (huComprovada)
                {
                    return ResultadoEnvioHu.Confirmado(huSap, fresco);
                }

                // SAP criou a HU, mas a confirmação LOCAL não pôde ser comprovada: estado seguro, sem 2º POST.
                return ResultadoEnvioHu.ConfirmacaoLocalNaoComprovada(
                    "A HU foi retornada pelo SAP, porém a confirmação local não pôde ser comprovada. "
                    + "Não envie novamente. Verifique o estado da caixa.", huSap, fresco);
            }

            case CenarioPostHandlingUnit.NaoAutorizado:
            {
                bool persistido = await _repositorio.RegistrarErroAsync(
                    codigo, tentativa, claimToken, resultado.HttpStatus, resultado.ResponseJsonSanitizado,
                    resultado.SapMessagesJson, resultado.MensagemSanitizada, ResultadoErroHu.NaoAutorizado,
                    podeReprocessar: false, cancellationToken);
                ProdutoAcabadoCaixa? fresco = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
                // REV5-§5: não basta o bool true — o snapshot fresco precisa estar coerente com ERRO_SAP.
                return persistido && fresco is { StatusIntegracao: StatusIntegracaoCaixa.ErroSap }
                    ? ResultadoEnvioHu.Erro(resultado.MensagemSanitizada, fresco)
                    : ResultadoEnvioHu.ConfirmacaoLocalNaoComprovada(
                        "SAP recusou o envio (não autorizado), porém a persistência local do erro não pôde ser comprovada. "
                        + "Não envie novamente. Verifique o estado da caixa.", null, fresco);
            }

            case CenarioPostHandlingUnit.ErroDefinitivo:
            {
                bool persistido = await _repositorio.RegistrarErroAsync(
                    codigo, tentativa, claimToken, resultado.HttpStatus, resultado.ResponseJsonSanitizado,
                    resultado.SapMessagesJson, resultado.MensagemSanitizada, ResultadoErroHu.ErroDefinitivo,
                    resultado.PodeReprocessar, cancellationToken);
                ProdutoAcabadoCaixa? fresco = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
                // REV5-§5: só declara "erro persistido" se o snapshot fresco comprova ERRO_SAP (senão fail-closed).
                return persistido && fresco is { StatusIntegracao: StatusIntegracaoCaixa.ErroSap }
                    ? ResultadoEnvioHu.Erro(resultado.MensagemSanitizada, fresco)
                    : ResultadoEnvioHu.ConfirmacaoLocalNaoComprovada(
                        "SAP retornou erro definitivo, porém a persistência local do erro não pôde ser comprovada. "
                        + "Não envie novamente. Verifique o estado da caixa.", null, fresco);
            }

            case CenarioPostHandlingUnit.Timeout:
            default:
            {
                // Timeout (ou NaoEnviado inesperado após claim): resultado indeterminado, sem retry/2º POST.
                bool persistido = await _repositorio.RegistrarTimeoutAsync(
                    codigo, tentativa, claimToken, resultado.SapMessagesJson,
                    string.IsNullOrWhiteSpace(resultado.MensagemSanitizada)
                        ? "Timeout no POST da HU: resultado indeterminado."
                        : resultado.MensagemSanitizada,
                    cancellationToken);
                ProdutoAcabadoCaixa? fresco = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);

                // §9: só afirmar INDETERMINADO_TIMEOUT se a persistência retornou true E o snapshot comprova.
                bool indeterminadoComprovado = persistido
                    && fresco is { StatusIntegracao: StatusIntegracaoCaixa.IndeterminadoTimeout };
                // REV5-§4: timeout NÃO tem reconciliação automática determinística (HandlingUnitExternalID="$1").
                // Orientação = verificação manual no SAP; ZERO retry/reprocessamento/2º POST.
                return indeterminadoComprovado
                    ? ResultadoEnvioHu.Timeout(
                        "Resultado indeterminado após possível POST. Não envie novamente. "
                        + "Verificação manual no SAP obrigatória.", fresco)
                    : ResultadoEnvioHu.ConfirmacaoLocalNaoComprovada(
                        "Resultado SAP/rede indeterminado e a persistência local não pôde ser comprovada. "
                        + "Não envie novamente. Verifique o estado da caixa.", null, fresco);
            }
        }
    }

    /// <summary>
    /// Mensagem oficial da incompatibilidade de contrato: após um POST com HandlingUnitExternalID="$1",
    /// o contrato SAP fornecido NÃO expõe uma chave determinística conhecida pelo C# para localizar a HU
    /// criada em caso de timeout. Por isso a reconciliação só ocorre com uma HU explicitamente conhecida.
    /// </summary>
    public const string IncompatibilidadeReconciliacao =
        "INCOMPATIBILIDADE_CONTRATO_SAP_RECONCILIACAO: após POST com HandlingUnitExternalID=\"$1\" não existe, "
        + "no contrato fornecido, uma chave SAP determinística conhecida pelo C# para localizar a HU criada após "
        + "timeout. O CodigoCaixaLocal (CX-...) é identificador LOCAL e NÃO é HandlingUnitExternalID. "
        + "Ares deve confirmar a chave de recuperação (ex.: filtro por identificador estável retornado no 201, "
        + "ou consulta por atributos determinísticos). Sem isso, a reconciliação permanece indeterminada — sem fake.";

    /// <summary>
    /// Reconciliação após INDETERMINADO_TIMEOUT. Só ocorre com uma HU SAP EXPLICITAMENTE conhecida
    /// (<paramref name="handlingUnitExternalIdConhecido"/>). NUNCA inventa a chave a partir do CodigoCaixaLocal:
    /// sem chave conhecida ⇒ Indeterminada com <see cref="IncompatibilidadeReconciliacao"/> (sem chamar o
    /// gateway, sem mascarar). 200+comparação aprovada ⇒ confirma; 404 ⇒ registra e PERMANECE em
    /// INDETERMINADO_TIMEOUT (nunca novo POST automático por 404).
    /// </summary>
    public async Task<ResultadoReconciliacaoHu> ReconciliarAsync(
        long codigo, string? handlingUnitExternalIdConhecido = null, CancellationToken cancellationToken = default)
    {
        ProdutoAcabadoCaixa? caixa = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
        if (caixa is null)
        {
            return ResultadoReconciliacaoHu.Indeterminada("Caixa não encontrada.", null);
        }

        if (caixa.StatusIntegracao != StatusIntegracaoCaixa.IndeterminadoTimeout)
        {
            return ResultadoReconciliacaoHu.Indeterminada(
                $"Reconciliação só a partir de INDETERMINADO_TIMEOUT (atual: {MapeadorStatusHuCaixa.ParaTextoBanco(caixa.StatusIntegracao)}).", caixa);
        }

        // §5: sem HU SAP determinística conhecida, NÃO reconcilia (não usa CodigoCaixaLocal, não chama gateway).
        string huEsperada = (handlingUnitExternalIdConhecido ?? string.Empty).Trim();
        if (huEsperada.Length == 0)
        {
            return ResultadoReconciliacaoHu.Indeterminada(IncompatibilidadeReconciliacao, caixa);
        }

        ResultadoReconciliacaoHandlingUnit reconc =
            await _gateway.ReconciliarHandlingUnitAsync(huEsperada, warehouse: string.Empty, cancellationToken);

        switch (reconc.Cenario)
        {
            case CenarioReconciliacaoHandlingUnit.Confirmado when reconc.ComparacaoAprovada:
            {
                // §10: verifica a póscondição da confirmação de reconciliação; só declara Confirmada se o
                // snapshot fresco comprova CONFIRMADA_SAP (senão permanece indeterminada, sem inventar estado).
                bool confirmadoLocal = await _repositorio.ConfirmarReconciliacaoAsync(
                    codigo, reconc.HandlingUnitExternalId ?? huEsperada, reconc.Warehouse, reconc.HttpStatus ?? 200,
                    reconc.ResponseJsonSanitizado, reconc.SapMessagesJson, reconc.Etag, reconc.CreatedByUserSap,
                    reconc.CreationDatetimeSap, comparacaoAprovada: true, cancellationToken);
                ProdutoAcabadoCaixa? frescoRec = await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);
                return confirmadoLocal && frescoRec is { StatusIntegracao: StatusIntegracaoCaixa.ConfirmadaSap }
                    ? ResultadoReconciliacaoHu.Confirmada(reconc.HandlingUnitExternalId ?? huEsperada, frescoRec)
                    : ResultadoReconciliacaoHu.Indeterminada(
                        "SAP indicou a HU, porém a confirmação local da reconciliação não pôde ser comprovada.", frescoRec);
            }

            case CenarioReconciliacaoHandlingUnit.NaoEncontrado:
            {
                // §10: captura a póscondição; em qualquer caso a caixa permanece em INDETERMINADO_TIMEOUT
                // (nunca novo POST automático por 404).
                await _repositorio.RegistrarReconciliacaoNaoEncontradaAsync(
                    codigo, reconc.HandlingUnitExternalId ?? huEsperada, reconc.Warehouse, reconc.HttpStatus ?? 404,
                    reconc.ResponseJsonSanitizado, reconc.SapMessagesJson,
                    string.IsNullOrWhiteSpace(reconc.MensagemSanitizada) ? "HU não encontrada na reconciliação." : reconc.MensagemSanitizada,
                    cancellationToken);
                return ResultadoReconciliacaoHu.NaoEncontrada(
                    "HU não encontrada (404): caixa permanece em INDETERMINADO_TIMEOUT. Nenhum novo POST automático.",
                    await _repositorio.ObterPorCodigoAsync(codigo, cancellationToken));
            }

            default:
                return ResultadoReconciliacaoHu.Indeterminada(
                    "Reconciliação indeterminada: nenhuma alteração de estado.", caixa);
        }
    }

    /// <summary>fn_liberar_reprocessamento: ERRO_SAP reprocessável → PRONTA_PARA_ENVIO.</summary>
    public Task<bool> LiberarReprocessamentoAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => _repositorio.LiberarReprocessamentoAsync(codigo, usuario, terminal, motivo, cancellationToken);

    /// <summary>fn_cancelar.</summary>
    public Task<bool> CancelarAsync(long codigo, long usuario, string terminal, string motivo, CancellationToken cancellationToken = default)
        => _repositorio.CancelarAsync(codigo, usuario, terminal, motivo, cancellationToken);

    /// <summary>fn_bloquear_configuracao.</summary>
    public Task<bool> BloquearConfiguracaoAsync(long codigo, long usuario, string terminal, string erro, string? sapMessagesJson, CancellationToken cancellationToken = default)
        => _repositorio.BloquearConfiguracaoAsync(codigo, usuario, terminal, erro, sapMessagesJson, cancellationToken);

    public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
        => _repositorio.ObterPorCodigoAsync(codigo, cancellationToken);

    public Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        => _repositorio.ObterAtivaPorTerminalAsync(terminal, cancellationToken);

    /// <summary>Lista persistida (read-only) das caixas da OP+terminal para recuperar a grid ao reabrir a tela.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPersistidasAsync(
        string numeroOrdemProducao, string terminal, CancellationToken cancellationToken = default)
        => _repositorio.ListarPorOrdemTerminalAsync(numeroOrdemProducao, terminal, cancellationToken);

    /// <summary>
    /// REV4-§12: recuperação da grid por CONTEXTO completo (OP+item+material+lote+terminal), evitando misturar
    /// caixas de outro item/material/lote sob a mesma OP. A Form passa os dados já carregados da OP.
    /// </summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPersistidasPorContextoAsync(
        string numeroOrdemProducao, string itemOrdemProducao, string material, string lote, string terminal,
        CancellationToken cancellationToken = default)
        => _repositorio.ListarPorContextoAsync(numeroOrdemProducao, itemOrdemProducao, material, lote, terminal, cancellationToken);

    /// <summary>INC-047: caixas por HU externo (seleção manual da Paletização). Read-only.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPorHandlingUnitsAsync(
        IReadOnlyList<string> husExternais, CancellationToken cancellationToken = default)
        => _repositorio.ListarPorHandlingUnitsAsync(husExternais, cancellationToken);

    /// <summary>INC-047: caixas por intervalo de HU externo (sequência da Paletização). Read-only.</summary>
    public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarCaixasPorIntervaloHandlingUnitAsync(
        string huInicial, string huFinal, string? material, CancellationToken cancellationToken = default)
        => _repositorio.ListarPorIntervaloHandlingUnitAsync(huInicial, huFinal, material, cancellationToken);
}

/// <summary>Resultado do registro+finalização da caixa (até AGUARDANDO_AUTORIZACAO_SAP).</summary>
public sealed record ResultadoFinalizacaoHu(bool Sucesso, string Mensagem, ProdutoAcabadoCaixa? Caixa, string RequestJson)
{
    public static ResultadoFinalizacaoHu Ok(ProdutoAcabadoCaixa caixa, string requestJson)
        => new(true, "Caixa registrada e finalizada; aguardando autorização de envio.", caixa, requestJson);

    public static ResultadoFinalizacaoHu Bloqueado(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(false, mensagem, caixa, string.Empty);
}

/// <summary>
/// Cenário do envio de UMA tentativa. <see cref="ConfirmacaoLocalNaoComprovada"/> (REV4-§6/§8/§9): o
/// resultado SAP/rede foi obtido, mas a póscondição de PERSISTÊNCIA local não pôde ser comprovada — estado
/// seguro que NUNCA autoriza 2º POST/retry/reprocessamento.
/// </summary>
public enum CenarioEnvioHu { Confirmado, Erro, Timeout, ClaimNaoObtido, NaoAutorizado, ConfirmacaoLocalNaoComprovada }

/// <summary>Resultado do envio HU (uma tentativa).</summary>
public sealed record ResultadoEnvioHu(CenarioEnvioHu Cenario, string Mensagem, string? HandlingUnitExternalId, ProdutoAcabadoCaixa? Caixa)
{
    public static ResultadoEnvioHu Confirmado(string hu, ProdutoAcabadoCaixa? caixa) => new(CenarioEnvioHu.Confirmado, "HU confirmada.", hu, caixa);
    public static ResultadoEnvioHu Erro(string mensagem, ProdutoAcabadoCaixa? caixa) => new(CenarioEnvioHu.Erro, mensagem, null, caixa);
    public static ResultadoEnvioHu Timeout(string mensagem, ProdutoAcabadoCaixa? caixa) => new(CenarioEnvioHu.Timeout, mensagem, null, caixa);
    public static ResultadoEnvioHu ClaimNaoObtido(string mensagem) => new(CenarioEnvioHu.ClaimNaoObtido, mensagem, null, null);
    public static ResultadoEnvioHu NaoAutorizadoParaEnvio(string mensagem, ProdutoAcabadoCaixa? caixa) => new(CenarioEnvioHu.NaoAutorizado, mensagem, null, caixa);

    /// <summary>REV4-§6/§8/§9: SAP respondeu, mas a persistência local não pôde ser comprovada. Sem 2º POST.</summary>
    public static ResultadoEnvioHu ConfirmacaoLocalNaoComprovada(string mensagem, string? hu, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioHu.ConfirmacaoLocalNaoComprovada, mensagem, hu, caixa);
}

/// <summary>Cenário da reconciliação.</summary>
public enum CenarioReconciliacaoHu { Confirmada, NaoEncontrada, Indeterminada }

/// <summary>Resultado da reconciliação.</summary>
public sealed record ResultadoReconciliacaoHu(CenarioReconciliacaoHu Cenario, string Mensagem, string? HandlingUnitExternalId, ProdutoAcabadoCaixa? Caixa)
{
    public static ResultadoReconciliacaoHu Confirmada(string hu, ProdutoAcabadoCaixa? caixa) => new(CenarioReconciliacaoHu.Confirmada, "HU reconciliada e confirmada.", hu, caixa);
    public static ResultadoReconciliacaoHu NaoEncontrada(string mensagem, ProdutoAcabadoCaixa? caixa) => new(CenarioReconciliacaoHu.NaoEncontrada, mensagem, null, caixa);
    public static ResultadoReconciliacaoHu Indeterminada(string mensagem, ProdutoAcabadoCaixa? caixa) => new(CenarioReconciliacaoHu.Indeterminada, mensagem, null, caixa);
}
