using System.Net;

namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class AuditoriaAcaoUsuarioCadastro
{
    public long? CodigoUsuario { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? Modulo { get; set; }
    public string? Tela { get; set; }
    public string Resultado { get; set; } = "SUCESSO";
    public string? Mensagem { get; set; }
    public IPAddress? IpOrigem { get; set; }
    public string? NomeMaquina { get; set; }
    public string? DadosContextoJson { get; set; }
}
