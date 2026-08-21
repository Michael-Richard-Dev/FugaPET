using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// REV4-Â§6: componente (consumo 261) de origem para montar o pipeline. Todos os campos sÃ£o obrigatÃ³rios;
/// origem real de Reservation/ReservationItem/Batch/Unidade = DEPENDENCIA_FUNCIONAL/ARES (nÃ£o inventar).
/// </summary>
public sealed record ProdutoAcabadoComponenteOrigem
{
    public string Material { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string Reservation { get; init; } = string.Empty;
    public string ReservationItem { get; init; } = string.Empty;
    public string Batch { get; init; } = string.Empty;
}

/// <summary>
/// REV4-Â§6: fonte de dados (caixa + OP + componentes persistidos/consultados) para o pipeline PA. A View NÃƒO
/// monta payload SAP: entrega esta origem e o builder converte em commands 261/101. Campos ausentes â‡’ bloqueio.
/// </summary>
public sealed record ProdutoAcabadoPipelineOrigem
{
    public long CodigoCaixa { get; init; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;      // REV5: preenchido pelo store/orquestrador; View nÃ£o define
    public DateTime? PostingDate { get; init; }                     // Â§8
    public DateTime? DocumentDate { get; init; }                    // Â§8
    public string TextoCabecalho { get; init; } = string.Empty;
    public IReadOnlyList<ProdutoAcabadoComponenteOrigem> Componentes { get; init; } = [];

    // 101 (produÃ§Ã£o do PA). Contrato confirmado 02/101/F; demais campos sÃ£o DEPENDENCIA_ARES.
    public string Material101 { get; init; } = string.Empty;
    public string Plant101 { get; init; } = string.Empty;
    public string StorageLocation101 { get; init; } = string.Empty;
    public string ManufacturingOrderItem { get; init; } = string.Empty;
    public string QuantityInEntryUnit { get; init; } = string.Empty;
    public string EntryUnit { get; init; } = string.Empty;
    public string Batch101 { get; init; } = string.Empty;
}

/// <summary>Resultado da montagem de commands. Sucesso=false â‡’ bloqueio objetivo, sem commands (zero HTTP).</summary>
public sealed record ResultadoComandosPipeline(
    bool Sucesso,
    string Mensagem,
    ProdutoAcabadoMovimento261Command? Comando261,
    ProdutoAcabadoMovimento101Command? Comando101)
{
    public static ResultadoComandosPipeline Bloqueado(string mensagem) => new(false, mensagem, null, null);
}

/// <summary>
/// REV4-Â§6: builder PURO de domÃ­nio que converte <see cref="ProdutoAcabadoPipelineOrigem"/> em commands 261/101.
/// Nenhuma inferÃªncia (sem "KG", sem datas MinValue, sem defaults). Qualquer campo obrigatÃ³rio ausente â‡’ bloqueio
/// objetivo e ZERO commands (o orquestrador nem Ã© chamado). NÃ£o faz HTTP nem toca banco.
/// </summary>
public static class ProdutoAcabadoPipelineCommandBuilder
{
    public static ResultadoComandosPipeline Construir(ProdutoAcabadoPipelineOrigem origem)
    {
        ArgumentNullException.ThrowIfNull(origem);

        if (string.IsNullOrWhiteSpace(origem.NumeroOrdem))
        {
            return ResultadoComandosPipeline.Bloqueado("Pipeline sem OP: bloqueado (nenhum POST).");
        }

        if (origem.PostingDate is not DateTime posting || posting == DateTime.MinValue
            || origem.DocumentDate is not DateTime document || document == DateTime.MinValue)
        {
            return ResultadoComandosPipeline.Bloqueado(
                "Pipeline sem PostingDate/DocumentDate reais: DEPENDENCIA_ARES (nenhum POST).");
        }

        // 261 â€” pelo menos um componente, todos completos (inclusive Unidade, SEM fallback "KG").
        if (origem.Componentes.Count == 0)
        {
            return ResultadoComandosPipeline.Bloqueado(
                "Pipeline sem componentes de consumo (261): DEPENDENCIA_ARES (nenhum POST).");
        }

        if (origem.Componentes.Any(c => string.IsNullOrWhiteSpace(c.Material) || string.IsNullOrWhiteSpace(c.Plant)
            || string.IsNullOrWhiteSpace(c.StorageLocation) || c.Quantidade <= 0m || string.IsNullOrWhiteSpace(c.Unidade)
            || string.IsNullOrWhiteSpace(c.Reservation) || string.IsNullOrWhiteSpace(c.ReservationItem)))
        {
            return ResultadoComandosPipeline.Bloqueado(
                "Componente 261 incompleto (Material/Plant/StorageLocation/Quantidade/Unidade/Reservation/ReservationItem): DEPENDENCIA_FUNCIONAL.");
        }

        // 101 â€” campos obrigatÃ³rios (fora os fixos 02/101/F do gateway).
        if (string.IsNullOrWhiteSpace(origem.Material101) || string.IsNullOrWhiteSpace(origem.Plant101)
            || string.IsNullOrWhiteSpace(origem.StorageLocation101) || string.IsNullOrWhiteSpace(origem.ManufacturingOrderItem)
            || string.IsNullOrWhiteSpace(origem.QuantityInEntryUnit) || string.IsNullOrWhiteSpace(origem.EntryUnit)
            || string.IsNullOrWhiteSpace(origem.Batch101))
        {
            return ResultadoComandosPipeline.Bloqueado(
                "ProduÃ§Ã£o 101 incompleta (Material/Plant/StorageLocation/ManufacturingOrderItem/Quantity/EntryUnit/Batch): DEPENDENCIA_ARES.");
        }

        ProdutoAcabadoMovimento261Command comando261 = new()
        {
            NumeroOrdem = origem.NumeroOrdem,
            CorrelationId = origem.CorrelationId,
            PostingDate = posting,
            DocumentDate = document,
            TextoCabecalho = origem.TextoCabecalho,
            Itens = origem.Componentes
                .Select(c => new ProdutoAcabadoMovimento261ItemCommand
                {
                    Material = c.Material,
                    Plant = c.Plant,
                    StorageLocation = c.StorageLocation,
                    Quantidade = c.Quantidade,
                    Unidade = c.Unidade,
                    Reservation = c.Reservation,
                    ReservationItem = c.ReservationItem,
                    Batch = c.Batch
                })
                .ToArray()
        };

        ProdutoAcabadoMovimento101Command comando101 = new()
        {
            NumeroOrdem = origem.NumeroOrdem,
            Material = origem.Material101,
            Plant = origem.Plant101,
            StorageLocation = origem.StorageLocation101,
            ManufacturingOrderItem = origem.ManufacturingOrderItem,
            QuantityInEntryUnit = origem.QuantityInEntryUnit,
            EntryUnit = origem.EntryUnit,
            Batch = origem.Batch101,
            PostingDate = posting,
            DocumentDate = document,
            TextoCabecalho = origem.TextoCabecalho
        };

        return new ResultadoComandosPipeline(true, "Commands 261/101 montados.", comando261, comando101);
    }
}


