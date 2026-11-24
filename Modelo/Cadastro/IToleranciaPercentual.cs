namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// O MÍNIMO que a regra de tolerância precisa saber: o percentual e se ele está valendo.
/// <para>
/// Existe para que <c>RegraTolerancia</c> sirva ao Recebimento (069) e aos processos de
/// produção (071) com UMA matemática só. A alternativa — copiar a conta para o novo cadastro — é
/// exatamente o erro que custou o dia 24/11/2026: uma trava que existia em um caminho e não no
/// outro. Duas cópias da mesma regra divergem; uma só, não.
/// </para>
/// <para>
/// Deliberadamente não inclui material, centro nem processo: a conta não depende de QUEM está sendo
/// avaliado, só de quanto se pode divergir. Quem decide a quem a tolerância pertence é quem a busca.
/// </para>
/// </summary>
public interface IToleranciaPercentual
{
    /// <summary>Percentual único, válido para mais E para menos. Zero = exigência exata.</summary>
    decimal PercentualTolerancia { get; }

    /// <summary>false = inativa. Quem avalia trata inativa como NÃO CADASTRADA, não como "sem limite".</summary>
    bool Situacao { get; }
}
