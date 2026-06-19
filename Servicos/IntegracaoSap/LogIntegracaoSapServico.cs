using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal sealed class LogIntegracaoSapServico : ILogIntegracaoSapServico
{
    private static readonly string[] MarcadoresSensiveis =
    [
        "authorization",
        "basic ",
        "password",
        "senha",
        "cookie",
        "set-cookie"
    ];

    private readonly LogIntegracaoSapRepositorio _repositorio;

    public LogIntegracaoSapServico(LogIntegracaoSapRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task RegistrarAsync(
        RegistroLogIntegracaoSap registro,
        CancellationToken cancellationToken = default)
    {
        try
        {
            RegistroLogIntegracaoSap seguro = registro with
            {
                CodigoUsuarioFugaPet =
                    registro.CodigoUsuarioFugaPet
                    ?? EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario,
                ChaveNegocio = Limitar(registro.ChaveNegocio, 200),
                MensagemTecnicaSanitizada =
                    SanitizarMensagem(registro.MensagemTecnicaSanitizada),
                RegistradoEmUtc = registro.RegistradoEmUtc.ToUniversalTime()
            };

            await _repositorio.InserirAsync(seguro, cancellationToken);
        }
        catch
        {
            // A rastreabilidade nunca pode mascarar ou interromper a integracao SAP.
        }
    }

    internal static string? SanitizarMensagem(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return null;
        }

        string valor = mensagem.Trim();
        if (MarcadoresSensiveis.Any(
                marcador => valor.Contains(marcador, StringComparison.OrdinalIgnoreCase)))
        {
            return "Mensagem tecnica removida por conter dado potencialmente sensivel.";
        }

        return Limitar(valor, 1000);
    }

    private static string? Limitar(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string texto = valor.Trim();
        return texto.Length <= limite ? texto : texto[..limite];
    }
}
