using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// Portao de seguranca da pesagem de Entrada de Produto: valida autenticacao, permissao,
/// origem do peso, peso positivo e situacao do pedido/item/material ANTES de gravar em
/// pesagem_entrada_item. Tentativas negadas sao auditadas (best-effort) sem que
/// uma falha de auditoria libere a operacao.
/// </summary>
public sealed class PesagemEntradaServico
{
    private const string OrigemManual = "DIGITADO";
    private static readonly string[] OrigensBalanca = ["LIDO", "MULTIPLA"];
    private const string TelaAuditoria = "Entrada de Produto";

    private readonly PesagemEntradaItemRepositorio _repositorio;
    private readonly AuditoriaServico? _auditoria;

    public PesagemEntradaServico()
        : this(
            new PesagemEntradaItemRepositorio(new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())),
            CriarAuditoriaPadrao())
    {
    }

    public PesagemEntradaServico(PesagemEntradaItemRepositorio repositorio)
        : this(repositorio, null)
    {
    }

    public PesagemEntradaServico(PesagemEntradaItemRepositorio repositorio, AuditoriaServico? auditoria)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
    }

    /// <summary>
    /// Valida e grava as pesagens. Lanca <see cref="ErroOperacionalEsperadoException"/> com mensagem
    /// amigavel quando alguma regra de seguranca falha. Retorna a quantidade gravada.
    /// </summary>
    public async Task<int> SalvarPesagensAsync(IReadOnlyList<PesagemEntradaItem> pesagens, CancellationToken cancellationToken = default)
    {
        // (1) Usuario autenticado e responsavel obrigatorio.
        long usuario = ExigirUsuarioAutenticado();

        // (2) Permissao da operacao (gravar = finalizar leitura).
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Finalizar))
        {
            await NegarAsync(usuario, AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.Finalizar), cancellationToken);
        }

        if (pesagens.Count == 0)
        {
            throw new ErroOperacionalEsperadoException("Nenhuma pesagem informada para gravar.");
        }

        // (3) Peso manual (origem MANUAL/DIGITADO) exige permissao PESO_MANUAL.
        bool possuiManual = pesagens.Any(pesagem =>
            string.Equals(pesagem.OrigemPeso?.Trim(), OrigemManual, StringComparison.OrdinalIgnoreCase));
        if (possuiManual && !AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual))
        {
            await NegarAsync(usuario, AutorizacaoEntradaProdutoServico.MensagemSemPermissao(PermissoesSistema.Acoes.PesoManual), cancellationToken);
        }

        // (4) Validacao por item: origem permitida, peso > 0, pedido/item ativos e material valido.
        foreach (PesagemEntradaItem pesagem in pesagens)
        {
            await ValidarPesagemAsync(usuario, pesagem, cancellationToken);
        }

        return await _repositorio.SalvarPesagensAsync(pesagens, cancellationToken);
    }

    private static long ExigirUsuarioAutenticado()
    {
        long? usuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (!usuario.HasValue)
        {
            // Sem sessao nao ha quem auditar; bloqueia mesmo assim.
            throw new ErroOperacionalEsperadoException("Usuario nao autenticado. Faca login para registrar a pesagem.");
        }

        return usuario.Value;
    }

    private async Task ValidarPesagemAsync(long usuario, PesagemEntradaItem pesagem, CancellationToken cancellationToken)
    {
        string origem = pesagem.OrigemPeso?.Trim() ?? string.Empty;
        bool origemPermitida = origem.Equals(OrigemManual, StringComparison.OrdinalIgnoreCase)
            || OrigensBalanca.Contains(origem, StringComparer.OrdinalIgnoreCase);
        if (!origemPermitida)
        {
            await NegarAsync(usuario, "Origem de peso invalida para a pesagem.", cancellationToken);
        }

        if (pesagem.PesoKg <= 0m)
        {
            await NegarAsync(usuario, "Peso da pesagem deve ser maior que zero.", cancellationToken);
        }

        if (pesagem.CodigoSapPedidoCompraItem <= 0)
        {
            await NegarAsync(usuario, "Item do pedido invalido para a pesagem.", cancellationToken);
        }

        ValidacaoItemPesagem validacao = await _repositorio.ValidarItemAsync(pesagem.CodigoSapPedidoCompraItem, cancellationToken);
        if (!validacao.Existe)
        {
            await NegarAsync(usuario, "Item do pedido nao encontrado para a pesagem.", cancellationToken);
        }

        if (!validacao.PedidoAtivo)
        {
            await NegarAsync(usuario, "Pedido de compra inativo. Pesagem nao permitida.", cancellationToken);
        }

        if (!validacao.ItemAtivo)
        {
            await NegarAsync(usuario, "Item do pedido inativo. Pesagem nao permitida.", cancellationToken);
        }

        if (!validacao.MaterialPresente)
        {
            await NegarAsync(usuario, "Item sem material valido. Pesagem nao permitida.", cancellationToken);
        }
    }

    // Registra a tentativa negada (best-effort) e lanca erro amigavel.
    // Falha de auditoria NUNCA pode liberar a operacao: o bloqueio acontece independentemente.
    private async Task NegarAsync(long usuario, string mensagem, CancellationToken cancellationToken)
    {
        if (_auditoria is not null)
        {
            try
            {
                await _auditoria.RegistrarAcessoNegadoAsync(usuario, mensagem, TelaAuditoria, cancellationToken);
            }
            catch
            {
                // Auditoria e best-effort; sua falha nao libera a pesagem.
            }
        }

        throw new ErroOperacionalEsperadoException(mensagem);
    }

    private static AuditoriaServico? CriarAuditoriaPadrao()
    {
        try
        {
            IFabricaConexaoBanco fabrica = new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar());
            return new AuditoriaServico(new AuditoriaAcaoUsuarioServico(new AuditoriaAcaoUsuarioRepositorio(fabrica)));
        }
        catch
        {
            // Sem auditoria disponivel, o portao de seguranca continua valendo (apenas nao audita).
            return null;
        }
    }
}
