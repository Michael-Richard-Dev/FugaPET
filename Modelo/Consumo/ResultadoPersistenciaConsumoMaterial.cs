namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>Cenarios do salvamento LOCAL do consumo (Tarefa 5).</summary>
public enum CenarioPersistenciaConsumo
{
    /// <summary>Consumo gravado localmente (PENDENTE_SAP).</summary>
    Salvo,

    /// <summary>Nenhuma pesagem de consumo registrada para salvar.</summary>
    SemPesagem,

    /// <summary>Dados invalidos (OP ausente, unidade != KG, excesso, peso liquido <= 0).</summary>
    DadosInvalidos,

    /// <summary>Falha tecnica ao persistir (rollback aplicado).</summary>
    Erro
}

/// <summary>Resultado do salvamento LOCAL do consumo.</summary>
public sealed class ResultadoPersistenciaConsumoMaterial
{
    public bool Sucesso => Cenario == CenarioPersistenciaConsumo.Salvo;
    public CenarioPersistenciaConsumo Cenario { get; init; }
    public long? CodigoLancamento { get; init; }
    public string Mensagem { get; init; } = string.Empty;

    public static ResultadoPersistenciaConsumoMaterial Salvo(long codigoLancamento, string mensagem)
        => new() { Cenario = CenarioPersistenciaConsumo.Salvo, CodigoLancamento = codigoLancamento, Mensagem = mensagem };

    public static ResultadoPersistenciaConsumoMaterial SemPesagem(string mensagem)
        => new() { Cenario = CenarioPersistenciaConsumo.SemPesagem, Mensagem = mensagem };

    public static ResultadoPersistenciaConsumoMaterial DadosInvalidos(string mensagem)
        => new() { Cenario = CenarioPersistenciaConsumo.DadosInvalidos, Mensagem = mensagem };

    public static ResultadoPersistenciaConsumoMaterial Erro(string mensagem)
        => new() { Cenario = CenarioPersistenciaConsumo.Erro, Mensagem = mensagem };
}
