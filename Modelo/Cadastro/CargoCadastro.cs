namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class CargoCadastro
{
    public const int TamanhoMinimoNome = 2;
    public const int TamanhoMaximoNome = 80;
    public const int TamanhoMaximoDescricao = 255;

    public long IdCargo { get; set; }
    public string NomeCargo { get; set; } = string.Empty;
    public string DescricaoCargo { get; set; } = string.Empty;
    public bool SituacaoCargo { get; set; } = true;
    public DateTime? CargoCriadoEm { get; set; }
    public long? CargoCriadoPor { get; set; }
    public long? CargoAtualizadoPor { get; set; }

    // Compatibilidade transitória com chamadas legadas da aplicação.
    public long Id
    {
        get => IdCargo;
        set => IdCargo = value;
    }

    public string Nome
    {
        get => NomeCargo;
        set => NomeCargo = value;
    }

    public string Descricao
    {
        get => DescricaoCargo;
        set => DescricaoCargo = value;
    }

    public bool Ativo
    {
        get => SituacaoCargo;
        set => SituacaoCargo = value;
    }
}

public sealed class ResumoDependenciasCargo
{
    public int UsuariosAtivos { get; init; }

    public bool PossuiDependenciasAtivas => UsuariosAtivos > 0;

    public string ObterMensagemBloqueio()
        => UsuariosAtivos == 0
            ? string.Empty
            : $"Nao e possivel inativar este cargo porque existe(m) {UsuariosAtivos} "
              + $"{(UsuariosAtivos == 1 ? "usuario ativo vinculado" : "usuarios ativos vinculados")}. "
              + "Remova ou troque o cargo desses usuarios antes de continuar.";
}
