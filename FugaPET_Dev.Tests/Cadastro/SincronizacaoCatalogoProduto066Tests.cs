using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Sincronizacao incremental do catalogo (incremento 066, 18/11/2026).
/// <para>
/// Pedido do usuario: "nao quero toda hora ter que buscar produto no SAP; o botao busca apenas os
/// novos". O que estes testes travam e exatamente isso — a segunda sincronizacao NAO pode repetir a
/// pergunta de cadastro para material que ja se conhece.
/// </para>
/// </summary>
public sealed class SincronizacaoCatalogoProduto066Tests
{
    [Fact]
    public async Task PrimeiraRodada_BuscaCadastroDeTodos()
    {
        SapFalso sap = new(["100", "200", "4000001"]);
        SincronizacaoCatalogoProdutoServico servico = new(new CatalogoFalso(), sap);

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.True(resumo.Sucesso);
        Assert.Equal(3, resumo.Novos);
        Assert.Equal(3, resumo.MestresLidos);
        Assert.Equal(["100", "200", "4000001"], sap.CadastrosPedidos);
    }

    /// <summary>O coracao do pedido: a segunda rodada sem novidade nao pergunta cadastro nenhum.</summary>
    [Fact]
    public async Task SegundaRodadaSemNovidade_NaoPedeCadastroDeNinguem()
    {
        SapFalso sap = new(["100", "200", "4000001"]);
        CatalogoFalso banco = new();
        SincronizacaoCatalogoProdutoServico servico = new(banco, sap);

        await servico.SincronizarAsync("3007");
        sap.CadastrosPedidos.Clear();

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.True(resumo.Sucesso);
        Assert.Equal(0, resumo.Novos);
        Assert.Equal(0, resumo.MestresLidos);
        Assert.Empty(sap.CadastrosPedidos);
        Assert.Contains("Nenhuma novidade", resumo.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>Produto novo no centro: pergunta SO por ele, nao pelos que ja conhece.</summary>
    [Fact]
    public async Task ProdutoNovo_PerguntaApenasPeloNovo()
    {
        SapFalso sap = new(["100", "200"]);
        CatalogoFalso banco = new();
        SincronizacaoCatalogoProdutoServico servico = new(banco, sap);

        await servico.SincronizarAsync("3007");
        sap.CadastrosPedidos.Clear();
        sap.CodigosDoCentro = ["100", "200", "4000009"];

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.Equal(1, resumo.Novos);
        Assert.Equal(["4000009"], sap.CadastrosPedidos);
    }

    /// <summary>Produto que saiu do centro e MARCADO, nunca apagado: pode haver vinculo sobre ele.</summary>
    [Fact]
    public async Task ProdutoQueSaiu_EMarcadoENaoApagado()
    {
        SapFalso sap = new(["100", "4000001"]);
        CatalogoFalso banco = new();
        SincronizacaoCatalogoProdutoServico servico = new(banco, sap);

        await servico.SincronizarAsync("3007");
        sap.CodigosDoCentro = ["100"];

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.Equal(1, resumo.Ausentes);
        ProdutoCatalogoLocal saiu = banco.Linhas.Single(l => l.Produto == "4000001");
        Assert.False(saiu.PresenteNoSap);
    }

    /// <summary>
    /// Falha ao LISTAR nao pode marcar ninguem como ausente: tratar "nao consegui perguntar" como
    /// "sumiu" apagaria o catalogo inteiro num erro de rede.
    /// </summary>
    [Fact]
    public async Task FalhaNaListagem_NaoMarcaNinguemComoAusente()
    {
        SapFalso sap = new(["100", "4000001"]);
        CatalogoFalso banco = new();
        SincronizacaoCatalogoProdutoServico servico = new(banco, sap);

        await servico.SincronizarAsync("3007");
        sap.FalharListagem = true;

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.False(resumo.Sucesso);
        Assert.Equal(0, resumo.Ausentes);
        Assert.All(banco.Linhas, l => Assert.True(l.PresenteNoSap));
    }

    /// <summary>
    /// Falha ao buscar CADASTRO deixa os materiais pendentes, para a proxima rodada tentar de novo.
    /// A listagem ja gravada continua valendo — nada se perde.
    /// </summary>
    [Fact]
    public async Task FalhaNoCadastro_DeixaPendenteParaAProximaRodada()
    {
        SapFalso sap = new(["100", "4000001"]) { FalharCadastro = true };
        CatalogoFalso banco = new();
        SincronizacaoCatalogoProdutoServico servico = new(banco, sap);

        ResumoSincronizacaoCatalogo resumo = await servico.SincronizarAsync("3007");

        Assert.False(resumo.Sucesso);
        Assert.Equal(2, resumo.Novos);
        Assert.All(banco.Linhas, l => Assert.True(l.MestrePendente));

        sap.FalharCadastro = false;
        ResumoSincronizacaoCatalogo segunda = await servico.SincronizarAsync("3007");

        Assert.True(segunda.Sucesso);
        Assert.Equal(2, segunda.MestresLidos);
    }

    // ------------------------------------------------------------------
    private sealed class SapFalso : ICatalogoProdutoCentroSapServico
    {
        public SapFalso(string[] codigos) => CodigosDoCentro = codigos;

        public string[] CodigosDoCentro { get; set; }
        public List<string> CadastrosPedidos { get; } = [];
        public bool FalharListagem { get; set; }
        public bool FalharCadastro { get; set; }

        public bool Configurado => true;

        public Task<ResultadoCodigosCentro> ListarCodigosDoCentroAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult(FalharListagem
                ? ResultadoCodigosCentro.Falha("sem rede", MotivoCatalogoIndisponivel.FalhaNaConsulta, 1)
                : ResultadoCodigosCentro.Ok(CodigosDoCentro, 1));

        public Task<ResultadoCadastroProdutos> ObterCadastroAsync(
            IReadOnlyList<string> codigos, CancellationToken cancellationToken = default)
        {
            CadastrosPedidos.AddRange(codigos);
            if (FalharCadastro)
            {
                return Task.FromResult(ResultadoCadastroProdutos.Falha("sem rede", 1));
            }

            return Task.FromResult(ResultadoCadastroProdutos.Ok(
                [.. codigos.Select(c => new ProdutoAcabadoDoCentro
                {
                    Produto = c,
                    TipoMaterial = c.StartsWith('4') ? "FERT" : "ROH",
                    UnidadeBase = "KG",
                })], 1));
        }

        public Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
            string centro, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("A sincronizacao usa as etapas granulares.");
    }

    /// <summary>Catalogo em memoria com a MESMA semantica do SQL: upsert, marcar ausente, pendentes.</summary>
    private sealed class CatalogoFalso : CatalogoProdutoCentroRepositorio
    {
        public CatalogoFalso() : base(null!) { }

        public List<ProdutoCatalogoLocal> Linhas { get; } = [];

        public override Task<IReadOnlyList<ProdutoCatalogoLocal>> ListarPorCentroAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProdutoCatalogoLocal>>([.. Linhas.Where(l => l.Centro == centro)]);

        public override Task<int> RegistrarVistosAsync(
            string centro, IReadOnlyList<string> produtos, CancellationToken cancellationToken = default)
        {
            int novos = 0;
            foreach (string produto in produtos)
            {
                ProdutoCatalogoLocal? linha = Linhas.FirstOrDefault(l => l.Centro == centro && l.Produto == produto);
                if (linha is null)
                {
                    Linhas.Add(new ProdutoCatalogoLocal { Centro = centro, Produto = produto, VistoEm = DateTime.UtcNow });
                    novos++;
                }
                else
                {
                    linha.VistoEm = DateTime.UtcNow;
                    linha.PresenteNoSap = true;
                }
            }
            return Task.FromResult(novos);
        }

        public override Task<int> MarcarAusentesAsync(
            string centro, IReadOnlyList<string> presentes, CancellationToken cancellationToken = default)
        {
            int marcados = 0;
            foreach (ProdutoCatalogoLocal linha in Linhas.Where(l => l.Centro == centro && l.PresenteNoSap))
            {
                if (!presentes.Contains(linha.Produto, StringComparer.Ordinal))
                {
                    linha.PresenteNoSap = false;
                    marcados++;
                }
            }
            return Task.FromResult(marcados);
        }

        public override Task<IReadOnlyList<string>> ListarPendentesDeMestreAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                [.. Linhas.Where(l => l.Centro == centro && l.MestreLidoEm is null).Select(l => l.Produto)]);

        public override Task<int> AtualizarMestresAsync(
            string centro, IReadOnlyList<ProdutoAcabadoDoCentro> mestres, CancellationToken cancellationToken = default)
        {
            int atualizados = 0;
            foreach (ProdutoAcabadoDoCentro mestre in mestres)
            {
                ProdutoCatalogoLocal? linha = Linhas.FirstOrDefault(l => l.Centro == centro && l.Produto == mestre.Produto);
                if (linha is null) continue;
                linha.Descricao = mestre.Descricao;
                linha.TipoMaterial = mestre.TipoMaterial;
                linha.UnidadeBase = mestre.UnidadeBase;
                linha.MestreLidoEm = DateTime.UtcNow;
                atualizados++;
            }
            return Task.FromResult(atualizados);
        }

        public override Task<DateTime?> ObterUltimaSincronizacaoAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTime?>(Linhas.Count == 0 ? null : Linhas.Max(l => l.VistoEm));
    }
}
