namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Cenarios do registro de uma pesagem LOCAL de consumo (Tarefa 4).</summary>
public enum CenarioPesagemConsumo
{
    /// <summary>Pesagem valida registrada localmente.</summary>
    Registrada,

    /// <summary>Componente em unidade != KG: conversao ainda nao implementada.</summary>
    UnidadeNaoSuportada,

    /// <summary>Peso bruto <= 0.</summary>
    PesoBrutoInvalido,

    /// <summary>Tara invalida (negativa) ou tara >= bruto (liquido <= 0).</summary>
    PesoLiquidoInvalido,

    /// <summary>O novo total pesado excederia a quantidade pendente do componente.</summary>
    ExcedePendente,

    /// <summary>Componente nao liberado para pesagem (sem deposito / sem saldo / unidade / consumido).</summary>
    ComponenteNaoLiberado
}

/// <summary>Resultado do registro de uma pesagem local de consumo.</summary>
public sealed class ResultadoPesagemConsumo
{
    public bool Sucesso => Cenario == CenarioPesagemConsumo.Registrada;
    public CenarioPesagemConsumo Cenario { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public PesagemConsumoMaterial? Pesagem { get; init; }

    public static ResultadoPesagemConsumo Registrada(PesagemConsumoMaterial pesagem, string mensagem)
        => new() { Cenario = CenarioPesagemConsumo.Registrada, Pesagem = pesagem, Mensagem = mensagem };

    public static ResultadoPesagemConsumo Bloqueada(CenarioPesagemConsumo cenario, string mensagem)
        => new() { Cenario = cenario, Mensagem = mensagem };
}
