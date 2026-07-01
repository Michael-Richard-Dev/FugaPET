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

        if (string.IsNullOrWhiteSpace(palete.PackagingMaterial))
        {
            return ResultadoPreviewProdutoAcabadoPalete.Falha("Material de embalagem do palete não informado.");
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
            HandlingUnitItems = palete.Caixas
                .Select(caixa => new ProdutoAcabadoPaleteItemRequest
                {
                    HandlingUnit = string.IsNullOrWhiteSpace(caixa.HandlingUnitCaixa)
                        ? caixa.CodigoCaixaLocal
                        : caixa.HandlingUnitCaixa
                })
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
