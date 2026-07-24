using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Controle.Processo;

/// <summary>
/// Controller do Controle de Apontamentos. A View NUNCA consulta SAP nem PostgreSQL diretamente:
/// tudo passa por aqui e é delegado ao <see cref="ProcessoControleApontamentosServico"/>.
/// </summary>
public sealed class ProcessoControleApontamentosController
{
    private readonly ProcessoControleApontamentosServico _servico;
    private readonly IControleApontamentosAutorizacaoServico _autorizacao;

    public ProcessoControleApontamentosController()
        : this(new ProcessoControleApontamentosServico(), new ControleApontamentosAutorizacaoServico())
    {
    }

    internal ProcessoControleApontamentosController(
        ProcessoControleApontamentosServico servico,
        IControleApontamentosAutorizacaoServico? autorizacao = null)
    {
        _servico = servico ?? throw new ArgumentNullException(nameof(servico));
        _autorizacao = autorizacao ?? new ControleApontamentosAutorizacaoServico();
    }

    /// <summary>
    /// Governa a ABERTURA da tela: exige CONTROLE_APONTAMENTOS/VISUALIZAR quando o 039 está aplicado;
    /// enquanto não estiver, aceita o fallback transitório de LEITURA_PRODUCAO/VISUALIZAR.
    /// </summary>
    public Task<bool> PodeVisualizarAsync(CancellationToken cancellationToken = default)
        => _autorizacao.PodeVisualizarAsync(cancellationToken);

    /// <summary>Decisão explícita sobre o pacote 039 estar aplicado (mesma usada pela autorização).</summary>
    public Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
        => _autorizacao.EstruturaControleApontamentosDisponivelAsync(cancellationToken);

    /// <summary>Interpreta o código sem tocar SAP/banco (eco imediato no painel lateral).</summary>
    public CodigoBarrasOperacao Interpretar(string? codigoLido)
        => _servico.Interpretar(codigoLido);

    /// <summary>
    /// Processa a leitura completa (interpretar → OP → operação → sequência → destino → início/retomada/término).
    /// <paramref name="confirmar"/> é a confirmação do operador, executada ANTES de efetivar o estado.
    /// </summary>
    public Task<ResultadoLeituraApontamento> ProcessarLeituraAsync(
        string? codigoLido,
        string usuario,
        string estacao,
        Func<ConfirmacaoApontamento, bool> confirmar,
        CancellationToken cancellationToken = default)
        => _servico.ProcessarLeituraAsync(codigoLido, usuario, estacao, confirmar, cancellationToken);

    /// <summary>
    /// Tela operacional concluiu a atividade: EM_ANDAMENTO → AGUARDANDO_FINALIZACAO, persistindo o
    /// vínculo com o registro criado (ex.: codigo_lancamento do Consumo). False quando não avançou.
    /// </summary>
    public Task<bool> RegistrarConclusaoOperacionalAsync(
        long codigoApontamento,
        ResultadoExecucaoProcesso resultado,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        CancellationToken cancellationToken = default)
        => _servico.RegistrarConclusaoOperacionalAsync(
            codigoApontamento, resultado, usuario, estacao, codigo, cancellationToken);
}
