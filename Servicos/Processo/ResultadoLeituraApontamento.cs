using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>Cenários FUNCIONAIS da leitura. Erro operacional é resultado, não exceção.</summary>
public enum CenarioLeituraApontamento
{
    CodigoInvalido,
    SemSessaoUsuario,

    /// <summary>Usuário sem a ação própria (INICIAR/FINALIZAR) da rotina CONTROLE_APONTAMENTOS.</summary>
    SemPermissaoAcao,

    OrdemNaoEncontrada,
    OrdemNaoLiberada,
    FalhaMapeamentoOperacoesSap,
    OperacaoNaoEncontrada,
    OperacaoAmbigua,
    ConfiguracaoAmbigua,
    EstruturaNaoAplicada,
    MapeamentoNaoConfigurado,

    /// <summary>A operação anterior obrigatória (ExigeOperacaoAnterior) ainda não está CONCLUIDA.</summary>
    OperacaoAnteriorNaoConcluida,

    InicioDuplicado,

    /// <summary>Operação já EM_ANDAMENTO pelo MESMO usuário: pode retomar (reabre a tela de destino).</summary>
    RetomadaDisponivel,

    /// <summary>Operação já EM_ANDAMENTO por OUTRO usuário: bloqueado.</summary>
    OperacaoEmAndamentoPorOutroUsuario,

    /// <summary>Atividade já concluída: não reabre; oriente a ler o código de término.</summary>
    OperacaoJaAguardandoTermino,

    TerminoSemInicio,
    TerminoDuplicado,
    TerminoNaoLiberado,
    TerminoAmbiguo,
    ConfirmacaoPendente,
    SucessoInicio,
    SucessoTermino,
    FalhaConsultaSap
}

/// <summary>
/// Resultado funcional da leitura. Carrega o suficiente para a View exibir e navegar, sem consultar
/// SAP/banco por conta própria.
/// </summary>
public sealed class ResultadoLeituraApontamento
{
    public CenarioLeituraApontamento Cenario { get; init; }
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>Código interpretado (sempre preserva o original), mesmo quando inválido.</summary>
    public CodigoBarrasOperacao? Codigo { get; init; }

    /// <summary>OP consultada no SAP, quando houve consulta bem-sucedida (alimenta o grid).</summary>
    public OrdemProducaoSap? Ordem { get; init; }

    /// <summary>Operação resolvida sem ambiguidade, quando aplicável.</summary>
    public OperacaoOrdemProducaoSap? Operacao { get; init; }

    public ConfiguracaoOperacaoProcesso? Configuracao { get; init; }

    /// <summary>Apontamento envolvido (início criado, retomado, ou o ativo localizado no término).</summary>
    public OperacaoProducaoApontamento? Apontamento { get; init; }

    /// <summary>Contexto para abrir a tela operacional (SucessoInicio e RetomadaDisponivel).</summary>
    public ContextoApontamentoProcesso? Contexto { get; init; }

    /// <summary>Estado completo da OP para o grid: todos os apontamentos persistidos.</summary>
    public IReadOnlyList<OperacaoProducaoApontamento> ApontamentosDaOrdem { get; init; } = [];

    /// <summary>Configurações ativas da OP (tipo de processo/tela por operação, no grid inteiro).</summary>
    public IReadOnlyList<ConfiguracaoOperacaoProcesso> ConfiguracoesDaOrdem { get; init; } = [];

    public bool Sucesso => Cenario is CenarioLeituraApontamento.SucessoInicio
        or CenarioLeituraApontamento.SucessoTermino;

    /// <summary>True quando a tela deve abrir o destino operacional (início novo ou retomada).</summary>
    public bool DeveAbrirDestino => Contexto is not null
        && Cenario is CenarioLeituraApontamento.SucessoInicio or CenarioLeituraApontamento.RetomadaDisponivel;

    public static ResultadoLeituraApontamento Falha(
        CenarioLeituraApontamento cenario,
        string mensagem,
        CodigoBarrasOperacao? codigo = null,
        OrdemProducaoSap? ordem = null)
        => new() { Cenario = cenario, Mensagem = mensagem, Codigo = codigo, Ordem = ordem };
}
