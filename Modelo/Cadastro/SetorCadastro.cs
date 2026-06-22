namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class SetorCadastro
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 255;

    public long CodigoSetor { get; set; }
    public string NomeSetor { get; set; } = string.Empty;
    public string DescricaoSetor { get; set; } = string.Empty;
    public bool SituacaoSetor { get; set; } = true;
    public DateTime? SetorCriadoEm { get; set; }
    public long? SetorCriadoPor { get; set; }
    public long? SetorAtualizadoPor { get; set; }

    // Compatibilidade transitória com chamadas legadas da aplicação.
    public long Codigo
    {
        get => CodigoSetor;
        set => CodigoSetor = value;
    }

    public string Nome
    {
        get => NomeSetor;
        set => NomeSetor = value;
    }

    public string Descricao
    {
        get => DescricaoSetor;
        set => DescricaoSetor = value;
    }

    public bool Ativo
    {
        get => SituacaoSetor;
        set => SituacaoSetor = value;
    }
}

public sealed class ResumoDependenciasSetor
{
    public int UsuariosPadraoAtivos { get; init; }
    public int VinculosUsuarioAtivos { get; init; }
    public int BalancasAtivas { get; init; }
    public int TarasAtivas { get; init; }
    public int ParametrosOperacaoAtivos { get; init; }
    public int LancamentosOperacionaisAtivos { get; init; }

    public bool PossuiDependenciasAtivas =>
        UsuariosPadraoAtivos > 0
        || VinculosUsuarioAtivos > 0
        || BalancasAtivas > 0
        || TarasAtivas > 0
        || ParametrosOperacaoAtivos > 0
        || LancamentosOperacionaisAtivos > 0;

    public string ObterMensagemBloqueio()
    {
        List<string> dependencias = [];

        Adicionar(dependencias, UsuariosPadraoAtivos, "usuario com setor padrao", "usuarios com setor padrao");
        Adicionar(dependencias, VinculosUsuarioAtivos, "vinculo ativo com usuario", "vinculos ativos com usuarios");
        Adicionar(dependencias, BalancasAtivas, "balanca ativa", "balancas ativas");
        Adicionar(dependencias, TarasAtivas, "tara ativa", "taras ativas");
        Adicionar(dependencias, ParametrosOperacaoAtivos, "configuracao operacional ativa", "configuracoes operacionais ativas");
        Adicionar(dependencias, LancamentosOperacionaisAtivos, "lancamento operacional ativo", "lancamentos operacionais ativos");

        return dependencias.Count == 0
            ? string.Empty
            : $"Nao e possivel inativar este setor porque existem dependencias ativas: {string.Join(", ", dependencias)}. Remova ou inative os vinculos antes de continuar.";
    }

    private static void Adicionar(List<string> dependencias, int quantidade, string singular, string plural)
    {
        if (quantidade > 0)
        {
            dependencias.Add($"{quantidade} {(quantidade == 1 ? singular : plural)}");
        }
    }
}
