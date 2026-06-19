namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class MapeamentoCampoEtiquetaCadastro
{
    public long CodigoMapeamentoCampoEtiqueta { get; set; }
    public long CodigoCampoEtiqueta { get; set; }
    public string OrigemDado { get; set; } = "USUARIO";
    public string ExpressaoOrigem { get; set; } = string.Empty;
    public string ValorPadrao { get; set; } = string.Empty;
    public bool ObrigatorioParaImpressao { get; set; }
    public string Observacao { get; set; } = string.Empty;
    public bool SituacaoMapeamentoCampoEtiqueta { get; set; } = true;
    public DateTime? MapeamentoCampoEtiquetaCriadoEm { get; set; }
    public long? MapeamentoCampoEtiquetaCriadoPor { get; set; }
    public long? MapeamentoCampoEtiquetaAtualizadoPor { get; set; }

    // Aliases legados
    public long Id { get => CodigoMapeamentoCampoEtiqueta; set => CodigoMapeamentoCampoEtiqueta = value; }
    public long IdCampoEtiqueta { get => CodigoCampoEtiqueta; set => CodigoCampoEtiqueta = value; }
    public bool Ativo { get => SituacaoMapeamentoCampoEtiqueta; set => SituacaoMapeamentoCampoEtiqueta = value; }
}
