namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class ModeloEtiquetaCadastro
{
    public long CodigoModeloEtiqueta { get; set; }
    public string NomeModeloEtiqueta { get; set; } = string.Empty;
    public int Versao { get; set; } = 1;
    public decimal? LarguraMm { get; set; }
    public decimal? AlturaMm { get; set; }
    public int? Dpi { get; set; } = 203;
    public string ConteudoZpl { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
    public bool SituacaoModeloEtiqueta { get; set; } = true;
    public DateTime? ModeloEtiquetaCriadoEm { get; set; }
    public long? ModeloEtiquetaCriadoPor { get; set; }
    public long? ModeloEtiquetaAtualizadoPor { get; set; }

    // Aliases legados
    public long Id { get => CodigoModeloEtiqueta; set => CodigoModeloEtiqueta = value; }
    public string Nome { get => NomeModeloEtiqueta; set => NomeModeloEtiqueta = value; }
    public bool Ativo { get => SituacaoModeloEtiqueta; set => SituacaoModeloEtiqueta = value; }
}
