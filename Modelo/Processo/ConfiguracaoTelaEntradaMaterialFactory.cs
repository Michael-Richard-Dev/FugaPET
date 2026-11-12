namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Entrada 24.1 (Ajuste 2): fábrica da configuração da tela de Entrada por modo.
/// TipoBalancaPreferencial usa os novos tipos (ENTRADA_MATERIA_PRIMA/ENTRADA_QUIMICOS); enquanto o
/// cadastro de balança não tiver esses tipos, a resolução de balança faz fallback seguro (sem SQL aqui).
/// </summary>
public static class ConfiguracaoTelaEntradaMaterialFactory
{
    public static ConfiguracaoTelaEntradaMaterial Criar(ModoEntradaMaterial modo)
        => modo switch
        {
            // GATE 073: porta única. Reutiliza infraestrutura de balança existente (sem criar novo tipo físico).
            ModoEntradaMaterial.RecebimentoMercadoria => new ConfiguracaoTelaEntradaMaterial
            {
                Modo = ModoEntradaMaterial.RecebimentoMercadoria,
                TituloTela = "Recebimento de Mercadoria",
                SubtituloTela = "Pesagem e recebimento de mercadorias por pedido de compra / SAP",
                NomeModulo = "Recebimento de Mercadoria",
                TipoBalancaPreferencial = "ENTRADA_MATERIA_PRIMA",
                UsarFiltroQuimicos = false,
                PlaceholderPesquisa = "Pesquisar itens do pedido...",
                MensagemLote = "Informe o lote da mercadoria antes da pesagem."
            },
            ModoEntradaMaterial.Quimico => new ConfiguracaoTelaEntradaMaterial
            {
                Modo = ModoEntradaMaterial.Quimico,
                TituloTela = "Entrada de Químicos",
                SubtituloTela = "Pesagem e entrada de químicos por pedido de compra / SAP",
                NomeModulo = "Entrada de Químicos",
                TipoBalancaPreferencial = "ENTRADA_QUIMICOS",
                UsarFiltroQuimicos = true,
                PlaceholderPesquisa = "Pesquisar itens de químicos...",
                MensagemLote = "Informe o lote do produto químico antes da pesagem."
            },
            _ => new ConfiguracaoTelaEntradaMaterial
            {
                Modo = ModoEntradaMaterial.MateriaPrima,
                TituloTela = "Entrada de Matéria-Prima",
                SubtituloTela = "Pesagem e entrada de matéria-prima por pedido de compra / SAP",
                NomeModulo = "Entrada de Matéria-Prima",
                TipoBalancaPreferencial = "ENTRADA_MATERIA_PRIMA",
                UsarFiltroQuimicos = false,
                PlaceholderPesquisa = "Pesquisar itens de matéria-prima...",
                MensagemLote = "Informe o lote da matéria-prima antes da pesagem."
            }
        };
}
