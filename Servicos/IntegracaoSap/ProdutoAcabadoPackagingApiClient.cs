using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ProdutoAcabadoPackagingApiClient
{
    public const string EndpointGetPackagingSet = "ZAPI_PACKAGING_SRV/GetPackagingSet";
    public const string MensagemNormaNaoEncontrada = "Norma de embalagem não localizada para o produto informado.";

    public static string MontarUrlConsulta(string material, string? packagingInstruction, string sapClient = "110")
    {
        string materialSeguro = (material ?? string.Empty).Trim().Replace("'", "''");
        string filtro = $"Material eq '{materialSeguro}'";
        if (!string.IsNullOrWhiteSpace(packagingInstruction))
        {
            string normaSegura = packagingInstruction.Trim().Replace("'", "''");
            filtro += $" and PackagingInstruction eq '{normaSegura}'";
        }

        return $"/sap/opu/odata/sap/{EndpointGetPackagingSet}?$filter={Uri.EscapeDataString(filtro)}&sap-client={sapClient}";
    }

    public static ProdutoAcabadoNormaEmbalagem CriarFallbackControlado(
        string material,
        int quantidadeProdutosPorCaixa,
        string packagingInstruction = "FALLBACK_MEMORIA")
    {
        if (quantidadeProdutosPorCaixa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidadeProdutosPorCaixa), "Quantidade por caixa deve ser maior que zero.");
        }

        return new ProdutoAcabadoNormaEmbalagem
        {
            Material = material.Trim(),
            PackagingInstruction = packagingInstruction,
            QuantidadeProdutosPorCaixa = quantidadeProdutosPorCaixa,
            Unidade = "UN",
            Itens =
            [
                new ProdutoAcabadoNormaItem
                {
                    Material = material.Trim(),
                    TipoMaterial = "P",
                    Quantidade = quantidadeProdutosPorCaixa,
                    Unidade = "UN",
                    Item = "0001"
                }
            ]
        };
    }
}
