using System.Net;
using System.Net.Sockets;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Servicos.Auditoria;

public sealed class AuditoriaAcaoUsuarioServico
{
    private readonly AuditoriaAcaoUsuarioRepositorio _repositorio;

    public AuditoriaAcaoUsuarioServico(AuditoriaAcaoUsuarioRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public Task RegistrarAsync(
        string acao,
        string resultado,
        long? codigoUsuario,
        string? mensagem = null,
        string? modulo = null,
        string? tela = null,
        string? dadosContextoJson = null,
        CancellationToken cancellationToken = default)
    {
        var evento = new AuditoriaAcaoUsuarioCadastro
        {
            CodigoUsuario = codigoUsuario,
            Acao = acao,
            Resultado = resultado,
            Modulo = modulo,
            Tela = tela,
            Mensagem = mensagem,
            DadosContextoJson = dadosContextoJson,
            NomeMaquina = ObterNomeMaquinaSeguro(),
            IpOrigem = ObterIpLocalSeguro()
        };

        return _repositorio.RegistrarAsync(evento, cancellationToken);
    }

    private static string? ObterNomeMaquinaSeguro()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return null;
        }
    }

    private static IPAddress? ObterIpLocalSeguro()
    {
        try
        {
            IPHostEntry entrada = Dns.GetHostEntry(Dns.GetHostName());
            return entrada.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
            return null;
        }
    }
}
