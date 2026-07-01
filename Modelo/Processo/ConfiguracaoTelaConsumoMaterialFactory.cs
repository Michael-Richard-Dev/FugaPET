namespace FugaPET_Dev.Modelo.Processo;

public static class ConfiguracaoTelaConsumoMaterialFactory
{
    public static ConfiguracaoTelaConsumoMaterial Criar(ModoConsumoMaterial modo)
        => modo switch
        {
            ModoConsumoMaterial.Quimico => new ConfiguracaoTelaConsumoMaterial
            {
                Modo = ModoConsumoMaterial.Quimico,
                TituloTela = "Consumo de Químicos",
                SubtituloTela = "Pesagem e consumo de químicos da ordem de produção",
                NomeModulo = "Consumo de Químicos",
                TipoBalancaPreferencial = "SAIDA_QUIMICOS",
                TextoSemBalancaConfigurada = "Balança de saída de químicos não configurada para esta operação.",
                UsarFiltroQuimicos = true
            },
            _ => new ConfiguracaoTelaConsumoMaterial
            {
                Modo = ModoConsumoMaterial.MateriaPrima,
                TituloTela = "Consumo de Matéria-Prima",
                SubtituloTela = "Pesagem e consumo de componentes da ordem de produção",
                NomeModulo = "Consumo de Matéria-Prima",
                TipoBalancaPreferencial = "CONSUMO_MATERIA_PRIMA",
                TextoSemBalancaConfigurada = "Balança de consumo de matéria-prima não configurada para esta operação.",
                UsarFiltroQuimicos = false
            }
        };
}
