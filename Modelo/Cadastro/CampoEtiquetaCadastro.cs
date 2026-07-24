namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class CampoEtiquetaCadastro
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 255;
    public const int TamanhoMaximoFormatoSaida = 100;

    public long CodigoCampoEtiqueta { get; set; }
    public long CodigoEtiqueta { get; set; }
    public string NomeCampo { get; set; } = string.Empty;
    public string DescricaoCampoEtiqueta { get; set; } = string.Empty;
    public string TipoDado { get; set; } = "TEXTO";
    public bool Obrigatorio { get; set; }
    public int? TamanhoMaximo { get; set; }
    public string FormatoSaida { get; set; } = string.Empty;
    public int Ordem { get; set; } = 1;
    public bool SituacaoCampoEtiqueta { get; set; } = true;
    public DateTime? CampoEtiquetaCriadoEm { get; set; }
    public long? CampoEtiquetaCriadoPor { get; set; }
    public long? CampoEtiquetaAtualizadoPor { get; set; }

    // Aliases legados
    public long Id { get => CodigoCampoEtiqueta; set => CodigoCampoEtiqueta = value; }
    public long IdEtiqueta { get => CodigoEtiqueta; set => CodigoEtiqueta = value; }
    public string Nome { get => NomeCampo; set => NomeCampo = value; }
    public bool Ativo { get => SituacaoCampoEtiqueta; set => SituacaoCampoEtiqueta = value; }
}
