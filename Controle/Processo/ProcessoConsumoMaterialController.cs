using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Controle.Processo;

public sealed class ProcessoConsumoMaterialController
{
    private readonly ConsumoMaterialServico _consumoMaterialServico;
    private readonly TaraController _taraController;

    public ProcessoConsumoMaterialController()
        : this(new ConsumoMaterialServico(), FabricaControladoresCadastro.CriarTaraController())
    {
    }

    public ProcessoConsumoMaterialController(ConsumoMaterialServico consumoMaterialServico)
        : this(consumoMaterialServico, FabricaControladoresCadastro.CriarTaraController())
    {
    }

    internal ProcessoConsumoMaterialController(
        ConsumoMaterialServico consumoMaterialServico,
        TaraController taraController)
    {
        _consumoMaterialServico = consumoMaterialServico;
        _taraController = taraController;
    }

    /// <summary>Taras ATIVAS do setor (mesma fonte da Entrada). Para a selecao de tara por componente.</summary>
    public Task<IReadOnlyList<TaraCadastro>> ListarTarasAtivasPorSetorAsync(
        long codigoSetor,
        CancellationToken cancellationToken = default)
        => _taraController.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    /// <summary>Peso (KG) da tara selecionada; 0 quando nao houver tara. Reaproveita o TaraController.</summary>
    public async Task<decimal> ObterPesoTaraAsync(long? idTara, CancellationToken cancellationToken = default)
    {
        if (idTara is not long id || id <= 0)
        {
            return 0m;
        }

        IReadOnlyList<TaraCadastro> taras = await _taraController.ListarAsync(cancellationToken);
        return taras.FirstOrDefault(tara => tara.Id == id)?.PesoKg ?? 0m;
    }

    public Task<ResultadoConsultaOrdemConsumo> ConsultarOrdemProducaoAsync(
        string? numeroOrdem,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.ConsultarOrdemAsync(numeroOrdem, cancellationToken);

    /// <summary>
    /// Tarefa Consumo 22.10.1: descrições reais dos componentes (A_ProductDescription), mapa Product → mestre.
    /// A tela usa para o join lógico; delega ao serviço (que resolve o Product Master governado/mock).
    /// </summary>
    public Task<IReadOnlyDictionary<string, ProdutoSapMestre>> ObterDescricoesComponentesAsync(
        IEnumerable<string> codigosProduto,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.ObterDescricoesComponentesAsync(codigosProduto, cancellationToken);

    /// <summary>Chave composta do componente (delega ao servico).</summary>
    public static string ChaveComponente(ComponenteConsumoMaterial componente)
        => ConsumoMaterialServico.ChaveComponente(componente);

    /// <summary>Envio CONTROLADO do consumo 261 ao SAP (CSRF/POST com WRITE_ENABLED). Bloqueia reenvio.</summary>
    public Task<ResultadoEnvioConsumoSap261> EnviarConsumoSap261Async(
        long codigoLancamento,
        string usuario,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.EnviarConsumoSap261Async(codigoLancamento, usuario, cancellationToken);

    public Task<ResultadoEnvioConfirmacaoProducao> EnviarConfirmacaoProducaoAsync(
        long codigoLancamento,
        string usuario,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.EnviarConfirmacaoProducaoAsync(codigoLancamento, usuario, cancellationToken);

    /// <summary>PREVIEW do payload de consumo 261 a partir de um lancamento em memoria. NAO envia SAP.</summary>
    public ResultadoPreviewConsumoSap261 GerarPreviewSap261(
        ConsumoMaterialLancamento lancamento,
        DateTime dataLancamentoUtc)
        => _consumoMaterialServico.GerarPreviewSap261(lancamento, dataLancamentoUtc);

    /// <summary>PREVIEW do payload de consumo 261 a partir de um lancamento persistido. NAO envia SAP.</summary>
    public Task<ResultadoPreviewConsumoSap261> GerarPreviewSap261Async(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.GerarPreviewSap261Async(codigoLancamento, cancellationToken);

    /// <summary>
    /// PREVIEW TECNICO (NAO enviado ao SAP) do caminho de Confirmacao de Producao para componente
    /// Backflush. Montagem PURA; sem POST/CSRF/PATCH.
    /// </summary>
    public ResultadoPreviewConfirmacaoProducao GerarPreviewConfirmacaoProducao(
        OrdemProducaoConsumo ordem,
        ComponenteConsumoMaterial componente,
        decimal quantidadeConsumidaLocal)
        => _consumoMaterialServico.GerarPreviewConfirmacaoProducao(ordem, componente, quantidadeConsumidaLocal);

    /// <summary>Tarefa 16: classifica a ROTA de envio do consumo (261 direto / Backflush / Misto / Bloqueado).</summary>
    public static RotaEnvioConsumo ClassificarRotaEnvio(IReadOnlyCollection<ComponenteConsumoMaterial> componentesConsumidos)
        => ConsumoMaterialServico.ClassificarRotaEnvio(componentesConsumidos);

    /// <summary>Tarefa 16: PREVIEW de Confirmacao de Producao a partir do lancamento salvo. NAO envia SAP.</summary>
    public Task<ResultadoPreviewConfirmacaoProducao> GerarPreviewConfirmacaoProducaoDoLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.GerarPreviewConfirmacaoProducaoDoLancamentoAsync(codigoLancamento, cancellationToken);

    /// <summary>Tarefa 17.6: PREVIEW REAL da Confirmacao (payload do POST, mesmo builder do envio). NAO envia SAP.</summary>
    public Task<ResultadoPreviewConfirmacaoProducaoSap> GerarPreviewConfirmacaoProducaoRealAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.GerarPreviewConfirmacaoProducaoRealAsync(codigoLancamento, cancellationToken);

    /// <summary>Salva LOCALMENTE o consumo (cabecalho + itens + pesagens), status PENDENTE_SAP. Sem SAP.</summary>
    public Task<ResultadoPersistenciaConsumoMaterial> SalvarConsumoLocalAsync(
        OrdemProducaoConsumo ordem,
        IReadOnlyCollection<ComponenteConsumoMaterial> componentes,
        IReadOnlyDictionary<string, List<PesagemConsumoMaterial>> pesagensPorComponente,
        string usuario,
        CancellationToken cancellationToken = default)
        => _consumoMaterialServico.SalvarConsumoLocalAsync(
            ordem, componentes, pesagensPorComponente, usuario, cancellationToken);

    /// <summary>Registra (em memoria) uma pesagem LOCAL de consumo. Sem banco/SAP/impressao.</summary>
    public ResultadoPesagemConsumo RegistrarPesagemConsumo(
        ComponenteConsumoMaterial componente,
        string numeroOrdem,
        decimal pesoBrutoKg,
        decimal pesoTaraKg,
        string origem,
        decimal totalJaPesadoLocalKg,
        int sequencia)
        => _consumoMaterialServico.RegistrarPesagemLocal(
            componente, numeroOrdem, pesoBrutoKg, pesoTaraKg, origem, totalJaPesadoLocalKg, sequencia);
}
