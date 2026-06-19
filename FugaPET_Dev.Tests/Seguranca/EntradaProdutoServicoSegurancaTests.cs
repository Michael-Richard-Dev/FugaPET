using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Portao do lancamento de Entrada de Produto (H7 + revalidacoes). Cobre as regras que
/// disparam ANTES de qualquer acesso ao banco (autenticacao, permissao, origem, pesos).
/// </summary>
public sealed class EntradaProdutoServicoSegurancaTests : IDisposable
{
    public EntradaProdutoServicoSegurancaTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    [Fact]
    public async Task DeveBloquear_SemUsuarioAutenticado()
    {
        EntradaProdutoServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m))));

        Assert.Contains("autenticado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_SemPermissaoFinalizar()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([]));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m))));
    }

    [Fact]
    public async Task DeveBloquear_SemPesagens()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        EntradaProdutoLancamento lancamento = new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem { NumeroItem = "10", Pesagens = [] }]
        };

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(lancamento));

        Assert.Contains("nenhuma pesagem", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_PesoBrutoZero()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(bruto: 0m, tara: 0m, liquido: 0m))));

        Assert.Contains("bruto", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_LiquidoIncoerenteComBrutoMenosTara()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        // liquido (9) != bruto - tara (5 - 1 = 4): rejeitado pela regra endurecida.
        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(bruto: 5m, tara: 1m, liquido: 9m))));

        Assert.Contains("liquido", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_BrutoMenorOuIgualTara()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        // bruto (3) <= tara (3): rejeitado antes de chegar ao banco.
        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(bruto: 3m, tara: 3m, liquido: 0m))));

        Assert.Contains("tara", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevePermitir_PequenaToleranciaNoLiquido()
    {
        // bruto - tara = 9.999; liquido = 10.000 -> diferenca 0.001 (dentro da tolerancia).
        // Sem repositorio configurado, passar das validacoes resulta em NullReference ao gravar,
        // o que confirma que a pesagem NAO foi barrada pela regra de peso.
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<NullReferenceException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(bruto: 11m, tara: 1.001m, liquido: 10m))));
    }

    [Fact]
    public async Task DeveBloquear_OrigemInvalida()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m, origem: "XPTO"))));

        Assert.Contains("origem", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_Manual_SemPermissaoPesoManual()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m, origem: "MANUAL"))));
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static EntradaProdutoServico CriarServico()
        => new(null!, null!, null!, new AutorizacaoCentroDepositoEntrada([], []), null);

    // Item sem vinculo SAP e sem setor: nenhuma validacao toca o banco.
    private static EntradaProdutoLancamento Lancamento(EntradaProdutoPesagem pesagem)
        => new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem { NumeroItem = "10", Pesagens = [pesagem] }]
        };

    private static EntradaProdutoPesagem Pesagem(decimal bruto, decimal tara, decimal? liquido = null, string origem = "BALANCA")
        => new()
        {
            PesoBrutoKg = bruto,
            PesoTaraKg = tara,
            PesoLiquidoKg = liquido ?? (bruto - tara),
            Origem = origem
        };

    private static PermissaoSessaoAplicacao Permissao(string acao)
        => new()
        {
            Modulo = PermissoesSistema.Modulos.ProcessoProducao,
            Rotina = PermissoesSistema.Rotinas.EntradaProduto,
            Acao = acao
        };

    private static SessaoUsuarioAplicacao Sessao(IReadOnlyList<PermissaoSessaoAplicacao> permissoes)
        => new()
        {
            IdUsuario = 9,
            Login = "operador",
            Nome = "Operador",
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        };
}
