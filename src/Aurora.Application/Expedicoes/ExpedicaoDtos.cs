namespace Aurora.Application.Expedicoes;

/// <summary>Dados da saída do pedido; a transportadora e o código de rastreio são opcionais.</summary>
public sealed record ExpedirRequest(string? Transportadora, string? CodigoRastreio);

/// <summary>Filtros da listagem; <see cref="VendedorId"/> restringe às expedições dos pedidos de um vendedor.</summary>
public sealed record FiltroDeExpedicoes(int? VendedorId, DateOnly? De, DateOnly? Ate);

public sealed record ExpedicaoResumo(
    int Id,
    int PedidoId,
    string PedidoNumero,
    string ClienteNome,
    int UsuarioId,
    string UsuarioNome,
    string? Transportadora,
    string? CodigoRastreio,
    DateTime DataHora);

public sealed record ExpedicaoDto(
    int Id,
    int PedidoId,
    string PedidoNumero,
    string ClienteNome,
    int UsuarioId,
    string UsuarioNome,
    string? Transportadora,
    string? CodigoRastreio,
    DateTime DataHora)
{
    public static ExpedicaoDto De(ExpedicaoResumo e) => new(
        e.Id,
        e.PedidoId,
        e.PedidoNumero,
        e.ClienteNome,
        e.UsuarioId,
        e.UsuarioNome,
        e.Transportadora,
        e.CodigoRastreio,
        e.DataHora);
}
