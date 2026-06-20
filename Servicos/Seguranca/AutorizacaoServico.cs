using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.Seguranca;

public static class AutorizacaoServico
{
    // Fonte unica: PermissoesSistema. Mantidos como aliases para nao quebrar chamadas existentes.
    public const string ModuloCadastro = PermissoesSistema.Modulos.Cadastro;
    public const string ModuloProcesso = PermissoesSistema.Modulos.ProcessoProducao;
    public const string ModuloEtiqueta = PermissoesSistema.Modulos.Etiqueta;
    public const string ModuloRelatorio = PermissoesSistema.Modulos.Relatorio;
    public const string ModuloConfiguracao = PermissoesSistema.Modulos.Configuracao;
    public const string ModuloSeguranca = PermissoesSistema.Modulos.Seguranca;
    public const string ModuloAuditoria = PermissoesSistema.Modulos.Historico;
    public const string ModuloSap = PermissoesSistema.Modulos.IntegracaoSap;

    public const string AcaoGerenciar = PermissoesSistema.Acoes.Gerenciar;
    public const string AcaoCriar = PermissoesSistema.Acoes.Criar;
    public const string AcaoEditar = PermissoesSistema.Acoes.Editar;
    public const string AcaoExcluir = PermissoesSistema.Acoes.Excluir;
    public const string AcaoConsultar = PermissoesSistema.Acoes.Consultar;
    public const string AcaoExecutar = PermissoesSistema.Acoes.Executar;
    public const string AcaoFinalizar = PermissoesSistema.Acoes.Finalizar;
    public const string AcaoCancelar = PermissoesSistema.Acoes.Cancelar;
    public const string AcaoVisualizar = PermissoesSistema.Acoes.Visualizar;
    public const string AcaoImprimir = PermissoesSistema.Acoes.Imprimir;
    public const string AcaoBloquear = PermissoesSistema.Acoes.Bloquear;
    public const string AcaoDesbloquear = PermissoesSistema.Acoes.Desbloquear;

    public static bool PodeAcessar(string modulo)
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            // Banco desabilitado: libera tudo apenas na demonstracao segura e explicita.
            return EstadoIntegracaoBanco.PodeUsarDadosSimulados;
        }

        SessaoUsuarioAplicacao? sessao = EstadoSessaoUsuarioAtual.SessaoAtual;
        if (sessao is null)
        {
            return false;
        }

        // Sem bypass por nome de perfil: autorizacao e 100% dirigida por perfil_permissao.
        // O perfil 'Administrador' acessa tudo porque recebe todas as permissoes no seed.
        return sessao.Permissoes.Any(permissao => string.Equals(permissao.Modulo, modulo, StringComparison.OrdinalIgnoreCase));
    }

    public static bool PossuiPermissao(string modulo, string rotina, string acao)
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            // Banco desabilitado: libera tudo apenas na demonstracao segura e explicita.
            return EstadoIntegracaoBanco.PodeUsarDadosSimulados;
        }

        SessaoUsuarioAplicacao? sessao = EstadoSessaoUsuarioAtual.SessaoAtual;
        if (sessao is null)
        {
            return false;
        }

        // Sem bypass por nome de perfil: confia nas permissoes seedadas (perfil_permissao).
        return sessao.Permissoes.Any(permissao => permissao.Corresponde(modulo, rotina, acao))
            || sessao.Permissoes.Any(permissao => permissao.Corresponde(modulo, rotina, AcaoGerenciar));
    }

    public static bool PodeGerenciarCadastro(string rotina)
    {
        return PossuiPermissao(PermissoesSistema.Modulos.Cadastro, rotina, PermissoesSistema.Acoes.Gerenciar);
    }

    public static string MensagemSemPermissao(string modulo, string rotina, string acao)
    {
        return $"Usuario sem permissao para {acao.ToLowerInvariant()} em {modulo}/{rotina}.";
    }
}
