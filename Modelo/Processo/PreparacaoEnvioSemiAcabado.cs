using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// GATE 104C-D: estado intermediário do envio 101 do semi-acabado APÓS a identidade durável
/// (codigo_semi_acabado_lancamento persistido ou recuperado) e a validação de OP/item/status, e ANTES do
/// armamento da capability, do claim (ENVIANDO_SAP) e do writer. Existe para que a escrita SAP só seja
/// armada depois que a PK durável já existe. Em falha, carrega o <see cref="ResultadoEnvioSemiAcabadoSap"/>
/// correspondente (sem armar nada, sem HTTP).
/// </summary>
public sealed class PreparacaoEnvioSemiAcabado
{
    private PreparacaoEnvioSemiAcabado(
        bool sucesso,
        long codigoLancamento,
        LancamentoSemiAcabado? lancamentoPersistido,
        MaterialDocumentSapRequest? requisicao,
        ResultadoEnvioSemiAcabadoSap? falha)
    {
        Sucesso = sucesso;
        CodigoLancamento = codigoLancamento;
        LancamentoPersistido = lancamentoPersistido;
        Requisicao = requisicao;
        Falha = falha;
    }

    /// <summary>Preparação concluída: identidade durável obtida e requisição 101 montada. Pronta para armar+enviar.</summary>
    public bool Sucesso { get; }

    /// <summary>codigo_semi_acabado_lancamento persistido/recuperado (&gt; 0 quando houve identidade durável).</summary>
    public long CodigoLancamento { get; }

    internal LancamentoSemiAcabado? LancamentoPersistido { get; }
    internal MaterialDocumentSapRequest? Requisicao { get; }

    /// <summary>Resultado de falha (estrutura/inconsistência/duplicidade/…) quando <see cref="Sucesso"/> é falso.</summary>
    public ResultadoEnvioSemiAcabadoSap? Falha { get; }

    internal static PreparacaoEnvioSemiAcabado Ok(
        long codigoLancamento,
        LancamentoSemiAcabado lancamentoPersistido,
        MaterialDocumentSapRequest requisicao)
        => new(true, codigoLancamento, lancamentoPersistido, requisicao, null);

    public static PreparacaoEnvioSemiAcabado ComFalha(ResultadoEnvioSemiAcabadoSap falha)
    {
        ArgumentNullException.ThrowIfNull(falha);
        return new PreparacaoEnvioSemiAcabado(false, falha.CodigoLancamento ?? 0, null, null, falha);
    }
}
