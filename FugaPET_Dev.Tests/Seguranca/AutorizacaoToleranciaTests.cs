using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;
using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Liberacao GERENCIAL do estouro de tolerancia (incremento 070, pedido do usuario em 24/11/2026).
/// <para>
/// O operador segue logado; o gerente digita a propria credencial e autoriza AQUELA pesagem.
/// </para>
/// </summary>
public sealed class AutorizacaoToleranciaTests
{
    // ---------------------------------------------------------------- dublês

    private sealed class RepositorioFalso : LiberacaoToleranciaRepositorio
    {
        public readonly List<LiberacaoToleranciaRecebimento> Gravadas = [];
        public bool Explode;

        public RepositorioFalso() : base(new FabricaConexaoNula()) { }

        /// <summary>Quantas vezes o repositorio foi acionado — uma gravacao por CHAMADA, nao por linha.</summary>
        public int Chamadas;

        public override Task<long> RegistrarAsync(
            LiberacaoToleranciaRecebimento liberacao, CancellationToken cancellationToken = default)
        {
            if (Explode) { throw new InvalidOperationException("banco fora"); }
            Chamadas++;
            Gravadas.Add(liberacao);
            return Task.FromResult((long)Gravadas.Count);
        }

        public override Task RegistrarLoteAsync(
            IReadOnlyList<LiberacaoToleranciaRecebimento> liberacoes,
            CancellationToken cancellationToken = default)
        {
            if (Explode) { throw new InvalidOperationException("banco fora"); }
            Chamadas++;
            Gravadas.AddRange(liberacoes);
            return Task.CompletedTask;
        }
    }

    private sealed class FabricaConexaoNula : IFabricaConexaoBanco
    {
        public Npgsql.NpgsqlConnection CriarConexao() => throw new NotSupportedException();
        public Task<Npgsql.NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public string ObterConnectionString() => throw new NotSupportedException();
    }

    private static ContextoEstouroTolerancia Contexto() => new()
    {
        NumeroPedido = "4500001234",
        NumeroItem = "00010",
        Material = "2000042",
        QuantidadePedida = 1000m,
        QuantidadeApurada = 1102m,
        LimiteAplicado = 1100m,
        PercentualTolerancia = 10m,
        TipoEstouro = "ACIMA_DO_TETO",
        LoginOperador = "OPERADOR",
        Terminal = "TERMINAL_TI_TESTE",
    };

    private static SessaoUsuarioAplicacao Gerente(bool comPermissao) => new()
    {
        IdUsuario = 42,
        Login = "GERENTE",
        Nome = "Gerente",
        PerfisCodigo = ["GERENTE"],
        Permissoes = comPermissao
            ? [new PermissaoSessaoAplicacao
                {
                    Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                    Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                    Acao = PermissoesSistema.Acoes.LiberarTolerancia,
                }]
            : [],
    };

    private static AutorizacaoToleranciaServico Servico(
        RepositorioFalso repo, Func<string, string, ResultadoAutenticacao> autenticar)
        => new((login, senha, ct) => Task.FromResult(autenticar(login, senha)), repo);

    // ---------------------------------------------------------------- caminho feliz

    [Fact]
    public async Task GerenteComPermissao_Autoriza_E_Registra()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Ok(Gerente(true)));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync("GERENTE", "senha", Contexto());

        Assert.True(r.Autorizada);
        LiberacaoToleranciaRecebimento gravada = Assert.Single(repo.Gravadas);
        Assert.Equal(LiberacaoToleranciaRecebimento.Autorizada, gravada.Resultado);
        Assert.Equal(42, gravada.CodigoUsuarioAutorizador);
        Assert.Equal("GERENTE", gravada.LoginAutorizador);
        // Os numeros do estouro vao congelados: a tolerancia cadastrada pode mudar depois.
        Assert.Equal(1102m, gravada.Contexto.QuantidadeApurada);
        Assert.Equal(1100m, gravada.Contexto.LimiteAplicado);
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task CredencialInvalida_Recusa_E_RegistraATentativa()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Falha("x"));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync("FULANO", "errada", Contexto());

        Assert.False(r.Autorizada);
        // A tentativa RECUSADA e gravada de proposito: um log so de sucessos nao mostra alguem
        // tentando adivinhar credencial de gerente.
        LiberacaoToleranciaRecebimento g = Assert.Single(repo.Gravadas);
        Assert.Equal(LiberacaoToleranciaRecebimento.Recusada, g.Resultado);
        Assert.Equal("FULANO", g.LoginAutorizador);
        Assert.Null(g.CodigoUsuarioAutorizador);
    }

    /// <summary>
    /// Usuario que EXISTE e autentica, mas nao tem a acao. A mensagem e especifica de proposito: a
    /// pessoa esta em pe na balanca e precisa saber que errou de usuario, nao de senha.
    /// </summary>
    [Fact]
    public async Task UsuarioSemPermissao_Recusa_ComMotivoEspecifico()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Ok(Gerente(false)));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync("OPERADOR", "senha", Contexto());

        Assert.False(r.Autorizada);
        Assert.Equal(AutorizacaoToleranciaServico.MotivoSemPermissao, r.Mensagem);
        LiberacaoToleranciaRecebimento g = Assert.Single(repo.Gravadas);
        Assert.Equal(42, g.CodigoUsuarioAutorizador);   // identificado, mas sem a acao
        Assert.Equal(AutorizacaoToleranciaServico.MotivoSemPermissao, g.MotivoRecusa);
    }

    [Theory]
    [InlineData("", "senha")]
    [InlineData("   ", "senha")]
    [InlineData("GERENTE", "")]
    public async Task SemCredencialDigitada_NemTentaAutenticar(string login, string senha)
    {
        RepositorioFalso repo = new();
        bool tentou = false;
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => { tentou = true; return ResultadoAutenticacao.Ok(Gerente(true)); });

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync(login, senha, Contexto());

        Assert.False(r.Autorizada);
        Assert.False(tentou);
        Assert.Empty(repo.Gravadas);   // a tabela exige login nao vazio; inventar sujaria a auditoria
    }

    // ---------------------------------------------------------------- fail-closed

    /// <summary>
    /// O PONTO MAIS IMPORTANTE: se a liberacao nao pode ser REGISTRADA, ela nao vale. Autorizar sem
    /// registro produziria exatamente o que o log existe para impedir — recebimento fora do limite
    /// sem ninguem respondendo por ele.
    /// </summary>
    [Fact]
    public async Task FalhaAoRegistrar_NAO_Autoriza()
    {
        RepositorioFalso repo = new() { Explode = true };
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Ok(Gerente(true)));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync("GERENTE", "senha", Contexto());

        Assert.False(r.Autorizada);
        Assert.Contains("não pôde ser registrada", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaAoAutenticar_NAO_Autoriza()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => throw new InvalidOperationException("banco fora"));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync("GERENTE", "senha", Contexto());

        Assert.False(r.Autorizada);
    }

    // ---------------------------------------------------------------- permissao, nao perfil

    /// <summary>
    /// Quem libera e quem tem a ACAO, nao quem se chama "Gerente". Checar pelo nome do perfil
    /// amarraria a regra a um texto que qualquer renomeacao quebraria em silencio.
    /// </summary>
    [Fact]
    public void PerfilChamadoGerenteSemAAcao_NaoLibera()
    {
        SessaoUsuarioAplicacao falso = new()
        {
            IdUsuario = 7,
            Login = "X",
            PerfisCodigo = ["GERENTE"],
            Permissoes = [],
        };

        Assert.False(AutorizacaoToleranciaServico.PossuiPermissaoDeLiberacao(falso));
        Assert.True(AutorizacaoToleranciaServico.PossuiPermissaoDeLiberacao(Gerente(true)));
    }

    // ---------------------------------------------------------------- finalizacao: varios itens

    private static ContextoEstouroTolerancia Falta(string item, string material, decimal apurada)
        => new()
        {
            NumeroPedido = "4500001234",
            NumeroItem = item,
            Material = material,
            QuantidadePedida = 1000m,
            QuantidadeApurada = apurada,
            LimiteAplicado = 900m,
            PercentualTolerancia = 10m,
            TipoEstouro = "ABAIXO_DO_PISO",
            LoginOperador = "OPERADOR",
            Terminal = "TERMINAL_TI_TESTE",
        };

    /// <summary>
    /// UMA senha digitada, UMA gravacao, mas UMA LINHA POR ITEM. Resumir os tres itens numa linha so
    /// tiraria do log justamente o que ele existe para responder: qual material faltou, e quanto.
    /// </summary>
    [Fact]
    public async Task VariosItensAbaixoDoPiso_UmaAutenticacao_UmaLinhaPorItem()
    {
        RepositorioFalso repo = new();
        int autenticacoes = 0;
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) =>
        {
            autenticacoes++;
            return ResultadoAutenticacao.Ok(Gerente(true));
        });

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync(
            "GERENTE", "senha",
            [Falta("00010", "2000042", 880m), Falta("00020", "2000043", 700m), Falta("00030", "2000044", 10m)]);

        Assert.True(r.Autorizada);
        Assert.Equal(1, autenticacoes);
        Assert.Equal(1, repo.Chamadas);      // uma transacao, nao tres
        Assert.Equal(3, repo.Gravadas.Count);
        Assert.Equal(["2000042", "2000043", "2000044"], repo.Gravadas.Select(g => g.Contexto.Material));
        Assert.All(repo.Gravadas, g => Assert.Equal(LiberacaoToleranciaRecebimento.Autorizada, g.Resultado));
        // Os numeros de cada item vao proprios, nao copiados do primeiro.
        Assert.Equal([880m, 700m, 10m], repo.Gravadas.Select(g => g.Contexto.QuantidadeApurada));
    }

    [Fact]
    public async Task LoteRecusado_RegistraTodosOsItensComoRecusados()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Falha("x"));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync(
            "FULANO", "errada", [Falta("00010", "2000042", 880m), Falta("00020", "2000043", 700m)]);

        Assert.False(r.Autorizada);
        Assert.Equal(2, repo.Gravadas.Count);
        Assert.All(repo.Gravadas, g => Assert.Equal(LiberacaoToleranciaRecebimento.Recusada, g.Resultado));
    }

    /// <summary>
    /// O fail-closed do lote e a razao de existir a transacao: se a gravacao falha, NADA foi gravado
    /// e nada e liberado. O desfecho parcial — dois itens autorizados de tres — nao existe.
    /// </summary>
    [Fact]
    public async Task LoteQueNaoGrava_NAO_Autoriza()
    {
        RepositorioFalso repo = new() { Explode = true };
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Ok(Gerente(true)));

        ResultadoAutorizacaoTolerancia r = await s.AutorizarAsync(
            "GERENTE", "senha", [Falta("00010", "2000042", 880m), Falta("00020", "2000043", 700m)]);

        Assert.False(r.Autorizada);
        Assert.Empty(repo.Gravadas);
    }

    [Fact]
    public async Task LoteVazio_NaoEChamadaValida()
    {
        RepositorioFalso repo = new();
        AutorizacaoToleranciaServico s = Servico(repo, (_, _) => ResultadoAutenticacao.Ok(Gerente(true)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => s.AutorizarAsync("GERENTE", "senha", Array.Empty<ContextoEstouroTolerancia>()));
    }

    // ---------------------------------------------------------------- so estouro e liberavel

    /// <summary>
    /// Falta de cadastro NAO e liberavel (decisao do usuario): cadastrar leva segundos, e deixar o
    /// gerente furar esse bloqueio faria o cadastro nunca ser feito.
    /// </summary>
    [Fact]
    public void FaltaDeCadastro_NaoViraTipoDeEstouro()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContextoEstouroTolerancia.TipoDoResultado(ResultadoTolerancia.SemCadastro));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContextoEstouroTolerancia.TipoDoResultado(ResultadoTolerancia.Dentro));

        Assert.Equal("ACIMA_DO_TETO", ContextoEstouroTolerancia.TipoDoResultado(ResultadoTolerancia.AcimaDoTeto));
        Assert.Equal("ABAIXO_DO_PISO", ContextoEstouroTolerancia.TipoDoResultado(ResultadoTolerancia.AbaixoDoPiso));
    }
}
