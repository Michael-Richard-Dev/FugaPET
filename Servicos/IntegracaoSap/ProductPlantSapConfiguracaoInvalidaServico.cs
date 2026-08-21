using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Arquivo de configuracao SAP existente porem malformado. NAO cai em mock nem em fake; a administracao
/// de lote fica indisponivel (retorna null) e o envio e bloqueado, sem expor caminho/conteudo do arquivo.
/// </summary>
internal sealed class ProductPlantSapConfiguracaoInvalidaServico : IProductPlantSapServico
{
    public bool Configurado => false;

    public Task<ProdutoCentroSapMestre?> ObterProdutoCentroAsync(
        string material,
        string centro,
        CancellationToken cancellationToken = default)
        => Task.FromResult<ProdutoCentroSapMestre?>(null);
}
