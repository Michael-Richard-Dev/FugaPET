using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Cadastro;

/// <summary>
/// Traz do SAP apenas o que o catalogo local ainda NAO conhece.
/// <para>
/// Antes disto, a tela de Vinculo de Produto consultava o SAP a cada abertura: para o centro 3007 do
/// mandante 130 eram 51 chamadas — 8 paginas para listar os 1521 materiais, 39 lotes para descobrir
/// o tipo de cada um e 4 lotes de descricao. Pedido do usuario em 18/11/2026.
/// </para>
/// <para>
/// A economia vem de uma assimetria: a LISTA do centro precisa ser lida por inteiro toda vez, porque
/// e ela que revela o que entrou e o que saiu; o CADASTRO de cada material, nao — material que ja se
/// conhece nunca mais e perguntado. Numa rodada sem novidade, isso significa 8 chamadas em vez de 51,
/// e zero se nada mudar alem disso.
/// </para>
/// </summary>
public sealed class SincronizacaoCatalogoProdutoServico
{
    private readonly CatalogoProdutoCentroRepositorio _repositorio;
    private readonly ICatalogoProdutoCentroSapServico _sap;

    public SincronizacaoCatalogoProdutoServico(
        CatalogoProdutoCentroRepositorio repositorio,
        ICatalogoProdutoCentroSapServico sap)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _sap = sap ?? throw new ArgumentNullException(nameof(sap));
    }

    /// <summary>
    /// Sincroniza o centro e devolve o que mudou.
    /// <para>
    /// A ordem importa e e esta: registrar o que existe, marcar o que sumiu, e so entao buscar o
    /// cadastro do que falta. Buscar antes de registrar deixaria material novo sem linha onde gravar
    /// o cadastro; marcar ausentes depois de buscar gastaria chamada com quem ja saiu.
    /// </para>
    /// </summary>
    public async Task<ResumoSincronizacaoCatalogo> SincronizarAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        string alvo = string.IsNullOrWhiteSpace(centro) ? VinculoProdutoEtiqueta.CentroPadrao : centro.Trim();

        ResultadoCodigosCentro lista = await _sap.ListarCodigosDoCentroAsync(alvo, cancellationToken);
        if (!lista.Sucesso)
        {
            // Falhou a listagem: NAO se marca nada como ausente. Tratar "nao consegui perguntar"
            // como "sumiu" apagaria o catalogo inteiro num erro de rede.
            return ResumoSincronizacaoCatalogo.Falha(lista.MensagemDiagnostico) with
            {
                ChamadasSap = lista.ChamadasSap,
            };
        }

        int novos = await _repositorio.RegistrarVistosAsync(alvo, lista.Codigos, cancellationToken);
        int ausentes = await _repositorio.MarcarAusentesAsync(alvo, lista.Codigos, cancellationToken);

        IReadOnlyList<string> pendentes = await _repositorio.ListarPendentesDeMestreAsync(alvo, cancellationToken);
        int chamadas = lista.ChamadasSap;
        int lidos = 0;

        if (pendentes.Count > 0)
        {
            ResultadoCadastroProdutos cadastro = await _sap.ObterCadastroAsync(pendentes, cancellationToken);
            chamadas += cadastro.ChamadasSap;

            if (!cadastro.Sucesso)
            {
                // A listagem ja foi gravada e vale; o que faltou foi o cadastro. Os materiais ficam
                // pendentes e a proxima sincronizacao tenta de novo — nada se perde.
                return new ResumoSincronizacaoCatalogo
                {
                    Sucesso = false,
                    Mensagem = $"Materiais do centro atualizados, mas o cadastro de {pendentes.Count} "
                             + $"deles não pôde ser lido: {cadastro.MensagemDiagnostico}",
                    Novos = novos,
                    Ausentes = ausentes,
                    TotalNoCentro = lista.Codigos.Count,
                    ChamadasSap = chamadas,
                };
            }

            lidos = await _repositorio.AtualizarMestresAsync(alvo, cadastro.Itens, cancellationToken);
        }

        return new ResumoSincronizacaoCatalogo
        {
            Sucesso = true,
            Mensagem = Descrever(novos, lidos, ausentes, chamadas),
            Novos = novos,
            MestresLidos = lidos,
            Ausentes = ausentes,
            TotalNoCentro = lista.Codigos.Count,
            ChamadasSap = chamadas,
        };
    }

    /// <summary>Catalogo local do centro, sem tocar no SAP.</summary>
    public Task<IReadOnlyList<ProdutoCatalogoLocal>> ListarLocalAsync(
        string centro, CancellationToken cancellationToken = default)
        => _repositorio.ListarPorCentroAsync(
            string.IsNullOrWhiteSpace(centro) ? VinculoProdutoEtiqueta.CentroPadrao : centro.Trim(),
            cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _repositorio.ObterUltimaSincronizacaoAsync(
            string.IsNullOrWhiteSpace(centro) ? VinculoProdutoEtiqueta.CentroPadrao : centro.Trim(),
            cancellationToken);

    /// <summary>
    /// Texto para o operador. "Nada novo" e uma resposta util: sem ela, ele clica no botao e fica
    /// sem saber se funcionou.
    /// </summary>
    private static string Descrever(int novos, int lidos, int ausentes, int chamadas)
    {
        List<string> partes = [];
        if (novos > 0) partes.Add($"{novos} produto(s) novo(s)");
        if (lidos > 0 && lidos != novos) partes.Add($"{lidos} cadastro(s) lido(s)");
        if (ausentes > 0) partes.Add($"{ausentes} que saíram do centro");

        string resumo = partes.Count == 0 ? "Nenhuma novidade no centro" : string.Join(", ", partes);
        return $"{resumo}. {chamadas} consulta(s) ao SAP.";
    }
}
