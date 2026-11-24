using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tolerância por produto NOS PROCESSOS DE PRODUÇÃO (incremento 071, pedido do usuário em
/// 24/11/2026): Consumo de Material e Produto Semi-Acabado.
/// </summary>
public sealed class ToleranciaProcessoProducaoTests
{
    // ---------------------------------------------------------------- o processo é parte da chave

    /// <summary>
    /// O PONTO DO INCREMENTO. Um semi-acabado é produzido numa OP e consumido em outra, então o
    /// mesmo código aparece nas duas telas — e as duas decisões são independentes.
    /// </summary>
    [Fact]
    public void MesmoProduto_PodeTerToleranciasDiferentesNosDoisProcessos()
    {
        ToleranciaProcessoProducao consumo = new()
        {
            Material = "1000090",
            Processo = ProcessoToleranciaProducao.Consumo,
            PercentualTolerancia = 5m,
        };

        ToleranciaProcessoProducao semiAcabado = new()
        {
            Material = "1000090",
            Processo = ProcessoToleranciaProducao.SemiAcabado,
            PercentualTolerancia = 12m,
        };

        // A unicidade no banco é (centro, processo, material): as duas linhas convivem.
        Assert.NotEqual(consumo.Processo, semiAcabado.Processo);
        Assert.Equal(consumo.Material, semiAcabado.Material);
        Assert.NotEqual(consumo.PercentualTolerancia, semiAcabado.PercentualTolerancia);
    }

    [Theory]
    [InlineData("CONSUMO", true)]
    [InlineData("SEMI_ACABADO", true)]
    [InlineData("ENTRADA", false)]
    [InlineData("consumo", false)]      // o banco compara EXATO; minúscula nunca casaria
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ProcessoValido_SegueAListaFechada(string? processo, bool esperado)
        => Assert.Equal(esperado, ProcessoToleranciaProducao.EhValido(processo));

    // ---------------------------------------------------------------- a matemática é a MESMA

    /// <summary>
    /// A conta é a do Recebimento, reusada — não uma segunda cópia. Duas cópias da mesma regra
    /// divergem no dia em que uma for corrigida e a outra não, que foi o erro de 24/11/2026.
    /// </summary>
    [Fact]
    public void AMatematica_EAMesmaDoRecebimento()
    {
        ToleranciaProcessoProducao producao = new() { PercentualTolerancia = 10m, Situacao = true };
        ToleranciaRecebimentoMaterial recebimento = new() { PercentualTolerancia = 10m, Situacao = true };

        VereditoTolerancia porProducao = RegraTolerancia.AvaliarDurantePesagem(1000m, 1101m, producao);
        VereditoTolerancia porRecebimento = RegraTolerancia.AvaliarDurantePesagem(1000m, 1101m, recebimento);

        Assert.Equal(porRecebimento.Resultado, porProducao.Resultado);
        Assert.Equal(porRecebimento.LimiteSuperior, porProducao.LimiteSuperior);
        Assert.Equal(porRecebimento.LimiteInferior, porProducao.LimiteInferior);
        Assert.Equal(ResultadoTolerancia.AcimaDoTeto, porProducao.Resultado);
    }

    /// <summary>Os dois modelos entram na mesma regra porque implementam a mesma abstração.</summary>
    [Fact]
    public void OsDoisModelos_SaoToleranciaPercentual()
    {
        Assert.IsAssignableFrom<IToleranciaPercentual>(new ToleranciaProcessoProducao());
        Assert.IsAssignableFrom<IToleranciaPercentual>(new ToleranciaRecebimentoMaterial());
    }

    /// <summary>Inativa conta como NÃO CADASTRADA, não como "sem limite".</summary>
    [Fact]
    public void ToleranciaInativa_BloqueiaComoSeNaoExistisse()
    {
        ToleranciaProcessoProducao inativa = new() { PercentualTolerancia = 50m, Situacao = false };

        VereditoTolerancia veredito = RegraTolerancia.AvaliarDurantePesagem(100m, 101m, inativa);

        Assert.Equal(ResultadoTolerancia.SemCadastro, veredito.Resultado);
        Assert.True(veredito.Bloqueia);
    }

    // ---------------------------------------------------------------- quem aparece em cada tela

    /// <summary>
    /// A lista de cada processo vem dos classificadores que JÁ existem, não de uma segunda lista de
    /// tipos escrita aqui — uma cópia funcionaria até alguém mudar uma das duas.
    /// </summary>
    [Theory]
    [InlineData("ROH", true)]    // matéria-prima
    [InlineData("VERP", true)]   // embalagem, que o Consumo também pesa
    [InlineData("HALB", true)]   // semiacabado consumido em outra OP
    [InlineData("HIBE", true)]   // químico
    [InlineData("FERT", false)]  // produto acabado não é consumido
    [InlineData("", false)]
    public void ListaDoConsumo_SegueOClassificadorDeComponente(string tipo, bool esperado)
        => Assert.Equal(
            esperado,
            Servicos.Cadastro.ToleranciaProcessoProducaoServico.ProdutoDoProcesso(
                ProcessoToleranciaProducao.Consumo, tipo, null));

    [Theory]
    [InlineData("HALB", true)]
    [InlineData("ROH", false)]
    [InlineData("FERT", false)]
    [InlineData("", false)]
    public void ListaDoSemiAcabado_SoHALB(string tipo, bool esperado)
        => Assert.Equal(
            esperado,
            Servicos.Cadastro.ToleranciaProcessoProducaoServico.ProdutoDoProcesso(
                ProcessoToleranciaProducao.SemiAcabado, tipo, null));

    /// <summary>Processo desconhecido não lista ninguém — fail-closed, não "tudo".</summary>
    [Fact]
    public void ProcessoDesconhecido_NaoListaNinguem()
        => Assert.False(
            Servicos.Cadastro.ToleranciaProcessoProducaoServico.ProdutoDoProcesso("QUALQUER", "ROH", null));

    // ---------------------------------------------------------------- o repositório

    private sealed class FabricaConexaoNula : IFabricaConexaoBanco
    {
        public Npgsql.NpgsqlConnection CriarConexao() => throw new NotSupportedException();
        public Task<Npgsql.NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public string ObterConnectionString() => throw new NotSupportedException();
    }

    /// <summary>
    /// A unicidade é do BANCO, não de consulta prévia: verificar antes e gravar depois deixa uma
    /// janela entre as duas coisas, e dois terminais salvando o mesmo produto passariam por ela.
    /// </summary>
    [Fact]
    public void Salvar_ResolveConflitoNoBanco_PelaChaveComProcesso()
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "ToleranciaProcessoProducaoRepositorio.cs"));

        Assert.Contains("ON CONFLICT (centro, processo, material) DO UPDATE", fonte, StringComparison.Ordinal);
        // Sem o processo na chave, salvar no Consumo sobrescreveria o Semi-Acabado em silêncio.
        Assert.DoesNotContain("ON CONFLICT (centro, material)", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_PodeSerConstruido()
        => Assert.NotNull(new ToleranciaProcessoProducaoRepositorio(new FabricaConexaoNula()));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
