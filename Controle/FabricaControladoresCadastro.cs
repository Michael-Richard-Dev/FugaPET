using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
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

    public static CampoEtiquetaController CriarCampoEtiquetaController()
    {
        CampoEtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        CampoEtiquetaServico servico = new(repositorio, CriarAuditoriaServico());
        return new CampoEtiquetaController(servico);
    }

    public static MapeamentoCampoEtiquetaController CriarMapeamentoCampoEtiquetaController()
    {
        MapeamentoCampoEtiquetaRepositorio repositorio = new(FabricaConexao.Value);
        MapeamentoCampoEtiquetaServico servico = new(repositorio, CriarAuditoriaServico());
        return new MapeamentoCampoEtiquetaController(servico);
    }

    public static PermissaoController CriarPermissaoController()
    {
        PermissaoRepositorio repositorio = new(FabricaConexao.Value);
        PerfilPermissaoRepositorio perfilPermissaoRepositorio = new(FabricaConexao.Value);
        PermissaoServico servico = new(repositorio, perfilPermissaoRepositorio, CriarAuditoriaServico());
        return new PermissaoController(servico);
    }

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
