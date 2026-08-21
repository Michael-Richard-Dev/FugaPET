using System.Text.Encodings.Web;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ProdutoAcabadoPaletePayloadBuilder
{
    public ResultadoPreviewProdutoAcabadoPalete MontarPreview(ProdutoAcabadoPalete palete)
    {
        ArgumentNullException.ThrowIfNull(palete);

        if (palete.PrimeiraCaixa > palete.UltimaCaixa)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Intervalo de caixas inválido.");
        }

        if (palete.Caixas.Count == 0)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Nenhuma caixa informada para o palete.");
        }

        if (palete.PesoBrutoKg <= palete.PesoLiquidoKg || palete.PesoLiquidoKg <= 0m || palete.TaraKg < 0m)
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Pesos do palete inválidos.");
        }

        // §12/§19: PackagingMaterial real é obrigatório (nunca PALLET01 hardcoded). Ausente ⇒ DEPENDENCIA_ARES / fail-closed.
        if (string.IsNullOrWhiteSpace(palete.PackagingMaterial))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Material de embalagem do palete não informado (DEPENDENCIA_ARES: PackagingMaterial real).");
        }

        // §10/§11/§18: SOMENTE caixa CONFIRMADA_SAP entra no palete.
        if (palete.Caixas.Any(caixa => caixa.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Todas as caixas do palete precisam estar CONFIRMADA_SAP.");
        }

        // §10 (problema 2): HU SAP individual OBRIGATÓRIA. SEM fallback para CodigoCaixaLocal — HU ausente ⇒ fail-closed.
        if (palete.Caixas.Any(caixa => string.IsNullOrWhiteSpace(caixa.HandlingUnitExternalId)))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha(
                "Caixa sem HU SAP individual: palete bloqueado (o payload só aceita HUs SAP reais).");
        }

        ProdutoAcabadoPaleteRequest payload = new()
        {
            HandlingUnitExternalID = palete.CodigoPaleteLocal,
            GrossWeight = palete.PesoBrutoKg,
            NetWeight = palete.PesoLiquidoKg,
            TareWeight = palete.TaraKg,
            WeightUnit = "KG",
            Plant = palete.Plant,
            StorageLocation = palete.StorageLocation,
            PackagingMaterial = palete.PackagingMaterial,
            // §18/§27: _HandlingUnitItem contém EXATAMENTE as HUs SAP das caixas (HandlingUnitExternalId), sem fallback.
            HandlingUnitItems = palete.Caixas
                .Select(caixa => new ProdutoAcabadoPaleteItemRequest { HandlingUnit = caixa.HandlingUnitExternalId! })
                .ToArray()
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        return ResultadoPreviewProdutoAcabadoPalete.Ok(payload, json);
    }
}
