using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class SetorServicoTests : IDisposable
{
    public SetorServicoTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
    }

    [Fact]
    public async Task InserirAsync_DeveInserirSetorValido()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        SetorServico servico = CriarServico(repositorio);

        ResultadoOperacao resultado = await servico.InserirAsync(new SetorCadastro
        {
            NomeSetor = "  Expedicao  ",
            DescricaoSetor = "Setor de expedicao",
            SituacaoSetor = true
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal(101, resultado.IdGerado);
        Assert.Equal("Expedicao", repositorio.SetorInserido?.NomeSetor);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeVazio()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro());

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do setor deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.SetorInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComUmCaractere()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro
        {
            NomeSetor = " A "
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do setor deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.SetorInserido);
    }

    [Fact]
    public async Task InserirAsync_DevePermitirNomeComDoisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro
        {
            NomeSetor = " AB "
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("AB", repositorio.SetorInserido?.NomeSetor);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComOitentaEUmCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro
        {
            NomeSetor = new string('A', 81)
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do setor deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.SetorInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearDescricaoComDuzentosECinquentaESeisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro
        {
            NomeSetor = "Entrada",
            DescricaoSetor = new string('D', 256)
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Descrição do setor deve ter no máximo 255 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.SetorInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeDuplicadoAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new() { NomeExiste = true };

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new SetorCadastro
        {
            NomeSetor = "Entrada"
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("Ja existe", resultado.Mensagem);
        Assert.Null(repositorio.SetorInserido);
    }

    [Fact]
    public async Task AtualizarAsync_DeveEditarDescricao()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            SetorExistente = Setor(10, "Entrada", ativo: true)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new SetorCadastro
        {
            CodigoSetor = 10,
            NomeSetor = "Entrada",
            DescricaoSetor = "Descricao atualizada",
            SituacaoSetor = true
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("Descricao atualizada", repositorio.SetorAtualizado?.DescricaoSetor);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearInativacaoDeSetorAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            SetorExistente = Setor(10, "Entrada", ativo: true)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new SetorCadastro
        {
            CodigoSetor = 10,
            NomeSetor = "Entrada",
            SituacaoSetor = false
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "A inativação do setor deve ser feita pela ação Inativar, pois exige validação de dependências.",
            resultado.Mensagem);
        Assert.Null(repositorio.SetorAtualizado);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearReativacaoDeSetorInativo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            SetorExistente = Setor(10, "Entrada", ativo: false)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new SetorCadastro
        {
            CodigoSetor = 10,
            NomeSetor = "Entrada",
            SituacaoSetor = true
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("A reativação do setor deve ser feita pela ação Reativar.", resultado.Mensagem);
        Assert.Null(repositorio.SetorAtualizado);
    }

    [Fact]
    public async Task ExcluirAsync_DeveInativarSetorSemDependencia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearSetorComUsuarioAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasSetor { UsuariosPadraoAtivos = 1 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("usuario", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearSetorComBalancaAtiva()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasSetor { BalancasAtivas = 2 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("balancas ativas", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearSetorComTaraAtiva()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasSetor { TarasAtivas = 1 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("tara ativa", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearSetorConfiguradoNoTerminalLocal()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new();
        SetorServico servico = new(repositorio, new AuditoriaSpy(), () => 10);

        ResultadoOperacao resultado = await servico.ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("terminal", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.DiagnosticoChamadas);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ReativarAsync_DeveReativarSetorInativo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            SetorExistente = Setor(10, "Entrada", ativo: false)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task ReativarAsync_DeveBloquearNomeDuplicadoAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            SetorExistente = Setor(10, "Entrada", ativo: false),
            NomeExiste = true
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Já existe um setor ativo com este nome. Não é possível reativar este setor.", resultado.Mensagem);
        Assert.Equal(0, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearOperacaoSemPermissao()
    {
        DefinirPermissao();
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("sem permissao", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.DiagnosticoChamadas);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static SetorServico CriarServico(RepositorioFake repositorio)
        => new(repositorio, new AuditoriaSpy(), () => null);

    private static SetorCadastro Setor(long codigo, string nome, bool ativo)
        => new()
        {
            CodigoSetor = codigo,
            NomeSetor = nome,
            SituacaoSetor = ativo
        };

    private static void DefinirPermissao(params string[] acoes)
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes = acoes
                .Select(acao => new PermissaoSessaoAplicacao
                {
                    Modulo = PermissoesSistema.Modulos.Cadastro,
                    Rotina = PermissoesSistema.Rotinas.Setor,
                    Acao = acao
                })
                .ToArray(),
            IntegracaoBancoHabilitada = true
        });
    }

    private sealed class RepositorioFake : SetorRepositorio
    {
        public bool NomeExiste { get; init; }
        public SetorCadastro? SetorExistente { get; init; }
        public ResumoDependenciasSetor Dependencias { get; init; } = new();
        public SetorCadastro? SetorInserido { get; private set; }
        public SetorCadastro? SetorAtualizado { get; private set; }
        public int DiagnosticoChamadas { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<SetorCadastro?> ObterPorIdAsync(long codigoSetor, CancellationToken cancellationToken = default)
            => Task.FromResult(SetorExistente);

        public override Task<bool> ExisteNomeAsync(string nomeSetor, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(NomeExiste);

        public override Task<long> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        {
            SetorInserido = setor;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        {
            SetorAtualizado = setor;
            return Task.FromResult(1);
        }

        public override Task<ResumoDependenciasSetor> ObterResumoDependenciasAtivasAsync(long codigoSetor, CancellationToken cancellationToken = default)
        {
            DiagnosticoChamadas++;
            return Task.FromResult(Dependencias);
        }

        public override Task<int> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long id, CancellationToken cancellationToken = default)
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
