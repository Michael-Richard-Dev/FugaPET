namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class TaraCadastro
{
    public long CodigoTara { get; set; }
    public long CodigoTipoTara { get; set; }
    public long CodigoSetor { get; set; }
    public string NomeTara { get; set; } = string.Empty;
    public string Tamanho { get; set; } = string.Empty;
    public decimal PesoKg { get; set; }
    public string Observacao { get; set; } = string.Empty;
    public bool SituacaoTara { get; set; } = true;
    public DateTime? TaraCriadoEm { get; set; }
    public long? TaraCriadoPor { get; set; }
    public long? TaraAtualizadoPor { get; set; }

    // Aliases de compatibilidade com Form/Service legados
    public long Id { get => CodigoTara; set => CodigoTara = value; }
    public long IdTipoTara { get => CodigoTipoTara; set => CodigoTipoTara = value; }
    public long IdSetor { get => CodigoSetor; set => CodigoSetor = value; }
    public string Nome { get => NomeTara; set => NomeTara = value; }
    public bool Ativo { get => SituacaoTara; set => SituacaoTara = value; }
}
