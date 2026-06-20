using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Portao do lancamento de Entrada de Produto (H3 + H7). Cobre as regras que disparam ANTES
/// de qualquer acesso ao banco: autenticacao, permissao (FINALIZAR ou EXECUTAR), setor,
/// origem e pesos. RegistrarLancamentoAsync retorna ResultadoOperacao; excecoes de infra
/// (null repositorio, DB fora do ar) propagam normalmente e sao testadas separadamente.
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
    public async Task DeveRetornarFalha_SemUsuarioAutenticado()
    {
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m)));

        Assert.False(resultado.Sucesso);
        Assert.Contains("autenticado", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_SemPermissaoExecutarNemFinalizar()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([]));
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m)));

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task DevePassarPermissao_ComPermissaoExecutar_SemFinalizar()
    {
        // EXECUTAR e suficiente; a validacao passa, falha apenas na persistencia (null repo).
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Executar)]));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<NullReferenceException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m))));
    }

    [Fact]
    public async Task DeveRetornarFalha_SemPesagens()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        EntradaProdutoLancamento lancamento = new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem { NumeroItem = "10", Pesagens = [] }]
        };

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(lancamento);

        Assert.False(resultado.Sucesso);
        Assert.Contains("nenhuma pesagem", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_PesoBrutoZero()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            Lancamento(Pesagem(bruto: 0m, tara: 0m, liquido: 0m)));

        Assert.False(resultado.Sucesso);
        Assert.Contains("bruto", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_LiquidoIncoerenteComBrutoMenosTara()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        // liquido (9) != bruto - tara (5 - 1 = 4): rejeitado pela regra endurecida.
        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            Lancamento(Pesagem(bruto: 5m, tara: 1m, liquido: 9m)));

        Assert.False(resultado.Sucesso);
        Assert.Contains("liquido", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_BrutoMenorOuIgualTara()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        // bruto (3) <= tara (3): rejeitado antes de chegar ao banco.
        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            Lancamento(Pesagem(bruto: 3m, tara: 3m, liquido: 0m)));

        Assert.False(resultado.Sucesso);
        Assert.Contains("tara", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevePassarPeso_PequenaToleranciaNoLiquido()
    {
        // bruto - tara = 9.999; liquido = 10.000 -> diferenca 0.001 (dentro da tolerancia).
        // Passar das validacoes resulta em NullReference ao gravar (repositorio nulo),
        // confirmando que a pesagem NAO foi barrada pela regra de peso.
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<NullReferenceException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(bruto: 11m, tara: 1.001m, liquido: 10m))));
    }

    [Fact]
    public async Task DeveRetornarFalha_OrigemInvalida()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            Lancamento(Pesagem(10m, 1m, origem: "XPTO")));

        Assert.False(resultado.Sucesso);
        Assert.Contains("origem", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_Manual_SemPermissaoPesoManual()
    {
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            Lancamento(Pesagem(10m, 1m, origem: "MANUAL")));

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task DeveRetornarFalha_SetorNaoAutorizado()
    {
        // Sessao com setor padrao 10; lancamento especifica setor 99 → negado.
        EstadoSessaoUsuarioAtual.Definir(SessaoComSetor(
            [Permissao(PermissoesSistema.Acoes.Finalizar)], idSetorPadrao: 10));
        EntradaProdutoServico servico = CriarServico();

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(
            LancamentoComSetor(Pesagem(10m, 1m), codigoSetor: 99));

        Assert.False(resultado.Sucesso);
        Assert.Contains("setor", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevePassarSetor_QuandoSetorDeLancamentoCoincidemComSessao()
    {
        // Mesmo setor na sessao e no lancamento → passa, falha no null repo.
        EstadoSessaoUsuarioAtual.Definir(SessaoComSetor(
            [Permissao(PermissoesSistema.Acoes.Finalizar)], idSetorPadrao: 10));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<NullReferenceException>(
            () => servico.RegistrarLancamentoAsync(LancamentoComSetor(Pesagem(10m, 1m), codigoSetor: 10)));
    }

    [Fact]
    public async Task DevePassarSetor_QuandoLancamentoNaoEspecificaSetor()
    {
        // Sem setor no lancamento nao ha restricao de setor.
        EstadoSessaoUsuarioAtual.Definir(SessaoComSetor(
            [Permissao(PermissoesSistema.Acoes.Finalizar)], idSetorPadrao: 10));
        EntradaProdutoServico servico = CriarServico();

        await Assert.ThrowsAsync<NullReferenceException>(
            () => servico.RegistrarLancamentoAsync(Lancamento(Pesagem(10m, 1m))));
    }

    [Fact]
    public async Task DeveRetornarFalha_ItemForaDoCentroAutorizado()
    {
        // H5: a guarda de centro/deposito tambem vale no Service (nao confiar so no Form).
        // Regra restringe ao centro 3007; item vem do centro 1410 → negado antes do banco.
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServicoComEscopo(
            new AutorizacaoCentroDepositoEntrada(centrosAutorizados: ["3007"], depositosAutorizados: []));

        EntradaProdutoLancamento lancamento = new()
        {
            NumeroPedido = "4500000010",
            Itens =
            [
                new EntradaProdutoItem
                {
                    NumeroItem = "10",
                    Centro = "1410",
                    Deposito = "141C",
                    Pesagens = [Pesagem(10m, 1m)]
                }
            ]
        };

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(lancamento);

        Assert.False(resultado.Sucesso);
        Assert.Contains("centro/deposito", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveRetornarFalha_ItemForaDoDepositoAutorizado()
    {
        // Centro ok (3007), mas deposito 999 fora da lista autorizada → negado no Service.
        EstadoSessaoUsuarioAtual.Definir(Sessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        EntradaProdutoServico servico = CriarServicoComEscopo(
            new AutorizacaoCentroDepositoEntrada(centrosAutorizados: ["3007"], depositosAutorizados: ["141C"]));

        EntradaProdutoLancamento lancamento = new()
        {
            NumeroPedido = "4500000010",
            Itens =
            [
                new EntradaProdutoItem
                {
                    NumeroItem = "10",
                    Centro = "3007",
                    Deposito = "999",
                    Pesagens = [Pesagem(10m, 1m)]
                }
            ]
        };

        ResultadoOperacao resultado = await servico.RegistrarLancamentoAsync(lancamento);

        Assert.False(resultado.Sucesso);
        Assert.Contains("centro/deposito", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    // Item sem vinculo SAP e sem setor: nenhuma validacao toca o banco.
    private static EntradaProdutoServico CriarServico()
        => new(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null);

    // Service com escopo de centro/deposito restritivo, para validar a guarda H5 no Service.
    private static EntradaProdutoServico CriarServicoComEscopo(AutorizacaoCentroDepositoEntrada escopo)
        => new(null!, null!, null!, null, escopo, null);

    private static EntradaProdutoLancamento Lancamento(EntradaProdutoPesagem pesagem)
        => new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem { NumeroItem = "10", Pesagens = [pesagem] }]
        };

    private static EntradaProdutoLancamento LancamentoComSetor(EntradaProdutoPesagem pesagem, long codigoSetor)
        => new()
        {
            NumeroPedido = "4500000010",
            CodigoSetor = codigoSetor,
            Itens = [new EntradaProdutoItem { NumeroItem = "10", Pesagens = [pesagem] }]
        };

    private static EntradaProdutoPesagem Pesagem(decimal bruto, decimal tara = 0m, decimal? liquido = null, string origem = "BALANCA")
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

    private static SessaoUsuarioAplicacao SessaoComSetor(
        IReadOnlyList<PermissaoSessaoAplicacao> permissoes, long idSetorPadrao)
        => new()
        {
            IdUsuario = 9,
            Login = "operador",
            Nome = "Operador",
            IdSetorPadrao = idSetorPadrao,
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        };
}
