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
    private readonly ProdutoAcabadoMaterialDocument101PayloadBuilder _materialDocument101Builder;
    private readonly ProdutoAcabadoPaletePayloadBuilder _paletePayloadBuilder;

    public ProdutoAcabadoController()
        : this(FabricaProductionOrderSapServico.Criar())
    {
    }

    public ProdutoAcabadoController(
        IProductionOrderSapServico productionOrderSapServico,
        TaraController? taraController = null,
        ProdutoAcabadoMaterialDocument101PayloadBuilder? materialDocument101Builder = null,
        ProdutoAcabadoPaletePayloadBuilder? paletePayloadBuilder = null)
    {
        _productionOrderSapServico = productionOrderSapServico ?? throw new ArgumentNullException(nameof(productionOrderSapServico));
        _taraController = taraController ?? FabricaControladoresCadastro.CriarTaraController();
        _materialDocument101Builder = materialDocument101Builder ?? new ProdutoAcabadoMaterialDocument101PayloadBuilder();
        _paletePayloadBuilder = paletePayloadBuilder ?? new ProdutoAcabadoPaletePayloadBuilder();
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
        string origemPesagem)
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

        return new ProdutoAcabadoCaixa
        {
            NumeroCaixa = numeroCaixa,
            CodigoCaixaLocal = $"CX-{ordem.NumeroOrdem}-{numeroCaixa:0000}",
            PesoBrutoKg = pesoBrutoKg,
            TaraKg = taraKg,
            PesoLiquidoKg = pesoLiquidoKg,
            QuantidadeProdutos = norma.QuantidadeProdutosPorCaixa,
            OrigemPesagem = origemPesagem
        };
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

    public ResultadoPreviewProdutoAcabado101 GerarPreviewMaterialDocument101(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoCaixa caixa,
        DateTime dataLancamentoUtc)
        => _materialDocument101Builder.MontarPreview101(ordem, caixa, dataLancamentoUtc);

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
