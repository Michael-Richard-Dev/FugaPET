namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tipos de processo que uma operação da OP pode ter. O roteamento para a tela é feito SEMPRE por
/// esta configuração (dados), nunca pela descrição da operação nem por if/switch de OP/produto.
/// </summary>
public static class TipoProcessoOperacao
{
    public const string ConsumoMateriaPrima = "CONSUMO_MATERIA_PRIMA";
    public const string ConsumoQuimicos = "CONSUMO_QUIMICOS";
    public const string ConsumoSemiAcabado = "CONSUMO_SEMI_ACABADO";
    public const string ResultadoApontamento = "RESULTADO_APONTAMENTO";
    public const string SemiAcabado = "SEMI_ACABADO";
    public const string ProdutoAcabado = "PRODUTO_ACABADO";

    /// <summary>Operação sem destino configurado: a leitura interpreta, mas o início fica bloqueado.</summary>
    public const string SemDestinoConfigurado = "SEM_DESTINO_CONFIGURADO";
}

/// <summary>
/// Configuração que liga uma operação SAP a uma tela operacional do FugaPET. Resolvida por dados
/// (centro + tipo de ordem + sequência + operação + suboperação + centro de trabalho), nunca por texto.
/// </summary>
public sealed class ConfiguracaoOperacaoProcesso
{
    public long CodigoConfiguracao { get; init; }
    public string Centro { get; init; } = string.Empty;
    public string TipoOrdem { get; init; } = string.Empty;
    public string SequenciaSap { get; init; } = string.Empty;
    public string OperacaoSap { get; init; } = string.Empty;
    public string SuboperacaoSap { get; init; } = string.Empty;
    public string CentroTrabalho { get; init; } = string.Empty;
    public string TipoProcesso { get; init; } = TipoProcessoOperacao.SemDestinoConfigurado;
    public string TelaDestino { get; init; } = string.Empty;

    /// <summary>Quando true, exige que a operação anterior da sequência esteja concluída.</summary>
    public bool ExigeOperacaoAnterior { get; init; }

    public bool Ativo { get; init; } = true;
}
