namespace Aurora.Domain.Enums;

/// <summary>Situação do pedido de compra; os nomes são os valores gravados na coluna PedidoCompra.Status.</summary>
public enum StatusPedidoCompra
{
    Solicitado,
    Emitido,
    ParcialmenteRecebido,
    Recebido,
    Cancelado,
}
