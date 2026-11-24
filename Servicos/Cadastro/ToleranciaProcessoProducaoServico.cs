using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Cadastro;

/// <summary>O que a tela de tolerância de processo recebe de uma listagem.</summary>
public sealed record ResultadoListagemToleranciaProcesso
{
    public IReadOnlyList<LinhaToleranciaProcesso> Linhas { get; init; } = [];

    public bool CatalogoDisponivel { get; init; }

    public string MensagemCatalogo { get; init; } = string.Empty;

    public int NaoConfigurados { get; init; }
}

/// <summary>
/// Cadastro da tolerância por produto nos processos de produção (incremento 071).
/// <para>
/// Gêmeo do <see cref="ToleranciaRecebimentoMaterialServico"/>, com o PROCESSO na chave.
/// </para>
/// </summary>
public sealed class ToleranciaProcessoProducaoServico
{
    private const string Entidade = "ToleranciaProcessoProducao";
    private const string Tela = "ToleranciaProcessoProducaoForm";

    private readonly ToleranciaProcessoProducaoRepositorio _repositorio;
    private readonly SincronizacaoCatalogoProdutoServico _catalogo;
    private readonly AuditoriaServico _auditoria;

    public ToleranciaProcessoProducaoServico(
        ToleranciaProcessoProducaoRepositorio repositorio,
        SincronizacaoCatalogoProdutoServico catalogo,
        AuditoriaServico auditoria)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
    }

    /// <summary>Traz do SAP o que o catálogo local ainda não conhece. Só o botão da tela aciona.</summary>
    public Task<ResumoSincronizacaoCatalogo> SincronizarCatalogoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _catalogo.SincronizarAsync(centro, cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _catalogo.ObterUltimaSincronizacaoAsync(centro, cancellationToken);

    /// <summary>
    /// O produto aparece na tela DESTE processo?
    /// <para>
    /// A pergunta já tinha resposta no sistema, e ela NÃO foi reescrita aqui — mesma disciplina do
    /// 069. Quem define o que o Consumo pesa é
    /// <see cref="ClassificadorComponenteConsumo.ClassificarComponenteParaConsumo"/> (ROH, VERP,
    /// HALB e HIBE, mais os grupos químicos homologados); quem define o semiacabado é
    /// <see cref="ClassificadorItemEntradaMaterial.ClassificarPorProductType"/> (HALB).
    /// </para>
    /// <para>
    /// Uma segunda lista de tipos aqui funcionaria até o dia em que alguém mudasse uma das duas, e aí
    /// o cadastro ofereceria produto que a tela recusa, ou a tela bloquearia produto que o cadastro
    /// nunca ofereceu.
    /// </para>
    /// <para>
    /// <b>LIMITE CONHECIDO:</b> o catálogo local (066) espelha o TIPO do material, mas não o GRUPO.
    /// Por isso a listagem passa <c>null</c> como grupo, e um produto que só seria químico pelo
    /// GRUPO homologado — não pelo tipo HIBE — não aparece na lista do Consumo. Ele continua podendo
    /// receber tolerância se for cadastrado por outro caminho, e o bloqueio da tela de Consumo
    /// continua valendo para ele. Resolver isso é acrescentar a coluna de grupo ao catálogo, não
    /// duplicar a classificação aqui.
    /// </para>
    /// </summary>
    public static bool ProdutoDoProcesso(string processo, string? tipoMaterialSap, string? grupoMaterialSap)
        => processo switch
        {
            ProcessoToleranciaProducao.Consumo =>
                ClassificadorComponenteConsumo.ClassificarComponenteParaConsumo(
                    tipoMaterialSap, grupoMaterialSap)
                    is ClassificacaoConsumoMaterial.MateriaPrima or ClassificacaoConsumoMaterial.Quimico,

            ProcessoToleranciaProducao.SemiAcabado =>
                ClassificadorItemEntradaMaterial.ClassificarPorProductType(tipoMaterialSap)
                    is ClassificacaoEntradaMaterial.Semiacabado,

            _ => false,
        };

    /// <summary>
    /// Lista o centro num processo: produtos daquele processo cruzados com as tolerâncias gravadas.
    /// <para>
    /// Lê o ESPELHO local, não o SAP — abrir a tela não pode custar uma rodada de chamadas de rede. A
    /// ida ao SAP é do botão de sincronizar, e só dele.
    /// </para>
    /// </summary>
    public async Task<ResultadoListagemToleranciaProcesso> ListarAsync(
        string centro, string processo, CancellationToken cancellationToken = default)
    {
        if (!ProcessoToleranciaProducao.EhValido(processo))
        {
            throw new ArgumentException($"Processo desconhecido: {processo}", nameof(processo));
        }

        string alvo = string.IsNullOrWhiteSpace(centro) ? ToleranciaProcessoProducao.CentroPadrao : centro.Trim();

        IReadOnlyList<ToleranciaProcessoProducao> gravadas =
            await _repositorio.ListarPorCentroEProcessoAsync(alvo, processo, cancellationToken);

        Dictionary<string, ToleranciaProcessoProducao> porMaterial =
            gravadas.ToDictionary(t => t.Material, StringComparer.Ordinal);

        bool catalogoDisponivel = true;
        string mensagemCatalogo = string.Empty;
        IReadOnlyList<ProdutoCatalogoLocal> catalogo = [];
        try
        {
            catalogo = await _catalogo.ListarLocalAsync(alvo, cancellationToken);
        }
        catch (Exception erro)
        {
            // Catálogo indisponível não some com as tolerâncias já gravadas: elas continuam
            // decidindo bloqueio nas telas de processo, então escondê-las seria mentir.
            catalogoDisponivel = false;
            mensagemCatalogo = $"Catálogo local indisponível ({erro.GetType().Name}). "
                             + "A lista mostra apenas os produtos que já têm tolerância gravada.";
        }

        List<LinhaToleranciaProcesso> linhas = [];
        HashSet<string> vistos = new(StringComparer.Ordinal);

        foreach (ProdutoCatalogoLocal produto in catalogo)
        {
            // grupo = null: o catálogo local não espelha o grupo do material. Ver ProdutoDoProcesso.
            if (!produto.PresenteNoSap
                || !ProdutoDoProcesso(processo, produto.TipoMaterial, grupoMaterialSap: null))
            {
                continue;
            }

            porMaterial.TryGetValue(produto.Produto, out ToleranciaProcessoProducao? gravada);
            linhas.Add(new LinhaToleranciaProcesso
            {
                Processo = processo,
                Material = produto.Produto,
                DescricaoCatalogo = produto.Descricao,
                TipoMaterialCatalogo = produto.TipoMaterial,
                Tolerancia = gravada,
                PresenteNoCatalogo = true,
            });
            vistos.Add(produto.Produto);
        }

        // Tolerâncias ÓRFÃS: produto que o centro não lista mais, mas que tem decisão gravada.
        // Mostradas em vez de escondidas, porque uma tolerância órfã continua decidindo bloqueio.
        foreach (ToleranciaProcessoProducao orfa in gravadas.Where(t => !vistos.Contains(t.Material)))
        {
            linhas.Add(new LinhaToleranciaProcesso
            {
                Processo = processo,
                Material = orfa.Material,
                DescricaoCatalogo = null,
                TipoMaterialCatalogo = orfa.TipoMaterial,
                Tolerancia = orfa,
                PresenteNoCatalogo = false,
            });
        }

        List<LinhaToleranciaProcesso> ordenadas =
            [.. linhas.OrderBy(l => l.Material, StringComparer.Ordinal)];

        return new ResultadoListagemToleranciaProcesso
        {
            Linhas = ordenadas,
            CatalogoDisponivel = catalogoDisponivel,
            MensagemCatalogo = mensagemCatalogo,
            NaoConfigurados = ordenadas.Count(l => !l.Configurado),
        };
    }

    /// <summary>
    /// Grava a tolerância de um produto num processo.
    /// <para>
    /// A unicidade é garantida pelo BANCO, não por consulta prévia: verificar antes e gravar depois
    /// deixa uma janela entre as duas coisas. Aqui só ficam as validações que o banco não tem como
    /// fazer, e as que viram mensagem em vez de exceção.
    /// </para>
    /// </summary>
    public async Task<ResultadoOperacao> SalvarAsync(
        ToleranciaProcessoProducao tolerancia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tolerancia);

        tolerancia.Centro = string.IsNullOrWhiteSpace(tolerancia.Centro)
            ? ToleranciaProcessoProducao.CentroPadrao
            : tolerancia.Centro.Trim();
        tolerancia.Material = (tolerancia.Material ?? string.Empty).Trim();

        if (!ProcessoToleranciaProducao.EhValido(tolerancia.Processo))
        {
            return ResultadoOperacao.Falha("Selecione o processo.");
        }

        if (tolerancia.Material.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um produto.");
        }

        if (tolerancia.PercentualTolerancia < 0m)
        {
            return ResultadoOperacao.Falha("A tolerância não pode ser negativa.");
        }

        // Acima de 100 o limite inferior ficaria negativo, e nenhuma produção é "menos que zero".
        // O banco também recusa (CHECK do 071); aqui a recusa vira mensagem em vez de exceção.
        if (tolerancia.PercentualTolerancia > ToleranciaProcessoProducao.PercentualMaximo)
        {
            return ResultadoOperacao.Falha(
                $"A tolerância não pode passar de {ToleranciaProcessoProducao.PercentualMaximo:0}%: "
                + "acima disso o limite inferior ficaria negativo.");
        }

        tolerancia.CriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        long codigo = await _repositorio.SalvarAsync(tolerancia, cancellationToken);

        await _auditoria.RegistrarCadastroAtualizadoAsync(
            Entidade, codigo, DescreverParaAuditoria(tolerancia), Tela, cancellationToken);

        return ResultadoOperacao.Ok("Tolerância salva.", codigo);
    }

    /// <summary>
    /// Apaga a tolerância do produto naquele processo.
    /// <para>
    /// NÃO é "voltar ao normal": sem tolerância cadastrada a pesagem daquele produto NAQUELE PROCESSO
    /// fica BLOQUEADA (decisão do usuário em 24/11/2026). A mensagem de sucesso diz isso, porque quem
    /// apaga precisa saber que acabou de parar um produto, e não de liberar.
    /// </para>
    /// </summary>
    public async Task<ResultadoOperacao> RemoverAsync(
        string centro, string processo, string material, CancellationToken cancellationToken = default)
    {
        if (!ProcessoToleranciaProducao.EhValido(processo))
        {
            return ResultadoOperacao.Falha("Selecione o processo.");
        }

        string alvo = string.IsNullOrWhiteSpace(centro) ? ToleranciaProcessoProducao.CentroPadrao : centro.Trim();
        string codigoMaterial = (material ?? string.Empty).Trim();

        if (codigoMaterial.Length == 0)
        {
            return ResultadoOperacao.Falha("Selecione um produto.");
        }

        long? removido = await _repositorio.RemoverAsync(alvo, processo, codigoMaterial, cancellationToken);
        if (removido is null)
        {
            return ResultadoOperacao.Falha("Este produto não tinha tolerância neste processo para remover.");
        }

        await _auditoria.RegistrarCadastroExcluidoAsync(
            Entidade, removido.Value,
            $"Tolerância removida do produto {codigoMaterial} em {ProcessoToleranciaProducao.Descrever(processo)} "
            + $"(centro {alvo})", Tela, cancellationToken);

        return ResultadoOperacao.Ok(
            $"Tolerância removida. A pesagem do produto {codigoMaterial} em "
            + $"{ProcessoToleranciaProducao.Descrever(processo)} fica bloqueada até ser cadastrada de novo.");
    }

    private static string DescreverParaAuditoria(ToleranciaProcessoProducao tolerancia)
        => $"Produto {tolerancia.Material} em {ProcessoToleranciaProducao.Descrever(tolerancia.Processo)} "
         + $"(centro {tolerancia.Centro}): tolerância ± {tolerancia.PercentualTolerancia:0.##}%, "
         + (tolerancia.Situacao ? "ativa" : "inativa");
}
