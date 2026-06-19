namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class BalancaCadastro
{
    public long CodigoBalanca { get; set; }
    public long CodigoSetor { get; set; }
    public string NomeBalanca { get; set; } = string.Empty;
    public string IdentificacaoLocal { get; set; } = string.Empty;
    public string EnderecoIp { get; set; } = string.Empty;
    public int? PortaTcp { get; set; }
    public string PortaSerial { get; set; } = string.Empty;
    public string TipoConexao { get; set; } = string.Empty;
    public int? BaudRate { get; set; }
    public int? DataBits { get; set; }
    public string Paridade { get; set; } = string.Empty;
    public decimal? StopBits { get; set; }
    public string FlowControl { get; set; } = string.Empty;
    public string Protocolo { get; set; } = string.Empty;
    public string ParametrosTecnicos { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
    public bool SituacaoBalanca { get; set; } = true;
    public DateTime? BalancaCriadoEm { get; set; }
    public long? BalancaCriadoPor { get; set; }
    public long? BalancaAtualizadoPor { get; set; }

    // Aliases para compatibilidade com Form/Servicos legados
    public long Id { get => CodigoBalanca; set => CodigoBalanca = value; }
    public long IdSetor { get => CodigoSetor; set => CodigoSetor = value; }
    public string Nome { get => NomeBalanca; set => NomeBalanca = value; }
    public bool Ativo { get => SituacaoBalanca; set => SituacaoBalanca = value; }
}
