using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// Responde a UMA pergunta da pesagem: por qual processo este produto deve ser pesado?
/// <para>
/// O vinculo produto x etiqueta (incremento 065) guarda duas decisoes diferentes: QUAL etiqueta e QUAL
/// processo. Quem pesa so precisa da segunda, e esta classe existe para entregar so ela — a tela de
/// Produto Acabado nao deve carregar o vocabulario inteiro do cadastro para decidir como se comporta.
/// </para>
/// </summary>
public class ProcessoPesagemProdutoServico
{
    private readonly VinculoProdutoEtiquetaRepositorio _repositorio;

    public ProcessoPesagemProdutoServico(VinculoProdutoEtiquetaRepositorio repositorio)
        => _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));

    /// <summary>
    /// O processo configurado para o produto, ou <c>null</c> quando nao ha vinculo.
    /// <para>
    /// NULO NAO E ERRO, e por isso nao vira excecao nem valor padrao aqui: "este produto nao foi
    /// configurado" e uma resposta legitima, e quem chama decide o que fazer com ela. Converter o nulo
    /// em CaixaACaixa dentro deste metodo apagaria a diferenca entre "configurado como caixa a caixa" e
    /// "nunca configurado" — duas situacoes que um dia vao querer tratamento diferente.
    /// </para>
    /// <para>
    /// Centro ou produto em branco devolvem nulo sem ir ao banco: sao a ordem ainda nao carregada, nao
    /// uma pergunta legitima.
    /// </para>
    /// </summary>
    public virtual async Task<ProcessoPesagemProduto?> ObterProcessoAsync(
        string? centro, string? produto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(centro) || string.IsNullOrWhiteSpace(produto))
        {
            return null;
        }

        VinculoProdutoEtiqueta? vinculo =
            await _repositorio.ObterPorProdutoAsync(centro.Trim(), produto.Trim(), cancellationToken);

        return vinculo?.ProcessoPesagem;
    }

    /// <summary>
    /// O produto exige o envio agrupado? Falso tambem quando nao ha vinculo.
    /// <para>
    /// E aqui, num lugar so, que o nulo vira comportamento: SEM VINCULO A PESAGEM SEGUE COMO SEMPRE
    /// FOI. Barrar quem nao tem vinculo pararia a producao por causa de cadastro faltando, e cadastro
    /// faltando se resolve cadastrando, nao parando a fabrica.
    /// </para>
    /// </summary>
    public async Task<bool> EhEnvioUnicoAsync(
        string? centro, string? produto, CancellationToken cancellationToken = default)
        => await ObterProcessoAsync(centro, produto, cancellationToken) == ProcessoPesagemProduto.EnvioUnico;
}
