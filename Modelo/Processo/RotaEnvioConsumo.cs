namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Rota de envio ao SAP de um LANCAMENTO/consumo salvo (agregada a partir dos componentes consumidos).
/// Usada apenas para habilitar/desabilitar botoes e mensagens — NAO altera persistencia nem faz POST.
/// </summary>
public enum RotaEnvioConsumo
{
    /// <summary>Todos os componentes consumidos sao elegiveis ao movimento 261 direto.</summary>
    Direto261,

    /// <summary>Todos os componentes consumidos sao Backflush (envio via Confirmacao de Producao).</summary>
    BackflushConfirmacao,

    /// <summary>Mistura de 261 direto + Backflush: envio automatico bloqueado ate definicao da regra.</summary>
    Misto,

    /// <summary>Algum item sem deposito/saldo/lote ou inconsistente: bloqueado.</summary>
    Bloqueado
}

/// <summary>Codigos textuais estaveis da rota (tela/log/testes).</summary>
public static class RotaEnvioConsumoExtensoes
{
    public const string CodigoDireto261 = "261_DIRETO";
    public const string CodigoBackflushConfirmacao = "BACKFLUSH_CONFIRMACAO";
    public const string CodigoMisto = "MISTO";
    public const string CodigoBloqueado = "BLOQUEADO";

    public static string Codigo(this RotaEnvioConsumo rota)
        => rota switch
        {
            RotaEnvioConsumo.Direto261 => CodigoDireto261,
            RotaEnvioConsumo.BackflushConfirmacao => CodigoBackflushConfirmacao,
            RotaEnvioConsumo.Misto => CodigoMisto,
            _ => CodigoBloqueado
        };
}
