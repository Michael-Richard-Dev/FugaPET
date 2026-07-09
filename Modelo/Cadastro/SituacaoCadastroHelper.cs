namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// Tarefa Tipo de Tara (Ajuste 1): interpretação EXPLÍCITA da situação selecionada na tela
/// ("Ativo"/"Inativo"). Texto vazio/inválido NÃO é convertido silenciosamente para Inativo —
/// retorna false para que a tela bloqueie e peça uma situação válida.
/// </summary>
public static class SituacaoCadastroHelper
{
    public const string Ativo = "Ativo";
    public const string Inativo = "Inativo";

    /// <summary>
    /// True quando o texto é exatamente "Ativo" ou "Inativo" (ignorando caixa/espaços). Em <paramref name="ativo"/>
    /// devolve true para "Ativo" e false para "Inativo". Retorna false (e ativo=false) para vazio/desconhecido.
    /// </summary>
    public static bool TryInterpretarSituacao(string? texto, out bool ativo)
    {
        string valor = (texto ?? string.Empty).Trim();

        if (string.Equals(valor, Ativo, StringComparison.OrdinalIgnoreCase))
        {
            ativo = true;
            return true;
        }

        if (string.Equals(valor, Inativo, StringComparison.OrdinalIgnoreCase))
        {
            ativo = false;
            return true;
        }

        ativo = false;
        return false;
    }

    /// <summary>Ajuste 4: rótulo de referência de taras vinculadas ("0 taras", "1 tara", "5 taras").</summary>
    public static string FormatarReferenciaTaras(int quantidade)
        => quantidade == 1 ? "1 tara" : $"{Math.Max(0, quantidade)} taras";
}
