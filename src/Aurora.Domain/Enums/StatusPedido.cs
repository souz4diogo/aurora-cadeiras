namespace Aurora.Domain.Enums;

/// <summary>Situação do pedido; os nomes são os valores gravados na coluna Pedido.Status.</summary>
public enum StatusPedido
{
    Aberto,
    Confirmado,
    EmProducao,
    Pronto,
    Expedido,
    Cancelado,
}
