using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Modelo de Etiqueta: regras do serviço alinhadas ao padrão maduro — validações, duplicidade GLOBAL
/// (nome+versão, ativo OU inativo), situação que muda só por Inativar/Reativar, e inativação bloqueada por
/// etiqueta ativa vinculada.
/// </summary>
public sealed class ModeloEtiquetaServicoTests : IDisposable
{
    public ModeloEtiquetaServicoTests()
        => Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

    private static ModeloEtiquetaCadastro Novo(
        string nome = "Etiqueta A",
        int versao = 1,
        int? dpi = 203,
        string zpl = "^XA^FO50,50^A0N,40,40^FDOla^FS^XZ",
        decimal? largura = null,
        decimal? altura = null,
        bool ativo = true)
        => new()
        {
            NomeModeloEtiqueta = nome,
            Versao = versao,
            Dpi = dpi,
            ConteudoZpl = zpl,
            LarguraMm = largura,
            AlturaMm = altura,
            Observacao = "obs",
            SituacaoModeloEtiqueta = ativo
        };

    // ---- validações de campos ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")] // < 2
    public async Task Inserir_NomeInvalido_DeveBloquear(string nome)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(nome: nome))).Sucesso);

    [Fact]
    public async Task Inserir_NomeMaior80_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(nome: new string('X', 81)))).Sucesso);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Inserir_VersaoZeroOuNegativa_DeveBloquear(int versao)
    {
        RepositorioFake repo = new();
        Assert.False((await Servico(repo).InserirAsync(Novo(versao: versao))).Sucesso);
        Assert.Null(repo.Inserido);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Inserir_ZplVazio_DeveBloquear(string zpl)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(zpl: zpl))).Sucesso);

    [Fact]
    public async Task Inserir_PreservaConteudoZplSemTrim()
    {
        RepositorioFake repo = new();
        const string zpl = "  ^XA\n^FO0,0^FDx^FS\n^XZ  ";
        await Servico(repo).InserirAsync(Novo(zpl: zpl));
        Assert.Equal(zpl, repo.Inserido!.ConteudoZpl); // sem Trim, quebras/espacos preservados
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Inserir_DpiInvalido_DeveBloquear(int dpi)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(dpi: dpi))).Sucesso);

    [Fact]
    public async Task Inserir_DpiNulo_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(dpi: null))).Sucesso);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Inserir_LarguraZeroOuNegativa_DeveBloquear(double largura)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(largura: (decimal)largura))).Sucesso);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Inserir_AlturaZeroOuNegativa_DeveBloquear(double altura)
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(altura: (decimal)altura))).Sucesso);

    [Fact]
    public async Task Inserir_LarguraComTresCasas_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(largura: 10.123m))).Sucesso);

    [Fact]
    public async Task Inserir_AlturaComTresCasas_DeveBloquear()
        => Assert.False((await Servico(new RepositorioFake()).InserirAsync(Novo(altura: 5.001m))).Sucesso);

    [Fact]
    public async Task Inserir_DimensoesComDuasCasas_DevePermitir()
    {
        RepositorioFake repo = new();
        Assert.True((await Servico(repo).InserirAsync(Novo(largura: 100.50m, altura: 80.25m))).Sucesso);
        Assert.Equal(100.50m, repo.Inserido!.LarguraMm);
        Assert.Equal(80.25m, repo.Inserido!.AlturaMm);
    }

    [Fact]
    public async Task Inserir_ObservacaoMaior255_DeveBloquear()
    {
        ModeloEtiquetaCadastro m = Novo();
        m.Observacao = new string('O', 256);
        Assert.False((await Servico(new RepositorioFake()).InserirAsync(m)).Sucesso);
    }

    // ---- duplicidade GLOBAL nome + versão ----

    [Fact]
    public async Task Inserir_DuplicidadeAtiva_DeveBloquear()
    {
        RepositorioFake repo = new() { NomeVersaoExiste = true };
        ResultadoOperacao r = await Servico(repo).InserirAsync(Novo());
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Null(repo.Inserido);
    }

    [Fact]
    public async Task Inserir_DuplicidadeInativa_DeveBloquear()
    {
        // Repo não filtra por situação: NomeVersaoExiste=true representa registro ativo OU inativo.
        RepositorioFake repo = new() { NomeVersaoExiste = true };
        ResultadoOperacao r = await Servico(repo).InserirAsync(Novo());
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemDuplicidadeGlobal, r.Mensagem);
    }

    [Fact]
    public async Task Inserir_MesmoNomeVersaoDiferente_DevePermitir()
    {
        RepositorioFake repo = new() { NomeVersaoExiste = false };
        Assert.True((await Servico(repo).InserirAsync(Novo(versao: 2))).Sucesso);
    }

    // ---- novo modelo deve nascer Ativo (defesa de servidor) ----

    [Fact]
    public async Task Inserir_ComSituacaoInativa_DeveBloquear()
    {
        RepositorioFake repo = new();
        ResultadoOperacao r = await Servico(repo).InserirAsync(Novo(ativo: false));
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemNovoDeveSerAtivo, r.Mensagem);
        Assert.Null(repo.Inserido); // não corrige false→true silenciosamente
    }

    [Fact]
    public async Task Inserir_ComSituacaoAtiva_DevePermitir()
    {
        RepositorioFake repo = new();
        ResultadoOperacao r = await Servico(repo).InserirAsync(Novo(ativo: true));
        Assert.True(r.Sucesso);
        Assert.NotNull(repo.Inserido);
        Assert.True(repo.Inserido!.SituacaoModeloEtiqueta);
    }

    // ---- situação muda só por Inativar/Reativar ----

    [Fact]
    public async Task Atualizar_TentaInativarPelaEdicao_DeveBloquear()
    {
        RepositorioFake repo = new() { Existente = Comp(10, true) };
        ModeloEtiquetaCadastro m = Novo(ativo: false);
        m.CodigoModeloEtiqueta = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(m);
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemInativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_TentaReativarPelaEdicao_DeveBloquear()
    {
        RepositorioFake repo = new() { Existente = Comp(10, false) };
        ModeloEtiquetaCadastro m = Novo(ativo: true);
        m.CodigoModeloEtiqueta = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(m);
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemReativarPelaAcao, r.Mensagem);
        Assert.Null(repo.Atualizado);
    }

    [Fact]
    public async Task Atualizar_SoDados_PreservaSituacaoEPermite()
    {
        RepositorioFake repo = new() { Existente = Comp(10, true) };
        ModeloEtiquetaCadastro m = Novo(nome: "Etiqueta B", ativo: true);
        m.CodigoModeloEtiqueta = 10;
        ResultadoOperacao r = await Servico(repo).AtualizarAsync(m);
        Assert.True(r.Sucesso);
        Assert.NotNull(repo.Atualizado);
        Assert.True(repo.Atualizado!.SituacaoModeloEtiqueta);
        Assert.Equal("Etiqueta B", repo.Atualizado.NomeModeloEtiqueta);
    }

    // ---- inativação x etiqueta ativa vinculada ----

    [Fact]
    public async Task Excluir_ComEtiquetaAtivaVinculada_DeveBloquear()
    {
        RepositorioFake repo = new() { Existente = Comp(10, true), EtiquetaAtivaVinculada = true };
        ResultadoOperacao r = await Servico(repo).ExcluirAsync(10);
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemEtiquetaAtivaVinculada, r.Mensagem);
        Assert.Equal(0, repo.ExcluirChamadas);
    }

    [Fact]
    public async Task Excluir_SemEtiquetaAtiva_DevePermitir()
    {
        RepositorioFake repo = new() { Existente = Comp(10, true), EtiquetaAtivaVinculada = false };
        ResultadoOperacao r = await Servico(repo).ExcluirAsync(10);
        Assert.True(r.Sucesso);
        Assert.Equal(1, repo.ExcluirChamadas);
    }

    [Fact]
    public async Task Excluir_ComEtiquetaInativa_NaoBloqueia()
    {
        // Só etiqueta ATIVA bloqueia: etiqueta inativa não conta como dependência.
        RepositorioFake repo = new() { Existente = Comp(10, true), EtiquetaAtivaVinculada = false };
        Assert.True((await Servico(repo).ExcluirAsync(10)).Sucesso);
    }

    // ---- reativação respeita duplicidade ----

    [Fact]
    public async Task Reativar_SemDuplicidade_Permite()
    {
        RepositorioFake repo = new() { Existente = Comp(10, false), NomeVersaoExiste = false };
        Assert.True((await Servico(repo).ReativarAsync(10)).Sucesso);
        Assert.Equal(1, repo.ReativarChamadas);
    }

    [Fact]
    public async Task Reativar_ComDuplicidade_DeveBloquear()
    {
        RepositorioFake repo = new() { Existente = Comp(10, false), NomeVersaoExiste = true };
        ResultadoOperacao r = await Servico(repo).ReativarAsync(10);
        Assert.False(r.Sucesso);
        Assert.Equal(ModeloEtiquetaServico.MensagemDuplicidadeGlobal, r.Mensagem);
        Assert.Equal(0, repo.ReativarChamadas);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static ModeloEtiquetaServico Servico(RepositorioFake repo)
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar, PermissoesSistema.Acoes.Editar, PermissoesSistema.Acoes.Excluir);
        return new ModeloEtiquetaServico(repo, new AuditoriaSpy());
    }

    private static ModeloEtiquetaCadastro Comp(long id, bool ativo)
        => new() { CodigoModeloEtiqueta = id, NomeModeloEtiqueta = "Etiqueta A", Versao = 1, Dpi = 203, ConteudoZpl = "^XA^XZ", SituacaoModeloEtiqueta = ativo };

    private static void DefinirPermissao(params string[] acoes)
        => EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes = acoes.Select(acao => new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.Etiqueta,
                Rotina = PermissoesSistema.Rotinas.ModeloEtiqueta,
                Acao = acao
            }).ToArray(),
            IntegracaoBancoHabilitada = true
        });

    private sealed class RepositorioFake : ModeloEtiquetaRepositorio
    {
        public bool NomeVersaoExiste { get; init; }
        public bool EtiquetaAtivaVinculada { get; init; }
        public ModeloEtiquetaCadastro? Existente { get; init; }
        public ModeloEtiquetaCadastro? Inserido { get; private set; }
        public ModeloEtiquetaCadastro? Atualizado { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long codigo, CancellationToken cancellationToken = default)
            => Task.FromResult(Existente);

        public override Task<bool> ExisteNomeVersaoAsync(string nome, int versao, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(NomeVersaoExiste);

        public override Task<bool> ExisteEtiquetaAtivaVinculadaAsync(long codigo, CancellationToken cancellationToken = default)
            => Task.FromResult(EtiquetaAtivaVinculada);

        public override Task<long> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
        {
            Inserido = modelo;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
        {
            Atualizado = modelo;
            return Task.FromResult(1);
        }

        public override Task<int> ExcluirAsync(long codigo, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long codigo, CancellationToken cancellationToken = default)
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
