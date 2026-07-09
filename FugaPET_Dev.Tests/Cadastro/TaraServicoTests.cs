using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Tara: regras do serviço alinhadas a Setor/Cargo/TipoTara — validações de tamanho/peso, duplicidade
/// GLOBAL por setor+tipo (ativo OU inativo), e situação que muda apenas por Inativar/Reativar.
/// </summary>
public sealed class TaraServicoTests : IDisposable
{
    public TaraServicoTests()
        => Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

    private static TaraCadastro Nova(string nome = "Caixa A", decimal peso = 1.5m, long tipo = 1, long setor = 1, bool ativo = true)
        => new() { NomeTara = nome, PesoKg = peso, CodigoTipoTara = tipo, CodigoSetor = setor, SituacaoTara = ativo, Tamanho = "M", Observacao = "obs" };

    // ---- validações ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]           // < 2
    public async Task Inserir_NomeInvalido_DeveBloquear(string nome)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Nova(nome: nome))).Sucesso);

    [Fact]
    public async Task Inserir_NomeMaior80_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Nova(nome: new string('X', 81)))).Sucesso);

    [Fact]
    public async Task Inserir_TamanhoMaior80_DeveBloquear()
    {
        TaraCadastro t = Nova();
        t.Tamanho = new string('T', 81);
        Assert.False((await Servico(new RepositorioFake()).InserirAsync(t)).Sucesso);
    }

    [Fact]
    public async Task Inserir_ObservacaoMaior255_DeveBloquear()
    {
        TaraCadastro t = Nova();
        t.Observacao = new string('O', 256);
        Assert.False((await Servico(new RepositorioFake()).InserirAsync(t)).Sucesso);
    }

    [Fact]
    public async Task Inserir_SemTipo_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Nova(tipo: 0))).Sucesso);

    [Fact]
    public async Task Inserir_SemSetor_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Nova(setor: 0))).Sucesso);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Inserir_PesoZeroOuNegativo_DeveBloquear(double peso)
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await Servico(repo).InserirAsync(Nova(peso: (decimal)peso));

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_PesoValido_ManteneKgSemConverterParaGramas()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await Servico(repo).InserirAsync(Nova(peso: 1.5m));

        Assert.True(r.Sucesso);
        Assert.Equal(1.5m, repo.Inserido!.PesoKg); // KG, não 1500
    }

    // ---- Ajuste 7: duplicidade GLOBAL por setor+tipo ----

    [Fact]
    public async Task Inserir_DuplicidadeGlobal_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new() { NomeExiste = true }; // existe (ativa OU inativa) no mesmo setor+tipo

        ResultadoOperacao r = await Servico(repo).InserirAsync(Nova());

        Assert.False(r.Sucesso);
        Assert.Equal(TaraServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Null(repo.Inserido);
    }

    // ---- Ajuste 5: situação muda só por Inativar/Reativar ----

    [Fact]
    public async Task Atualizar_TentaInativarPelaEdicao_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, true) };

        TaraCadastro t = Nova(ativo: false);
        t.CodigoTara = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(t);

        Assert.False(r.Sucesso);
        Assert.Equal(TaraServico.MensagemInativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_TentaReativarPelaEdicao_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, false) };

        TaraCadastro t = Nova(ativo: true);
        t.CodigoTara = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(t);

        Assert.False(r.Sucesso);
        Assert.Equal(TaraServico.MensagemReativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_SoDados_PreservaSituacaoEPermite()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, true) };

        TaraCadastro t = Nova(nome: "Caixa B", ativo: true);
        t.CodigoTara = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(t);

        Assert.True(r.Sucesso);
        Assert.NotNull(repo.Atualizado);
        Assert.True(repo.Atualizado!.SituacaoTara);
        Assert.Equal("Caixa B", repo.Atualizado.NomeTara);
    }

    [Fact]
    public async Task Atualizar_NomeDuplicadoGlobal_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, true), NomeExiste = true };

        TaraCadastro t = Nova(nome: "Outro");
        t.CodigoTara = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(t);

        Assert.False(r.Sucesso);
        Assert.Equal(TaraServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    // ---- Excluir/Reativar ----

    [Fact]
    public async Task Excluir_Inativa()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repo = new();

        Assert.True((await Servico(repo).ExcluirAsync(10)).Sucesso);
        Assert.Equal(1, repo.ExcluirChamadas);
    }

    [Fact]
    public async Task Reativar_SemDuplicidade_Permite()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, false), NomeExiste = false };

        Assert.True((await Servico(repo).ReativarAsync(10)).Sucesso);
        Assert.Equal(1, repo.ReativarChamadas);
    }

    [Fact]
    public async Task Reativar_ComDuplicidadeGlobal_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new() { Existente = Comp(10, false), NomeExiste = true };

        Assert.False((await Servico(repo).ReativarAsync(10)).Sucesso);
        Assert.Equal(0, repo.ReativarChamadas);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static TaraServico Servico(RepositorioFake repo)
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar, PermissoesSistema.Acoes.Editar, PermissoesSistema.Acoes.Excluir);
        return new TaraServico(repo, new AuditoriaSpy());
    }

    private static TaraCadastro Comp(long id, bool ativo)
        => new() { CodigoTara = id, NomeTara = "Caixa A", CodigoTipoTara = 1, CodigoSetor = 1, SituacaoTara = ativo, PesoKg = 1.5m };

    private static void DefinirPermissao(params string[] acoes)
        => EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes = acoes.Select(acao => new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.Cadastro,
                Rotina = PermissoesSistema.Rotinas.Tara,
                Acao = acao
            }).ToArray(),
            IntegracaoBancoHabilitada = true
        });

    private sealed class RepositorioFake : TaraRepositorio
    {
        public bool NomeExiste { get; init; }
        public TaraCadastro? Existente { get; init; }
        public TaraCadastro? Inserido { get; private set; }
        public TaraCadastro? Atualizado { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<TaraCadastro?> ObterPorIdAsync(long codigoTara, CancellationToken cancellationToken = default)
            => Task.FromResult(Existente);

        public override Task<bool> ExisteNomeNoSetorTipoAsync(string nome, long codigoSetor, long codigoTipoTara, long? ignorarCodigo, CancellationToken cancellationToken = default)
            => Task.FromResult(NomeExiste);

        public override Task<long> InserirAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
        {
            Inserido = tara;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
        {
            Atualizado = tara;
            return Task.FromResult(1);
        }

        public override Task<int> ExcluirAsync(long codigoTara, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long codigoTara, CancellationToken cancellationToken = default)
        {
            ReativarChamadas++;
            return Task.FromResult(1);
        }
    }

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public AuditoriaSpy() : base(null!)
        {
        }

        public override Task RegistrarCadastroCriadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroAtualizadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroExcluidoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroReativadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
