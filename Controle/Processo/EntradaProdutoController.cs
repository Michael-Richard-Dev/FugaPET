using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Controle.Processo;

/// <summary>
/// Coordenador da tela de Entrada de Produto (ProcessoEntradaProdutoForm).
///
/// Refactor H9: este controller e o dono dos servicos usados pela tela e coordena o fluxo de
/// negocio, para que o Form apenas capture selecao e apresente dados/mensagens.
///
/// C12: a FINALIZACAO grava SOMENTE local (sem PATCH automatico). A escrita de peso no SAP fica
/// num fluxo SEPARADO e controlado (<see cref="EnviarPesoEntradaParaSapHomologacaoAsync"/>), que so
/// roda em HOMOLOGACAO, com escrita habilitada e permissao ENVIAR_SAP.
/// </summary>
public sealed class EntradaProdutoController
{
    public IntegracaoEntradaSapServico Sap { get; }
    public EntradaProdutoServico EntradaProduto { get; }
    public BalancaLeituraServico BalancaLeitura { get; }
    public ImpressoraEtiquetaServico ImpressoraEtiqueta { get; }
    public ImpressaoEntradaServico Impressao { get; }
    public AutorizacaoCentroDepositoEntrada AutorizacaoCentroDeposito { get; }
    public TaraController Tara { get; }

    private readonly Func<bool> _ehAmbienteHomologacao;
    private readonly Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>> _carregarItensParaEnvio;

    // Ctor padrao: a fabrica decide mock/real (a tela nao decide nem instancia servico SAP concreto).
    public EntradaProdutoController()
        : this(
            new IntegracaoEntradaSapServico(FabricaPedidoCompraSapServico.Criar()),
            new EntradaProdutoServico(),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            AutorizacaoCentroDepositoEntrada.CarregarDoAmbiente(),
            FabricaControladoresCadastro.CriarTaraController())
    {
    }

    internal EntradaProdutoController(
        IntegracaoEntradaSapServico sap,
        EntradaProdutoServico entradaProdutoServico,
        BalancaLeituraServico balancaLeituraServico,
        ImpressoraEtiquetaServico impressoraEtiquetaServico,
        AutorizacaoCentroDepositoEntrada autorizacaoCentroDeposito,
        TaraController taraController,
        Func<bool>? ehAmbienteHomologacao = null,
        Func<long, CancellationToken, Task<IReadOnlyList<EntradaProdutoItemEnvioSap>>>? carregarItensParaEnvio = null)
    {
        Sap = sap ?? throw new ArgumentNullException(nameof(sap));
        EntradaProduto = entradaProdutoServico ?? throw new ArgumentNullException(nameof(entradaProdutoServico));
        BalancaLeitura = balancaLeituraServico ?? throw new ArgumentNullException(nameof(balancaLeituraServico));
        ImpressoraEtiqueta = impressoraEtiquetaServico ?? throw new ArgumentNullException(nameof(impressoraEtiquetaServico));
        Impressao = new ImpressaoEntradaServico(ImpressoraEtiqueta);
        AutorizacaoCentroDeposito = autorizacaoCentroDeposito ?? throw new ArgumentNullException(nameof(autorizacaoCentroDeposito));
        Tara = taraController ?? throw new ArgumentNullException(nameof(taraController));
        _ehAmbienteHomologacao = ehAmbienteHomologacao ?? AmbienteIntegracaoSap.EhHomologacao;
        _carregarItensParaEnvio = carregarItensParaEnvio
            ?? ((codigoLancamento, cancellationToken) =>
                EntradaProduto.ListarItensParaEnvioSapAsync(codigoLancamento, cancellationToken));
    }

    /// <summary>
    /// C12: finaliza a leitura gravando SOMENTE local (rastreabilidade completa). NAO executa PATCH
    /// no SAP — o envio de peso ao SAP e um fluxo separado e controlado. Nao lanca: devolve um
    /// <see cref="ResultadoFinalizacaoEntrada"/> que a tela apenas apresenta.
    /// </summary>
    public async Task<ResultadoFinalizacaoEntrada> FinalizarLeituraAsync(
        EntradaProdutoLancamento lancamento,
        CancellationToken cancellationToken = default)
    {
        if (lancamento.Itens.Count == 0)
        {
            return new ResultadoFinalizacaoEntrada { Cenario = CenarioFinalizacaoEntrada.NenhumaLeitura };
        }

        try
        {
            ResultadoOperacao resultadoLancamento =
                await EntradaProduto.RegistrarLancamentoAsync(lancamento, cancellationToken);
            if (!resultadoLancamento.Sucesso)
            {
                return new ResultadoFinalizacaoEntrada
                {
                    Cenario = CenarioFinalizacaoEntrada.LancamentoNaoGravado,
                    MensagemFalhaLancamento = resultadoLancamento.Mensagem
                };
            }

            return new ResultadoFinalizacaoEntrada
            {
                Cenario = CenarioFinalizacaoEntrada.GravadoLocal,
                CodigoLancamento = resultadoLancamento.IdGerado ?? 0,
                Gravados = lancamento.Itens.Count
            };
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico($"ERRO ao gravar pesagens.{Environment.NewLine}{ex}");
            return new ResultadoFinalizacaoEntrada { Cenario = CenarioFinalizacaoEntrada.ErroAoGravar };
        }
    }

    /// <summary>
    /// C12: envio CONTROLADO do peso ao SAP de homologacao (PATCH), SEPARADO da finalizacao local.
    /// So executa o PATCH quando TODAS as travas estao habilitadas: ambiente HOMOLOGACAO, permissao
    /// ENVIAR_SAP e escrita habilitada (FUGAPET_SAP_WRITE_ENABLED). A integracao ativa, a
    /// configuracao SAP e a chave de escrita ainda sao reaplicadas pelo servico governado no PATCH.
    /// Registra o resultado por item de forma sanitizada (sem segredo/payload).
    /// </summary>
    public async Task<ResultadoEnvioSapEntrada> EnviarPesoEntradaParaSapHomologacaoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        if (!_ehAmbienteHomologacao())
        {
            Sap.RegistrarDiagnostico($"Envio SAP bloqueado (lancamento {codigoLancamento}): ambiente nao e homologacao.");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.AmbienteNaoHomologacao };
        }

        if (!AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.EnviarSap))
        {
            Sap.RegistrarDiagnostico($"Envio SAP bloqueado (lancamento {codigoLancamento}): sem permissao ENVIAR_SAP.");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.SemPermissao };
        }

        if (!Sap.EscritaSapHabilitada)
        {
            Sap.RegistrarDiagnostico($"Envio SAP bloqueado (lancamento {codigoLancamento}): escrita SAP desabilitada.");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.EscritaDesabilitada };
        }

        IReadOnlyList<EntradaProdutoItemEnvioSap> itens;
        try
        {
            itens = await _carregarItensParaEnvio(codigoLancamento, cancellationToken);
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico(
                $"Envio SAP: falha ao carregar itens do lancamento {codigoLancamento}.{Environment.NewLine}{ex}");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.Falha };
        }

        if (itens.Count == 0)
        {
            Sap.RegistrarDiagnostico($"Envio SAP bloqueado: lancamento {codigoLancamento} sem itens gravados.");
            return new ResultadoEnvioSapEntrada { Cenario = CenarioEnvioSapEntrada.LancamentoSemItens };
        }

        List<ResultadoItemEnvioSap> resultados = [];
        int enviados = 0;
        foreach (EntradaProdutoItemEnvioSap item in itens)
        {
            ResultadoOperacao resultado = await Sap.AtualizarPesoItemSapAsync(
                item.NumeroPedido,
                item.NumeroItem,
                item.PesoLiquidoKg,
                item.PesoBrutoKg,
                cancellationToken);

            resultados.Add(new ResultadoItemEnvioSap(item.NumeroItem, resultado.Sucesso, resultado.Mensagem));
            if (resultado.Sucesso)
            {
                enviados++;
            }

            Sap.RegistrarDiagnostico(
                $"Envio SAP lancamento {codigoLancamento} item {item.NumeroItem}: "
                + $"{(resultado.Sucesso ? "OK" : "FALHA")} - {resultado.Mensagem}");
        }

        CenarioEnvioSapEntrada cenario = enviados == itens.Count
            ? CenarioEnvioSapEntrada.Enviado
            : enviados == 0
                ? CenarioEnvioSapEntrada.Falha
                : CenarioEnvioSapEntrada.Parcial;

        return new ResultadoEnvioSapEntrada
        {
            Cenario = cenario,
            Enviados = enviados,
            Total = itens.Count,
            Itens = resultados
        };
    }

    /// <summary>
    /// Consulta um pedido especifico no SAP (sincroniza o cache local), le os dados de cabecalho e
    /// os itens, e aplica o filtro de escopo (centro/deposito autorizado). Devolve apenas dados; a
    /// concorrencia de UI (cancelar consulta anterior, gate, duplo clique) fica na tela.
    /// </summary>
    public async Task<ResultadoConsultaPedido> ConsultarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao sincronizacao = await Sap.SincronizarPedidoAsync(numeroPedido, cancellationToken);
        if (!sincronizacao.Sucesso)
        {
            return new ResultadoConsultaPedido { Sucesso = false, Mensagem = sincronizacao.Mensagem };
        }

        PedidoCompraSapAgregado? pedido =
            await Sap.ObterPedidoAgregadoAsync(
                numeroPedido,
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (pedido is null)
        {
            return new ResultadoConsultaPedido
            {
                Sucesso = false,
                Mensagem = "Pedido sincronizado, mas nao encontrado no cache local."
            };
        }

        // Escopo Jales (H5): exibe apenas itens dentro do centro/deposito autorizado.
        IReadOnlyList<PedidoCompraSapItem> itensAutorizados = pedido.Itens
            .Where(item => AutorizacaoCentroDeposito.ItemAutorizado(item.Centro, item.Deposito))
            .ToList();

        return new ResultadoConsultaPedido
        {
            Sucesso = true,
            Mensagem = sincronizacao.Mensagem,
            NumeroPedido = pedido.NumeroPedido,
            Fornecedor = pedido.Fornecedor,
            DataPedido = pedido.DataPedido,
            TipoPedido = pedido.TipoPedido,
            ItensAutorizados = itensAutorizados,
            ItensOcultados = pedido.Itens.Count - itensAutorizados.Count
        };
    }
}

/// <summary>Cenarios da finalizacao LOCAL (C12: sem PATCH automatico).</summary>
public enum CenarioFinalizacaoEntrada
{
    NenhumaLeitura,
    LancamentoNaoGravado,
    GravadoLocal,
    ErroAoGravar
}

/// <summary>Resultado da finalizacao local (somente dados; a tela formata a apresentacao).</summary>
public sealed class ResultadoFinalizacaoEntrada
{
    public CenarioFinalizacaoEntrada Cenario { get; init; }
    public long? CodigoLancamento { get; init; }
    public int Gravados { get; init; }
    public string? MensagemFalhaLancamento { get; init; }
}

/// <summary>Cenarios do envio CONTROLADO de peso ao SAP (fluxo separado da finalizacao).</summary>
public enum CenarioEnvioSapEntrada
{
    AmbienteNaoHomologacao,
    SemPermissao,
    EscritaDesabilitada,
    LancamentoSemItens,
    Enviado,
    Parcial,
    Falha
}

/// <summary>Resultado por item do envio de peso ao SAP (mensagem ja sanitizada pelo servico).</summary>
public sealed record ResultadoItemEnvioSap(string NumeroItem, bool Sucesso, string Mensagem);

/// <summary>Resultado do envio controlado de peso ao SAP de homologacao.</summary>
public sealed class ResultadoEnvioSapEntrada
{
    public CenarioEnvioSapEntrada Cenario { get; init; }
    public int Enviados { get; init; }
    public int Total { get; init; }
    public IReadOnlyList<ResultadoItemEnvioSap> Itens { get; init; } = [];
}

/// <summary>Resultado da consulta de um pedido (dados ja filtrados pelo escopo; a tela so apresenta).</summary>
public sealed class ResultadoConsultaPedido
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string NumeroPedido { get; init; } = string.Empty;
    public string Fornecedor { get; init; } = string.Empty;
    public DateOnly? DataPedido { get; init; }
    public string TipoPedido { get; init; } = string.Empty;
    public IReadOnlyList<PedidoCompraSapItem> ItensAutorizados { get; init; } = [];
    public int ItensOcultados { get; init; }
}
