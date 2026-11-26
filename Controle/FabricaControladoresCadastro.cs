using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Controle;

public static class FabricaControladoresCadastro
{
    private static readonly Lazy<IFabricaConexaoBanco> FabricaConexao = new(() =>
        new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar()));

    public static AuditoriaServico CriarAuditoriaServico()
    {
        AuditoriaAcaoUsuarioRepositorio repositorio = new(FabricaConexao.Value);
        AuditoriaAcaoUsuarioServico acaoUsuarioServico = new(repositorio);
        return new AuditoriaServico(acaoUsuarioServico);
    }

    public static SetorController CriarSetorController()
    {
        SetorRepositorio repositorio = new(FabricaConexao.Value);
        SetorServico servico = new(repositorio, CriarAuditoriaServico());
        return new SetorController(servico);
    }

    public static CargoController CriarCargoController()
    {
        CargoRepositorio repositorio = new(FabricaConexao.Value);
        CargoServico servico = new(repositorio, CriarAuditoriaServico());
        return new CargoController(servico);
    }

    public static TipoTaraController CriarTipoTaraController()
    {
        TipoTaraRepositorio repositorio = new(FabricaConexao.Value);
        TipoTaraServico servico = new(repositorio, CriarAuditoriaServico());
        return new TipoTaraController(servico);
    }

    public static PerfilAcessoController CriarPerfilAcessoController()
    {
        PerfilAcessoRepositorio repositorio = new(FabricaConexao.Value);
        PerfilAcessoServico servico = new(repositorio, CriarAuditoriaServico());
        return new PerfilAcessoController(servico);
    }

    public static UsuarioController CriarUsuarioController()
    {
        UsuarioRepositorio usuarioRepositorio = new(FabricaConexao.Value);
        UsuarioPerfilRepositorio usuarioPerfilRepositorio = new(FabricaConexao.Value);
        UsuarioSetorRepositorio usuarioSetorRepositorio = new(FabricaConexao.Value);

        AuditoriaServico auditoriaServico = CriarAuditoriaServico();
        UsuarioServico usuarioServico = new(usuarioRepositorio, new SenhaServico(), auditoriaServico);
        UsuarioPerfilServico usuarioPerfilServico = new(usuarioPerfilRepositorio, auditoriaServico);
        UsuarioSetorServico usuarioSetorServico = new(usuarioSetorRepositorio, auditoriaServico);

        return new UsuarioController(usuarioServico, usuarioPerfilServico, usuarioSetorServico);
    }

    public static BalancaController CriarBalancaController()
    {
        BalancaRepositorio repositorio = new(FabricaConexao.Value);
        BalancaServico servico = new(repositorio, CriarAuditoriaServico());
        return new BalancaController(servico);
    }

    public static TaraController CriarTaraController()
    {
        TaraRepositorio repositorio = new(FabricaConexao.Value);
        TaraServico servico = new(repositorio, CriarAuditoriaServico());
        return new TaraController(servico);
    }

    public static ModeloEtiquetaController CriarModeloEtiquetaController()
    {
        ModeloEtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        ModeloEtiquetaServico servico = new(repositorio, CriarAuditoriaServico());
        return new ModeloEtiquetaController(servico);
    }

    public static EtiquetaController CriarEtiquetaController()
    {
        EtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        ModeloEtiquetaRepositorio modeloRepositorio = new(FabricaConexao.Value);
        EtiquetaServico servico = new(repositorio, modeloRepositorio, CriarAuditoriaServico());
        return new EtiquetaController(servico);
    }

    /// <summary>
    /// Vinculo produto × etiqueta × processo. A composicao do catalogo SAP passa pela fabrica
    /// propria dele, que e quem sabe escolher entre servico real, mock de demonstracao e estado de
    /// configuracao invalida — a tela nunca decide isso.
    /// </summary>
    public static VinculoProdutoEtiquetaController CriarVinculoProdutoEtiquetaController()
    {
        VinculoProdutoEtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        EtiquetaRepositorio etiquetaRepositorio = new(FabricaConexao.Value);
        CatalogoProdutoCentroRepositorio catalogoRepositorio = new(FabricaConexao.Value);

        // A tela le do catalogo LOCAL; o SAP so e consultado pela sincronizacao, que o botao aciona.
        SincronizacaoCatalogoProdutoServico sincronizacao = new(
            catalogoRepositorio,
            FabricaCatalogoProdutoCentroSapServico.Criar());

        VinculoProdutoEtiquetaServico servico = new(
            repositorio,
            etiquetaRepositorio,
            sincronizacao,
            CriarAuditoriaServico());
        return new VinculoProdutoEtiquetaController(servico);
    }

    /// <summary>
    /// Servico que a PESAGEM usa para descobrir o processo do produto. Mora aqui, e nao numa fabrica
    /// de processo, porque le a tabela do cadastro e reusa a mesma fabrica de conexao.
    /// </summary>
    public static ProcessoPesagemProdutoServico CriarProcessoPesagemProdutoServico()
    {
        VinculoProdutoEtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        return new ProcessoPesagemProdutoServico(repositorio);
    }

    /// <summary>
    /// Tolerancia de recebimento por material (incremento 069). Mesma composicao do vinculo: le o
    /// catalogo LOCAL, e o SAP so e tocado pelo botao de sincronizar.
    /// </summary>
    public static ToleranciaRecebimentoMaterialController CriarToleranciaRecebimentoMaterialController()
        => new(CriarToleranciaRecebimentoMaterialServico());

    /// <summary>
    /// O mesmo servico que a tela usa, para o RECEBIMENTO consultar a tolerancia do material. Uma
    /// composicao so: se o recebimento montasse a sua, as duas poderiam ler de lugares diferentes.
    /// </summary>
    public static ToleranciaRecebimentoMaterialServico CriarToleranciaRecebimentoMaterialServico()
    {
        ToleranciaRecebimentoMaterialRepositorio repositorio = new(FabricaConexao.Value);
        CatalogoProdutoCentroRepositorio catalogoRepositorio = new(FabricaConexao.Value);

        SincronizacaoCatalogoProdutoServico sincronizacao = new(
            catalogoRepositorio,
            FabricaCatalogoProdutoCentroSapServico.Criar());

        return new ToleranciaRecebimentoMaterialServico(
            repositorio,
            sincronizacao,
            CriarAuditoriaServico());
    }

    /// <summary>
    /// Tolerancia por produto nos processos de producao (incremento 071). Mesma composicao do 069:
    /// le o catalogo LOCAL, e o SAP so e tocado pelo botao de sincronizar.
    /// </summary>
    public static ToleranciaProcessoProducaoController CriarToleranciaProcessoProducaoController()
        => new(CriarToleranciaProcessoProducaoServico());

    /// <summary>
    /// O mesmo servico que a tela usa, para as TELAS DE PROCESSO consultarem a tolerancia do produto.
    /// Uma composicao so: se o Consumo montasse a sua, as duas poderiam ler de lugares diferentes.
    /// </summary>
    public static ToleranciaProcessoProducaoServico CriarToleranciaProcessoProducaoServico()
    {
        ToleranciaProcessoProducaoRepositorio repositorio = new(FabricaConexao.Value);
        CatalogoProdutoCentroRepositorio catalogoRepositorio = new(FabricaConexao.Value);

        SincronizacaoCatalogoProdutoServico sincronizacao = new(
            catalogoRepositorio,
            FabricaCatalogoProdutoCentroSapServico.Criar());

        return new ToleranciaProcessoProducaoServico(
            repositorio,
            sincronizacao,
            CriarAuditoriaServico());
    }

    /// <summary>
    /// Leitura direta da tolerancia de UM produto num processo, para as telas de Consumo e
    /// Semi-Acabado cobrarem o limite a cada pesagem sem carregar o centro inteiro.
    /// </summary>
    public static ToleranciaProcessoProducaoRepositorio CriarToleranciaProcessoProducaoRepositorio()
        => new(FabricaConexao.Value);

    /// <summary>
    /// Liberacao gerencial do estouro de tolerancia na PRODUCAO (071). Reusa a mesma autenticacao e
    /// a mesma permissao do Recebimento; muda so a tabela de log.
    /// </summary>
    public static AutorizacaoToleranciaProducaoServico CriarAutorizacaoToleranciaProducaoServico()
        => new(CriarAutenticacaoServico(), new LiberacaoToleranciaProcessoRepositorio(FabricaConexao.Value));

    public static PermissaoController CriarPermissaoController()
    {
        PermissaoRepositorio repositorio = new(FabricaConexao.Value);
        PerfilPermissaoRepositorio perfilPermissaoRepositorio = new(FabricaConexao.Value);
        PermissaoServico servico = new(repositorio, perfilPermissaoRepositorio, CriarAuditoriaServico());
        return new PermissaoController(servico);
    }

    /// <summary>
    /// Liberacao gerencial do estouro de tolerancia (incremento 070). Reusa a MESMA composicao da
    /// autenticacao do login: assim o gerente passa pelas mesmas regras de BCrypt, usuario inativo e
    /// usuario bloqueado, sem um segundo caminho de autenticacao que um dia divergiria.
    /// </summary>
    public static AutorizacaoToleranciaServico CriarAutorizacaoToleranciaServico()
        => new(CriarAutenticacaoServico(), new LiberacaoToleranciaRepositorio(FabricaConexao.Value));

    public static AutenticacaoServico CriarAutenticacaoServico()
    {
        UsuarioRepositorio usuarioRepositorio = new(FabricaConexao.Value);
        UsuarioPerfilRepositorio usuarioPerfilRepositorio = new(FabricaConexao.Value);
        PerfilAcessoRepositorio perfilAcessoRepositorio = new(FabricaConexao.Value);
        PerfilPermissaoRepositorio perfilPermissaoRepositorio = new(FabricaConexao.Value);
        UsuarioSetorRepositorio usuarioSetorRepositorio = new(FabricaConexao.Value);
        AuditoriaServico auditoriaServico = CriarAuditoriaServico();

        return new AutenticacaoServico(
            usuarioRepositorio,
            usuarioPerfilRepositorio,
            perfilAcessoRepositorio,
            perfilPermissaoRepositorio,
            usuarioSetorRepositorio,
            auditoriaServico,
            new SenhaServico());
    }
}
