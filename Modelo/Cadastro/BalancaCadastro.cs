namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class BalancaCadastro
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoIdentificacaoLocal = 120;
    public const int TamanhoMaximoEnderecoIp = 45;
    public const int TamanhoMaximoPortaSerial = 50;
    public const int TamanhoMaximoTipoConexao = 20;
    public const int TamanhoMaximoParidade = 10;
    public const int TamanhoMaximoFlowControl = 20;
    public const int TamanhoMaximoProtocolo = 50;
    public const int TamanhoMaximoObservacao = 255;

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

public sealed class ResumoDependenciasBalanca
{
    public int PesagensEntradaProduto { get; init; }
    public int HusCaixaAtivas { get; init; }
    public int PesagensHuCaixa { get; init; }
    public int PesagensEntradaItem { get; init; }
    public int LeiturasEntradaItem { get; init; }

    public int TotalDependenciasOperacionais
        => PesagensEntradaProduto + HusCaixaAtivas + PesagensHuCaixa + PesagensEntradaItem + LeiturasEntradaItem;

    public bool PossuiDependenciasAtivas => TotalDependenciasOperacionais > 0;

    public string ObterMensagemBloqueio()
        => !PossuiDependenciasAtivas
            ? string.Empty
            : $"Nao e possivel inativar esta balanca porque existe(m) {TotalDependenciasOperacionais} "
              + $"{(TotalDependenciasOperacionais == 1 ? "registro operacional de pesagem vinculado" : "registros operacionais de pesagem vinculados")}. "
              + "Estorne ou troque a balanca desses registros antes de continuar.";
}
