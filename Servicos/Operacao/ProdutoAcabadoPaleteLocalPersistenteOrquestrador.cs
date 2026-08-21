using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed record ResultadoPaletePersistenciaLocal(bool Sucesso, long? CodigoHuPalete, string Mensagem)
{
    public static ResultadoPaletePersistenciaLocal Ok(long codigoHuPalete)
        => new(true, codigoHuPalete, "Palete criado localmente em RASCUNHO.");

    public static ResultadoPaletePersistenciaLocal Bloqueado(string mensagem)
        => new(false, null, mensagem);
}

/// <summary>
/// Orquestra somente a formação LOCAL persistente do palete: fn_pa_045_palete_criar +
/// fn_pa_045_palete_vincular_caixa. Não executa claim, INT012 ou POST.
/// </summary>
public sealed class ProdutoAcabadoPaleteLocalPersistenteOrquestrador
{
    private readonly IProdutoAcabadoPipeline045Operacoes _ops;

    public ProdutoAcabadoPaleteLocalPersistenteOrquestrador(IProdutoAcabadoPipeline045Operacoes ops)
        => _ops = ops ?? throw new ArgumentNullException(nameof(ops));

    public async Task<ResultadoPaletePersistenciaLocal> CriarAsync(
        ProdutoAcabadoPalete palete,
        long usuario,
        string terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(palete);

        if (string.IsNullOrWhiteSpace(palete.PackagingMaterial))
        {
            return ResultadoPaletePersistenciaLocal.Bloqueado(
                "Material de embalagem do palete não informado. Palete local não criado.");
        }

        long[] codigosCaixas = palete.Caixas
            .OrderBy(caixa => caixa.NumeroCaixa)
            .Select(caixa => caixa.CodigoProdutoAcabadoCaixa.GetValueOrDefault())
            .ToArray();
        if (codigosCaixas.Length == 0 || codigosCaixas.Any(codigo => codigo <= 0))
        {
            return ResultadoPaletePersistenciaLocal.Bloqueado(
                "Todas as caixas do palete precisam estar persistidas antes da formação local.");
        }

        // GATE 046-E §3/§4: criação + TODOS os vínculos em UMA ÚNICA transação (atômica). Sem sucesso parcial:
        // qualquer falha/cancelamento antes do COMMIT ⇒ ROLLBACK integral e retorno fail-closed. Zero claim/INT012/POST.
        ResultadoCriacaoPaleteLocalAtomica atomico = await _ops.CriarPaleteComCaixasAsync(
            palete.PackagingMaterial,
            palete.Plant,
            palete.StorageLocation,
            palete.PesoBrutoKg,
            palete.PesoLiquidoKg,
            palete.TaraKg,
            codigosCaixas,
            usuario,
            terminal,
            cancellationToken);

        return atomico.Sucesso && atomico.CodigoHuPalete is long codigoHuPalete
            ? ResultadoPaletePersistenciaLocal.Ok(codigoHuPalete)
            : ResultadoPaletePersistenciaLocal.Bloqueado(atomico.Motivo);
    }
}
