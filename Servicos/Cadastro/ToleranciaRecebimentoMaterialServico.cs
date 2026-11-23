using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Cadastro;

/// <summary>O que a tela precisa para desenhar uma consulta inteira.</summary>
public sealed record ResultadoListagemTolerancia
{
    public IReadOnlyList<LinhaToleranciaMaterial> Linhas { get; init; } = [];

    /// <summary>Falso = o catalogo local esta vazio e as linhas vem SO das tolerancias ja gravadas.</summary>
    public bool CatalogoDisponivel { get; init; }

    /// <summary>Por que o catalogo nao veio, quando nao veio. Vazio em caso de sucesso.</summary>
    public string MensagemCatalogo { get; init; } = string.Empty;

    /// <summary>
    /// Materiais do recebimento que AINDA NAO tem tolerancia ativa. A tela mostra esse numero porque,
    /// por decisao do usuario, cada um deles e um recebimento que vai BLOQUEAR — e e melhor saber
    /// disso no cadastro do que descobrir na balanca.
    /// </summary>
    public int NaoConfigurados { get; init; }
}

/// <summary>
/// Regras do cadastro de tolerancia de recebimento por material.
/// <para>
/// Espelha <see cref="VinculoProdutoEtiquetaServico"/> de proposito: as duas telas juntam as mesmas
/// duas fontes que nao se conhecem — o catalogo de materiais, que vive no SAP e e espelhado localmente
/// (incremento 066), e a decisao do usuario, que vive no banco. Nenhuma manda na outra, e por isso a
/// listagem devolve tambem o material que so existe de um lado, em vez de descartar em silencio.
/// </para>
/// </summary>
public sealed class ToleranciaRecebimentoMaterialServico
{
    private const string Entidade = "ToleranciaRecebimentoMaterial";
    private const string Tela = "ToleranciaRecebimentoMaterialForm";

    private readonly ToleranciaRecebimentoMaterialRepositorio _repositorio;
    private readonly SincronizacaoCatalogoProdutoServico _catalogo;
    private readonly AuditoriaServico _auditoria;

    public ToleranciaRecebimentoMaterialServico(
        ToleranciaRecebimentoMaterialRepositorio repositorio,
        SincronizacaoCatalogoProdutoServico catalogo,
        AuditoriaServico auditoria)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
    }

    /// <summary>Traz do SAP o que o catalogo local ainda nao conhece. So o botao da tela aciona.</summary>
    public Task<ResumoSincronizacaoCatalogo> SincronizarCatalogoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _catalogo.SincronizarAsync(centro, cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _catalogo.ObterUltimaSincronizacaoAsync(centro, cancellationToken);

    /// <summary>
    /// O material aparece nesta tela?
    /// <para>
    /// A pergunta ja tinha resposta no sistema, e ela NAO foi reescrita aqui: quem define "material que
    /// passa pela tela de recebimento" e
    /// <see cref="ClassificadorItemEntradaMaterial.ItemPertenceAoModo"/> no modo
    /// <see cref="ModoEntradaMaterial.RecebimentoMercadoria"/> — hoje ROH, HIBE, VERP e HALB. Uma
    /// segunda lista aqui funcionaria ate o dia em que alguem mudasse uma das duas, e aí o cadastro
    /// ofereceria material que o recebimento recusa, ou o recebimento bloquearia material que o
    /// cadastro nunca ofereceu.
    /// </para>
    /// </summary>
    public static bool MaterialDoRecebimento(string? tipoMaterialSap)
        => ClassificadorItemEntradaMaterial.ItemPertenceAoModo(
            ClassificadorItemEntradaMaterial.ClassificarPorProductType(tipoMaterialSap),
            ModoEntradaMaterial.RecebimentoMercadoria);

    /// <summary>
    /// Lista o centro: materiais do recebimento cruzados com as tolerancias gravadas.
    /// <para>
    /// Le o ESPELHO local, nao o SAP — abrir a tela nao pode custar uma rodada de chamadas de rede. A
    /// ida ao SAP e do botao de sincronizar, e so dele.
    /// </para>
    /// </summary>
    public async Task<ResultadoListagemTolerancia> ListarAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        string alvo = string.IsNullOrWhiteSpace(centro) ? ToleranciaRecebimentoMaterial.CentroPadrao : centro.Trim();

        IReadOnlyList<ToleranciaRecebimentoMaterial> gravadas =
            await _repositorio.ListarPorCentroAsync(alvo, cancellationToken);

        Dictionary<string, ToleranciaRecebimentoMaterial> porMaterial =
            gravadas.ToDictionary(t => t.Material, StringComparer.Ordinal);

        IReadOnlyList<ProdutoCatalogoLocal> catalogo = await _catalogo.ListarLocalAsync(alvo, cancellationToken);

        List<LinhaToleranciaMaterial> linhas = [];
        HashSet<string> vistos = new(StringComparer.Ordinal);

        foreach (ProdutoCatalogoLocal material in catalogo.Where(EhDoRecebimentoPresente))
        {
            porMaterial.TryGetValue(material.Produto, out ToleranciaRecebimentoMaterial? gravada);
            linhas.Add(new LinhaToleranciaMaterial
            {
                Material = material.Produto,
                DescricaoCatalogo = material.Descricao,
                TipoMaterialCatalogo = material.TipoMaterial,
                Tolerancia = gravada,
                PresenteNoCatalogo = true,
            });
            vistos.Add(material.Produto);
        }

        // Tolerancia gravada para material que o catalogo nao trouxe. Aparece marcada, porque uma
        // tolerancia orfa continua decidindo bloqueio no recebimento.
        foreach (ToleranciaRecebimentoMaterial gravada in gravadas.Where(t => !vistos.Contains(t.Material)))
        {
            linhas.Add(new LinhaToleranciaMaterial
            {
                Material = gravada.Material,
                DescricaoCatalogo = null,
                TipoMaterialCatalogo = null,
                Tolerancia = gravada,
                PresenteNoCatalogo = false,
            });
        }

        bool temCatalogo = catalogo.Count > 0;
        List<LinhaToleranciaMaterial> ordenadas = [.. linhas.OrderBy(l => l.Material, StringComparer.Ordinal)];

        return new ResultadoListagemTolerancia
        {
            Linhas = ordenadas,
            CatalogoDisponivel = temCatalogo,
            NaoConfigurados = ordenadas.Count(l => !l.Configurado),
            MensagemCatalogo = temCatalogo
                ? string.Empty
                : "Catálogo local vazio para este centro. Use \"Atualizar do SAP\" para trazer os materiais.",
        };
    }

    /// <summary>
    /// O que a tela mostra: material do RECEBIMENTO que o SAP ainda lista no centro.
    /// <para>
    /// O catalogo local guarda todos os tipos de proposito — e assim que a sincronizacao sabe o que ja
    /// perguntou. O filtro de exibicao fica aqui.
    /// </para>
    /// </summary>
    private static bool EhDoRecebimentoPresente(ProdutoCatalogoLocal material)
        => material.PresenteNoSap && MaterialDoRecebimento(material.TipoMaterial);

    /// <summary>
    /// Grava a tolerancia de um material.
    /// <para>
    /// A unicidade por material e garantida pelo BANCO, nao por consulta previa: verificar antes e
    /// gravar depois deixa uma janela entre as duas coisas. Aqui so ficam as validacoes que o banco nao
    /// tem como fazer.
    /// </para>
    /// </summary>
    public async Task<ResultadoOperacao> SalvarAsync(
        ToleranciaRecebimentoMaterial tolerancia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tolerancia);

        tolerancia.Centro = string.IsNullOrWhiteSpace(tolerancia.Centro)
            ? ToleranciaRecebimentoMaterial.CentroPadrao
            : tolerancia.Centro.Trim();
        tolerancia.Material = (tolerancia.Material ?? string.Empty).Trim();

        if (tolerancia.Material.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um material.");
        }

        if (tolerancia.PercentualTolerancia < 0m)
        {
            return ResultadoOperacao.Falha("A tolerância não pode ser negativa.");
        }

        // Acima de 100 o limite inferior ficaria negativo, e nenhum recebimento e "menos que zero".
        // O banco tambem recusa (CHECK do 069); aqui a recusa vira mensagem em vez de excecao.
        if (tolerancia.PercentualTolerancia > ToleranciaRecebimentoMaterial.PercentualMaximo)
        {
            return ResultadoOperacao.Falha(
                $"A tolerância não pode passar de {ToleranciaRecebimentoMaterial.PercentualMaximo:0}%: "
                + "acima disso o limite inferior ficaria negativo.");
        }

        tolerancia.CriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        long codigo = await _repositorio.SalvarAsync(tolerancia, cancellationToken);

        await _auditoria.RegistrarCadastroAtualizadoAsync(
            Entidade, codigo, DescreverParaAuditoria(tolerancia), Tela, cancellationToken);

        return ResultadoOperacao.Ok("Tolerância salva.", codigo);
    }

    /// <summary>
    /// Apaga a tolerancia do material.
    /// <para>
    /// NAO e "voltar ao normal": sem tolerancia cadastrada o recebimento do material fica BLOQUEADO
    /// (decisao do usuario em 23/11/2026). A mensagem de sucesso diz isso, porque quem apaga precisa
    /// saber que acabou de parar um material, e nao de liberar.
    /// </para>
    /// </summary>
    public async Task<ResultadoOperacao> RemoverAsync(
        string centro, string material, CancellationToken cancellationToken = default)
    {
        string alvo = string.IsNullOrWhiteSpace(centro) ? ToleranciaRecebimentoMaterial.CentroPadrao : centro.Trim();
        string codigoMaterial = (material ?? string.Empty).Trim();

        if (codigoMaterial.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um material.");
        }

        long? removido = await _repositorio.RemoverAsync(alvo, codigoMaterial, cancellationToken);
        if (removido is null)
        {
            return ResultadoOperacao.Falha("Este material não tinha tolerância para remover.");
        }

        await _auditoria.RegistrarCadastroExcluidoAsync(
            Entidade, removido.Value,
            $"Tolerância removida do material {codigoMaterial} (centro {alvo})", Tela, cancellationToken);

        return ResultadoOperacao.Ok(
            $"Tolerância removida. O recebimento do material {codigoMaterial} fica bloqueado até ser cadastrada de novo.");
    }

    private static string DescreverParaAuditoria(ToleranciaRecebimentoMaterial tolerancia)
        => $"Material {tolerancia.Material} (centro {tolerancia.Centro}): tolerância "
         + $"± {tolerancia.PercentualTolerancia:0.##}%, "
         + (tolerancia.Situacao ? "ativa" : "inativa");
}
