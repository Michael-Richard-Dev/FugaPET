using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Controle.Processo;

public sealed class ProdutoAcabadoController
{
    public const string EndpointConsultaOpProdutoAcabado =
        "GET /sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/A_ProductionOrder_2('<OP>')?$format=json&$expand=to_ProductionOrderItem,to_ProductionOrderOperation,to_ProductionOrderStatus&sap-client=110";

    private readonly IProductionOrderSapServico _productionOrderSapServico;
    private readonly TaraController _taraController;
    private readonly ProdutoAcabadoPaletePayloadBuilder _paletePayloadBuilder;

    // Fluxo de caixa individual (Handling Unit): persistência, preview HU e gateway SAP — todos por injeção.
    private readonly IProdutoAcabadoRepositorio _repositorio;
    private readonly ProdutoAcabadoHandlingUnitCaixaPayloadBuilder _huPayloadBuilder;
    private readonly IProdutoAcabadoHandlingUnitSapServico _huSapServico;

    public ProdutoAcabadoController()
        : this(FabricaProductionOrderSapServico.Criar())
    {
    }

    public ProdutoAcabadoController(
        IProductionOrderSapServico productionOrderSapServico,
        TaraController? taraController = null,
        ProdutoAcabadoPaletePayloadBuilder? paletePayloadBuilder = null,
        IProdutoAcabadoRepositorio? repositorio = null,
        ProdutoAcabadoHandlingUnitCaixaPayloadBuilder? huPayloadBuilder = null,
        IProdutoAcabadoHandlingUnitSapServico? huSapServico = null)
    {
        _productionOrderSapServico = productionOrderSapServico ?? throw new ArgumentNullException(nameof(productionOrderSapServico));
        _taraController = taraController ?? FabricaControladoresCadastro.CriarTaraController();
        _paletePayloadBuilder = paletePayloadBuilder ?? new ProdutoAcabadoPaletePayloadBuilder();
        // Produção: persistência BLOQUEADA por schema pendente (não finge banco). Testes injetam fake em memória.
        _repositorio = repositorio ?? new ProdutoAcabadoRepositorioIndisponivel();
        _huPayloadBuilder = huPayloadBuilder ?? new ProdutoAcabadoHandlingUnitCaixaPayloadBuilder();
        // Produção: gateway NÃO AUTORIZADO (sem HTTP, sem simular sucesso). Testes injetam fake controlado.
        _huSapServico = huSapServico ?? new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
    }

    public bool SapSimulado => _productionOrderSapServico.EhSimulado;

    public async Task<ResultadoConsultaProdutoAcabado> ConsultarOrdemProducaoAsync(
        string numeroOp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroOp))
        {
            return ResultadoConsultaProdutoAcabado.Falha("Informe a OP para consulta.");
        }

        ResultadoConsultaOrdemProducaoSap resultadoSap =
            await _productionOrderSapServico.ConsultarOrdemAsync(numeroOp.Trim(), cancellationToken);
        if (resultadoSap.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || resultadoSap.Ordem is null)
        {
            string mensagem = string.IsNullOrWhiteSpace(resultadoSap.MensagemSanitizada)
                ? "Não foi possível consultar a OP no SAP."
                : resultadoSap.MensagemSanitizada;
            return ResultadoConsultaProdutoAcabado.Falha(mensagem);
        }

        OrdemProducaoSap ordemSap = resultadoSap.Ordem;
        if (!ordemSap.Liberada)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas ainda não está liberada para produto acabado.");
        }

        if (ordemSap.Confirmada || ordemSap.Excluida)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas está encerrada, confirmada ou marcada para exclusão.");
        }

        ProdutoAcabadoOrdem ordem = MapearOrdem(ordemSap);
        if (string.IsNullOrWhiteSpace(ordem.MaterialProduzido))
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP encontrada, mas sem item produzido para produto acabado.");
        }

        if (ordem.QuantidadePendente <= 0m)
        {
            return ResultadoConsultaProdutoAcabado.Falha("OP sem saldo pendente para entrada de produto acabado.");
        }

        ProdutoAcabadoNormaEmbalagem norma = ConsultarOuPrepararNormaEmbalagem(ordem.MaterialProduzido, 1);
        return ResultadoConsultaProdutoAcabado.Ok(ordem, norma, resultadoSap.MensagemSanitizada);
    }

    public ProdutoAcabadoNormaEmbalagem ConsultarOuPrepararNormaEmbalagem(
        string material,
        int quantidadeProdutosPorCaixa,
        string? packagingInstruction = null)
    {
        if (quantidadeProdutosPorCaixa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidadeProdutosPorCaixa), "Quantidade por caixa deve ser maior que zero.");
        }

        return ProdutoAcabadoPackagingApiClient.CriarFallbackControlado(
            material,
            quantidadeProdutosPorCaixa,
            string.IsNullOrWhiteSpace(packagingInstruction) ? "FALLBACK_MEMORIA" : packagingInstruction);
    }

    public ProdutoAcabadoCaixa MontarCaixa(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        int numeroCaixa,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        string terminal = "",
        long? codigoUsuario = null)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(norma);

        if (norma.QuantidadeProdutosPorCaixa <= 0)
        {
            throw new InvalidOperationException("Quantidade por caixa deve ser maior que zero.");
        }

        decimal pesoLiquidoKg = pesoBrutoKg - taraKg;
        if (pesoLiquidoKg <= 0m)
        {
            throw new InvalidOperationException("Peso líquido da caixa deve ser maior que zero.");
        }

        // §16: material de embalagem da CAIXA — nunca PALLET01 (material de palete). Vem da norma
        // (MaterialCaixa) ou de um valor controlado informado; a origem é sempre explícita.
        string embalagem = !string.IsNullOrWhiteSpace(materialEmbalagem)
            ? materialEmbalagem.Trim()
            : (norma.MaterialCaixa ?? string.Empty).Trim();

        return new ProdutoAcabadoCaixa
        {
            NumeroCaixa = numeroCaixa,
            CodigoCaixaLocal = $"CX-{ordem.NumeroOrdem}-{numeroCaixa:0000}",
            NumeroOrdemProducao = ordem.NumeroOrdem.Trim(),
            ItemOrdemProducao = ordem.ItemOrdem.Trim(),
            Material = ordem.MaterialProduzido.Trim(),
            Lote = ordem.Lote.Trim(),
            Centro = ordem.Centro.Trim(),
            Deposito = ordem.DepositoDestino.Trim(),
            MaterialEmbalagem = embalagem,
            OrigemMaterialEmbalagem = origemMaterialEmbalagem,
            PesoBrutoKg = pesoBrutoKg,
            TaraKg = taraKg,
            PesoLiquidoKg = pesoLiquidoKg,
            UnidadePeso = "KG",
            QuantidadeProdutos = norma.QuantidadeProdutosPorCaixa,
            UnidadeQuantidade = string.IsNullOrWhiteSpace(norma.Unidade) ? "UN" : norma.Unidade.Trim().ToUpperInvariant(),
            OrigemPesagem = origemPesagem,
            CorrelationId = Guid.NewGuid(),
            StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal,
            Terminal = terminal ?? string.Empty,
            CodigoUsuario = codigoUsuario
        };
    }

    /// <summary>
    /// Monta uma caixa sem número/código definitivos. A identidade sequencial só pode ser atribuída
    /// atomicamente por IProdutoAcabadoRepositorio.RegistrarCaixaAsync.
    /// </summary>
    public ProdutoAcabadoCaixa MontarCaixaSemIdentidadeSequencial(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        string terminal = "",
        long? codigoUsuario = null)
    {
        ProdutoAcabadoCaixa caixa = MontarCaixa(
            ordem,
            norma,
            0,
            pesoBrutoKg,
            taraKg,
            origemPesagem,
            materialEmbalagem,
            origemMaterialEmbalagem,
            terminal,
            codigoUsuario);
        caixa.CodigoCaixaLocal = string.Empty;
        return caixa;
    }

    public ProdutoAcabadoPalete MontarPalete(
        ProdutoAcabadoOrdem ordem,
        IReadOnlyList<ProdutoAcabadoCaixa> caixas,
        int primeiraCaixa,
        int ultimaCaixa,
        string packagingMaterial)
    {
        if (primeiraCaixa > ultimaCaixa)
        {
            throw new InvalidOperationException("Intervalo de caixas inválido.");
        }

        ProdutoAcabadoCaixa[] selecionadas = caixas
            .Where(caixa => caixa.NumeroCaixa >= primeiraCaixa && caixa.NumeroCaixa <= ultimaCaixa)
            .OrderBy(caixa => caixa.NumeroCaixa)
            .ToArray();
        if (selecionadas.Length != (ultimaCaixa - primeiraCaixa + 1))
        {
            throw new InvalidOperationException("Todas as caixas do intervalo precisam existir.");
        }

        if (selecionadas.Any(caixa => !string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal)))
        {
            throw new InvalidOperationException("Caixa já paletizada não pode entrar em outro palete.");
        }

        string codigoPalete = $"PLT-{ordem.NumeroOrdem}-{primeiraCaixa:0000}-{ultimaCaixa:0000}";
        foreach (ProdutoAcabadoCaixa caixa in selecionadas)
        {
            caixa.CodigoPaleteLocal = codigoPalete;
        }

        return new ProdutoAcabadoPalete
        {
            CodigoPaleteLocal = codigoPalete,
            PrimeiraCaixa = primeiraCaixa,
            UltimaCaixa = ultimaCaixa,
            PesoBrutoKg = selecionadas.Sum(caixa => caixa.PesoBrutoKg),
            PesoLiquidoKg = selecionadas.Sum(caixa => caixa.PesoLiquidoKg),
            TaraKg = selecionadas.Sum(caixa => caixa.TaraKg),
            Plant = ordem.Centro,
            StorageLocation = ordem.DepositoDestino,
            PackagingMaterial = packagingMaterial,
            Caixas = selecionadas
        };
    }

    // ============================ Caixa individual (Handling Unit) ============================

    /// <summary>
    /// Finaliza UMA caixa localmente: uma caixa por vez (bloqueia se há caixa ativa no terminal), gera
    /// correlation_id, persiste, monta o preview da HU e persiste o request_payload sanitizado. NÃO envia
    /// ao SAP e NÃO cria palete/Material Document. Retorna o diagnóstico para a Form.
    /// </summary>
    public async Task<ResultadoFinalizacaoCaixa> FinalizarCaixaLocalAsync(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoNormaEmbalagem norma,
        decimal pesoBrutoKg,
        decimal taraKg,
        string origemPesagem,
        string terminal,
        long? codigoUsuario = null,
        string? materialEmbalagem = null,
        OrigemMaterialEmbalagemCaixa origemMaterialEmbalagem = OrigemMaterialEmbalagemCaixa.NaoInformada,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(norma);

        // §3/§18: uma caixa por vez — só inicia nova quando a anterior está CONFIRMADA_SAP ou CANCELADA.
        ProdutoAcabadoCaixa? ativa = await _repositorio.ObterCaixaAtivaPorTerminalAsync(terminal, cancellationToken);
        if (ativa is not null && TransicaoStatusIntegracaoCaixa.BloqueiaNovaCaixa(ativa.StatusIntegracao))
        {
            return ResultadoFinalizacaoCaixa.Bloqueada(
                $"Já existe uma caixa ativa (status {ativa.StatusIntegracao}). "
                + "Conclua, confirme no SAP ou cancele a caixa atual antes de iniciar outra.",
                ativa);
        }

        ProdutoAcabadoCaixa caixa = MontarCaixaSemIdentidadeSequencial(
            ordem, norma, pesoBrutoKg, taraKg, origemPesagem,
            materialEmbalagem, origemMaterialEmbalagem, terminal, codigoUsuario);

        ProdutoAcabadoCaixa persistida = await _repositorio.RegistrarCaixaAsync(caixa, cancellationToken);

        ResultadoPreviewHandlingUnitCaixa preview = _huPayloadBuilder.MontarPreview(ordem, persistida);
        if (!preview.Sucesso)
        {
            // Sem dados suficientes para o preview: caixa fica FINALIZADA_LOCAL, envio bloqueado.
            return ResultadoFinalizacaoCaixa.PreviewPendente(persistida, preview);
        }

        // Preview OK, mas o contrato externo ainda não foi confirmado ⇒ AGUARDANDO_AUTORIZACAO_SAP.
        persistida.RequestPayload = preview.PayloadJsonSanitizado;
        persistida.TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado);
        persistida.TransicionarPara(StatusIntegracaoCaixa.AguardandoAutorizacaoSap);
        if (persistida.CodigoProdutoAcabadoCaixa is long codigo)
        {
            await _repositorio.AtualizarPreviewHuAsync(
                codigo, preview.PayloadJsonSanitizado, StatusIntegracaoCaixa.AguardandoAutorizacaoSap, cancellationToken);
        }

        return ResultadoFinalizacaoCaixa.Ok(persistida, preview, _huSapServico.EnvioAutorizado);
    }

    /// <summary>Prontidão do envio manual: só pode enviar quando o gateway está autorizado e a caixa está PRONTA.</summary>
    public async Task<ResultadoDiagnosticoEnvioCaixa> DiagnosticarProntidaoEnvioCaixaAsync(
        long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
    {
        ProdutoAcabadoCaixa? caixa = await _repositorio.ObterCaixaPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        if (caixa is null)
        {
            return new ResultadoDiagnosticoEnvioCaixa(false, "Caixa não encontrada.", null);
        }

        if (!_huSapServico.EnvioAutorizado)
        {
            return new ResultadoDiagnosticoEnvioCaixa(false,
                "Envio ao SAP ainda não autorizado (contrato OP_HANDLINGUNIT_0001 pendente).", caixa.StatusIntegracao);
        }

        bool pronta = caixa.StatusIntegracao is StatusIntegracaoCaixa.ProntaParaEnvio
            or StatusIntegracaoCaixa.AguardandoAutorizacaoSap or StatusIntegracaoCaixa.ErroSap;
        return new ResultadoDiagnosticoEnvioCaixa(
            pronta, pronta ? "Caixa apta para envio manual." : $"Caixa em {caixa.StatusIntegracao}.", caixa.StatusIntegracao);
    }

    /// <summary>
    /// Envio manual da caixa à HU SAP: uma única tentativa controlada, claim atômico, sem palete/Material
    /// Document. Enquanto o gateway não estiver autorizado, retorna bloqueio controlado e preserva a caixa.
    /// </summary>
    public async Task<ResultadoEnvioCaixaHu> EnviarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
    {
        ProdutoAcabadoCaixa? caixa = await _repositorio.ObterCaixaPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        if (caixa is null)
        {
            return ResultadoEnvioCaixaHu.Bloqueado("Caixa não encontrada.", null);
        }

        // §18: caixa CONFIRMADA_SAP ou EnviandoSap não pode ser reenviada.
        if (caixa.StatusIntegracao == StatusIntegracaoCaixa.ConfirmadaSap)
        {
            return ResultadoEnvioCaixaHu.Bloqueado("Caixa já confirmada no SAP. Reenvio bloqueado.", caixa);
        }

        if (caixa.StatusIntegracao == StatusIntegracaoCaixa.EnviandoSap)
        {
            return ResultadoEnvioCaixaHu.Bloqueado("Envio já em processamento. Aguarde.", caixa);
        }

        if (!_huSapServico.EnvioAutorizado)
        {
            return ResultadoEnvioCaixaHu.NaoAutorizado(
                "Envio da Handling Unit ainda não autorizado (contrato OP_HANDLINGUNIT_0001 pendente). "
                + "Caixa preservada; nenhum POST foi executado.",
                caixa);
        }

        // Caminho autorizado (futuro): persiste PRONTA_PARA_ENVIO no repositório ANTES do claim, para que
        // o claim atômico PRONTA_PARA_ENVIO → ENVIANDO_SAP encontre o estado esperado mesmo com Repository
        // que devolve objetos desconectados. Não confiar no estado local do objeto carregado.
        if (caixa.StatusIntegracao is StatusIntegracaoCaixa.AguardandoAutorizacaoSap or StatusIntegracaoCaixa.ErroSap)
        {
            await _repositorio.AtualizarPreviewHuAsync(
                codigoProdutoAcabadoCaixa, caixa.RequestPayload ?? string.Empty,
                StatusIntegracaoCaixa.ProntaParaEnvio, cancellationToken);
        }

        // Claim ATÔMICO: devolve o SNAPSHOT reservado (EnviandoSap) ou null. A partir daqui usamos SOMENTE
        // o snapshot — nunca o objeto pré-claim (que pode estar desconectado em PRONTA_PARA_ENVIO).
        ProdutoAcabadoCaixa? reservada = await _repositorio.TentarReservarEnvioSapAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        if (reservada is null)
        {
            return ResultadoEnvioCaixaHu.Bloqueado("Envio já reservado por outra tentativa (duplo clique/concorrência).", caixa);
        }

        ProdutoAcabadoCaixaParaEnvio paraEnvio = MontarCaixaParaEnvio(reservada);
        ResultadoCriacaoHandlingUnitCaixa resultado =
            await _huSapServico.CriarHandlingUnitCaixaAsync(paraEnvio, cancellationToken);

        if (resultado.Sucesso && !string.IsNullOrWhiteSpace(resultado.HandlingUnitExternalId))
        {
            await _repositorio.RegistrarSucessoHuAsync(
                codigoProdutoAcabadoCaixa, resultado.HandlingUnitExternalId!, resultado.ResponsePayloadSanitizado,
                resultado.HttpStatus, cancellationToken);
            reservada.HandlingUnitExternalId = resultado.HandlingUnitExternalId;
            reservada.ResponsePayload = resultado.ResponsePayloadSanitizado;
            reservada.HttpStatus = resultado.HttpStatus;
            reservada.EnviadoSapEm = DateTimeOffset.Now;
            reservada.ConfirmadoSapEm = DateTimeOffset.Now;
            reservada.TransicionarPara(StatusIntegracaoCaixa.ConfirmadaSap); // EnviandoSap → ConfirmadaSap (válido)
            return ResultadoEnvioCaixaHu.Confirmado(resultado.HandlingUnitExternalId!, reservada);
        }

        await _repositorio.RegistrarErroHuAsync(
            codigoProdutoAcabadoCaixa, resultado.MensagemSanitizada, resultado.ResponsePayloadSanitizado,
            resultado.HttpStatus, cancellationToken);
        reservada.ErroSanitizado = resultado.MensagemSanitizada;
        reservada.ResponsePayload = resultado.ResponsePayloadSanitizado;
        reservada.HttpStatus = resultado.HttpStatus;
        reservada.TransicionarPara(StatusIntegracaoCaixa.ErroSap); // EnviandoSap → ErroSap (válido)
        return ResultadoEnvioCaixaHu.Falha(resultado.MensagemSanitizada, reservada);
    }

    /// <summary>Reprocessamento controlado: ERRO_SAP → PRONTA_PARA_ENVIO, mesmo correlation_id (nunca recria a caixa).</summary>
    public async Task<ResultadoEnvioCaixaHu> ReprocessarCaixaHandlingUnitAsync(
        long codigoProdutoAcabadoCaixa, CancellationToken cancellationToken = default)
    {
        ProdutoAcabadoCaixa? caixa = await _repositorio.ObterCaixaPorCodigoAsync(codigoProdutoAcabadoCaixa, cancellationToken);
        if (caixa is null)
        {
            return ResultadoEnvioCaixaHu.Bloqueado("Caixa não encontrada.", null);
        }

        if (caixa.StatusIntegracao != StatusIntegracaoCaixa.ErroSap)
        {
            return ResultadoEnvioCaixaHu.Bloqueado($"Reprocessamento só a partir de ERRO_SAP (atual: {caixa.StatusIntegracao}).", caixa);
        }

        caixa.TransicionarPara(StatusIntegracaoCaixa.ProntaParaEnvio);
        if (caixa.CodigoProdutoAcabadoCaixa is long codigo && caixa.RequestPayload is string payload)
        {
            await _repositorio.AtualizarPreviewHuAsync(codigo, payload, StatusIntegracaoCaixa.ProntaParaEnvio, cancellationToken);
        }

        // §5: reprocessamento NÃO é um envio confirmado — nenhum POST foi executado, a HU continua não
        // confirmada e o correlation_id é preservado. Cenário próprio (não reutilizar Confirmado).
        return ResultadoEnvioCaixaHu.ProntaParaReenvio(caixa);
    }

    /// <summary>Preview da HU (puro, sem persistência) — para exibição/diagnóstico na Form e testes.</summary>
    public ResultadoPreviewHandlingUnitCaixa GerarPreviewHandlingUnitCaixa(ProdutoAcabadoOrdem ordem, ProdutoAcabadoCaixa caixa)
        => _huPayloadBuilder.MontarPreview(ordem, caixa);

    private static ProdutoAcabadoCaixaParaEnvio MontarCaixaParaEnvio(ProdutoAcabadoCaixa caixa)
        => new()
        {
            CodigoProdutoAcabadoCaixa = caixa.CodigoProdutoAcabadoCaixa,
            CodigoCaixaLocal = caixa.CodigoCaixaLocal,
            CorrelationId = caixa.CorrelationId,
            NumeroOrdemProducao = caixa.NumeroOrdemProducao,
            ItemOrdemProducao = caixa.ItemOrdemProducao,
            Material = caixa.Material,
            Lote = caixa.Lote,
            Centro = caixa.Centro,
            Deposito = caixa.Deposito,
            MaterialEmbalagem = caixa.MaterialEmbalagem,
            PesoBruto = caixa.PesoBrutoKg,
            PesoLiquido = caixa.PesoLiquidoKg,
            Tara = caixa.TaraKg,
            UnidadePeso = caixa.UnidadePeso,
            Quantidade = caixa.QuantidadeProdutos,
            UnidadeQuantidade = caixa.UnidadeQuantidade,
            RequestPayloadSanitizado = caixa.RequestPayload ?? string.Empty
        };

    public ResultadoPreviewProdutoAcabadoPalete GerarPreviewPalete(ProdutoAcabadoPalete palete)
        => _paletePayloadBuilder.MontarPreview(palete);

    public Task<IReadOnlyList<TaraCadastro>> ListarTarasAtivasPorSetorAsync(
        long codigoSetor,
        CancellationToken cancellationToken = default)
        => _taraController.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    private static ProdutoAcabadoOrdem MapearOrdem(OrdemProducaoSap ordemSap)
    {
        ItemOrdemProducaoSap? item = ordemSap.Itens.FirstOrDefault();
        decimal planejada = item?.QuantidadePrevista ?? ordemSap.QuantidadePrevista;
        decimal entregue = item?.QuantidadeEntregue ?? 0m;

        return new ProdutoAcabadoOrdem
        {
            NumeroOrdem = ordemSap.NumeroOrdem.Trim(),
            MaterialProduzido = PrimeiroTexto(item?.Material, ordemSap.MaterialProduzido),
            // Tarefa 21.6.4 (Ajuste 2): o SAP não retorna descrição do material aqui — NÃO usar o código
            // como descrição (senão o card Produto Acabado duplica). Fica vazio até haver texto real.
            DescricaoMaterial = string.Empty,
            Centro = PrimeiroTexto(item?.Centro, ordemSap.Centro),
            DepositoDestino = PrimeiroTexto(item?.Deposito, ordemSap.Deposito),
            QuantidadePlanejada = planejada,
            QuantidadeEntregue = entregue,
            QuantidadePendente = Math.Max(planejada - entregue, 0m),
            Unidade = PrimeiroTexto(item?.Unidade, ordemSap.Unidade, "KG").ToUpperInvariant(),
            Lote = PrimeiroTexto(item?.Lote, ordemSap.Lote),
            ItemOrdem = item?.ItemOrdem?.Trim() ?? string.Empty,
            Operacao = ordemSap.Operacoes.FirstOrDefault()?.Operacao ?? string.Empty,
            StatusOrdem = ordemSap.Liberada ? "LIBERADA" : "NAO_LIBERADA",
            Liberada = ordemSap.Liberada,
            EncerradaOuDeletada = ordemSap.Confirmada || ordemSap.Excluida
        };
    }

    private static string PrimeiroTexto(params string?[] valores)
        => valores.FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))?.Trim() ?? string.Empty;
}

public sealed class ResultadoConsultaProdutoAcabado
{
    private ResultadoConsultaProdutoAcabado(bool sucesso, string mensagem, ProdutoAcabadoOrdem? ordem, ProdutoAcabadoNormaEmbalagem? norma)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Ordem = ordem;
        NormaEmbalagem = norma;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public ProdutoAcabadoOrdem? Ordem { get; }
    public ProdutoAcabadoNormaEmbalagem? NormaEmbalagem { get; }

    public static ResultadoConsultaProdutoAcabado Ok(ProdutoAcabadoOrdem ordem, ProdutoAcabadoNormaEmbalagem norma, string mensagem)
        => new(true, string.IsNullOrWhiteSpace(mensagem) ? "OP consultada para produto acabado." : mensagem, ordem, norma);

    public static ResultadoConsultaProdutoAcabado Falha(string mensagem)
        => new(false, mensagem, null, null);
}

public enum CenarioFinalizacaoCaixa { Ok, Bloqueada, PreviewPendente }

/// <summary>Resultado da finalização local da caixa (uma caixa por vez), com preview HU e diagnóstico p/ a Form.</summary>
public sealed record ResultadoFinalizacaoCaixa(
    CenarioFinalizacaoCaixa Cenario,
    string Mensagem,
    ProdutoAcabadoCaixa? Caixa,
    ResultadoPreviewHandlingUnitCaixa? Preview,
    bool PodeEnviar)
{
    public bool Sucesso => Cenario == CenarioFinalizacaoCaixa.Ok;

    public static ResultadoFinalizacaoCaixa Ok(ProdutoAcabadoCaixa caixa, ResultadoPreviewHandlingUnitCaixa preview, bool podeEnviar)
        => new(CenarioFinalizacaoCaixa.Ok,
            "Caixa finalizada localmente. Preview da Handling Unit gerado (envio manual pendente de autorização SAP).",
            caixa, preview, podeEnviar);

    public static ResultadoFinalizacaoCaixa Bloqueada(string mensagem, ProdutoAcabadoCaixa? caixaAtiva)
        => new(CenarioFinalizacaoCaixa.Bloqueada, mensagem, caixaAtiva, null, false);

    public static ResultadoFinalizacaoCaixa PreviewPendente(ProdutoAcabadoCaixa caixa, ResultadoPreviewHandlingUnitCaixa preview)
        => new(CenarioFinalizacaoCaixa.PreviewPendente,
            $"Caixa finalizada localmente, mas o preview da HU não pôde ser gerado: {preview.Mensagem}",
            caixa, preview, false);
}

/// <summary>Diagnóstico de prontidão do envio manual da caixa.</summary>
public sealed record ResultadoDiagnosticoEnvioCaixa(bool PodeEnviar, string Mensagem, StatusIntegracaoCaixa? Status);

public enum CenarioEnvioCaixaHu { Confirmado, Falha, Bloqueado, NaoAutorizado, ProntaParaReenvio }

/// <summary>Resultado do envio manual da caixa à HU SAP (nunca cria palete/Material Document).</summary>
public sealed record ResultadoEnvioCaixaHu(
    CenarioEnvioCaixaHu Cenario,
    string Mensagem,
    string? HandlingUnitExternalId,
    ProdutoAcabadoCaixa? Caixa)
{
    public bool Sucesso => Cenario == CenarioEnvioCaixaHu.Confirmado;

    /// <summary>True quando a caixa apenas foi preparada para reenvio (nenhum POST executado, HU não confirmada).</summary>
    public bool Reprocessavel => Cenario == CenarioEnvioCaixaHu.ProntaParaReenvio;

    public static ResultadoEnvioCaixaHu Confirmado(string handlingUnitExternalId, ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.Confirmado,
            $"Handling Unit {handlingUnitExternalId} confirmada no SAP.",
            handlingUnitExternalId, caixa);

    public static ResultadoEnvioCaixaHu Falha(string mensagem, ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.Falha, mensagem, null, caixa);

    public static ResultadoEnvioCaixaHu Bloqueado(string mensagem, ProdutoAcabadoCaixa? caixa)
        => new(CenarioEnvioCaixaHu.Bloqueado, mensagem, null, caixa);

    public static ResultadoEnvioCaixaHu NaoAutorizado(string mensagem, ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.NaoAutorizado, mensagem, null, caixa);

    /// <summary>§5: caixa em ERRO_SAP foi preparada (PRONTA_PARA_ENVIO) para novo envio — sem POST, HU não confirmada.</summary>
    public static ResultadoEnvioCaixaHu ProntaParaReenvio(ProdutoAcabadoCaixa caixa)
        => new(CenarioEnvioCaixaHu.ProntaParaReenvio,
            "Caixa preparada para reenvio (PRONTA_PARA_ENVIO). Nenhum POST executado; HU ainda não confirmada.",
            null, caixa);
}
