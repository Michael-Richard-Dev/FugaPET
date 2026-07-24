namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Contrato passado da tela de Controle de Apontamentos para a tela operacional de destino
/// (Consumo, Semiacabado, Produto Acabado…). A tela de destino abre JÁ com esta OP e não pode
/// trocar para outra; o fechamento da tela NÃO conclui o apontamento (só a leitura do término conclui).
/// </summary>
public sealed class ContextoApontamentoProcesso
{
    public long CodigoApontamento { get; init; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string ItemOrdem { get; init; } = string.Empty;
    public string Produto { get; init; } = string.Empty;
    public string Sequencia { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string Suboperacao { get; init; } = string.Empty;
    public string DescricaoOperacao { get; init; } = string.Empty;
    public string CentroTrabalho { get; init; } = string.Empty;

    /// <summary>Tipo de processo configurado (CONSUMO_MATERIA_PRIMA, CONSUMO_QUIMICOS, …).</summary>
    public string TipoProcesso { get; init; } = string.Empty;

    public string Usuario { get; init; } = string.Empty;
    public string Estacao { get; init; } = string.Empty;
    public DateTime IniciadoEm { get; init; }
    public string CodigoBarrasInicio { get; init; } = string.Empty;
}

/// <summary>
/// Resultado devolvido pela tela operacional ao Controle de Apontamentos. Fechar a tela sem concluir
/// a atividade devolve <see cref="NaoConcluido"/> — o apontamento permanece EM_ANDAMENTO.
/// </summary>
public enum ResultadoExecucaoProcessoApontamento
{
    /// <summary>Tela fechada sem concluir a atividade: NÃO libera término.</summary>
    NaoConcluido = 0,

    /// <summary>Atividade concluída localmente: apontamento vai para AGUARDANDO_FINALIZACAO.</summary>
    ConcluidoLocalmente = 1,

    /// <summary>Concluída e confirmada no SAP: libera término.</summary>
    ConfirmadoSap = 2,

    /// <summary>Erro SAP: não libera término; estado controlado, causa exibida.</summary>
    ErroSap = 3,

    /// <summary>Divergência SAP: não libera término; exige verificação/suporte.</summary>
    DivergenciaSap = 4,

    Cancelado = 5
}

/// <summary>
/// Resultado COMPLETO da tela operacional. Substitui o retorno baseado só em enum: carrega o vínculo
/// com o registro criado pelo processo (ex.: codigo_lancamento do Consumo) para que o apontamento possa
/// ser reconstruído após reinício. O vínculo é OPCIONAL — aberturas manuais (sem apontamento) não o usam.
/// </summary>
public sealed class ResultadoExecucaoProcesso
{
    public static ResultadoExecucaoProcesso NaoConcluido { get; } =
        new(ResultadoExecucaoProcessoApontamento.NaoConcluido, null, string.Empty, false);

    public ResultadoExecucaoProcesso(
        ResultadoExecucaoProcessoApontamento resultado,
        long? codigoRegistroProcesso,
        string mensagem,
        bool indicadorConfirmadoSap)
    {
        Resultado = resultado;
        CodigoRegistroProcesso = codigoRegistroProcesso;
        Mensagem = mensagem ?? string.Empty;
        IndicadorConfirmadoSap = indicadorConfirmadoSap;
    }

    public ResultadoExecucaoProcessoApontamento Resultado { get; }

    /// <summary>Código do registro criado pelo processo (Consumo: codigo_lancamento). Null quando não houve.</summary>
    public long? CodigoRegistroProcesso { get; }

    public string Mensagem { get; }
    public bool IndicadorConfirmadoSap { get; }

    /// <summary>Só estes dois resultados liberam a transição EM_ANDAMENTO → AGUARDANDO_FINALIZACAO.</summary>
    public bool AtividadeConcluida => Resultado is ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente
        or ResultadoExecucaoProcessoApontamento.ConfirmadoSap;
}
