using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composicao unica do catalogo de produtos do centro, no mesmo desenho das outras fabricas SAP:
/// demonstracao/banco desligado → serie vazia declarada; configuracao malformada → estado invalido
/// declarado; caso contrario → servico real.
/// <para>
/// Nenhum dos tres devolve lista vazia "de sucesso" quando nao pode perguntar. Essa e a razao de a
/// fabrica existir: a tela recebe sempre um resultado que sabe dizer POR QUE esta vazio.
/// </para>
/// </summary>
public static class FabricaCatalogoProdutoCentroSapServico
{
    public static ICatalogoProdutoCentroSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static ICatalogoProdutoCentroSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static ICatalogoProdutoCentroSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        bool podeUsarDadosSimulados = EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
            bancoHabilitado, modoDemonstracao, ambienteDemonstrativo);

        if (podeUsarDadosSimulados)
        {
            return new CatalogoProdutoCentroSapIndisponivelServico(
                "Ambiente de demonstração: o catálogo de produtos do SAP não é consultado.");
        }

        try
        {
            return new CatalogoProdutoCentroSapServico(carregarConfiguracaoSap());
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            CatalogoProdutoCentroSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Catalogo bloqueado.");
            return new CatalogoProdutoCentroSapIndisponivelServico(
                "Configuração SAP inválida nesta estação: o catálogo de produtos não pode ser consultado.");
        }
    }
}

/// <summary>
/// Catalogo que nao consulta nada e diz por que. Usado em demonstracao e quando a configuracao SAP
/// esta malformada — nos dois casos, devolver lista vazia sem motivo faria a tela parecer um centro
/// sem produtos.
/// </summary>
internal sealed class CatalogoProdutoCentroSapIndisponivelServico : ICatalogoProdutoCentroSapServico
{
    private readonly string _motivo;

    public CatalogoProdutoCentroSapIndisponivelServico(string motivo) => _motivo = motivo;

    public bool Configurado => false;

    public Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
        string centro, CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoCatalogoProdutoCentro.Falha(_motivo));

    public Task<ResultadoCodigosCentro> ListarCodigosDoCentroAsync(
        string centro, CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoCodigosCentro.Falha(_motivo, MotivoCatalogoIndisponivel.NaoConfigurado));

    public Task<ResultadoCadastroProdutos> ObterCadastroAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoCadastroProdutos.Falha(_motivo));
}
