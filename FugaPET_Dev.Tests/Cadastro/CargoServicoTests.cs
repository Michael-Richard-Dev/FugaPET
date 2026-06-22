using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CargoServicoTests : IDisposable
{
    public CargoServicoTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
    }

    [Fact]
    public async Task InserirAsync_DeveInserirCargoValido()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = "  Operador  ",
            DescricaoCargo = "Operador de producao",
            SituacaoCargo = true
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal(101, resultado.IdGerado);
        Assert.Equal("Operador", repositorio.CargoInserido?.NomeCargo);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeVazio()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro());

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do cargo deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.CargoInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComUmCaractere()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = " A "
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do cargo deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.CargoInserido);
    }

    [Fact]
    public async Task InserirAsync_DevePermitirNomeComDoisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = " AB "
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("AB", repositorio.CargoInserido?.NomeCargo);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComOitentaEUmCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = new string('A', 81)
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome do cargo deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.CargoInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearDescricaoComDuzentosECinquentaESeisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = "Operador",
            DescricaoCargo = new string('D', 256)
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("Descrição do cargo deve ter no máximo 255 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.CargoInserido);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeDuplicadoAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new() { NomeExiste = true };

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(new CargoCadastro
        {
            NomeCargo = "Operador"
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("Ja existe", resultado.Mensagem);
        Assert.Null(repositorio.CargoInserido);
    }

    [Fact]
    public async Task AtualizarAsync_DeveEditarDescricao()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            CargoExistente = Cargo(10, "Operador", ativo: true)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new CargoCadastro
        {
            IdCargo = 10,
            NomeCargo = "Operador",
            DescricaoCargo = "Descricao atualizada",
            SituacaoCargo = true
        });

        Assert.True(resultado.Sucesso);
        Assert.Equal("Descricao atualizada", repositorio.CargoAtualizado?.DescricaoCargo);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearInativacaoDeCargoAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            CargoExistente = Cargo(10, "Operador", ativo: true)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new CargoCadastro
        {
            IdCargo = 10,
            NomeCargo = "Operador",
            SituacaoCargo = false
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "A inativação do cargo deve ser feita pela ação Inativar, pois exige validação de dependências.",
            resultado.Mensagem);
        Assert.Null(repositorio.CargoAtualizado);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearReativacaoDeCargoInativo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            CargoExistente = Cargo(10, "Operador", ativo: false)
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(new CargoCadastro
        {
            IdCargo = 10,
            NomeCargo = "Operador",
            SituacaoCargo = true
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal("A reativação do cargo deve ser feita pela ação Reativar.", resultado.Mensagem);
        Assert.Null(repositorio.CargoAtualizado);
    }

    [Fact]
    public async Task ExcluirAsync_DeveInativarCargoSemDependencia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearCargoComUsuarioAtivo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasCargo { UsuariosAtivos = 1 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("usuario", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ReativarAsync_DeveReativarCargoInativo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new()
        {
            CargoExistente = Cargo(10, "Operador", ativo: false)
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
            CargoExistente = Cargo(10, "Operador", ativo: false),
            NomeExiste = true
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Já existe um cargo ativo com este nome. Não é possível reativar este cargo.", resultado.Mensagem);
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

    private static CargoServico CriarServico(RepositorioFake repositorio)
        => new(repositorio, new AuditoriaSpy());

    private static CargoCadastro Cargo(long id, string nome, bool ativo)
        => new()
        {
            IdCargo = id,
            NomeCargo = nome,
            SituacaoCargo = ativo
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
                    Rotina = PermissoesSistema.Rotinas.Cargo,
                    Acao = acao
                })
                .ToArray(),
            IntegracaoBancoHabilitada = true
        });
    }

    private sealed class RepositorioFake : CargoRepositorio
    {
        public bool NomeExiste { get; init; }
        public CargoCadastro? CargoExistente { get; init; }
        public ResumoDependenciasCargo Dependencias { get; init; } = new();
        public CargoCadastro? CargoInserido { get; private set; }
        public CargoCadastro? CargoAtualizado { get; private set; }
        public int DiagnosticoChamadas { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<CargoCadastro?> ObterPorIdAsync(long codigoCargo, CancellationToken cancellationToken = default)
            => Task.FromResult(CargoExistente);

        public override Task<bool> ExisteNomeAsync(string nomeCargo, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(NomeExiste);

        public override Task<long> InserirAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
        {
            CargoInserido = cargo;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(CargoCadastro cargo, CancellationToken cancellationToken = default)
        {
            CargoAtualizado = cargo;
            return Task.FromResult(1);
        }

        public override Task<ResumoDependenciasCargo> ObterResumoDependenciasAtivasAsync(long codigoCargo, CancellationToken cancellationToken = default)
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
