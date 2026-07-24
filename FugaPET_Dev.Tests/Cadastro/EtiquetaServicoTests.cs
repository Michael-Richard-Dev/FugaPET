using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class EtiquetaServicoTests : IDisposable
{
    [Fact]
    public async Task Inserir_ValidaNormalizaEInsereEtiquetaAtiva()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        EtiquetaServico servico = CriarServico(repositorio);

        ResultadoOperacao resultado = await servico.InserirAsync(new EtiquetaCadastro
        {
            CodigoInterno = "  cx-01  ",
            NomeEtiqueta = "  Caixa padrão  ",
            TipoEtiqueta = " caixa ",
            CodigoModeloEtiqueta = 10,
            DescricaoEtiqueta = "  descrição  ",
            SituacaoEtiqueta = true
        });

        Assert.True(resultado.Sucesso);
        Assert.NotNull(repositorio.Inserida);
        Assert.Equal("cx-01", repositorio.Inserida!.CodigoInterno);
        Assert.Equal("Caixa padrão", repositorio.Inserida.NomeEtiqueta);
        Assert.Equal("CAIXA", repositorio.Inserida.TipoEtiqueta);
        Assert.Equal("descrição", repositorio.Inserida.DescricaoEtiqueta);
        Assert.True(repositorio.Inserida.SituacaoEtiqueta);
    }

    [Fact]
    public async Task Inserir_EtiquetaInativa_BloqueiaSemCorrigirSilenciosamente()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        EtiquetaCadastro etiqueta = EtiquetaValida(ativa: false);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(etiqueta);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemNovaDeveSerAtiva, resultado.Mensagem);
        Assert.False(etiqueta.SituacaoEtiqueta);
        Assert.Null(repositorio.Inserida);
    }

    [Theory]
    [InlineData("", "Etiqueta válida", "CAIXA")]
    [InlineData("COD", "A", "CAIXA")]
    [InlineData("COD", "Etiqueta válida", "LIVRE")]
    public async Task Inserir_DadosInvalidos_Bloqueia(string codigo, string nome, string tipo)
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new EtiquetaCadastro
        {
            CodigoInterno = codigo,
            NomeEtiqueta = nome,
            TipoEtiqueta = tipo,
            CodigoModeloEtiqueta = 10
        });

        Assert.False(resultado.Sucesso);
        Assert.Null(repositorio.Inserida);
    }

    [Fact]
    public async Task Inserir_LimitesExcedidos_Bloqueia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new EtiquetaCadastro
        {
            CodigoInterno = new string('C', 81),
            NomeEtiqueta = new string('N', 81),
            TipoEtiqueta = "CAIXA",
            CodigoModeloEtiqueta = 10,
            DescricaoEtiqueta = new string('D', 256)
        });

        Assert.False(resultado.Sucesso);
        Assert.Null(repositorio.Inserida);
    }

    [Fact]
    public async Task Inserir_DuplicidadeGlobalAtivaOuInativa_Bloqueia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new() { CodigoExiste = true };

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(EtiquetaValida());

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemDuplicidadeGlobal, resultado.Mensagem);
        Assert.Null(repositorio.Inserida);
    }

    [Fact]
    public async Task Atualizar_TentativaDeInativar_BloqueiaAntesDoUpdate()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            Existente = EtiquetaValida(1, true)
        };
        EtiquetaCadastro alterada = EtiquetaValida(1, false);

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(alterada);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemInativarPelaAcao, resultado.Mensagem);
        Assert.Null(repositorio.Atualizada);
    }

    [Fact]
    public async Task Atualizar_TentativaDeReativar_BloqueiaAntesDoUpdate()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            Existente = EtiquetaValida(1, false)
        };
        EtiquetaCadastro alterada = EtiquetaValida(1, true);

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(alterada);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemReativarPelaAcao, resultado.Mensagem);
        Assert.Null(repositorio.Atualizada);
    }

    [Fact]
    public async Task Atualizar_DadosValidos_PreservaSituacaoEAtualiza()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { Existente = EtiquetaValida(1, true) };
        EtiquetaCadastro alterada = EtiquetaValida(1, true);
        alterada.NomeEtiqueta = "Etiqueta atualizada";

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(alterada);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Etiqueta atualizada", repositorio.Atualizada?.NomeEtiqueta);
        Assert.True(repositorio.Atualizada?.SituacaoEtiqueta);
    }

    [Fact]
    public async Task Excluir_ComProdutoAtivoVinculado_Bloqueia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Existente = EtiquetaValida(1, true),
            ProdutosAtivos = 1
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(1);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemDependenciaProduto, resultado.Mensagem);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task Excluir_SemProdutoAtivo_Inativa()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new() { Existente = EtiquetaValida(1, true) };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(1);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task Reativar_ModeloInativo_Bloqueia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { Existente = EtiquetaValida(1, false) };
        EtiquetaServico servico = CriarServico(repositorio, modeloAtivo: false);

        ResultadoOperacao resultado = await servico.ReativarAsync(1);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemModeloInativoReativacao, resultado.Mensagem);
        Assert.Equal(0, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task Reativar_CodigoDuplicadoGlobal_Bloqueia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            Existente = EtiquetaValida(1, false),
            CodigoExiste = true
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(1);

        Assert.False(resultado.Sucesso);
        Assert.Equal(EtiquetaServico.MensagemDuplicidadeGlobal, resultado.Mensagem);
        Assert.Equal(0, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task Reativar_ModeloAtivoSemDuplicidade_Reativa()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { Existente = EtiquetaValida(1, false) };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(1);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task OperacaoSemPermissao_Bloqueia()
    {
        EstadoSessaoUsuarioAtual.Limpar();
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(EtiquetaValida());

        Assert.False(resultado.Sucesso);
        Assert.Null(repositorio.Inserida);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static EtiquetaServico CriarServico(RepositorioFake repositorio, bool modeloAtivo = true)
        => new(
            repositorio,
            (_, _) => Task.FromResult<ModeloEtiquetaCadastro?>(new ModeloEtiquetaCadastro
            {
                CodigoModeloEtiqueta = 10,
                NomeModeloEtiqueta = "Modelo",
                SituacaoModeloEtiqueta = modeloAtivo
            }),
            new AuditoriaSpy());

    private static EtiquetaCadastro EtiquetaValida(long id = 0, bool ativa = true) => new()
    {
        CodigoEtiqueta = id,
        CodigoModeloEtiqueta = 10,
        CodigoInterno = "ETQ-01",
        NomeEtiqueta = "Etiqueta válida",
        TipoEtiqueta = "CAIXA",
        SituacaoEtiqueta = ativa
    };

    private static void DefinirPermissao(params string[] acoes)
        => EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "teste",
            Nome = "Usuário de teste",
            IntegracaoBancoHabilitada = true,
            Permissoes = acoes.Select(acao => new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.Etiqueta,
                Rotina = PermissoesSistema.Rotinas.Etiqueta,
                Acao = acao
            }).ToArray()
        });

    private sealed class RepositorioFake : EtiquetaRepositorio
    {
        public RepositorioFake() : base(null!) { }

        public EtiquetaCadastro? Existente { get; init; }
        public bool CodigoExiste { get; init; }
        public int ProdutosAtivos { get; init; }
        public EtiquetaCadastro? Inserida { get; private set; }
        public EtiquetaCadastro? Atualizada { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public override Task<EtiquetaCadastro?> ObterPorIdAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
            => Task.FromResult(Existente);

        public override Task<bool> ExisteCodigoInternoAsync(string codigoInterno, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CodigoExiste);

        public override Task<long> InserirAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
        {
            Inserida = etiqueta;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
        {
            Atualizada = etiqueta;
            return Task.FromResult(1);
        }

        public override Task<int> ExcluirAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
        {
            ReativarChamadas++;
            return Task.FromResult(1);
        }

        public override Task<ResumoDependenciasEtiqueta> ObterResumoDependenciasAtivasAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResumoDependenciasEtiqueta { ProdutosAtivosVinculados = ProdutosAtivos });
    }

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public AuditoriaSpy() : base(null!) { }

        public override Task RegistrarCadastroCriadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroAtualizadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroExcluidoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarCadastroReativadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
