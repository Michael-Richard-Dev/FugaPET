using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Monta o PREVIEW da Handling Unit de uma caixa individual (API_HANDLINGUNIT / OP_HANDLINGUNIT_0001).
/// Puro: valida os campos LOCAIS, gera o modelo de preview e um JSON sanitizado/legível. NÃO faz HTTP,
/// NÃO solicita CSRF, NÃO acessa Repository nem Form. Enquanto o contrato externo não for confirmado
/// pelo Ares, o preview é gerado mas o POST permanece bloqueado (status AGUARDANDO_AUTORIZACAO_SAP).
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitCaixaPayloadBuilder
{
    // Pendências fixas do contrato externo até o Ares fornecer $metadata/payload/tipos/campos/formato.
    private static readonly IReadOnlyList<string> PendenciasContratoBase =
    [
        "Aguardando $metadata oficial da OP_HANDLINGUNIT_0001.",
        "Nomes externos das propriedades JSON não confirmados (preview usa nomes internos em português).",
        "Campos obrigatórios e tipos exatos não confirmados.",
        "Formato de números/datas e chave de idempotência SAP não confirmados."
    ];

    public ResultadoPreviewHandlingUnitCaixa MontarPreview(ProdutoAcabadoOrdem ordem, ProdutoAcabadoCaixa caixa)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(caixa);

        string? erro = Validar(ordem, caixa);
        if (erro is not null)
        {
            return ResultadoPreviewHandlingUnitCaixa.Falha(erro, PendenciasContratoBase);
        }

        ProdutoAcabadoHandlingUnitCaixaPreview preview = new()
        {
            NumeroOrdemProducao = ordem.NumeroOrdem.Trim(),
            ItemOrdemProducao = ordem.ItemOrdem.Trim(),
            Material = caixa.Material.Trim(),
            Lote = caixa.Lote.Trim(),
            Centro = caixa.Centro.Trim(),
            Deposito = caixa.Deposito.Trim(),
            MaterialEmbalagem = caixa.MaterialEmbalagem.Trim(),
            PesoBruto = caixa.PesoBrutoKg,
            PesoLiquido = caixa.PesoLiquidoKg,
            Tara = caixa.TaraKg,
            UnidadePeso = string.IsNullOrWhiteSpace(caixa.UnidadePeso) ? "KG" : caixa.UnidadePeso.Trim().ToUpperInvariant(),
            Quantidade = caixa.QuantidadeProdutos,
            UnidadeQuantidade = string.IsNullOrWhiteSpace(caixa.UnidadeQuantidade) ? "UN" : caixa.UnidadeQuantidade.Trim().ToUpperInvariant(),
            CorrelationId = caixa.CorrelationId,
            CodigoCaixaLocal = caixa.CodigoCaixaLocal.Trim(),
            ContratoSapConfirmado = false
        };

        return ResultadoPreviewHandlingUnitCaixa.Ok(preview, SerializarSanitizado(preview), PendenciasContratoBase);
    }

    // Validações mínimas §10 (sem inventar fallback nem conversão).
    private static string? Validar(ProdutoAcabadoOrdem ordem, ProdutoAcabadoCaixa caixa)
    {
        if (string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
        {
            return "OP não informada.";
        }

        if (string.IsNullOrWhiteSpace(caixa.Material))
        {
            return "Material da caixa não informado.";
        }

        if (string.IsNullOrWhiteSpace(caixa.Centro))
        {
            return "Centro não informado.";
        }

        if (string.IsNullOrWhiteSpace(caixa.Deposito))
        {
            return "Depósito não informado.";
        }

        if (string.IsNullOrWhiteSpace(caixa.MaterialEmbalagem))
        {
            return "Material de embalagem da caixa não informado.";
        }

        if (caixa.PesoBrutoKg <= 0m)
        {
            return "Peso bruto deve ser maior que zero.";
        }

        if (caixa.TaraKg < 0m)
        {
            return "Tara não pode ser negativa.";
        }

        if (caixa.PesoLiquidoKg <= 0m)
        {
            return "Peso líquido deve ser maior que zero.";
        }

        if (Math.Abs(caixa.PesoLiquidoKg - (caixa.PesoBrutoKg - caixa.TaraKg)) > 0.001m)
        {
            return "Peso líquido deve ser igual a peso bruto menos tara.";
        }

        if (caixa.QuantidadeProdutos <= 0)
        {
            return "Quantidade de produtos deve ser maior que zero.";
        }

        if (string.IsNullOrWhiteSpace(caixa.UnidadeQuantidade))
        {
            return "Unidade de quantidade não informada.";
        }

        if (string.IsNullOrWhiteSpace(caixa.UnidadePeso))
        {
            return "Unidade de peso não informada.";
        }

        return caixa.CorrelationId == Guid.Empty ? "Correlation_id inválido (não gerado)." : null;
    }

    // JSON legível apenas com campos funcionais (não há credenciais no preview) — marcado como não confirmado.
    private static string SerializarSanitizado(ProdutoAcabadoHandlingUnitCaixaPreview preview)
    {
        var corpo = new
        {
            _aviso = "PREVIEW INTERNO — contrato SAP NAO confirmado (nomes internos, nao correspondem ao JSON da OP_HANDLINGUNIT_0001).",
            ContratoSapConfirmado = preview.ContratoSapConfirmado,
            preview.NumeroOrdemProducao,
            preview.ItemOrdemProducao,
            preview.Material,
            preview.Lote,
            preview.Centro,
            preview.Deposito,
            preview.MaterialEmbalagem,
            preview.PesoBruto,
            preview.PesoLiquido,
            preview.Tara,
            preview.UnidadePeso,
            preview.Quantidade,
            preview.UnidadeQuantidade,
            CorrelationId = preview.CorrelationId.ToString(),
            preview.CodigoCaixaLocal
        };

        return JsonSerializer.Serialize(corpo, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>Resultado da montagem do preview HU: sucesso/mensagem, preview, JSON sanitizado e pendências de contrato.</summary>
public sealed class ResultadoPreviewHandlingUnitCaixa
{
    private ResultadoPreviewHandlingUnitCaixa(
        bool sucesso,
        string mensagem,
        ProdutoAcabadoHandlingUnitCaixaPreview? preview,
        string payloadJsonSanitizado,
        IReadOnlyList<string> pendenciasContrato)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Preview = preview;
        PayloadJsonSanitizado = payloadJsonSanitizado;
        PendenciasContrato = pendenciasContrato;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public ProdutoAcabadoHandlingUnitCaixaPreview? Preview { get; }
    public string PayloadJsonSanitizado { get; }
    public IReadOnlyList<string> PendenciasContrato { get; }

    /// <summary>Preview gerado, mas o contrato externo ainda não foi confirmado ⇒ envio permanece bloqueado.</summary>
    public bool ContratoSapConfirmado => Preview?.ContratoSapConfirmado ?? false;

    public static ResultadoPreviewHandlingUnitCaixa Ok(
        ProdutoAcabadoHandlingUnitCaixaPreview preview, string payloadJson, IReadOnlyList<string> pendencias)
        => new(true, "Preview da Handling Unit gerado (contrato SAP ainda não confirmado).", preview, payloadJson, pendencias);

    public static ResultadoPreviewHandlingUnitCaixa Falha(string mensagem, IReadOnlyList<string> pendencias)
        => new(false, mensagem, null, string.Empty, pendencias);
}
