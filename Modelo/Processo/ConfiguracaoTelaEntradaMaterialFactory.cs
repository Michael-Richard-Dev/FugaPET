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
            ModoEntradaMaterial.Quimico => new ConfiguracaoTelaEntradaMaterial
            {
                Modo = ModoEntradaMaterial.Quimico,
                TituloTela = "Entrada de Químicos",
                SubtituloTela = "Pesagem e entrada de químicos por pedido de compra / SAP",
                NomeModulo = "Entrada de Químicos",
                TipoBalancaPreferencial = "ENTRADA_QUIMICOS",
                UsarFiltroQuimicos = true
            },
            _ => new ConfiguracaoTelaEntradaMaterial
            {
                Modo = ModoEntradaMaterial.MateriaPrima,
                TituloTela = "Entrada de Matéria-Prima",
                SubtituloTela = "Pesagem e entrada de matéria-prima por pedido de compra / SAP",
                NomeModulo = "Entrada de Matéria-Prima",
                TipoBalancaPreferencial = "ENTRADA_MATERIA_PRIMA",
                UsarFiltroQuimicos = false
            }
        };
}
