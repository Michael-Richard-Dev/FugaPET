using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class BalancaServicoTests : IDisposable
{
    public BalancaServicoTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
    }

    [Fact]
    public async Task InserirAsync_DeveInserirSerialValido()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(Serial());

        Assert.True(resultado.Sucesso);
        Assert.NotNull(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearSerialSemPortaSerial()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.PortaSerial = "  ";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Porta serial é obrigatória para conexão SERIAL.", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearSerialSemBaudRate()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.BaudRate = null;

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Baud rate é obrigatório para conexão SERIAL.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveNormalizarTipoEParidadeSerial()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.TipoConexao = " serial ";
        balanca.Paridade = " even ";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.True(resultado.Sucesso);
        Assert.Equal("SERIAL", repositorio.BalancaInserida?.TipoConexao);
        Assert.Equal("EVEN", repositorio.BalancaInserida?.Paridade);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearSerialComParidadeInvalida()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.Paridade = "INVALIDA";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Paridade inválida", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearSerialComStopBitsInvalido()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.StopBits = 3m;

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Stop bits inválido. Use: 1, 1.5 ou 2.", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearFlowControlInvalido()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Serial();
        balanca.FlowControl = "INVALIDO";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Controle de fluxo invalido", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearTipoConexaoAcimaDoLimite()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.TipoConexao = new string('A', 21);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Tipo de conexão deve ter no máximo 20 caracteres.", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveNormalizarParametrosTecnicos()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.ParametrosTecnicos = "  {\"modo\":\"teste\"}  ";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.True(resultado.Sucesso);
        Assert.Equal("{\"modo\":\"teste\"}", repositorio.BalancaInserida?.ParametrosTecnicos);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearTcpSemIp()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Tcp();
        balanca.EnderecoIp = "";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Endereço IP é obrigatório para conexão TCP_IP.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearTcpSemPorta()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Tcp();
        balanca.PortaTcp = null;

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Porta TCP é obrigatória para conexão TCP_IP.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearTcpComPortaForaDoIntervalo()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Tcp();
        balanca.PortaTcp = 70000;

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Porta TCP deve estar entre 1 e 65535.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveInserirManualSemIpNemPorta()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(Manual());

        Assert.True(resultado.Sucesso);
        Assert.NotNull(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveInserirUsbComIdentificacaoLocal()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(Usb());

        Assert.True(resultado.Sucesso);
        Assert.NotNull(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearUsbSemIdentificacaoLocal()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Usb();
        balanca.IdentificacaoLocal = "";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Identificação do local é obrigatória para conexão USB.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComUmCaractere()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.NomeBalanca = " A ";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome da balança deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DevePermitirNomeComDoisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.NomeBalanca = " AB ";

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.True(resultado.Sucesso);
        Assert.Equal("AB", repositorio.BalancaInserida?.NomeBalanca);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeComOitentaEUmCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.NomeBalanca = new string('A', 81);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Nome da balança deve ter entre 2 e 80 caracteres.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearObservacaoComDuzentosECinquentaESeisCaracteres()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        BalancaCadastro balanca = Manual();
        balanca.Observacao = new string('O', 256);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Observação deve ter no máximo 255 caracteres.", resultado.Mensagem);
    }

    [Fact]
    public async Task InserirAsync_DeveBloquearNomeDuplicadoAtivoNoMesmoSetor()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        repositorio.NomesAtivos.Add(("BALANCA X", 1));
        BalancaCadastro balanca = Manual("Balanca X", setor: 1);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Contains("mesmo setor", resultado.Mensagem);
        Assert.Null(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task InserirAsync_DevePermitirMesmoNomeEmSetorDiferente()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Criar);
        RepositorioFake repositorio = new();
        repositorio.NomesAtivos.Add(("BALANCA X", 1));
        BalancaCadastro balanca = Manual("Balanca X", setor: 2);

        ResultadoOperacao resultado = await CriarServico(repositorio).InserirAsync(balanca);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(repositorio.BalancaInserida);
    }

    [Fact]
    public async Task AtualizarAsync_DeveEditarDadosValidos()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { BalancaExistente = Serial("Balanca 1", setor: 1, ativo: true, id: 10) };
        BalancaCadastro balanca = Serial("Balanca 1", setor: 1, ativo: true, id: 10);
        balanca.Observacao = "Atualizada";

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(balanca);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Atualizada", repositorio.BalancaAtualizada?.Observacao);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearInativacaoDeBalancaAtiva()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { BalancaExistente = Serial(ativo: true, id: 10) };
        BalancaCadastro balanca = Serial(ativo: false, id: 10);

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "A inativação da balança deve ser feita pela ação Inativar, pois exige validação de dependências.",
            resultado.Mensagem);
        Assert.Null(repositorio.BalancaAtualizada);
    }

    [Fact]
    public async Task AtualizarAsync_DeveBloquearReativacaoDeBalancaInativa()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { BalancaExistente = Serial(ativo: false, id: 10) };
        BalancaCadastro balanca = Serial(ativo: true, id: 10);

        ResultadoOperacao resultado = await CriarServico(repositorio).AtualizarAsync(balanca);

        Assert.False(resultado.Sucesso);
        Assert.Equal("A reativação da balança deve ser feita pela ação Reativar.", resultado.Mensagem);
        Assert.Null(repositorio.BalancaAtualizada);
    }

    [Fact]
    public async Task ExcluirAsync_DeveInativarBalancaSemDependencia()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new();

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearComPesagemOperacional()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasBalanca { PesagensEntradaProduto = 1 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("pesagem", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearComHuCaixaAtiva()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new()
        {
            Dependencias = new ResumoDependenciasBalanca { HusCaixaAtivas = 1 }
        };

        ResultadoOperacao resultado = await CriarServico(repositorio).ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("registro operacional", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ExcluirAsync_DeveBloquearBalancaPadraoDeTerminal()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Excluir);
        RepositorioFake repositorio = new();
        BalancaServico servico = new(repositorio, new AuditoriaSpy(), () => [10L]);

        ResultadoOperacao resultado = await servico.ExcluirAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("terminal", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repositorio.DiagnosticoChamadas);
        Assert.Equal(0, repositorio.ExcluirChamadas);
    }

    [Fact]
    public async Task ReativarAsync_DeveReativarBalancaInativa()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { BalancaExistente = Serial(ativo: false, id: 10) };

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, repositorio.ReativarChamadas);
    }

    [Fact]
    public async Task ReativarAsync_DeveBloquearNomeDuplicadoAtivoNoMesmoSetor()
    {
        DefinirPermissao(PermissoesSistema.Acoes.Editar);
        RepositorioFake repositorio = new() { BalancaExistente = Serial("Balanca X", setor: 1, ativo: false, id: 10) };
        repositorio.NomesAtivos.Add(("BALANCA X", 1));

        ResultadoOperacao resultado = await CriarServico(repositorio).ReativarAsync(10);

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "Já existe uma balança ativa com este nome neste setor. Não é possível reativar esta balança.",
            resultado.Mensagem);
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

    private static BalancaServico CriarServico(RepositorioFake repositorio)
        => new(repositorio, new AuditoriaSpy(), () => Array.Empty<long>());

    private static BalancaCadastro Serial(string nome = "Balanca Serial", long setor = 1, bool ativo = true, long id = 0)
        => new()
        {
            CodigoBalanca = id,
            CodigoSetor = setor,
            NomeBalanca = nome,
            TipoConexao = "SERIAL",
            PortaSerial = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            Paridade = "NONE",
            StopBits = 1m,
            SituacaoBalanca = ativo
        };

    private static BalancaCadastro Tcp(string nome = "Balanca TCP", long setor = 1)
        => new()
        {
            CodigoSetor = setor,
            NomeBalanca = nome,
            TipoConexao = "TCP_IP",
            EnderecoIp = "192.168.0.10",
            PortaTcp = 4001
        };

    private static BalancaCadastro Usb(string nome = "Balanca USB", long setor = 1)
        => new()
        {
            CodigoSetor = setor,
            NomeBalanca = nome,
            TipoConexao = "USB",
            IdentificacaoLocal = "USB-001"
        };

    private static BalancaCadastro Manual(string nome = "Balanca Manual", long setor = 1)
        => new()
        {
            CodigoSetor = setor,
            NomeBalanca = nome,
            TipoConexao = "MANUAL"
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
                    Rotina = PermissoesSistema.Rotinas.Balanca,
                    Acao = acao
                })
                .ToArray(),
            IntegracaoBancoHabilitada = true
        });
    }

    private sealed class RepositorioFake : BalancaRepositorio
    {
        public BalancaCadastro? BalancaExistente { get; init; }
        public ResumoDependenciasBalanca Dependencias { get; init; } = new();
        public HashSet<(string Nome, long Setor)> NomesAtivos { get; } = new();
        public BalancaCadastro? BalancaInserida { get; private set; }
        public BalancaCadastro? BalancaAtualizada { get; private set; }
        public int DiagnosticoChamadas { get; private set; }
        public int ExcluirChamadas { get; private set; }
        public int ReativarChamadas { get; private set; }

        public RepositorioFake() : base(null!)
        {
        }

        public override Task<BalancaCadastro?> ObterPorIdAsync(long codigoBalanca, CancellationToken cancellationToken = default)
            => Task.FromResult(BalancaExistente);

        public override Task<bool> ExisteNomeNoSetorAsync(string nome, long codigoSetor, long? ignorarCodigo, CancellationToken cancellationToken = default)
            => Task.FromResult(NomesAtivos.Contains((nome.Trim().ToUpperInvariant(), codigoSetor)));

        public override Task<long> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        {
            BalancaInserida = balanca;
            return Task.FromResult(101L);
        }

        public override Task<int> AtualizarAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        {
            BalancaAtualizada = balanca;
            return Task.FromResult(1);
        }

        public override Task<ResumoDependenciasBalanca> ObterResumoDependenciasAtivasAsync(long codigoBalanca, CancellationToken cancellationToken = default)
        {
            DiagnosticoChamadas++;
            return Task.FromResult(Dependencias);
        }

        public override Task<int> ExcluirAsync(long codigoBalanca, CancellationToken cancellationToken = default)
        {
            ExcluirChamadas++;
            return Task.FromResult(1);
        }

        public override Task<int> ReativarAsync(long codigoBalanca, CancellationToken cancellationToken = default)
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
