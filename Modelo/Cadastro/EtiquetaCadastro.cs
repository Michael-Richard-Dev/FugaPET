namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class EtiquetaCadastro
{
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
