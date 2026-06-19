namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Autorizacao especifica da Entrada de Produto. O fallback legado existe somente
/// para a transicao do incremental 020 e deve ser removido apos a migracao ser validada.
/// </summary>
public static class AutorizacaoEntradaProdutoServico
{
    public static bool PossuiPermissao(string acao)
    {
        if (AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                acao))
        {
            return true;
        }

        return PossuiPermissaoLegadaEquivalente(acao);
    }

    public static bool PossuiPermissaoImpressao(string acao)
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.Etiqueta,
            PermissoesSistema.Rotinas.ImpressaoEtiqueta,
            acao);

    public static string MensagemSemPermissao(string acao)
        => AutorizacaoServico.MensagemSemPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.EntradaProduto,
            acao);

    private static bool PossuiPermissaoLegadaEquivalente(string acao)
        => acao switch
        {
            PermissoesSistema.Acoes.Consultar => PossuiLeituraLegada(PermissoesSistema.Acoes.Consultar),
            PermissoesSistema.Acoes.Executar => PossuiLeituraLegada(PermissoesSistema.Acoes.Executar),
            PermissoesSistema.Acoes.Finalizar => PossuiLeituraLegada(PermissoesSistema.Acoes.Finalizar),
            PermissoesSistema.Acoes.Cancelar => PossuiLeituraLegada(PermissoesSistema.Acoes.Cancelar),
            PermissoesSistema.Acoes.SincronizarCache => PossuiLeituraLegada(PermissoesSistema.Acoes.Executar),
            PermissoesSistema.Acoes.EnviarSap => PossuiLeituraLegada(PermissoesSistema.Acoes.Finalizar),
            PermissoesSistema.Acoes.PesoManual =>
                PossuiLeituraLegada(PermissoesSistema.Acoes.Finalizar)
                && AutorizacaoServico.PossuiPermissao(
                    PermissoesSistema.Modulos.IntegracaoSap,
                    PermissoesSistema.Rotinas.CacheSap,
                    PermissoesSistema.Acoes.Sincronizar),
            _ => false
        };

    private static bool PossuiLeituraLegada(string acao)
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.LeituraProducao,
            acao);
}
