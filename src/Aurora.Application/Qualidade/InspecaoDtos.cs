using Aurora.Domain.Enums;

namespace Aurora.Application.Qualidade;

/// <summary>
/// Inspeção de uma OP. <paramref name="Resultado"/> é "Aprovada" ou "Reprovada"; a reprovada exige a estação de
/// retrabalho, de onde as etapas são reabertas (RN06).
/// </summary>
public sealed record InspecaoRequest(string Resultado, byte? EstacaoRetrabalhoId, string? Observacao);

/// <summary>Filtros do histórico; as datas são dias (UTC) inclusivos.</summary>
public sealed record FiltroDeInspecoes(int? OrdemProducaoId, ResultadoInspecao? Resultado, DateOnly? De, DateOnly? Ate);

/// <summary>Inspeção (leitura) com a OP, o pedido e o estado em que ficaram depois dela.</summary>
public sealed record InspecaoResumo(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    string PedidoNumero,
    string ProdutoNome,
    ResultadoInspecao Resultado,
    byte? EstacaoRetrabalhoId,
    string? EstacaoRetrabalhoNome,
    string? Observacao,
    int InspetorId,
    string InspetorNome,
    DateTime DataHora,
    StatusOrdemProducao StatusOrdem,
    StatusPedido StatusPedido);

public sealed record InspecaoDto(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    string PedidoNumero,
    string ProdutoNome,
    string Resultado,
    byte? EstacaoRetrabalhoId,
    string? EstacaoRetrabalhoNome,
    string? Observacao,
    int InspetorId,
    string InspetorNome,
    DateTime DataHora,
    string StatusOrdem,
    string StatusPedido)
{
    public static InspecaoDto De(InspecaoResumo i) => new(
        i.Id,
        i.OrdemProducaoId,
        i.NumeroOp,
        i.PedidoNumero,
        i.ProdutoNome,
        i.Resultado.ToString(),
        i.EstacaoRetrabalhoId,
        i.EstacaoRetrabalhoNome,
        i.Observacao,
        i.InspetorId,
        i.InspetorNome,
        i.DataHora,
        i.StatusOrdem.ToString(),
        i.StatusPedido.ToString());
}
