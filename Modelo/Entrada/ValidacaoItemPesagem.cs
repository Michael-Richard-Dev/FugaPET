namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>Resultado da validacao de um item de pedido para pesagem.</summary>
public sealed record ValidacaoItemPesagem(
    bool Existe,
    bool PedidoAtivo,
    bool ItemAtivo,
    bool MaterialPresente,
    string NumeroPedido = "",
    string NumeroItem = "",
    string Material = "",
    string Centro = "",
    string Deposito = "",
    string Unidade = "");
