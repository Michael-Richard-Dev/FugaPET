using FugaPET_Dev.Modelo.Consumo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia LOCAL do consumo de materia-prima (consumo_material_lancamento / _item / _pesagem).
/// Sem envio SAP. O salvamento e atomico (cabecalho + itens + pesagens em uma transacao).
/// </summary>
public interface IConsumoMaterialRepositorio
{
    /// <summary>Grava o consumo completo em UMA transacao (rollback em erro). Retorna o codigo do lancamento.</summary>
    Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default);

    Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
        ConsultaConsumoMaterialFiltro filtro,
        CancellationToken cancellationToken = default);

    Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CLAIM atomico do envio: tenta mover o lancamento (e itens) de PENDENTE_SAP para ENVIANDO_SAP via
    /// UPDATE condicional. Retorna true SOMENTE se reservou (rowsAffected == 1). Evita envio 261 duplicado
    /// entre instancias/sessoes concorrentes.
    /// </summary>
    Task<bool> TentarReservarEnvioSapAsync(
        long codigoLancamento,
        DateTime reservadoEmUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Marca FALHA_SAP (lancamento + itens) somente quando estiverem ENVIANDO_SAP (apos reserva).</summary>
    Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca o consumo como CONFIRMADO_SAP (lancamento + itens) gravando documento/exercicio/enviado_em,
    /// em UMA transacao, SOMENTE quando estiver ENVIANDO_SAP e sem documento. Lanca se nao atualizar
    /// exatamente uma linha (concorrencia/estado inesperado).
    /// </summary>
    Task MarcarConsumoConfirmadoSapAsync(
        long codigoLancamento,
        string? documentoMaterialSap,
        string? exercicioDocumentoMaterialSap,
        DateTime enviadoSapEmUtc,
        CancellationToken cancellationToken = default);
}
