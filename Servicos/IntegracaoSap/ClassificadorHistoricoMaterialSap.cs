using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE R6: converte os movimentos lidos (ja restritos ao item do pedido) no resultado de historico
/// consumido pelo motor P5. Funcao PURA.
/// <para>
/// REGRAS:
/// 1. Documento sem tipo/identidade ⇒ INCERTO.
/// 2. Estorno 102 SEM vinculo documental, ou com vinculo que NAO aponta para um 101 observado,
///    ⇒ INCERTO. Correspondencia de estorno NUNCA e presumida.
/// 3. Material/centro divergentes do esperado ⇒ INCERTO (nao se descarta silenciosamente um
///    movimento que pode ser do mesmo item).
/// 4. <see cref="ResultadoHistoricoMaterialSap.AbrangenciaComprovada"/> depende de ATESTACAO externa:
///    a consulta por si NAO prova cobertura, e zero documentos jamais implica P5=FALSE.
/// </para>
/// </summary>
public static class ClassificadorHistoricoMaterialSap
{
    public static ResultadoHistoricoMaterialSap Classificar(
        IReadOnlyList<MovimentoHistoricoMaterial> movimentos,
        bool paginacaoCompleta,
        bool abrangenciaComprovada,
        string? materialEsperado = null,
        string? centroEsperado = null)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        bool incerto = false;
        List<MovimentoHistoricoMaterial> entradas101 = [];
        List<MovimentoHistoricoMaterial> estornos102 = [];

        foreach (MovimentoHistoricoMaterial movimento in movimentos)
        {
            if (movimento.EhIncerto)
            {
                incerto = true;
                continue;
            }

            if (!CampoCompativel(movimento.Material, materialEsperado)
                || !CampoCompativel(movimento.Centro, centroEsperado))
            {
                // Divergencia de material/centro para o MESMO item do pedido e anomalia, nao descarte.
                incerto = true;
                continue;
            }

            if (movimento.EhEntrada101)
            {
                entradas101.Add(movimento);
                continue;
            }

            if (movimento.EhEstorno102)
            {
                estornos102.Add(movimento);
                continue;
            }

            // Outro tipo de movimento no mesmo item (cancelamento/outros): nao classificavel aqui.
            incerto = true;
        }

        // Estorno so conta quando aponta DOCUMENTALMENTE para um 101 observado.
        HashSet<string> documentos101 = new(
            entradas101.Select(e => e.MaterialDocument), StringComparer.Ordinal);

        int estornosCorrelacionados = 0;
        foreach (MovimentoHistoricoMaterial estorno in estornos102)
        {
            if (!estorno.EstornoComVinculoDocumental
                || !documentos101.Contains(estorno.EstornaMaterialDocument!.Trim()))
            {
                incerto = true;
                continue;
            }

            estornosCorrelacionados++;
        }

        return ResultadoHistoricoMaterialSap.Lido(
            documentos101: entradas101.Count,
            movimentos102: estornosCorrelacionados,
            paginacaoCompleta: paginacaoCompleta,
            abrangenciaComprovada: abrangenciaComprovada,
            temDocumentoIncerto: incerto);
    }

    /// <summary>
    /// Compara um campo do movimento com o esperado. Esperado vazio ⇒ nao valida (nada a comparar).
    /// Campo AUSENTE no documento com esperado informado ⇒ incompativel (nunca "passa" por omissao).
    /// </summary>
    private static bool CampoCompativel(string? doMovimento, string? esperado)
    {
        if (string.IsNullOrWhiteSpace(esperado))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(doMovimento)
               && string.Equals(doMovimento.Trim(), esperado.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
