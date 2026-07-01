using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Controle.Processo;

public sealed class SemiAcabadoController
{
    private readonly IProductionOrderSapServico _productionOrderSapServico;
    private readonly SemiAcabadoMaterialDocument101PayloadBuilder _materialDocument101Builder;
    private readonly TaraController _taraController;

    public SemiAcabadoController()
        : this(FabricaProductionOrderSapServico.Criar())
    {
    }

    public SemiAcabadoController(
        IProductionOrderSapServico productionOrderSapServico,
        SemiAcabadoMaterialDocument101PayloadBuilder? materialDocument101Builder = null,
        TaraController? taraController = null)
    {
        _productionOrderSapServico = productionOrderSapServico ?? throw new ArgumentNullException(nameof(productionOrderSapServico));
        _materialDocument101Builder = materialDocument101Builder ?? new SemiAcabadoMaterialDocument101PayloadBuilder();
        _taraController = taraController ?? FabricaControladoresCadastro.CriarTaraController();
    }

    public bool SapSimulado => _productionOrderSapServico.EhSimulado;

    public async Task<ResultadoConsultaSemiAcabado> ConsultarOrdemProducaoAsync(
        string numeroOp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroOp))
        {
            return ResultadoConsultaSemiAcabado.Falha("Informe a OP para consulta.");
        }

        ResultadoConsultaOrdemProducaoSap resultadoSap =
            await _productionOrderSapServico.ConsultarOrdemAsync(numeroOp.Trim(), cancellationToken);

        if (resultadoSap.Cenario != CenarioConsultaOrdemProducaoSap.Encontrada || resultadoSap.Ordem is null)
        {
            string mensagem = string.IsNullOrWhiteSpace(resultadoSap.MensagemSanitizada)
                ? "Nao foi possivel consultar a OP no SAP."
                : resultadoSap.MensagemSanitizada;
            return ResultadoConsultaSemiAcabado.Falha(mensagem);
        }

        OrdemProducaoSap ordemSap = resultadoSap.Ordem;
        if (!ordemSap.Liberada)
        {
            return ResultadoConsultaSemiAcabado.Falha("OP encontrada, mas ainda nao esta liberada para entrada de semi-acabado.");
        }

        if (ordemSap.Confirmada || ordemSap.Excluida)
        {
            return ResultadoConsultaSemiAcabado.Falha("OP encontrada, mas esta encerrada, confirmada ou marcada para exclusao.");
        }

        IReadOnlyList<SemiAcabadoOrdem> itens = MapearItensProduzidos(ordemSap);
        if (itens.Count == 0)
        {
            return ResultadoConsultaSemiAcabado.Falha("OP encontrada, mas sem item produzido para entrada de semi-acabado.");
        }

        return ResultadoConsultaSemiAcabado.Ok(ordemSap.NumeroOrdem, itens, resultadoSap.MensagemSanitizada);
    }

    public LancamentoSemiAcabado MontarLancamentoLocal(
        SemiAcabadoOrdem ordem,
        IReadOnlyList<PesagemSemiAcabado> pesagens,
        string usuario)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(pesagens);

        return new LancamentoSemiAcabado
        {
            Ordem = ordem,
            Pesagens = pesagens.ToArray(),
            Usuario = usuario?.Trim() ?? string.Empty,
            CriadoEm = DateTime.Now
        };
    }

    public ResultadoPreviewSemiAcabado101 GerarPreviewMaterialDocument101(
        LancamentoSemiAcabado lancamento,
        DateTime dataLancamentoUtc)
        => _materialDocument101Builder.MontarPreview101(lancamento, dataLancamentoUtc);

    public Task<IReadOnlyList<TaraCadastro>> ListarTarasAtivasPorSetorAsync(
        long codigoSetor,
        CancellationToken cancellationToken = default)
        => _taraController.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    private static IReadOnlyList<SemiAcabadoOrdem> MapearItensProduzidos(OrdemProducaoSap ordemSap)
    {
        if (ordemSap.Itens.Count == 0)
        {
            return [MapearItem(ordemSap, null)];
        }

        return ordemSap.Itens
            .Select(item => MapearItem(ordemSap, item))
            .Where(item => !string.IsNullOrWhiteSpace(item.MaterialProduzido))
            .ToArray();
    }

    private static SemiAcabadoOrdem MapearItem(OrdemProducaoSap ordemSap, ItemOrdemProducaoSap? item)
    {
        decimal planejada = item?.QuantidadePrevista ?? ordemSap.QuantidadePrevista;
        decimal entregue = item?.QuantidadeEntregue ?? 0m;
        decimal pendente = Math.Max(planejada - entregue, 0m);
        bool encerrada = ordemSap.Confirmada || ordemSap.Excluida;

        return new SemiAcabadoOrdem
        {
            NumeroOrdem = ordemSap.NumeroOrdem.Trim(),
            MaterialProduzido = PrimeiroTexto(item?.Material, ordemSap.MaterialProduzido),
            DescricaoMaterial = PrimeiroTexto(item?.Material, ordemSap.MaterialProduzido),
            Centro = PrimeiroTexto(item?.Centro, ordemSap.Centro),
            DepositoDestino = PrimeiroTexto(item?.Deposito, ordemSap.Deposito),
            QuantidadePlanejada = planejada,
            QuantidadeEntregue = entregue,
            QuantidadePendente = pendente,
            Unidade = PrimeiroTexto(item?.Unidade, ordemSap.Unidade, "KG").ToUpperInvariant(),
            Lote = PrimeiroTexto(item?.Lote, ordemSap.Lote),
            ItemOrdem = item?.ItemOrdem?.Trim() ?? string.Empty,
            Operacao = ordemSap.Operacoes.FirstOrDefault()?.Operacao ?? string.Empty,
            StatusOrdem = MontarStatus(ordemSap, encerrada),
            Liberada = ordemSap.Liberada,
            EncerradaOuDeletada = encerrada
        };
    }

    private static string PrimeiroTexto(params string?[] valores)
        => valores.FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor))?.Trim() ?? string.Empty;

    private static string MontarStatus(OrdemProducaoSap ordemSap, bool encerrada)
    {
        if (ordemSap.Excluida)
        {
            return "EXCLUIDA";
        }

        if (ordemSap.Confirmada)
        {
            return "CONFIRMADA";
        }

        return ordemSap.Liberada && !encerrada ? "LIBERADA" : "NAO_LIBERADA";
    }
}

public sealed class ResultadoConsultaSemiAcabado
{
    private ResultadoConsultaSemiAcabado(bool sucesso, string mensagem, string numeroOp, IReadOnlyList<SemiAcabadoOrdem> itens)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        NumeroOp = numeroOp;
        Itens = itens;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public string NumeroOp { get; }
    public IReadOnlyList<SemiAcabadoOrdem> Itens { get; }

    public static ResultadoConsultaSemiAcabado Ok(string numeroOp, IReadOnlyList<SemiAcabadoOrdem> itens, string mensagem)
        => new(true, string.IsNullOrWhiteSpace(mensagem) ? "OP consultada com sucesso." : mensagem, numeroOp, itens);

    public static ResultadoConsultaSemiAcabado Falha(string mensagem)
        => new(false, mensagem, string.Empty, []);
}
