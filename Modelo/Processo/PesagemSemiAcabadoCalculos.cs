namespace FugaPET_Dev.Modelo.Processo;

public static class PesagemSemiAcabadoCalculos
{
    public const string StatusValida = "VALIDA";
    public const string StatusCancelada = "CANCELADA";

    public static bool PesagemValida(PesagemSemiAcabado? pesagem)
        => pesagem is not null
            && !string.Equals(pesagem.StatusPesagem, StatusCancelada, StringComparison.OrdinalIgnoreCase);

    public static decimal SomarPesoLiquidoValido(IEnumerable<PesagemSemiAcabado> pesagens)
        => pesagens.Where(PesagemValida).Sum(pesagem => pesagem.PesoLiquidoKg);

    public static int ContarValidas(IEnumerable<PesagemSemiAcabado> pesagens)
        => pesagens.Count(PesagemValida);

    public static PesagemSemiAcabado? UltimaValida(IEnumerable<PesagemSemiAcabado> pesagens)
        => pesagens.LastOrDefault(PesagemValida);
}
