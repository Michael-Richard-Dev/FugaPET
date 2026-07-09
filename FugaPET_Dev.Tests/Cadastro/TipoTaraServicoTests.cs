using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Tipo de Tara: regras do serviço (situação, duplicidade, e bloqueio de inativação com Tara ativa vinculada)
/// + helpers puros de situação/referência.
/// </summary>
public sealed class TipoTaraServicoTests : IDisposable
{
    public TipoTaraServicoTests()
        => Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

    // ---- inserir/duplicidade/nome ----

    [Fact]
    public async Task Inserir_TipoAtivoValido_DeveInserir()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = "  Plástico  ", SituacaoTipoTara = true });

        Assert.True(r.Sucesso);
        Assert.Equal("Plástico", repo.Inserido?.NomeTipoTara);
    }

    [Fact]
    public async Task Inserir_NomeVazio_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = "   " });

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_DuplicidadeAtiva_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new() { NomeExiste = true };

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = "Plástico" });

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_DuplicidadeGlobalIncluindoInativo_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new() { NomeExiste = true }; // existe inativo com o mesmo nome

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = "Teste3" });

        Assert.False(r.Sucesso);
        Assert.Equal(TipoTaraServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Null(repo.Inserido);
    }

    // ---- Ajuste 4: validações de tamanho ----

    [Theory]
    [InlineData("A")]      // 1 caractere < 2
    [InlineData("  A  ")]  // após trim = 1
    public async Task Inserir_NomeMenorQue2_DeveBloquear(string nome)
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = nome });

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_NomeMaiorQue80_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = new string('X', 81) });

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_DescricaoMaiorQue255_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repo = new();

        ResultadoOperacao r = await CriarServico(repo).InserirAsync(new TipoTaraCadastro { NomeTipoTara = "Plástico", DescricaoTipoTara = new string('D', 256) });

        Assert.False(r.Sucesso);
        Assert.Null(repo.Inserido);
    }

    // ---- Ajuste 2: situação muda apenas por Inativar/Reativar (não por AtualizarAsync) ----

    [Fact]
    public async Task Atualizar_TentaInativarPelaEdicao_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = true }
        };

        ResultadoOperacao r = await CriarServico(repo).AtualizarAsync(
            new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = false });

        Assert.False(r.Sucesso);
        Assert.Equal(TipoTaraServico.MensagemInativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_TentaReativarPelaEdicao_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = false }
        };

        ResultadoOperacao r = await CriarServico(repo).AtualizarAsync(
            new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = true });

        Assert.False(r.Sucesso);
        Assert.Equal(TipoTaraServico.MensagemReativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_SoNomeDescricao_PreservaSituacaoEPermite()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = true }
        };

        // O form envia a mesma situação; renomeia.
        ResultadoOperacao r = await CriarServico(repo).AtualizarAsync(
            new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico PET", SituacaoTipoTara = true });

        Assert.True(r.Sucesso);
        Assert.NotNull(repo.Atualizado);
        Assert.True(repo.Atualizado!.SituacaoTipoTara); // situação preservada
        Assert.Equal("Plástico PET", repo.Atualizado.NomeTipoTara);
    }

    [Fact]
    public async Task Atualizar_NomeDuplicadoGlobal_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = true },
            NomeExiste = true // já existe OUTRO registro (ativo ou inativo) com o nome novo
        };

        ResultadoOperacao r = await CriarServico(repo).AtualizarAsync(
            new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Vidro", SituacaoTipoTara = true });

        Assert.False(r.Sucesso);
        Assert.Equal(TipoTaraServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    // ---- Ajuste 3: bloqueio de inativação com Tara ativa vinculada (pela ação Inativar/Excluir) ----

    [Fact]
    public async Task Excluir_ComTaraAtivaVinculada_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repo = new() { TemTaraAtivaVinculada = true };

        ResultadoOperacao r = await CriarServico(repo).ExcluirAsync(10);

        Assert.False(r.Sucesso);
        Assert.Equal(TipoTaraServico.MensagemBloqueioInativacao, r.Mensagem);
        Assert.Equal(0, repo.ExcluirChamadas);
    }

    [Fact]
    public async Task Excluir_SemTaraAtivaVinculada_DevePermitir()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repo = new() { TemTaraAtivaVinculada = false };

        ResultadoOperacao r = await CriarServico(repo).ExcluirAsync(10);

        Assert.True(r.Sucesso);
        Assert.Equal(1, repo.ExcluirChamadas);
    }

    // ---- Ajuste 8: reativar respeitando duplicidade ativa ----

    [Fact]
    public async Task Reativar_ComNomeDuplicadoAtivo_DeveBloquear()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = false },
            NomeExiste = true
        };

        ResultadoOperacao r = await CriarServico(repo).ReativarAsync(10);

        Assert.False(r.Sucesso);
        Assert.Equal(0, repo.ReativarChamadas);
    }

    [Fact]
    public async Task Reativar_SemDuplicidade_DevePermitir()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repo = new()
        {
            Existente = new TipoTaraCadastro { CodigoTipoTara = 10, NomeTipoTara = "Plástico", SituacaoTipoTara = false },
            NomeExiste = false
        };

        ResultadoOperacao r = await CriarServico(repo).ReativarAsync(10);

        Assert.True(r.Sucesso);
        Assert.Equal(1, repo.ReativarChamadas);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static TipoTaraServico CriarServico(RepositorioFake repo) => new(repo, new AuditoriaSpy());

    private static void DefinirPermissao(params string[] acoes)
        => EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes = acoes.Select(acao => new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.Cadastro,
                Rotina = PermissoesSistema.Rotinas.TipoTara,
                Acao = acao
            }).ToArray(),
            IntegracaoBancoHabilitada = true
        });

    private sealed class RepositorioFake : TipoTaraRepositorio
    {
        public bool NomeExiste { get; init; }
        public bool TemTaraAtivaVinculada { get; init; }
        public TipoTaraCadastro? Existente { get; init; }
        public TipoTaraCadastro? Inserido { get; private set; }
        public TipoTaraCadastro? Atualizado { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<TipoTaraCadastro?> ObterPorIdAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
            => Task.FromResult(Existente);

        public override Task<bool> ExisteNomeAsync(string nome, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(NomeExiste);

        public override Task<bool> ExisteTaraAtivaVinculadaAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
            => Task.FromResult(TemTaraAtivaVinculada);

        public override Task<ResumoDependenciasTipoTara> ObterResumoDependenciasAtivasAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResumoDependenciasTipoTara { TarasAtivas = TemTaraAtivaVinculada ? 1 : 0 });

        public override Task<long> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
        {
            Inserido = tipo;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
        {
            Atualizado = tipo;
            return Task.FromResult(1);
        }

        public override Task<int> ExcluirAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
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

        public override Task RegistrarCadastroCriadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarCadastroAtualizadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarCadastroExcluidoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarCadastroReativadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
