namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ResultadoValidacaoLoteEntrada
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public DadosLoteEntrada? DadosNormalizados { get; init; }
    public EntradaProdutoLoteEmMemoria? LoteExistente { get; init; }

    public static ResultadoValidacaoLoteEntrada Aprovado(
        DadosLoteEntrada dados,
        string mensagem = "Lote validado.",
        EntradaProdutoLoteEmMemoria? loteExistente = null)
        => new()
        {
            Sucesso = true,
            Mensagem = mensagem,
            DadosNormalizados = dados,
            LoteExistente = loteExistente
        };

    public static ResultadoValidacaoLoteEntrada Reprovado(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}
