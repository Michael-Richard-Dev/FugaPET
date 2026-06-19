namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class PermissaoCadastro
{
    public long IdPermissao { get; set; }
    public string ModuloPermissao { get; set; } = string.Empty;
    public string RotinaPermissao { get; set; } = string.Empty;
    public string AcaoPermissao { get; set; } = string.Empty;
    public string DescricaoPermissao { get; set; } = string.Empty;
    public bool SituacaoPermissao { get; set; } = true;
    public DateTime? PermissaoCriadoEm { get; set; }
    public long? PermissaoCriadoPor { get; set; }
    public long? PermissaoAtualizadoPor { get; set; }

    // Aliases legados
    public long Id { get => IdPermissao; set => IdPermissao = value; }
    public bool Ativo { get => SituacaoPermissao; set => SituacaoPermissao = value; }
}
