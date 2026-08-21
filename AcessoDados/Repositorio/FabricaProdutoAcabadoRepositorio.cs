using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Composição do repositório de Produto Acabado (HU). Banco habilitado e não demonstrativo → repositório
/// PostgreSQL real (<see cref="ProdutoAcabadoRepositorio"/>) usando a conexão configurada da aplicação;
/// caso contrário → fail-closed (<see cref="ProdutoAcabadoRepositorioIndisponivel"/>, não finge banco).
/// </summary>
public static class FabricaProdutoAcabadoRepositorio
{
    public static IProdutoAcabadoRepositorio Criar()
    {
        if (EstadoIntegracaoBanco.PodeUsarDadosSimulados || !EstadoIntegracaoBanco.Habilitado)
        {
            return new ProdutoAcabadoRepositorioIndisponivel();
        }

        ConfiguracaoBancoPostgreSql configuracao = LeitorConfiguracaoBancoPostgreSql.Carregar();
        // §6: reporta (não resolve) quando o runtime não usa a role da aplicação fugapet_dev_app.
        // NUNCA aplica SET ROLE em código produtivo — isso é exclusivo dos testes funcionais.
        DiagnosticoRuntimeBanco.AvisarSeRuntimeNaoAprovado(configuracao);
        return new ProdutoAcabadoRepositorio(new FabricaConexaoPostgreSql(configuracao));
    }
}
