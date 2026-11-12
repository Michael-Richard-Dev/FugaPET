using System.Runtime.CompilerServices;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal static class ClassificacaoMaterialDocumentP5
{
    private sealed record Evidencia(ClassificacaoEntradaMaterial Tipo);
    private static readonly ConditionalWeakTable<MaterialDocumentSapItemRequest, Evidencia> Evidencias = new();

    internal static MaterialDocumentSapItemRequest Registrar(MaterialDocumentSapItemRequest item, ProdutoSapMestre? produto)
    {
        ClassificacaoEntradaMaterial tipo = produto is { Consultado: true }
            && string.Equals(produto.CodigoProduto?.Trim(), item.Material.Trim(), StringComparison.Ordinal)
                ? ClassificadorItemEntradaMaterial.ClassificarPorProductType(produto.TipoMaterialSap)
                : ClassificacaoEntradaMaterial.Indefinido;
        Evidencias.Add(item, new(tipo));
        return item;
    }

    internal static ClassificacaoEntradaMaterial Obter(MaterialDocumentSapItemRequest item)
        => Evidencias.TryGetValue(item, out Evidencia? evidencia)
            ? evidencia.Tipo : ClassificacaoEntradaMaterial.Indefinido;

    internal static bool Bloqueia(MaterialDocumentSapItemRequest item)
    {
        ClassificacaoEntradaMaterial tipo = Obter(item);
        if (tipo == ClassificacaoEntradaMaterial.MateriaPrima)
            return true;
        bool pedido = !string.IsNullOrWhiteSpace(item.PurchaseOrder)
            || !string.IsNullOrWhiteSpace(item.PurchaseOrderItem)
            || item.GoodsMovementRefDocType == "B";
        if (!pedido)
            return string.IsNullOrWhiteSpace(item.ManufacturingOrder)
                || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmi) || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmiu);
        return tipo is not (ClassificacaoEntradaMaterial.Quimico
            or ClassificacaoEntradaMaterial.Embalagem or ClassificacaoEntradaMaterial.Semiacabado)
            || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmi) || !string.IsNullOrEmpty(item.Zz1PesoEntradaMmiu);
    }
}
