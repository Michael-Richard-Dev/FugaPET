using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal static class BloqueioP5R7
{
    internal static bool MutacoesHomologadas => false;
    internal const string Mensagem = "P5 R7 bloqueado: dependências e segurança não homologadas.";
    internal static bool ExigeP5(MaterialDocumentSapRequest request)
        => request.Itens.Any(ClassificacaoMaterialDocumentP5.Bloqueia);
}

internal sealed record ChaveP5
{
    internal string Pedido { get; }
    internal string Item { get; }
    internal ChaveP5(string pedido, string item)
    {
        pedido = pedido.Trim();
        item = item.Trim();
        if (!Digitos(pedido, 10) || !Digitos(item, 5))
            throw new ArgumentException("P5_CHAVE_INVALIDA");
        Pedido = pedido;
        Item = item.PadLeft(5, '0');
    }
    private static bool Digitos(string value, int max) => value.Length > 0 && value.Length <= max
        && value.All(caractere => caractere is >= '0' and <= '9');
}

internal sealed record EscopoP5(ChaveP5 Chave, long Lancamento, string Material)
{
    internal bool Valido => Lancamento > 0 && !string.IsNullOrWhiteSpace(Material);
}

internal sealed record HomologacaoP5(
    bool QaR6 = false, bool Seguranca = false, bool FonteSap = false,
    bool Banco = false, bool DispatcherUnico = false, bool AutorizacaoHera = false,
    TimeSpan? EvidenciaMaxAge = null, TimeSpan? RecoveryLease = null)
{
    internal bool Completa => QaR6 && Seguranca && FonteSap && Banco && DispatcherUnico && AutorizacaoHera
        && EvidenciaMaxAge > TimeSpan.Zero && RecoveryLease > TimeSpan.Zero;
}

internal enum EstadoP5 { Bloqueado, Reservado, EnvioIniciado, Indeterminado, Confirmado, NaoContabilizado, CanceladoSemEnvio }

internal sealed class ClaimP5(EscopoP5 escopo, long tentativa, Guid token, Guid correlacao,
    long geracaoRecovery = 0, long versaoGuarda = 0)
{
    private int _utilizado;
    internal EscopoP5 Escopo { get; } = escopo;
    internal long Tentativa { get; } = tentativa;
    internal Guid Token { get; } = token;
    internal Guid Correlacao { get; } = correlacao;
    internal long GeracaoRecovery { get; } = geracaoRecovery;
    internal long VersaoGuarda { get; } = versaoGuarda;
    internal bool Valido => Escopo.Valido && Tentativa > 0 && Token != Guid.Empty && Correlacao != Guid.Empty;
    internal bool Consumir() => Interlocked.Exchange(ref _utilizado, 1) == 0;
    public override string ToString() => "P5_CLAIM_REDACTED";
}

internal sealed class RecoveryP5(EscopoP5 escopo, long tentativa, Guid correlacao, Guid token,
    long geracao, EstadoP5 estado, DateTimeOffset? envioIniciadoEm)
{
    internal EscopoP5 Escopo { get; } = escopo;
    internal long Tentativa { get; } = tentativa;
    internal Guid Correlacao { get; } = correlacao;
    internal Guid Token { get; } = token;
    internal long Geracao { get; } = geracao;
    internal EstadoP5 Estado { get; } = estado;
    internal DateTimeOffset? EnvioIniciadoEm { get; } = envioIniciadoEm;
    internal bool Valido => Escopo.Valido && Tentativa > 0 && Geracao > 0 && Token != Guid.Empty && Correlacao != Guid.Empty;
    public override string ToString() => "P5_RECOVERY_REDACTED";
}

internal sealed record DocumentoP5(string Numero, string Ano, string Item)
{
    internal bool Valido => !string.IsNullOrWhiteSpace(Numero) && !string.IsNullOrWhiteSpace(Item)
        && Ano.Length == 4 && Ano.All(caractere => caractere is >= '0' and <= '9');
}

internal sealed record ProvaSapP5(ChaveP5 Chave, string Referencia, string Fonte,
    DateTimeOffset ObtidaEm, DateTimeOffset ValidaAte, bool HistoricoCompleto,
    bool EstornosReconciliados, int Movimentos, int Estornos, DocumentoP5? Documento = null)
{
    internal bool Valida(ChaveP5 chave, DateTimeOffset agora, TimeSpan? maxAge, DateTimeOffset? depoisDe = null) =>
        Chave == chave && !string.IsNullOrWhiteSpace(Referencia) && !string.IsNullOrWhiteSpace(Fonte)
        && HistoricoCompleto && EstornosReconciliados && Movimentos >= 0 && Estornos >= 0
        && maxAge > TimeSpan.Zero && ObtidaEm <= agora && ValidaAte > agora && ValidaAte > ObtidaEm
        && ValidaAte - ObtidaEm <= maxAge && agora - ObtidaEm <= maxAge
        && (!depoisDe.HasValue || ObtidaEm > depoisDe.Value);
    internal bool Negativa => Movimentos == 0 && Estornos == 0 && Documento is null;
    public override string ToString() => "P5_PROVA_REDACTED";
}

internal sealed record RetornoHttpP5(int? Status, DocumentoP5? Documento, bool CorrespondenciaValidada)
{
    internal bool Confirmado => Status is >= 200 and <= 299 && CorrespondenciaValidada && Documento?.Valido == true;
}

internal sealed record ResultadoFluxoP5(EstadoP5 Estado, bool Persistido, string Codigo);

internal sealed class CommitP5IndeterminadoException(ClaimP5 claim) : Exception("P5_COMMIT_INDETERMINADO")
{
    internal ClaimP5 Claim { get; } = claim;
    public override string ToString() => "P5_COMMIT_INDETERMINADO";
}

internal interface IFonteProvaP5
{
    Task<ProvaSapP5?> ObterValidadaAsync(EscopoP5 escopo, CancellationToken cancellationToken);
}

internal interface IBackendP5
{
    Task<EstadoP5> ConfirmarResultadoPersistidoAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
        => Task.FromResult(EstadoP5.Bloqueado);
    bool ResultadoLegadoAtomicoDisponivel => false;
    bool ReservaLegadaCoordenadaDisponivel => false;
    bool RecoveryR6BAprovado => false;
    Task<EstadoP5> ConfirmarEnvioDuravelAsync(ClaimP5 claim, CancellationToken cancellationToken)
        => Task.FromResult(EstadoP5.Bloqueado);
    Task<bool> CancelarReservaPreHttpCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
        => Task.FromResult(false);
    Task<bool> D2SemImpedimentoAsync(EscopoP5 escopo, CancellationToken cancellationToken);
    Task<ClaimP5> ReservarAsync(EscopoP5 escopo, ProvaSapP5 prova, CancellationToken cancellationToken);
    Task MarcarEnvioCommitAsync(ClaimP5 claim, CancellationToken cancellationToken);
    Task RegistrarResultadoCommitAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken);
    Task<RecoveryP5> AssumirRecoveryCommitAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken);
    Task FinalizarRecoveryCommitAsync(RecoveryP5 recovery, ProvaSapP5 prova, CancellationToken cancellationToken);
    Task CancelarRecoveryCommitAsync(RecoveryP5 recovery, CancellationToken cancellationToken);
    Task<EstadoP5> ConsultarEstadoAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken);
}
