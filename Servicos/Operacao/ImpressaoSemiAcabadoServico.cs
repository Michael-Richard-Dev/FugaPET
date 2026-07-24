using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class ImpressaoSemiAcabadoServico
{
    private const int TamanhoMaximoCodigoEtiqueta = 60;

    private readonly ImpressoraEtiquetaServico _impressoraEtiquetaServico;

    public ImpressaoSemiAcabadoServico()
        : this(new ImpressoraEtiquetaServico())
    {
    }

    public ImpressaoSemiAcabadoServico(ImpressoraEtiquetaServico impressoraEtiquetaServico)
    {
        _impressoraEtiquetaServico = impressoraEtiquetaServico;
    }

    public Task ImprimirNovaPesagemAsync(
        SemiAcabadoOrdem ordem,
        PesagemSemiAcabado pesagem)
        => _impressoraEtiquetaServico.ImprimirEtiquetaProducaoAsync(
            CriarEtiqueta(ordem, pesagem, pesagem.SaldoAposPesagemKg));

    public Task ReimprimirPesagemAsync(
        SemiAcabadoOrdem ordem,
        PesagemSemiAcabado pesagem)
    {
        if (string.IsNullOrWhiteSpace(pesagem.CodigoEtiqueta))
        {
            System.Diagnostics.Trace.TraceWarning("[ImpressaoSemiAcabado] Reimpressão bloqueada: CodigoEtiqueta ausente.");
            throw new ErroOperacionalEsperadoException(
                "A pesagem não possui código de etiqueta persistido. Reimpressão bloqueada por inconsistência de rastreabilidade.");
        }

        return _impressoraEtiquetaServico.ReimprimirEtiquetaProducaoAsync(
            CriarEtiqueta(ordem, pesagem, pesagem.SaldoAposPesagemKg));
    }

    public static string GerarCodigoEtiqueta(SemiAcabadoOrdem ordem)
    {
        string op = Sanitizar(ordem.NumeroOrdem);
        string item = Sanitizar(ordem.ItemOrdem);
        string unico = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string codigo = string.IsNullOrEmpty(item)
            ? $"SA-{op}-{unico}"
            : $"SA-{op}-{item}-{unico}";
        return codigo.Length <= TamanhoMaximoCodigoEtiqueta ? codigo : codigo[..TamanhoMaximoCodigoEtiqueta];
    }

    public static decimal CalcularSaldoRestanteAtePesagem(
        SemiAcabadoOrdem ordem,
        PesagemSemiAcabado pesagem,
        IReadOnlyList<PesagemSemiAcabado> contexto)
    {
        decimal acumulado = contexto
            .Where(PesagemSemiAcabadoCalculos.PesagemValida)
            .Where(item => item.Sequencia <= pesagem.Sequencia)
            .Sum(item => item.PesoLiquidoKg);
        return Math.Max(ordem.QuantidadePendente - acumulado, 0m);
    }

    private static DadosEtiquetaProducao CriarEtiqueta(
        SemiAcabadoOrdem ordem,
        PesagemSemiAcabado pesagem,
        decimal saldoRestante)
        => new()
        {
            OrdemProducao = ordem.NumeroOrdem,
            CodigoProducao = pesagem.CodigoEtiqueta,
            CodigoProduto = ordem.MaterialProduzido,
            DescricaoProduto = ordem.DescricaoMaterial,
            Lote = ordem.Lote,
            Quantidade = "1",
            Peso = $"{pesagem.PesoLiquidoKg:0.###} {(string.IsNullOrWhiteSpace(ordem.Unidade) ? "KG" : ordem.Unidade)}",
            Saldo = $"{Math.Max(saldoRestante, 0m):0.###} {(string.IsNullOrWhiteSpace(ordem.Unidade) ? "KG" : ordem.Unidade)}",
            DataClassificacao = pesagem.RegistradoEm.ToString("dd/MM/yyyy HH:mm")
        };

    private static string Sanitizar(string? valor)
        => new((valor ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
}
