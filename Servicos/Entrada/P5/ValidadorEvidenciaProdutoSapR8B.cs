namespace FugaPET_Dev.Servicos.Entrada.P5;

internal static class ValidadorEvidenciaProdutoSapR8B
{
    internal static ResultadoConsistenciaProdutoSapR8B VerificarConsistencia(
        IdentidadeItemProdutoSapR8B identidade, ClaimP5 tentativa, ContextoBackendProdutoSapR8B origemEsperada,
        EvidenciaProdutoSapR8B candidata, ReciboEvidenciaProdutoSapR8B? persistida,
        VinculoEvidenciaTentativaP5R8B? vinculo)
    {
        ContextoBackendProdutoSapR8B contexto = candidata.Contexto;
        if (string.IsNullOrWhiteSpace(contexto.Ambiente) || contexto.Ambiente != origemEsperada.Ambiente
            || string.IsNullOrWhiteSpace(contexto.Mandante) || contexto.Mandante != origemEsperada.Mandante
            || contexto.OrigemTecnica != "API_PRODUCT_SRV/A_Product"
            || contexto.OrigemTecnica != origemEsperada.OrigemTecnica
            || string.IsNullOrWhiteSpace(contexto.IdentidadeBackend) || contexto.IdentidadeBackend != origemEsperada.IdentidadeBackend
            || contexto.CorrelacaoColetaBackend == Guid.Empty
            || contexto.ConsultaIniciadaEm == default || contexto.ConsultaConcluidaEm < contexto.ConsultaIniciadaEm)
            return ResultadoConsistenciaProdutoSapR8B.MetadadosInvalidos;
        if (!identidade.Valida || candidata.Campos.Product != identidade.Escopo.Material)
            return ResultadoConsistenciaProdutoSapR8B.IdentidadeDivergente;
        if (candidata.Campos.ProductType is not ("ROH" or "HIBE"))
            return ResultadoConsistenciaProdutoSapR8B.ClassificacaoNaoComprovada;
        if (persistida is null || persistida.RegistradaEmBanco is null
            || string.IsNullOrWhiteSpace(persistida.ReferenciaEvidencia) || persistida.VersaoEvidencia <= 0)
            return ResultadoConsistenciaProdutoSapR8B.NaoPersistida;
        if (persistida.RevogadaEmBanco is not null)
            return ResultadoConsistenciaProdutoSapR8B.Revogada;
        if (persistida.Evidencia.Campos.ProductType != candidata.Campos.ProductType)
            return ResultadoConsistenciaProdutoSapR8B.ClassificacaoAlterada;
        if (persistida.Evidencia != candidata)
            return ResultadoConsistenciaProdutoSapR8B.EvidenciaDivergente;
        if (vinculo?.VinculadaEmBanco is null)
            return ResultadoConsistenciaProdutoSapR8B.SemVinculo;
        if (vinculo.Identidade != identidade)
            return ResultadoConsistenciaProdutoSapR8B.IdentidadeDivergente;
        if (!MesmaTentativa(tentativa, vinculo.Tentativa) || tentativa.Escopo != identidade.Escopo)
            return ResultadoConsistenciaProdutoSapR8B.TentativaDivergente;
        if (vinculo.ProductType != candidata.Campos.ProductType)
            return ResultadoConsistenciaProdutoSapR8B.ClassificacaoAlterada;
        if (vinculo.ReferenciaEvidencia != persistida.ReferenciaEvidencia
            || vinculo.VersaoEvidencia != persistida.VersaoEvidencia
            || vinculo.CorrelacaoColetaBackend != contexto.CorrelacaoColetaBackend)
            return ResultadoConsistenciaProdutoSapR8B.EvidenciaDivergente;
        return ResultadoConsistenciaProdutoSapR8B.CoerenteSemAutoridade;
    }

    private static bool MesmaTentativa(ClaimP5 esperada, ClaimP5 vinculada)
        => esperada.Valido && vinculada.Valido && esperada.VersaoGuarda > 0 && esperada.GeracaoRecovery >= 0
            && esperada.Escopo == vinculada.Escopo && esperada.Tentativa == vinculada.Tentativa
            && esperada.Token == vinculada.Token && esperada.Correlacao == vinculada.Correlacao
            && esperada.VersaoGuarda == vinculada.VersaoGuarda && esperada.GeracaoRecovery == vinculada.GeracaoRecovery;
}
