using System.Globalization;
using System.Text.Json;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Orquestra a impressão/reimpressão da etiqueta da CAIXA de Produto Acabado. Reaproveita o
/// <see cref="ImpressoraEtiquetaServico"/> (resolução da Zebra do terminal, permissão Imprimir/Reimprimir,
/// gate anti-concorrência, diagnóstico IMPRESSAO_ZEBRA_DIAGNOSTICO). É um efeito PURAMENTE local de impressão:
/// NÃO chama SAP (claim/autorização/gateway/POST /HandlingUnit), NÃO altera o status HU nem numero_tentativa.
/// A ELEGIBILIDADE é validada no SERVICE (não só na Form): só imprime/reimprime uma caixa CONFIRMADA_SAP com
/// HU SAP e identidade persistida. Registra auditoria estruturada (dados_contexto) diferenciando IMPRESSAO×REIMPRESSAO.
/// </summary>
public sealed class ImpressaoProdutoAcabadoServico
{
    public const string TipoOperacaoImpressao = "IMPRESSAO";
    public const string TipoOperacaoReimpressao = "REIMPRESSAO";

    public const string ResultadoSucesso = "SUCESSO";
    public const string ResultadoFalha = "FALHA";

    private readonly ImpressoraEtiquetaServico _impressoraEtiquetaServico;
    // Seam de auditoria (caixa, tipoOperacao, resultado) → Task. Injetável em teste; padrão = auditoria real.
    private readonly Func<ProdutoAcabadoCaixa, string, string, Task> _auditarEtiqueta;

    public ImpressaoProdutoAcabadoServico()
        : this(new ImpressoraEtiquetaServico())
    {
    }

    public ImpressaoProdutoAcabadoServico(ImpressoraEtiquetaServico impressoraEtiquetaServico)
        : this(impressoraEtiquetaServico, auditarEtiqueta: null)
    {
    }

    internal ImpressaoProdutoAcabadoServico(
        ImpressoraEtiquetaServico impressoraEtiquetaServico,
        Func<ProdutoAcabadoCaixa, string, string, Task>? auditarEtiqueta)
    {
        _impressoraEtiquetaServico = impressoraEtiquetaServico;
        _auditarEtiqueta = auditarEtiqueta ?? RegistrarAuditoriaPadraoAsync;
    }

    public Task ImprimirCaixaAsync(ProdutoAcabadoCaixa caixa, string? descricaoMaterial = null)
    {
        ValidarCaixaElegivelParaEtiqueta(caixa);
        return ExecutarImpressaoComAuditoriaAsync(caixa, TipoOperacaoImpressao,
            () => _impressoraEtiquetaServico.ImprimirEtiquetaCaixaProdutoAcabadoAsync(CriarEtiqueta(caixa, descricaoMaterial)));
    }

    public Task ReimprimirCaixaAsync(ProdutoAcabadoCaixa caixa, string? descricaoMaterial = null)
    {
        ValidarCaixaElegivelParaEtiqueta(caixa);
        return ExecutarImpressaoComAuditoriaAsync(caixa, TipoOperacaoReimpressao,
            () => _impressoraEtiquetaServico.ReimprimirEtiquetaCaixaProdutoAcabadoAsync(CriarEtiqueta(caixa, descricaoMaterial)));
    }

    /// <summary>
    /// Executa a impressão/reimpressão e registra o evento de negócio ESTRUTURADO por caixa em ambos os
    /// desfechos: SUCESSO ao concluir; FALHA (mesmo dados_contexto, mensagem genérica segura) antes de
    /// RELANÇAR a exceção original. A auditoria de negócio NUNCA substitui/mascara a exceção de impressão
    /// (o diagnóstico técnico detalhado continua no IMPRESSAO_ZEBRA_DIAGNOSTICO, preservado).
    /// </summary>
    internal async Task ExecutarImpressaoComAuditoriaAsync(
        ProdutoAcabadoCaixa caixa, string tipoOperacao, Func<Task> acaoImpressao)
    {
        try
        {
            await acaoImpressao();
        }
        catch
        {
            await AuditarSeguroAsync(caixa, tipoOperacao, ResultadoFalha);
            throw; // exceção ORIGINAL propaga; a auditoria nunca a substitui/mascara
        }

        await AuditarSeguroAsync(caixa, tipoOperacao, ResultadoSucesso);
    }

    private async Task AuditarSeguroAsync(ProdutoAcabadoCaixa caixa, string tipoOperacao, string resultado)
    {
        // Auditoria de negócio nunca pode mascarar/substituir a exceção original de impressão.
        try
        {
            await _auditarEtiqueta(caixa, tipoOperacao, resultado);
        }
        catch
        {
            // Suprimido de propósito.
        }
    }

    /// <summary>
    /// Regra central (SERVICE) da etiqueta de Produto Acabado: só é elegível a caixa CONFIRMADA_SAP com
    /// identidade persistida (codigo/codigo_caixa_local) e HU SAP não vazia. Bloqueia todos os demais estados
    /// (CANCELADA/INDETERMINADO_TIMEOUT/ERRO_SAP/ENVIANDO_SAP/PRONTA/AGUARDANDO/PREVIEW/FINALIZADA/EM_PESAGEM/
    /// BLOQUEADA). Nenhum fluxo operacional de impressão/reimpressão alcança "HU SAP: PENDENTE". NÃO toca SAP.
    /// </summary>
    public static void ValidarCaixaElegivelParaEtiqueta(ProdutoAcabadoCaixa caixa)
    {
        ArgumentNullException.ThrowIfNull(caixa);

        if (caixa.CodigoProdutoAcabadoCaixa is null || string.IsNullOrWhiteSpace(caixa.CodigoCaixaLocal))
        {
            throw new ErroOperacionalEsperadoException(
                "A caixa não possui identidade persistida (código local). Impressão/reimpressão indisponível.");
        }

        if (caixa.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap)
        {
            throw new ErroOperacionalEsperadoException(
                "Somente caixas CONFIRMADA_SAP podem imprimir/reimprimir etiqueta. "
                + $"Estado atual: {MapeadorStatusHuCaixa.ParaTextoBanco(caixa.StatusIntegracao)}.");
        }

        if (string.IsNullOrWhiteSpace(caixa.HandlingUnitExternalId))
        {
            throw new ErroOperacionalEsperadoException(
                "A caixa não possui HU SAP confirmada. Impressão/reimpressão de etiqueta indisponível.");
        }
    }

    /// <summary>Monta a etiqueta a partir da caixa PERSISTIDA (fonte da verdade). HU vazia ⇒ "PENDENTE" (fallback técnico).</summary>
    public static DadosEtiquetaCaixaProdutoAcabado CriarEtiqueta(ProdutoAcabadoCaixa caixa, string? descricaoMaterial = null)
    {
        string unidade = string.IsNullOrWhiteSpace(caixa.UnidadePeso) ? "KG" : caixa.UnidadePeso.Trim();
        return new DadosEtiquetaCaixaProdutoAcabado
        {
            OrdemProducao = caixa.NumeroOrdemProducao,
            ItemOrdem = caixa.ItemOrdemProducao,
            Material = caixa.Material,
            DescricaoMaterial = descricaoMaterial ?? string.Empty,
            Lote = caixa.Lote,
            NumeroCaixa = caixa.NumeroCaixa > 0
                ? caixa.NumeroCaixa.ToString("0000", CultureInfo.InvariantCulture)
                : "-",
            CodigoCaixaLocal = caixa.CodigoCaixaLocal,
            PesoBruto = FormatarPeso(caixa.PesoBrutoKg, unidade),
            Tara = FormatarPeso(caixa.TaraKg, unidade),
            PesoLiquido = FormatarPeso(caixa.PesoLiquidoKg, unidade),
            Quantidade = $"{caixa.QuantidadeProdutos.ToString(CultureInfo.InvariantCulture)} "
                + (string.IsNullOrWhiteSpace(caixa.UnidadeQuantidade) ? "UN" : caixa.UnidadeQuantidade.Trim()),
            HandlingUnitSap = caixa.HandlingUnitExternalId ?? string.Empty,
            DataHora = (caixa.ConfirmadoSapEm ?? caixa.AtualizadoEm ?? caixa.CriadoEm).ToString("dd/MM/yyyy HH:mm"),
            Terminal = caixa.Terminal
        };
    }

    /// <summary>
    /// Contexto ESTRUTURADO da auditoria (auditoria_acao_usuario.dados_contexto JSONB). Apenas dados de
    /// rastreabilidade da caixa — NUNCA senha/token/cookie/CSRF/credenciais.
    /// </summary>
    public static string MontarContextoAuditoriaJson(ProdutoAcabadoCaixa caixa, string tipoOperacao)
    {
        ArgumentNullException.ThrowIfNull(caixa);
        var contexto = new Dictionary<string, object?>
        {
            ["codigo_hu_caixa"] = caixa.CodigoProdutoAcabadoCaixa,
            ["codigo_caixa_local"] = caixa.CodigoCaixaLocal,
            ["numero_caixa"] = caixa.NumeroCaixa,
            ["tipo_operacao"] = tipoOperacao,
            ["terminal"] = caixa.Terminal,
            ["hu_sap"] = caixa.HandlingUnitExternalId ?? string.Empty
        };
        return JsonSerializer.Serialize(contexto);
    }

    private static async Task RegistrarAuditoriaPadraoAsync(ProdutoAcabadoCaixa caixa, string tipoOperacao, string resultado)
    {
        // Auditoria de negócio nunca pode quebrar o fluxo de impressão nem mascarar a exceção física.
        try
        {
            string acao = tipoOperacao == TipoOperacaoReimpressao
                ? "PRODUTO_ACABADO_ETIQUETA_REIMPRESSAO"
                : "PRODUTO_ACABADO_ETIQUETA_IMPRESSAO";
            string operacaoTexto = tipoOperacao == TipoOperacaoReimpressao ? "reimpressão" : "impressão";
            // Mensagem genérica SEGURA (o detalhe técnico fica no IMPRESSAO_ZEBRA_DIAGNOSTICO).
            string mensagem = resultado == ResultadoFalha
                ? $"Falha na {operacaoTexto} da etiqueta da caixa {caixa.CodigoCaixaLocal}."
                : $"Etiqueta da caixa {caixa.CodigoCaixaLocal} ({tipoOperacao}).";
            string contexto = MontarContextoAuditoriaJson(caixa, tipoOperacao);
            await FabricaControladoresCadastro.CriarAuditoriaServico().RegistrarEventoOperacionalAsync(
                acao,
                resultado,
                mensagem,
                "ProcessoProdutoAcabadoForm",
                dadosContextoJson: contexto);
        }
        catch
        {
            // Suprimido de propósito (auditoria indisponível não impede/mascara a impressão).
        }
    }

    private static string FormatarPeso(decimal valor, string unidade)
        => $"{valor.ToString("0.###", CultureInfo.InvariantCulture)} {unidade}";
}
