namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Resultado da leitura de peso da balanca. Evita exceptions para fluxos esperados
/// (balanca nao configurada/encontrada) — a tela so checa Sucesso e exibe Mensagem.
/// </summary>
public sealed record ResultadoLeituraPeso(bool Sucesso, string Peso, string Mensagem)
{
    public static ResultadoLeituraPeso Ok(string peso) => new(true, peso, string.Empty);

    public static ResultadoLeituraPeso Falha(string mensagem) => new(false, string.Empty, mensagem);
}
