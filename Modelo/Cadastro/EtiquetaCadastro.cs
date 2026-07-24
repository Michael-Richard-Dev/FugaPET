namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class EtiquetaCadastro
{
    public const int TamanhoMaximoCodigoInterno = 80;
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 255;

    public long CodigoEtiqueta { get; set; }
    public long CodigoModeloEtiqueta { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string NomeEtiqueta { get; set; } = string.Empty;
    public string TipoEtiqueta { get; set; } = string.Empty;
    public string DescricaoEtiqueta { get; set; } = string.Empty;
    public bool SituacaoEtiqueta { get; set; } = true;
    public long? EtiquetaCriadoPor { get; set; }
    public DateTime? EtiquetaCriadoEm { get; set; }
    public long? EtiquetaAtualizadoPor { get; set; }
    public DateTime? EtiquetaAtualizadoEm { get; set; }
    public string NomeModeloEtiqueta { get; set; } = string.Empty;
    public int VersaoModeloEtiqueta { get; set; }
    public bool SituacaoModeloEtiqueta { get; set; }

    public string ModeloExibicao => string.IsNullOrWhiteSpace(NomeModeloEtiqueta)
        ? "-"
        : $"{NomeModeloEtiqueta} - v{VersaoModeloEtiqueta}";

    public long Id
    {
        get => CodigoEtiqueta;
        set => CodigoEtiqueta = value;
    }

    public long IdModeloEtiqueta
    {
        get => CodigoModeloEtiqueta;
        set => CodigoModeloEtiqueta = value;
    }

    public string Codigo
    {
        get => CodigoInterno;
        set => CodigoInterno = value;
    }

    public string Nome
    {
        get => NomeEtiqueta;
        set => NomeEtiqueta = value;
    }

    public string Descricao
    {
        get => DescricaoEtiqueta;
        set => DescricaoEtiqueta = value;
    }

    public bool Ativo
    {
        get => SituacaoEtiqueta;
        set => SituacaoEtiqueta = value;
    }
}

public sealed class ResumoDependenciasEtiqueta
{
    public int ProdutosAtivosVinculados { get; init; }
    public bool PossuiDependenciasAtivas => ProdutosAtivosVinculados > 0;
}
