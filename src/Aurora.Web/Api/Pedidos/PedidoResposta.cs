namespace Aurora.Web.Api.Pedidos;

public sealed record PedidoResposta(
    int Id,
    string Numero,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    DateTime DataPedido,
    DateOnly PrazoEntrega,
    string Status,
    string? Observacao,
    decimal ValorTotal,
    bool Atrasado,
    IReadOnlyList<PedidoItemResposta> Itens)
{
    public const string StatusAberto = "Aberto";

    public bool PodeEditar => Status == StatusAberto;

    /// <summary>O cancelamento vale até a produção terminar (Aberto, Confirmado ou Em produção).</summary>
    public bool PodeCancelar => Status is StatusAberto or "Confirmado" or "EmProducao";
}
