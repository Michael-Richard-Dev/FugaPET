using System.Globalization;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Interpretação DEFENSIVA da resposta crua da norma de embalagem (§5/§6). Puro: sem HTTP, sem estado.
/// Regra provisória validada: item tipo P = material de embalagem; item tipo I do material consultado =
/// produto acondicionado. Bloqueia (sem preparar preview HU) em qualquer ambiguidade/inconsistência,
/// sempre identificando material + motivo. PackagingInstruction vazio NÃO bloqueia. Materiais são
/// strings preservadas (sem conversão numérica, sem remover zeros à esquerda). Nenhum fallback fictício.
/// </summary>
public static class InterpretadorNormaEmbalagemProdutoAcabado
{
    private const string TipoEmbalagem = "P";

    // Produto acondicionado: as evidências disponíveis retornam tipo "I" ou "M" — ambos aceitos.
    private static readonly string[] TiposProdutoAcondicionado = ["I", "M"];

    public static ResultadoNormaEmbalagemProdutoAcabado Interpretar(
        string materialConsultado,
        ConsultaNormaEmbalagemSapResponse? resposta,
        OrigemNormaEmbalagem origem = OrigemNormaEmbalagem.ConsultadaSap)
    {
        string material = (materialConsultado ?? string.Empty).Trim();

        if (resposta is null)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "resposta da API nula.");
        }

        // Material principal deve ser o consultado (comparação exata, preservando zeros à esquerda).
        if (!string.Equals(resposta.Material.Trim(), material, StringComparison.Ordinal))
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(
                material, $"material principal da norma ({resposta.Material.Trim()}) diverge do consultado.");
        }

        IReadOnlyList<ConsultaNormaEmbalagemSapItemResponse> itens = resposta.PkgInstructionItems ?? [];

        // 1/2: exatamente UM item P (enquanto não houver regra oficial para múltiplos materiais de embalagem).
        ConsultaNormaEmbalagemSapItemResponse[] itensP = itens
            .Where(item => EhTipo(item, TipoEmbalagem))
            .ToArray();
        if (itensP.Length == 0)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "nenhum item de embalagem (tipo P) na norma.");
        }
        if (itensP.Length > 1)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(
                material, "mais de um item de embalagem (tipo P): norma ambígua.");
        }

        ConsultaNormaEmbalagemSapItemResponse itemP = itensP[0];

        // 3: item de produto (tipo I ou M) com Material == material consultado.
        ConsultaNormaEmbalagemSapItemResponse[] itensI = itens
            .Where(item => EhProdutoAcondicionado(item)
                        && string.Equals(item.Material.Trim(), material, StringComparison.Ordinal))
            .ToArray();
        if (itensI.Length == 0)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(
                material, "nenhum item de produto acondicionado (tipo I/M) correspondente ao material.");
        }

        // Mais de um item de produto correspondente: só é válido se todos tiverem os MESMOS valores relevantes.
        ConsultaNormaEmbalagemSapItemResponse itemI = itensI[0];
        if (itensI.Length > 1 && itensI.Any(i =>
                !string.Equals(i.Quantity.Trim(), itemI.Quantity.Trim(), StringComparison.Ordinal)
                || !string.Equals(i.QtyUOM.Trim(), itemI.QtyUOM.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(
                material, "mais de um item tipo I correspondente com valores divergentes: norma ambígua.");
        }

        // 4/8: campos da embalagem (item P).
        string materialCaixa = itemP.Material.Trim();
        if (string.IsNullOrWhiteSpace(materialCaixa))
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "material de embalagem (item P) vazio.");
        }

        if (!TentarLerQuantidade(itemP.Quantity, out decimal quantidadeEmbalagem))
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "quantidade da embalagem (item P) inválida.");
        }

        // 5/6: quantidade/unidade por caixa (item I).
        if (!TentarLerQuantidade(itemI.Quantity, out decimal quantidadePorCaixa) || quantidadePorCaixa <= 0m)
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "quantidade por caixa (item I) menor ou igual a zero.");
        }

        string unidadeQuantidade = itemI.QtyUOM.Trim();
        if (string.IsNullOrWhiteSpace(unidadeQuantidade))
        {
            return ResultadoNormaEmbalagemProdutoAcabado.Bloqueada(material, "unidade da quantidade por caixa (item I) vazia.");
        }

        // 9/10: PackagingInstruction pode vir vazio — não bloqueia; registra como pendência.
        string codigoNorma = resposta.PackagingInstruction.Trim();
        List<string> pendencias = [];
        if (string.IsNullOrWhiteSpace(codigoNorma))
        {
            pendencias.Add("PackagingInstruction não informada pelo SAP.");
        }

        return ResultadoNormaEmbalagemProdutoAcabado.Ok(new NormaEmbalagemProdutoAcabado
        {
            MaterialProduto = material,
            CodigoNorma = codigoNorma,
            MaterialCaixa = materialCaixa,
            QuantidadeEmbalagem = quantidadeEmbalagem,
            UnidadeEmbalagem = itemP.QtyUOM.Trim(),
            QuantidadePorCaixa = quantidadePorCaixa,
            UnidadeQuantidade = unidadeQuantidade,
            Origem = origem,
            Status = DescreverOrigem(origem),
            Pendencias = pendencias
        });
    }

    private static bool EhTipo(ConsultaNormaEmbalagemSapItemResponse item, string tipo)
        => string.Equals((item.MaterialType ?? string.Empty).Trim(), tipo, StringComparison.OrdinalIgnoreCase);

    private static bool EhProdutoAcondicionado(ConsultaNormaEmbalagemSapItemResponse item)
        => TiposProdutoAcondicionado.Any(tipo => EhTipo(item, tipo));

    private static bool TentarLerQuantidade(string valor, out decimal quantidade)
        => decimal.TryParse(
            (valor ?? string.Empty).Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out quantidade);

    private static string DescreverOrigem(OrigemNormaEmbalagem origem)
        => origem switch
        {
            OrigemNormaEmbalagem.ApiDev => "API DEV",
            OrigemNormaEmbalagem.ConsultadaSap => "CONSULTADA SAP",
            _ => "INDEFINIDA"
        };
}
