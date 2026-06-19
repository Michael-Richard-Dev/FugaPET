using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Decisao central de disponibilidade e autorizacao da integracao SAP.</summary>
internal sealed class EstadoIntegracaoSapServico
{
    private const string ChaveIntegracaoAtiva = "INTEGRACAO_SAP_ATIVA";
    private const string TelaOrigem = "IntegracaoSap";

    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly Func<CancellationToken, Task<bool?>> _obterIntegracaoAtivaAsync;
    private readonly Func<OperacaoIntegracaoSap, bool> _usuarioAutorizado;
    private readonly AuditoriaServico? _auditoriaServico;
    private readonly bool _ambientePermitido;

    public EstadoIntegracaoSapServico(
        ConfiguracaoSap configuracaoSap,
        ConfiguracaoGeralRepositorio configuracaoRepositorio,
        AuditoriaServico auditoriaServico)
        : this(
            configuracaoSap,
            cancellationToken => configuracaoRepositorio.ObterBooleanoAsync(
                ChaveIntegracaoAtiva,
                cancellationToken),
            UsuarioAutorizado,
            auditoriaServico,
            EstadoIntegracaoBanco.Habilitado && !EstadoIntegracaoBanco.ModoDemonstracao)
    {
    }

    internal EstadoIntegracaoSapServico(
        ConfiguracaoSap configuracaoSap,
        Func<CancellationToken, Task<bool?>> obterIntegracaoAtivaAsync,
        Func<OperacaoIntegracaoSap, bool> usuarioAutorizado,
        AuditoriaServico? auditoriaServico,
        bool ambientePermitido)
    {
        _configuracaoSap = configuracaoSap;
        _obterIntegracaoAtivaAsync = obterIntegracaoAtivaAsync;
        _usuarioAutorizado = usuarioAutorizado;
        _auditoriaServico = auditoriaServico;
        _ambientePermitido = ambientePermitido;
    }

    public async Task<ResultadoOperacao> ValidarAsync(
        OperacaoIntegracaoSap operacao,
        CancellationToken cancellationToken = default)
    {
        if (!_ambientePermitido)
        {
            return await BloquearAsync(
                operacao,
                "Integracao SAP indisponivel neste ambiente.",
                cancellationToken);
        }

        bool? integracaoAtiva;
        try
        {
            integracaoAtiva = await _obterIntegracaoAtivaAsync(cancellationToken);
        }
        catch
        {
            return await BloquearAsync(
                operacao,
                "Nao foi possivel validar a ativacao central da integracao SAP.",
                cancellationToken);
        }

        if (integracaoAtiva != true)
        {
            return await BloquearAsync(
                operacao,
                "Integracao SAP desativada pela configuracao central.",
                cancellationToken);
        }

        if (!_configuracaoSap.Configurado)
        {
            return await BloquearAsync(
                operacao,
                ConfiguracaoSap.MensagemConfiguracaoAusente,
                cancellationToken);
        }

        if (!_usuarioAutorizado(operacao))
        {
            return await BloquearAsync(
                operacao,
                "Usuario sem permissao para executar esta operacao SAP.",
                cancellationToken);
        }

        return ResultadoOperacao.Ok();
    }

    private async Task<ResultadoOperacao> BloquearAsync(
        OperacaoIntegracaoSap operacao,
        string mensagem,
        CancellationToken cancellationToken)
    {
        if (_auditoriaServico is not null)
        {
            await _auditoriaServico.RegistrarBloqueioIntegracaoSapAsync(
                $"SAP_{operacao.ToString().ToUpperInvariant()}_BLOQUEADA",
                mensagem,
                TelaOrigem,
                cancellationToken);
        }

        return ResultadoOperacao.Falha(mensagem);
    }

    private static bool UsuarioAutorizado(OperacaoIntegracaoSap operacao)
        => operacao switch
        {
            OperacaoIntegracaoSap.Consulta => AutorizacaoEntradaProdutoServico.PossuiPermissao(
                PermissoesSistema.Acoes.Consultar),
            OperacaoIntegracaoSap.Sincronizacao => AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                PermissoesSistema.Acoes.SincronizarCache)
                || AutorizacaoEntradaProdutoServico.PossuiPermissao(
                    PermissoesSistema.Acoes.SincronizarCache),
            OperacaoIntegracaoSap.Escrita => AutorizacaoEntradaProdutoServico.PossuiPermissao(
                PermissoesSistema.Acoes.EnviarSap),
            _ => false
        };
}
