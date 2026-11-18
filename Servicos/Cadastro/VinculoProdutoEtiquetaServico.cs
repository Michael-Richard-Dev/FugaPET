using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Cadastro;

/// <summary>O que a tela precisa para desenhar uma consulta inteira.</summary>
public sealed record ResultadoListagemVinculo
{
    public IReadOnlyList<LinhaVinculoProduto> Linhas { get; init; } = [];

    /// <summary>Verdadeiro quando o SAP respondeu. Falso = as linhas vem SO dos vinculos ja gravados.</summary>
    public bool CatalogoDisponivel { get; init; }

    /// <summary>Por que o catalogo nao veio, quando nao veio. Vazio em caso de sucesso.</summary>
    public string MensagemCatalogo { get; init; } = string.Empty;
}

/// <summary>
/// Regras do vinculo produto × etiqueta × processo.
/// <para>
/// Junta duas fontes que nao se conhecem: o catalogo de produtos, que vive no SAP, e a decisao do
/// usuario, que vive no banco local. Nenhuma das duas manda na outra — por isso a listagem devolve
/// tambem produtos que so existem de um lado, em vez de descartar silenciosamente.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiquetaServico
{
    private const string Entidade = "VinculoProdutoEtiqueta";
    private const string Tela = "VinculoProdutoEtiquetaForm";

    /// <summary>
    /// Tipo de etiqueta que vale para a SAIDA DE PRODUTO ACABADO — o unico que esta tela oferece.
    /// <para>
    /// Decisao do usuario em 18/11/2026. O cadastro de etiqueta aceita CAIXA, PALETE, HU, INTERNA e
    /// OUTRA; etiqueta de palete ou interna nao tem o que fazer na pesagem de caixa, e deixar todas
    /// na lista convidava a vincular a errada. Classificar e responsabilidade do cadastro de
    /// etiqueta: o que for marcado CAIXA aparece aqui, o resto nunca.
    /// </para>
    /// </summary>
    public const string TipoEtiquetaSaidaProdutoAcabado = "CAIXA";

    private readonly VinculoProdutoEtiquetaRepositorio _repositorio;
    private readonly EtiquetaRepositorio _etiquetaRepositorio;
    private readonly ICatalogoProdutoCentroSapServico _catalogo;
    private readonly AuditoriaServico _auditoria;

    public VinculoProdutoEtiquetaServico(
        VinculoProdutoEtiquetaRepositorio repositorio,
        EtiquetaRepositorio etiquetaRepositorio,
        ICatalogoProdutoCentroSapServico catalogo,
        AuditoriaServico auditoria)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _etiquetaRepositorio = etiquetaRepositorio ?? throw new ArgumentNullException(nameof(etiquetaRepositorio));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
    }

    /// <summary>
    /// Etiquetas que podem ser escolhidas: ATIVAS e do tipo da saida de produto acabado.
    /// Ver <see cref="TipoEtiquetaSaidaProdutoAcabado"/> para o porque do filtro por tipo.
    /// </summary>
    public async Task<IReadOnlyList<EtiquetaCadastro>> ListarEtiquetasDisponiveisAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EtiquetaCadastro> todas = await _etiquetaRepositorio.ListarAsync(cancellationToken);
        return [.. todas
            .Where(e => e.SituacaoEtiqueta && EhDaSaidaDeProdutoAcabado(e))
            .OrderBy(e => e.NomeEtiqueta, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static bool EhDaSaidaDeProdutoAcabado(EtiquetaCadastro etiqueta)
        => string.Equals(
            (etiqueta.TipoEtiqueta ?? string.Empty).Trim(),
            TipoEtiquetaSaidaProdutoAcabado,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lista o centro: produtos do SAP cruzados com os vinculos gravados.
    /// <para>
    /// Com o SAP fora do ar a tela NAO fica vazia — ela mostra o que ja foi vinculado, marcado como
    /// ausente do catalogo, mais o motivo. Quem decide se da para trabalhar assim e o operador, com
    /// o motivo na frente; esconder a indisponibilidade e que seria mentira.
    /// </para>
    /// </summary>
    public async Task<ResultadoListagemVinculo> ListarAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        string alvo = string.IsNullOrWhiteSpace(centro) ? VinculoProdutoEtiqueta.CentroPadrao : centro.Trim();

        IReadOnlyList<(VinculoProdutoEtiqueta Vinculo, string NomeEtiqueta)> gravados =
            await _repositorio.ListarPorCentroAsync(alvo, cancellationToken);

        Dictionary<string, (VinculoProdutoEtiqueta Vinculo, string NomeEtiqueta)> porProduto =
            gravados.ToDictionary(g => g.Vinculo.Produto, StringComparer.Ordinal);

        ResultadoCatalogoProdutoCentro catalogo =
            await _catalogo.ListarProdutosAcabadosAsync(alvo, cancellationToken);

        List<LinhaVinculoProduto> linhas = [];
        HashSet<string> vistos = new(StringComparer.Ordinal);

        if (catalogo.Sucesso)
        {
            foreach (ProdutoAcabadoDoCentro produto in catalogo.Produtos)
            {
                porProduto.TryGetValue(produto.Produto, out var gravado);
                linhas.Add(new LinhaVinculoProduto
                {
                    Produto = produto.Produto,
                    DescricaoSap = produto.Descricao,
                    TipoProdutoSap = produto.TipoMaterial,
                    Vinculo = gravado.Vinculo,
                    NomeEtiqueta = gravado.NomeEtiqueta ?? string.Empty,
                    PresenteNoSap = true,
                });
                vistos.Add(produto.Produto);
            }
        }

        // Vinculo gravado para produto que o catalogo nao trouxe. Pode ser produto retirado do centro
        // ou SAP indisponivel — a linha aparece marcada, porque um vinculo orfao continua decidindo
        // etiqueta e processo na pesagem.
        foreach (var gravado in gravados.Where(g => !vistos.Contains(g.Vinculo.Produto)))
        {
            linhas.Add(new LinhaVinculoProduto
            {
                Produto = gravado.Vinculo.Produto,
                DescricaoSap = null,
                TipoProdutoSap = null,
                Vinculo = gravado.Vinculo,
                NomeEtiqueta = gravado.NomeEtiqueta,
                PresenteNoSap = false,
            });
        }

        return new ResultadoListagemVinculo
        {
            Linhas = [.. linhas.OrderBy(l => l.Produto, StringComparer.Ordinal)],
            CatalogoDisponivel = catalogo.Sucesso,
            MensagemCatalogo = catalogo.Sucesso ? string.Empty : catalogo.MensagemDiagnostico,
        };
    }

    /// <summary>
    /// Grava a decisao de um produto.
    /// <para>
    /// A unicidade por produto — o "um pra um" — e garantida pelo banco, nao por consulta previa:
    /// verificar antes e gravar depois deixa uma janela entre as duas coisas. Aqui so ficam as
    /// validacoes que o banco nao tem como fazer.
    /// </para>
    /// </summary>
    public async Task<ResultadoOperacao> SalvarAsync(
        VinculoProdutoEtiqueta vinculo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vinculo);

        vinculo.Centro = string.IsNullOrWhiteSpace(vinculo.Centro)
            ? VinculoProdutoEtiqueta.CentroPadrao
            : vinculo.Centro.Trim();
        vinculo.Produto = (vinculo.Produto ?? string.Empty).Trim();

        if (vinculo.Produto.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um produto.");
        }

        if (vinculo.SemEtiqueta)
        {
            vinculo.CodigoEtiqueta = null;
        }
        else if (vinculo.CodigoEtiqueta is null or <= 0)
        {
            return ResultadoOperacao.Falha(
                "Escolha uma etiqueta ou marque que este produto não leva etiqueta.");
        }
        else
        {
            EtiquetaCadastro? etiqueta = await _etiquetaRepositorio.ObterPorIdAsync(
                vinculo.CodigoEtiqueta.Value, cancellationToken);

            if (etiqueta is null)
            {
                return ResultadoOperacao.Falha("A etiqueta escolhida não existe mais.");
            }
            if (!etiqueta.SituacaoEtiqueta)
            {
                return ResultadoOperacao.Falha(
                    $"A etiqueta '{etiqueta.NomeEtiqueta}' está inativa e não pode ser vinculada.");
            }

            // O combo da tela ja filtra, mas a regra tem de valer tambem para quem chamar o servico
            // por outro caminho — e para o caso de a etiqueta ter mudado de tipo depois de escolhida.
            if (!EhDaSaidaDeProdutoAcabado(etiqueta))
            {
                return ResultadoOperacao.Falha(
                    $"A etiqueta '{etiqueta.NomeEtiqueta}' é do tipo {etiqueta.TipoEtiqueta} e não vale "
                    + "para a saída de produto acabado.");
            }
        }

        vinculo.CriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        long codigo = await _repositorio.SalvarAsync(vinculo, cancellationToken);

        await _auditoria.RegistrarCadastroAtualizadoAsync(
            Entidade, codigo, DescreverParaAuditoria(vinculo), Tela, cancellationToken);

        return ResultadoOperacao.Ok("Vínculo salvo.", codigo);
    }

    /// <summary>Desfaz o vinculo: o produto volta a "não configurado".</summary>
    public async Task<ResultadoOperacao> RemoverAsync(
        string centro, string produto, CancellationToken cancellationToken = default)
    {
        string alvo = string.IsNullOrWhiteSpace(centro) ? VinculoProdutoEtiqueta.CentroPadrao : centro.Trim();
        string codigo = (produto ?? string.Empty).Trim();

        if (codigo.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um produto.");
        }

        long? removido = await _repositorio.RemoverAsync(alvo, codigo, cancellationToken);
        if (removido is null)
        {
            return ResultadoOperacao.Falha("Este produto não tinha vínculo para remover.");
        }

        await _auditoria.RegistrarCadastroExcluidoAsync(
            Entidade, removido.Value, $"Vínculo removido do produto {codigo} (centro {alvo})", Tela, cancellationToken);

        return ResultadoOperacao.Ok("Vínculo removido.");
    }

    private static string DescreverParaAuditoria(VinculoProdutoEtiqueta vinculo)
    {
        string etiqueta = vinculo.SemEtiqueta
            ? "sem etiqueta"
            : $"etiqueta {vinculo.CodigoEtiqueta}";

        return $"Produto {vinculo.Produto} (centro {vinculo.Centro}): {etiqueta}, "
             + $"processo {VinculoProdutoEtiqueta.TextoDoProcesso(vinculo.ProcessoPesagem)}";
    }
}
