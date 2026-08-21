using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Abstração injetável do GET (somente leitura) da norma/estrutura de embalagem do Produto Acabado
/// (contrato INT012). Nunca executa POST/PATCH. Nunca lança por erro de rede/config: devolve um
/// <see cref="ResultadoConsultaNormaEmbalagemSap"/> TIPADO (cenário + diagnóstico sanitizado), nunca só
/// null. A Form NUNCA acessa HttpClient diretamente — passa por aqui.
/// </summary>
public interface IProdutoAcabadoNormaEmbalagemSapServico
{
    bool Configurado { get; }

    Task<ResultadoConsultaNormaEmbalagemSap> ObterNormaAsync(
        string material,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação para quando a API da norma não está configurada (ou ambiente demonstrativo/inválido):
/// não faz HTTP e devolve o cenário NaoConfigurada. O controller traduz para "NAO CONFIGURADA" e bloqueia
/// o preview HU (sem fallback fictício).
/// </summary>
public sealed class ProdutoAcabadoNormaEmbalagemSapNaoConfiguradoServico : IProdutoAcabadoNormaEmbalagemSapServico
{
    public bool Configurado => false;

    public Task<ResultadoConsultaNormaEmbalagemSap> ObterNormaAsync(
        string material,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoConsultaNormaEmbalagemSap.DeNaoConfigurada());
}
