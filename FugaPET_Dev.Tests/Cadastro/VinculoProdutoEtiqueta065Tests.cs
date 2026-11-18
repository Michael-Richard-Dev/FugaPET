using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Incremento 065 — vinculo de produto acabado a etiqueta e processo.
/// <para>
/// Cobre as regras que vivem no CODIGO. A unicidade por produto ("um pra um") nao esta aqui porque
/// ela mora no banco, como UNIQUE: testa-la com repositorio falso provaria o falso, nao a regra.
/// Quem a prova e o script de VALIDACAO do incremento, que tenta violar e espera recusa.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiqueta065Tests
{
    // ===================== processo de pesagem =====================

    [Theory]
    [InlineData(ProcessoPesagemProduto.CaixaACaixa, "CAIXA_A_CAIXA")]
    [InlineData(ProcessoPesagemProduto.EnvioUnico, "ENVIO_UNICO")]
    public void Processo_VaiEVoltaDoTextoDoBanco(ProcessoPesagemProduto processo, string texto)
    {
        Assert.Equal(texto, VinculoProdutoEtiqueta.TextoDoProcesso(processo));
        Assert.Equal(processo, VinculoProdutoEtiqueta.ProcessoDoTexto(texto));
    }

    /// <summary>
    /// Processo desconhecido LANCA. Cair num padrao faria a operacao seguir por um caminho que
    /// ninguem escolheu — e, pior, silenciosamente.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("CAIXA")]
    [InlineData("ENVIO_EM_LOTE")]
    public void Processo_DesconhecidoLanca(string texto)
        => Assert.Throws<InvalidOperationException>(() => VinculoProdutoEtiqueta.ProcessoDoTexto(texto));

    // ===================== validacoes do servico =====================

    [Fact]
    public async Task Salvar_SemEtiquetaEsemMarcar_Recusa()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out _);

        ResultadoOperacao resultado = await servico.SalvarAsync(new VinculoProdutoEtiqueta
        {
            Produto = "4000548",
            SemEtiqueta = false,
            CodigoEtiqueta = null,
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("etiqueta", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Salvar_MarcandoSemEtiqueta_LimpaAEtiquetaEscolhida()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out RepositorioFalso repositorio, out _);

        ResultadoOperacao resultado = await servico.SalvarAsync(new VinculoProdutoEtiqueta
        {
            Produto = "4000548",
            SemEtiqueta = true,
            CodigoEtiqueta = 10,   // o usuario tinha escolhido antes de marcar a caixa
        });

        Assert.True(resultado.Sucesso);
        Assert.Null(repositorio.UltimoSalvo!.CodigoEtiqueta);
        Assert.True(repositorio.UltimoSalvo.SemEtiqueta);
    }

    /// <summary>
    /// Decisao do usuario em 18/11/2026: so etiqueta de CAIXA vale para a saida de produto acabado.
    /// A de palete existe, esta ativa, e mesmo assim NAO pode aparecer para escolha.
    /// </summary>
    [Fact]
    public async Task ListarEtiquetas_SoTrazAsDaSaidaDeProdutoAcabado()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out _);

        IReadOnlyList<EtiquetaCadastro> disponiveis = await servico.ListarEtiquetasDisponiveisAsync();

        EtiquetaCadastro unica = Assert.Single(disponiveis);
        Assert.Equal(VinculoProdutoEtiquetaServico.TipoEtiquetaSaidaProdutoAcabado, unica.TipoEtiqueta);
    }

    /// <summary>A regra vale no servico, nao so no combo: etiqueta de outro tipo e recusada ao salvar.</summary>
    [Fact]
    public async Task Salvar_EtiquetaDeOutroTipo_Recusa()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out EtiquetaRepositorioFalso etiquetas);
        etiquetas.Tipo = "PALETE";

        ResultadoOperacao resultado = await servico.SalvarAsync(new VinculoProdutoEtiqueta
        {
            Produto = "4000548",
            SemEtiqueta = false,
            CodigoEtiqueta = 10,
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("PALETE", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Salvar_EtiquetaInativa_Recusa()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out EtiquetaRepositorioFalso etiquetas);
        etiquetas.Ativa = false;

        ResultadoOperacao resultado = await servico.SalvarAsync(new VinculoProdutoEtiqueta
        {
            Produto = "4000548",
            SemEtiqueta = false,
            CodigoEtiqueta = 10,
        });

        Assert.False(resultado.Sucesso);
        Assert.Contains("inativa", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A auditoria da exclusao precisa dizer QUAL registro saiu. Antes o servico passava 0 fixo, e o
    /// log gravava "Registro 0" — apareceu ao conferir a auditoria de um teste real em 18/11/2026.
    /// </summary>
    [Fact]
    public async Task Remover_AuditaOCodigoDoVinculoApagado()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out RepositorioFalso repositorio, out _);
        repositorio.Existentes.Add((new VinculoProdutoEtiqueta
        {
            CodigoVinculo = 42,
            Produto = "4000073",
            SemEtiqueta = true,
        }, string.Empty));

        ResultadoOperacao resultado = await servico.RemoverAsync("3007", "4000073");

        Assert.True(resultado.Sucesso);
        Assert.Empty(repositorio.Existentes);
    }

    [Fact]
    public async Task Remover_ProdutoSemVinculo_Recusa()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out _);

        ResultadoOperacao resultado = await servico.RemoverAsync("3007", "4000073");

        Assert.False(resultado.Sucesso);
        Assert.Contains("não tinha vínculo", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Salvar_SemProduto_Recusa()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(out _, out _);

        ResultadoOperacao resultado = await servico.SalvarAsync(new VinculoProdutoEtiqueta { Produto = "  " });

        Assert.False(resultado.Sucesso);
        Assert.Contains("produto", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== listagem: SAP x banco =====================

    /// <summary>
    /// SAP fora do ar NAO esvazia a tela: os vinculos ja gravados continuam aparecendo, marcados
    /// como ausentes do catalogo, e a mensagem diz por que. Esconde-los deixaria vinculo orfao
    /// decidindo pesagem sem ninguem ver.
    /// </summary>
    [Fact]
    public async Task Listar_CatalogoIndisponivel_MantemOsVinculosGravados()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(
            out RepositorioFalso repositorio, out _, catalogoFalha: "SAP fora do ar");

        repositorio.Existentes.Add((new VinculoProdutoEtiqueta
        {
            CodigoVinculo = 1,
            Produto = "4000548",
            SemEtiqueta = true,
            ProcessoPesagem = ProcessoPesagemProduto.CaixaACaixa,
        }, string.Empty));

        ResultadoListagemVinculo resultado = await servico.ListarAsync("3007");

        Assert.False(resultado.CatalogoDisponivel);
        Assert.Equal("SAP fora do ar", resultado.MensagemCatalogo);
        LinhaVinculoProduto linha = Assert.Single(resultado.Linhas);
        Assert.Equal("4000548", linha.Produto);
        Assert.False(linha.PresenteNoSap);
        Assert.True(linha.Configurado);
    }

    [Fact]
    public async Task Listar_CruzaProdutoDoSapComVinculoGravado()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(
            out RepositorioFalso repositorio, out _,
            produtos: [
                new ProdutoAcabadoDoCentro { Produto = "4000548", Descricao = "DOGUITOS", TipoMaterial = "FERT" },
                new ProdutoAcabadoDoCentro { Produto = "4000549", Descricao = "OUTRO", TipoMaterial = "FERT" },
            ]);

        repositorio.Existentes.Add((new VinculoProdutoEtiqueta
        {
            CodigoVinculo = 1,
            Produto = "4000548",
            CodigoEtiqueta = 10,
            ProcessoPesagem = ProcessoPesagemProduto.EnvioUnico,
        }, "Etiqueta Caixa 8x5"));

        ResultadoListagemVinculo resultado = await servico.ListarAsync("3007");

        Assert.True(resultado.CatalogoDisponivel);
        Assert.Equal(2, resultado.Linhas.Count);

        LinhaVinculoProduto comVinculo = resultado.Linhas.Single(l => l.Produto == "4000548");
        Assert.True(comVinculo.Configurado);
        Assert.True(comVinculo.PresenteNoSap);
        Assert.Equal("Etiqueta Caixa 8x5", comVinculo.SituacaoParaExibir);

        LinhaVinculoProduto semVinculo = resultado.Linhas.Single(l => l.Produto == "4000549");
        Assert.False(semVinculo.Configurado);
        Assert.Equal("Não configurado", semVinculo.SituacaoParaExibir);
    }

    /// <summary>"Sem etiqueta" e uma decisao gravada, e a tela tem de distingui-la de "nao decidido".</summary>
    [Fact]
    public async Task Listar_SemEtiquetaNaoSeConfundeComNaoConfigurado()
    {
        VinculoProdutoEtiquetaServico servico = CriarServico(
            out RepositorioFalso repositorio, out _,
            produtos: [new ProdutoAcabadoDoCentro { Produto = "4000548", TipoMaterial = "FERT" }]);

        repositorio.Existentes.Add((new VinculoProdutoEtiqueta
        {
            CodigoVinculo = 1,
            Produto = "4000548",
            SemEtiqueta = true,
        }, string.Empty));

        ResultadoListagemVinculo resultado = await servico.ListarAsync("3007");

        LinhaVinculoProduto linha = Assert.Single(resultado.Linhas);
        Assert.True(linha.Configurado);
        Assert.Equal("Sem etiqueta", linha.SituacaoParaExibir);
    }

    // ===================== montagem =====================

    private static VinculoProdutoEtiquetaServico CriarServico(
        out RepositorioFalso repositorio,
        out EtiquetaRepositorioFalso etiquetas,
        IReadOnlyList<ProdutoAcabadoDoCentro>? produtos = null,
        string? catalogoFalha = null)
    {
        repositorio = new RepositorioFalso();
        etiquetas = new EtiquetaRepositorioFalso();
        return new VinculoProdutoEtiquetaServico(
            repositorio,
            etiquetas,
            new CatalogoFalso(produtos ?? [], catalogoFalha),
            new AuditoriaServico(null!));
    }

    private sealed class CatalogoFalso : ICatalogoProdutoCentroSapServico
    {
        private readonly IReadOnlyList<ProdutoAcabadoDoCentro> _produtos;
        private readonly string? _falha;

        public CatalogoFalso(IReadOnlyList<ProdutoAcabadoDoCentro> produtos, string? falha)
        {
            _produtos = produtos;
            _falha = falha;
        }

        public bool Configurado => _falha is null;

        public Task<ResultadoCatalogoProdutoCentro> ListarProdutosAcabadosAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult(_falha is null
                ? ResultadoCatalogoProdutoCentro.Ok(_produtos)
                : ResultadoCatalogoProdutoCentro.Falha(_falha));
    }

    private sealed class RepositorioFalso : VinculoProdutoEtiquetaRepositorio
    {
        public RepositorioFalso() : base(null!) { }

        public List<(VinculoProdutoEtiqueta, string)> Existentes { get; } = [];
        public VinculoProdutoEtiqueta? UltimoSalvo { get; private set; }

        public override Task<IReadOnlyList<(VinculoProdutoEtiqueta Vinculo, string NomeEtiqueta)>> ListarPorCentroAsync(
            string centro, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(VinculoProdutoEtiqueta, string)>>([.. Existentes]);

        public override Task<long> SalvarAsync(
            VinculoProdutoEtiqueta vinculo, CancellationToken cancellationToken = default)
        {
            UltimoSalvo = vinculo;
            return Task.FromResult(1L);
        }

        public override Task<long?> RemoverAsync(
            string centro, string produto, CancellationToken cancellationToken = default)
        {
            long? codigo = Existentes.FirstOrDefault(e => e.Item1.Produto == produto).Item1?.CodigoVinculo;
            Existentes.RemoveAll(e => e.Item1.Produto == produto);
            return Task.FromResult(codigo);
        }
    }

    private sealed class EtiquetaRepositorioFalso : EtiquetaRepositorio
    {
        public EtiquetaRepositorioFalso() : base(null!) { }

        public bool Ativa { get; set; } = true;
        public string Tipo { get; set; } = "CAIXA";

        public override Task<IReadOnlyList<EtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EtiquetaCadastro>>(
                [
                new EtiquetaCadastro { CodigoEtiqueta = 10, NomeEtiqueta = "Etiqueta Caixa 8x5", TipoEtiqueta = Tipo, SituacaoEtiqueta = Ativa },
                new EtiquetaCadastro { CodigoEtiqueta = 20, NomeEtiqueta = "Etiqueta de Palete", TipoEtiqueta = "PALETE", SituacaoEtiqueta = true },
            ]);

        public override Task<EtiquetaCadastro?> ObterPorIdAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
            => Task.FromResult<EtiquetaCadastro?>(
                new EtiquetaCadastro { CodigoEtiqueta = codigoEtiqueta, NomeEtiqueta = "Etiqueta Caixa 8x5", TipoEtiqueta = Tipo, SituacaoEtiqueta = Ativa });
    }
}
