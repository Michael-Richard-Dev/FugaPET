using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Modelo.Entrada;

public static class ValidadorEntradaProdutoPersistenciaLotes
{
    public static void ValidarSetorObrigatorio(long? codigoSetorLancamento, ContextoAuditoriaEntradaLotes contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (codigoSetorLancamento is not long setorLancamento || setorLancamento <= 0)
        {
            throw new InvalidOperationException("Setor do lancamento e obrigatorio para persistir entrada com lotes.");
        }

        if (contexto.CodigoSetorUsuario <= 0)
        {
            throw new InvalidOperationException("Setor padrao do usuario e obrigatorio para registrar a entrada com lotes.");
        }

        if (setorLancamento != contexto.CodigoSetorUsuario)
        {
            throw new InvalidOperationException("Setor do lancamento nao autorizado para o usuario.");
        }
    }

    public static void ValidarItemObrigatorio(EntradaProdutoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.CodigoSapPedidoCompraItem is not long codigoSap || codigoSap <= 0)
        {
            throw new InvalidOperationException($"Codigo SAP do item {NormalizarItemMensagem(item.NumeroItem)} e obrigatorio para persistir entrada com lotes.");
        }

        if (string.IsNullOrWhiteSpace(item.NumeroItem))
        {
            throw new InvalidOperationException("Numero do item SAP e obrigatorio para persistir entrada com lotes.");
        }

        ValidarTextoObrigatorio(item.Material, $"Material do item {NormalizarItemMensagem(item.NumeroItem)} e obrigatorio para persistir entrada com lotes.");
        ValidarTextoObrigatorio(item.Centro, $"Centro do item {NormalizarItemMensagem(item.NumeroItem)} e obrigatorio para persistir entrada com lotes.");
        ValidarTextoObrigatorio(item.Deposito, $"Deposito do item {NormalizarItemMensagem(item.NumeroItem)} e obrigatorio para persistir entrada com lotes.");
        ValidarTextoObrigatorio(item.Unidade, $"Unidade do item {NormalizarItemMensagem(item.NumeroItem)} e obrigatoria para persistir entrada com lotes.");
    }

    public static void ValidarCoerenciaCacheSap(
        string numeroPedidoSnapshot,
        EntradaProdutoItem item,
        ValidacaoItemPesagem validacao)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(validacao);

        Comparar("pedido", NormalizarTexto(numeroPedidoSnapshot), NormalizarTexto(validacao.NumeroPedido), item.NumeroItem);
        Comparar("item", NormalizarItem(item.NumeroItem), NormalizarItem(validacao.NumeroItem), item.NumeroItem);
        Comparar("material", NormalizarTexto(item.Material), NormalizarTexto(validacao.Material), item.NumeroItem);
        Comparar("centro", NormalizarTexto(item.Centro), NormalizarTexto(validacao.Centro), item.NumeroItem);
        Comparar("deposito", NormalizarTexto(item.Deposito), NormalizarTexto(validacao.Deposito), item.NumeroItem);
        Comparar("unidade", NormalizarTexto(item.Unidade), NormalizarTexto(validacao.Unidade), item.NumeroItem);
    }

    public static void ValidarTaraDoSetor(long? codigoTara, IReadOnlySet<long> tarasDoSetor, string numeroItem)
    {
        ArgumentNullException.ThrowIfNull(tarasDoSetor);

        if (codigoTara is long tara && tara > 0 && !tarasDoSetor.Contains(tara))
        {
            throw new InvalidOperationException($"Tara selecionada nao pertence ao setor autorizado (item {numeroItem}).");
        }
    }

    public static void ValidarBalancaDoSetor(
        long? codigoBalanca,
        BalancaCadastro? balanca,
        bool repositorioDisponivel,
        long codigoSetorLancamento,
        string numeroItem)
    {
        if (codigoBalanca is not long codigo || codigo <= 0)
        {
            return;
        }

        if (!repositorioDisponivel)
        {
            throw new InvalidOperationException($"Validacao de balanca indisponivel para o item {numeroItem}.");
        }

        if (balanca is null || !balanca.SituacaoBalanca)
        {
            throw new InvalidOperationException($"Balanca informada nao encontrada ou inativa (item {numeroItem}).");
        }

        if (balanca.CodigoSetor != codigoSetorLancamento)
        {
            throw new InvalidOperationException($"Balanca nao pertence ao setor do lancamento (item {numeroItem}).");
        }
    }

    private static void ValidarTextoObrigatorio(string? valor, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(mensagem);
        }
    }

    private static void Comparar(string campo, string valorSnapshot, string valorCache, string numeroItem)
    {
        if (!string.Equals(valorSnapshot, valorCache, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Divergencia no campo {campo} do item {NormalizarItemMensagem(numeroItem)} em relacao ao cache SAP.");
        }
    }

    private static string NormalizarTexto(string? valor)
        => (valor ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizarItem(string? numeroItem)
        => EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(numeroItem);

    private static string NormalizarItemMensagem(string? numeroItem)
        => string.IsNullOrWhiteSpace(numeroItem) ? "sem numero" : numeroItem.Trim();
}


