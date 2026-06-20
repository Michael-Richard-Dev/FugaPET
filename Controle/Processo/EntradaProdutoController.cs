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
/// negocio, para que o Form apenas capture selecao e apresente dados/mensagens. O servico de SAP
/// continua vindo da fabrica (FabricaPedidoCompraSapServico) — quem decide mock/real e a fabrica,
/// nunca o Form nem este controller.
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
        TaraController taraController)
    {
        Sap = sap ?? throw new ArgumentNullException(nameof(sap));
        EntradaProduto = entradaProdutoServico ?? throw new ArgumentNullException(nameof(entradaProdutoServico));
        BalancaLeitura = balancaLeituraServico ?? throw new ArgumentNullException(nameof(balancaLeituraServico));
        ImpressoraEtiqueta = impressoraEtiquetaServico ?? throw new ArgumentNullException(nameof(impressoraEtiquetaServico));
        Impressao = new ImpressaoEntradaServico(ImpressoraEtiqueta);
        AutorizacaoCentroDeposito = autorizacaoCentroDeposito ?? throw new ArgumentNullException(nameof(autorizacaoCentroDeposito));
        Tara = taraController ?? throw new ArgumentNullException(nameof(taraController));
    }

    /// <summary>
    /// Finaliza a leitura: grava o lancamento LOCALMENTE (rastreabilidade completa) e, somente
    /// depois, tenta a ESCRITA no SAP (PATCH de peso) — respeitando autorizacao e a chave de escrita.
    /// Nao lanca: devolve um <see cref="ResultadoFinalizacaoEntrada"/> que a tela apenas apresenta.
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
            // (1) Persistencia local ANTES de qualquer escrita no SAP.
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

            long codigoLancamento = resultadoLancamento.IdGerado ?? 0;
            int gravados = lancamento.Itens.Count;

            // (2) Monta as atualizacoes de peso a partir do lancamento ja gravado.
            List<AtualizacaoPesoSap> atualizacoes = MontarAtualizacoesSap(lancamento);
            if (atualizacoes.Count == 0)
            {
                return new ResultadoFinalizacaoEntrada
                {
                    Cenario = CenarioFinalizacaoEntrada.GravadoSemSap,
                    CodigoLancamento = codigoLancamento,
                    Gravados = gravados
                };
            }

            // (3) Autorizacao da escrita no SAP por modulo+rotina+acao (nunca por nome de perfil).
            if (!AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.EnviarSap))
            {
                return new ResultadoFinalizacaoEntrada
                {
                    Cenario = CenarioFinalizacaoEntrada.GravadoSapNaoAutorizado,
                    CodigoLancamento = codigoLancamento,
                    Gravados = gravados
                };
            }

            // (4) PATCH de peso no SAP (a chave de escrita ainda e respeitada pelo servico).
            int enviados = 0;
            string? ultimaFalha = null;
            foreach (AtualizacaoPesoSap atualizacao in atualizacoes)
            {
                ResultadoOperacao resultado = await Sap.AtualizarPesoItemSapAsync(
                    atualizacao.NumeroPedido,
                    atualizacao.NumeroItem,
                    atualizacao.PesoLiquido,
                    atualizacao.PesoBruto,
                    cancellationToken);

                if (resultado.Sucesso)
                {
                    enviados++;
                }
                else
                {
                    ultimaFalha = resultado.Mensagem;
                }
            }

            return new ResultadoFinalizacaoEntrada
            {
                Cenario = enviados == atualizacoes.Count
                    ? CenarioFinalizacaoEntrada.GravadoSapEnviado
                    : CenarioFinalizacaoEntrada.GravadoSapParcial,
                CodigoLancamento = codigoLancamento,
                Gravados = gravados,
                SapEnviados = enviados,
                SapTotal = atualizacoes.Count,
                UltimaFalhaSap = ultimaFalha
            };
        }
        catch (Exception ex)
        {
            Sap.RegistrarDiagnostico($"ERRO ao gravar pesagens.{Environment.NewLine}{ex}");
            return new ResultadoFinalizacaoEntrada { Cenario = CenarioFinalizacaoEntrada.ErroAoGravar };
        }
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

    private static List<AtualizacaoPesoSap> MontarAtualizacoesSap(EntradaProdutoLancamento lancamento)
    {
        List<AtualizacaoPesoSap> atualizacoes = [];
        string numeroPedido = lancamento.NumeroPedido?.Trim() ?? string.Empty;
        foreach (EntradaProdutoItem item in lancamento.Itens)
        {
            decimal pesoLiquido = EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(item.Pesagens);
            decimal pesoBruto = EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(item.Pesagens);
            if (pesoLiquido > 0m
                && !string.IsNullOrWhiteSpace(numeroPedido)
                && !string.IsNullOrWhiteSpace(item.NumeroItem))
            {
                atualizacoes.Add(new AtualizacaoPesoSap(numeroPedido, item.NumeroItem, pesoLiquido, pesoBruto));
            }
        }

        return atualizacoes;
    }

    private sealed record AtualizacaoPesoSap(
        string NumeroPedido,
        string NumeroItem,
        decimal PesoLiquido,
        decimal PesoBruto);
}

/// <summary>Cenarios possiveis da finalizacao de leitura, para a tela apresentar a mensagem certa.</summary>
public enum CenarioFinalizacaoEntrada
{
    NenhumaLeitura,
    LancamentoNaoGravado,
    GravadoSemSap,
    GravadoSapNaoAutorizado,
    GravadoSapEnviado,
    GravadoSapParcial,
    ErroAoGravar
}

/// <summary>Resultado da finalizacao de leitura (somente dados; a tela formata a apresentacao).</summary>
public sealed class ResultadoFinalizacaoEntrada
{
    public CenarioFinalizacaoEntrada Cenario { get; init; }
    public long? CodigoLancamento { get; init; }
    public int Gravados { get; init; }
    public int SapEnviados { get; init; }
    public int SapTotal { get; init; }
    public string? MensagemFalhaLancamento { get; init; }
    public string? UltimaFalhaSap { get; init; }
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
