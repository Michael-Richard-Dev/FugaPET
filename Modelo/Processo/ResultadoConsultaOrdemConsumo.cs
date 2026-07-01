namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Cenarios da consulta da Ordem de Producao na Tela de Consumo de Materia-Prima.</summary>
public enum CenarioConsultaOrdemConsumo
{
    /// <summary>Ordem obrigatoria nao informada (validacao local, sem chamar o SAP).</summary>
    OrdemObrigatoria,

    /// <summary>OP carregada e liberada para consumo.</summary>
    Carregada,

    /// <summary>OP encontrada porem nao liberada/excluida: dados exibidos, pesagem bloqueada.</summary>
    NaoLiberada,

    /// <summary>OP encontrada porem SEM componentes de consumo (nem no expand nem no fallback).</summary>
    SemComponentes,

    /// <summary>OP nao encontrada no SAP: limpar a tela.</summary>
    NaoEncontrada,

    /// <summary>SAP indisponivel/erro: mensagem amigavel, log sanitizado.</summary>
    Indisponivel
}

public sealed class ResultadoConsultaOrdemConsumo
{
    public bool Sucesso { get; init; }
    public bool ConsultaPendenteImplementacao { get; init; }
    public CenarioConsultaOrdemConsumo Cenario { get; init; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public OrdemProducaoConsumo? Ordem { get; init; }

    /// <summary>Diagnostico tecnico SANITIZADO (contagens de componentes/motivo). Sem segredo.</summary>
    public string Diagnostico { get; init; } = string.Empty;

    public static ResultadoConsultaOrdemConsumo Falha(string mensagem)
        => new()
        {
            Sucesso = false,
            Cenario = CenarioConsultaOrdemConsumo.OrdemObrigatoria,
            Mensagem = mensagem
        };

    public static ResultadoConsultaOrdemConsumo PendenteImplementacao(string numeroOrdem, string mensagem)
        => new()
        {
            Sucesso = true,
            ConsultaPendenteImplementacao = true,
            NumeroOrdem = numeroOrdem,
            Mensagem = mensagem
        };

    public static ResultadoConsultaOrdemConsumo Carregada(OrdemProducaoConsumo ordem, string mensagem, string diagnostico = "")
        => new()
        {
            Sucesso = true,
            Cenario = CenarioConsultaOrdemConsumo.Carregada,
            NumeroOrdem = ordem.NumeroOrdem,
            Mensagem = mensagem,
            Ordem = ordem,
            Diagnostico = diagnostico
        };

    public static ResultadoConsultaOrdemConsumo NaoLiberada(OrdemProducaoConsumo ordem, string mensagem, string diagnostico = "")
        => new()
        {
            Sucesso = true,
            Cenario = CenarioConsultaOrdemConsumo.NaoLiberada,
            NumeroOrdem = ordem.NumeroOrdem,
            Mensagem = mensagem,
            Ordem = ordem,
            Diagnostico = diagnostico
        };

    public static ResultadoConsultaOrdemConsumo SemComponentes(OrdemProducaoConsumo ordem, string mensagem, string diagnostico = "")
        => new()
        {
            Sucesso = false,
            Cenario = CenarioConsultaOrdemConsumo.SemComponentes,
            NumeroOrdem = ordem.NumeroOrdem,
            Mensagem = mensagem,
            Ordem = ordem,
            Diagnostico = diagnostico
        };

    public static ResultadoConsultaOrdemConsumo NaoEncontrada(string numeroOrdem, string mensagem)
        => new()
        {
            Sucesso = false,
            Cenario = CenarioConsultaOrdemConsumo.NaoEncontrada,
            NumeroOrdem = numeroOrdem,
            Mensagem = mensagem
        };

    public static ResultadoConsultaOrdemConsumo Indisponivel(string numeroOrdem, string mensagem)
        => new()
        {
            Sucesso = false,
            Cenario = CenarioConsultaOrdemConsumo.Indisponivel,
            NumeroOrdem = numeroOrdem,
            Mensagem = mensagem
        };
}
