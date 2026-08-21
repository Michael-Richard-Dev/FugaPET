using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Posicao SAP JA PREPARADA do documento de material 101, com o mapeamento de quais LOTES LOCAIS a
/// compoem. Separa a "posicao SAP final" (que pode consolidar varios lotes quando o material NAO e
/// administrado por lote) do lote local persistido. Preserva <see cref="CodigosLotesLocais"/> para
/// rastreabilidade interna — nunca se perde qual(is) lote(s) originou(aram) a posicao.
/// </summary>
public sealed record EntradaProdutoPosicaoMaterialDocument(
    MaterialDocumentSapItemRequest Item,
    IReadOnlyList<long> CodigosLotesLocais);
