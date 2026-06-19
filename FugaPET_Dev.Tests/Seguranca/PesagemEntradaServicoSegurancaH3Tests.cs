using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// H3 - PesagemEntradaServico como portao de seguranca. Valida as regras que disparam ANTES
/// de qualquer acesso ao banco (autenticacao, permissao, peso, origem). Sem dependencia de DB.
/// </summary>
public sealed class PesagemEntradaServicoSegurancaH3Tests : IDisposable
{
    public PesagemEntradaServicoSegurancaH3Tests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    [Fact]
    public async Task DeveBloquear_QuandoNaoHaUsuarioAutenticado()
    {
        PesagemEntradaServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync([Pesagem(peso: 10m, origem: "LIDO")]));

        Assert.Contains("autenticado", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_QuandoUsuarioSemPermissaoFinalizar()
    {
        // Sessao autenticada porem sem nenhuma permissao de Entrada de Produto.
        EstadoSessaoUsuarioAtual.Definir(CriarSessao([]));
        PesagemEntradaServico servico = CriarServico();

        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync([Pesagem(peso: 10m, origem: "LIDO")]));
    }

    [Fact]
    public async Task DeveBloquear_PesoManual_SemPermissaoPesoManual()
    {
        // Tem FINALIZAR, mas nao tem PESO_MANUAL; origem manual deve ser negada.
        EstadoSessaoUsuarioAtual.Definir(CriarSessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        PesagemEntradaServico servico = CriarServico();

        await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync([Pesagem(peso: 10m, origem: "DIGITADO")]));
    }

    [Fact]
    public async Task DeveBloquear_PesoMenorOuIgualAZero()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        PesagemEntradaServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync([Pesagem(peso: 0m, origem: "LIDO")]));

        Assert.Contains("peso", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeveBloquear_OrigemInvalida()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessao([Permissao(PermissoesSistema.Acoes.Finalizar)]));
        PesagemEntradaServico servico = CriarServico();

        ErroOperacionalEsperadoException erro = await Assert.ThrowsAsync<ErroOperacionalEsperadoException>(
            () => servico.SalvarPesagensAsync([Pesagem(peso: 10m, origem: "XPTO")]));

        Assert.Contains("origem", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private static PesagemEntradaServico CriarServico()
        => new(new PesagemEntradaItemRepositorio(new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())), null);

    private static PesagemEntradaItem Pesagem(decimal peso, string origem)
        => new() { CodigoSapPedidoCompraItem = 1, PesoKg = peso, OrigemPeso = origem };

    private static PermissaoSessaoAplicacao Permissao(string acao)
        => new()
        {
            Modulo = PermissoesSistema.Modulos.ProcessoProducao,
            Rotina = PermissoesSistema.Rotinas.EntradaProduto,
            Acao = acao
        };

    private static SessaoUsuarioAplicacao CriarSessao(IReadOnlyList<PermissaoSessaoAplicacao> permissoes)
        => new()
        {
            IdUsuario = 7,
            Login = "operador_teste",
            Nome = "Operador Teste",
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        };
}
