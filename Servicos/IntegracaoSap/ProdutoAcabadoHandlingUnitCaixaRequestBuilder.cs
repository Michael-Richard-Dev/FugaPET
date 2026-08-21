using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Monta o REQUEST tipado da Handling Unit de caixa (contrato SAP CONFIRMADO) e o JSON sanitizado que vai
/// para o preview/persistência. Puro: valida os campos locais e projeta a caixa/ordem no
/// <see cref="HandlingUnitCaixaRequest"/> com os nomes JSON exatos. NÃO faz HTTP, NÃO acessa Repository/Form.
/// Nunca envia tara/OP/correlation_id/usuario/terminal ao SAP. Sem hardcode de massa (valores vêm da caixa).
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitCaixaRequestBuilder
{
    /// <summary>Endpoint relativo do POST (documentado no evento; o host real fica na config do gateway).</summary>
    public const string EndpointRelativo = "/HandlingUnit?sap-client=110";

    /// <summary>Marcador temporário do HandlingUnitExternalID conforme contrato (SAP atribui a HU real).</summary>
    public const string HandlingUnitExternalIdTemporario = "$1";

    private static readonly JsonSerializerOptions OpcoesJson = new() { WriteIndented = false };

    public ResultadoRequestHandlingUnitCaixa Montar(ProdutoAcabadoCaixa caixa)
    {
        ArgumentNullException.ThrowIfNull(caixa);

        string? erro = Validar(caixa);
        if (erro is not null)
        {
            return ResultadoRequestHandlingUnitCaixa.Falha(erro);
        }

        string centro = caixa.Centro.Trim();
        string deposito = caixa.Deposito.Trim();
        string unidadeQtd = string.IsNullOrWhiteSpace(caixa.UnidadeQuantidade)
            ? "UN"
            : caixa.UnidadeQuantidade.Trim().ToUpperInvariant();

        HandlingUnitCaixaRequest request = new()
        {
            HandlingUnitExternalId = HandlingUnitExternalIdTemporario,
            GrossWeight = caixa.PesoBrutoKg,
            NetWeight = caixa.PesoLiquidoKg,
            WeightUnit = "KG",
            WeightUnitIsoCode = "KGM",
            Warehouse = string.Empty,
            Plant = centro,
            StorageLocation = deposito,
            PackagingMaterial = caixa.MaterialEmbalagem.Trim(),
            HandlingUnitItem =
            [
                new HandlingUnitCaixaItemRequest
                {
                    HandlingUnitExternalId = HandlingUnitExternalIdTemporario,
                    HandlingUnitTypeOfContent = "1",
                    Plant = centro,
                    StorageLocation = deposito,
                    Material = caixa.Material.Trim(),
                    Batch = caixa.Lote.Trim(),
                    HandlingUnitQuantity = caixa.QuantidadeProdutos,
                    HandlingUnitQuantityUnit = unidadeQtd
                }
            ]
        };

        string json = JsonSerializer.Serialize(request, OpcoesJson);
        return ResultadoRequestHandlingUnitCaixa.Ok(request, json, EndpointRelativo);
    }

    private static string? Validar(ProdutoAcabadoCaixa caixa)
    {
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

        if (string.IsNullOrWhiteSpace(caixa.Lote))
        {
            return "Lote não informado.";
        }

        if (caixa.PesoBrutoKg <= 0m)
        {
            return "Peso bruto deve ser maior que zero.";
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

        return string.IsNullOrWhiteSpace(caixa.UnidadeQuantidade) ? "Unidade de quantidade não informada." : null;
    }
}

/// <summary>Resultado do builder do request HU: DTO tipado + JSON sanitizado + endpoint, ou falha de validação.</summary>
public sealed class ResultadoRequestHandlingUnitCaixa
{
    private ResultadoRequestHandlingUnitCaixa(
        bool sucesso, string mensagem, HandlingUnitCaixaRequest? request, string jsonSanitizado, string endpoint)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Request = request;
        JsonSanitizado = jsonSanitizado;
        Endpoint = endpoint;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public HandlingUnitCaixaRequest? Request { get; }
    public string JsonSanitizado { get; }
    public string Endpoint { get; }

    public static ResultadoRequestHandlingUnitCaixa Ok(HandlingUnitCaixaRequest request, string json, string endpoint)
        => new(true, "Request da Handling Unit montado.", request, json, endpoint);

    public static ResultadoRequestHandlingUnitCaixa Falha(string mensagem)
        => new(false, mensagem, null, string.Empty, string.Empty);
}
