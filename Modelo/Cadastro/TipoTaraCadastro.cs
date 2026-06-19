namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class TipoTaraCadastro
{
    public long CodigoTipoTara { get; set; }
    public string NomeTipoTara { get; set; } = string.Empty;
    public string DescricaoTipoTara { get; set; } = string.Empty;
    public bool SituacaoTipoTara { get; set; } = true;
    public DateTime? TipoTaraCriadoEm { get; set; }
    public long? TipoTaraCriadoPor { get; set; }
    public long? TipoTaraAtualizadoPor { get; set; }

    // Aliases de compatibilidade
    public long Id { get => CodigoTipoTara; set => CodigoTipoTara = value; }
    public string Nome { get => NomeTipoTara; set => NomeTipoTara = value; }
    public string Descricao { get => DescricaoTipoTara; set => DescricaoTipoTara = value; }
    public bool Ativo { get => SituacaoTipoTara; set => SituacaoTipoTara = value; }
}
