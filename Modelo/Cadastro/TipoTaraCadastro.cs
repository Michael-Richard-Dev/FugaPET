namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class TipoTaraCadastro
{
    // Tarefa Tipo de Tara (Ajuste 4): limites de tamanho, alinhados a Setor/Cargo.
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 255;

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

/// <summary>
/// Tarefa Tipo de Tara (Ajuste 6): linha de diagnóstico de nomes duplicados (global, por
/// upper(trim(nome))). Apenas leitura/diagnóstico — nenhuma correção automática de dados.
/// </summary>
public sealed record DuplicadoTipoTara(string NomeNormalizado, int Quantidade, string Registros);

/// <summary>
/// Tarefa Tipo de Tara (Ajuste 10): resumo de dependências ativas do tipo de tara, no padrão de
/// <c>ResumoDependenciasCargo</c>. Bloqueia a inativação quando houver Tara ativa vinculada.
/// </summary>
public sealed class ResumoDependenciasTipoTara
{
    public int TarasAtivas { get; init; }

    public bool PossuiDependenciasAtivas => TarasAtivas > 0;

    public string ObterMensagemBloqueio()
        => TarasAtivas == 0
            ? string.Empty
            : "Não é possível inativar este tipo de tara porque existem taras ativas vinculadas a ele.";
}
