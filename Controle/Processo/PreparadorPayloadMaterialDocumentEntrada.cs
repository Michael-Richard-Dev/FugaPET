using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Controle.Processo;

/// <summary>Resultado da preparação do payload 101: posições prontas OU um bloqueio pré-POST (nunca ambos).</summary>
internal sealed record ResultadoPreparacaoPayloadEntrada(
    IReadOnlyList<EntradaProdutoPosicaoMaterialDocument>? Posicoes,
    ResultadoEnvioSapEntrada? Bloqueio);

/// <summary>
/// Prepara as posições do documento de material 101 de forma CONDICIONAL à administração de lote SAP
/// (A_ProductPlant / IsBatchManagementRequired) por material × centro:
/// <list type="bullet">
/// <item>true  → uma posição por LOTE local, com Batch/ManufactureDate/ShelfLifeExpirationDate (valida lote+datas);</item>
/// <item>false → consolida os lotes locais por (PO, item, material, centro, depósito, KG) numa única posição, SEM Batch/datas;</item>
/// <item>indeterminado/null/consulta falhou → BLOQUEIA antes do POST (nunca assume true nem false).</item>
/// </list>
/// Consulta A_ProductPlant UMA vez por material+centro por envio (cache). Não faz POST. Preserva os
/// códigos de lote local de cada posição (rastreabilidade). NÃO infere administração de lote pela mera
/// existência de entrada_produto_lote — o lote local existe para TODOS os materiais por decisão do FugaPET.
/// </summary>
internal static class PreparadorPayloadMaterialDocumentEntrada
{
    public static async Task<ResultadoPreparacaoPayloadEntrada> PrepararAsync(
        long codigoLancamento,
        string numeroPedido,
        IReadOnlyList<EntradaProdutoItemEnvioSap> lotes,
        Func<string, string, CancellationToken, Task<ProdutoCentroSapMestre?>> consultarProdutoCentro,
        CancellationToken cancellationToken)
    {
        DateTime hoje = DateTime.Today;
        int total = lotes.Count;
        List<EntradaProdutoPosicaoMaterialDocument> posicoes = [];
        Dictionary<string, ProdutoCentroSapMestre?> cachePorMaterialCentro = new(StringComparer.Ordinal);

        // A administração de lote é POR material × centro: agrupa por essa combinação.
        IEnumerable<IGrouping<(string Material, string Centro), EntradaProdutoItemEnvioSap>> grupos = lotes
            .GroupBy(l => ((l.Material ?? string.Empty).Trim(), (l.Centro ?? string.Empty).Trim()));

        foreach (IGrouping<(string Material, string Centro), EntradaProdutoItemEnvioSap> grupo in grupos)
        {
            string material = grupo.Key.Material;
            string centro = grupo.Key.Centro;

            // Dados obrigatórios do movimento 101 (todos os casos), antes de qualquer consulta/POST.
            ResultadoEnvioSapEntrada? bloqueioBasico = ValidarBasico(total, grupo);
            if (bloqueioBasico is not null)
            {
                return new ResultadoPreparacaoPayloadEntrada(null, bloqueioBasico);
            }

            // Consulta A_ProductPlant UMA vez por material+centro por envio.
            string chave = material + "|" + centro;
            if (!cachePorMaterialCentro.TryGetValue(chave, out ProdutoCentroSapMestre? mestre))
            {
                mestre = await consultarProdutoCentro(material, centro, cancellationToken);
                cachePorMaterialCentro[chave] = mestre;
            }

            // CASO C: indeterminado (null, consulta falhou, material/centro não encontrado, flag ausente).
            if (mestre is null || !mestre.AdministracaoLoteDefinida)
            {
                return new ResultadoPreparacaoPayloadEntrada(null, Bloqueio(total,
                    $"Lançamento {codigoLancamento}: não foi possível determinar a administração de lote do "
                    + $"material {material} no centro {centro} (A_ProductPlant/IsBatchManagementRequired). "
                    + "Envio ao SAP bloqueado."));
            }

            if (mestre.IsBatchManagementRequired == true)
            {
                // CASO A: uma posição por LOTE, com Batch/datas.
                foreach (EntradaProdutoItemEnvioSap lote in grupo)
                {
                    ResultadoEnvioSapEntrada? bloqueioLote = ValidarLoteAdministrado(codigoLancamento, lote, hoje, total);
                    if (bloqueioLote is not null)
                    {
                        return new ResultadoPreparacaoPayloadEntrada(null, bloqueioLote);
                    }

                    posicoes.Add(new EntradaProdutoPosicaoMaterialDocument(
                        EntradaProdutoController.MontarItemMaterialDocument(numeroPedido, lote, comLote: true),
                        [lote.CodigoEntradaProdutoLote]));
                }
            }
            else
            {
                // CASO B: material NÃO administrado por lote — consolida por (item, depósito) somando o líquido,
                // SEM Batch/datas. (material/centro fixos no grupo; PO = numeroPedido; EntryUnit = KG.)
                IEnumerable<IGrouping<(string Item, string Deposito), EntradaProdutoItemEnvioSap>> subgrupos = grupo
                    .GroupBy(l => ((l.NumeroItem ?? string.Empty).Trim(), (l.Deposito ?? string.Empty).Trim()));

                foreach (IGrouping<(string Item, string Deposito), EntradaProdutoItemEnvioSap> sub in subgrupos)
                {
                    decimal somaLiquido = sub.Sum(l => l.PesoLiquidoKg);
                    decimal somaBruto = sub.Sum(l => l.PesoBrutoKg);
                    EntradaProdutoItemEnvioSap representante = sub.First() with
                    {
                        PesoLiquidoKg = somaLiquido,
                        PesoBrutoKg = somaBruto
                    };
                    IReadOnlyList<long> codigosLotes = sub.Select(l => l.CodigoEntradaProdutoLote).ToList();

                    posicoes.Add(new EntradaProdutoPosicaoMaterialDocument(
                        EntradaProdutoController.MontarItemMaterialDocument(numeroPedido, representante, comLote: false),
                        codigosLotes));
                }
            }
        }

        return new ResultadoPreparacaoPayloadEntrada(posicoes, null);
    }

    // Dados obrigatórios do movimento 101 (independem de administração de lote).
    private static ResultadoEnvioSapEntrada? ValidarBasico(
        int total, IEnumerable<EntradaProdutoItemEnvioSap> grupo)
    {
        foreach (EntradaProdutoItemEnvioSap lote in grupo)
        {
            string descItem = string.IsNullOrWhiteSpace(lote.NumeroItem) ? "(sem número)" : lote.NumeroItem.Trim();

            if (string.IsNullOrWhiteSpace(lote.NumeroItem)
                || string.IsNullOrWhiteSpace(lote.Material)
                || string.IsNullOrWhiteSpace(lote.Centro)
                || string.IsNullOrWhiteSpace(lote.Deposito))
            {
                return Bloqueio(total, $"Item {descItem} sem material, centro, depósito ou item do pedido. Envio bloqueado.");
            }

            if (lote.PesoLiquidoKg <= 0m)
            {
                return Bloqueio(total, $"Item {descItem} sem peso líquido positivo para envio SAP.");
            }
        }

        return null;
    }

    // Material administrado por lote: lote + validade obrigatórios; datas coerentes. Validade NUNCA calculada.
    private static ResultadoEnvioSapEntrada? ValidarLoteAdministrado(
        long codigoLancamento, EntradaProdutoItemEnvioSap lote, DateTime hoje, int total)
    {
        string descItem = string.IsNullOrWhiteSpace(lote.NumeroItem) ? "(sem número)" : lote.NumeroItem.Trim();
        string descLote = string.IsNullOrWhiteSpace(lote.NumeroLote) ? "(sem lote)" : lote.NumeroLote.Trim();

        if (string.IsNullOrWhiteSpace(lote.NumeroLote))
        {
            return Bloqueio(total,
                $"Lançamento {codigoLancamento}, item {descItem}: material administrado por lote sem lote (Batch). Envio bloqueado.");
        }

        if (!lote.DataValidade.HasValue)
        {
            return Bloqueio(total,
                $"Lançamento {codigoLancamento}, item {descItem}, lote {descLote}: "
                + "material administrado por lote sem data de validade (ShelfLifeExpirationDate). Envio bloqueado.");
        }

        DateTime validade = lote.DataValidade.Value.Date;

        if (lote.DataFabricacao.HasValue && lote.DataFabricacao.Value.Date > hoje)
        {
            return Bloqueio(total,
                $"Lançamento {codigoLancamento}, item {descItem}, lote {descLote}: data de fabricação no futuro. Envio bloqueado.");
        }

        if (lote.DataFabricacao.HasValue && validade < lote.DataFabricacao.Value.Date)
        {
            return Bloqueio(total,
                $"Lançamento {codigoLancamento}, item {descItem}, lote {descLote}: "
                + "data de validade anterior à data de fabricação. Envio bloqueado.");
        }

        if (validade < hoje)
        {
            return Bloqueio(total,
                $"Lançamento {codigoLancamento}, item {descItem}, lote {descLote}: "
                + "data de validade anterior à data de lançamento. Envio bloqueado.");
        }

        return null;
    }

    private static ResultadoEnvioSapEntrada Bloqueio(int total, string mensagem)
        => new()
        {
            Cenario = CenarioEnvioSapEntrada.DadosIncompletos,
            Total = total,
            Mensagem = mensagem
        };
}
