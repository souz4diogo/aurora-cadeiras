using Aurora.Domain.Enums;

namespace Aurora.Application.Producao;

/// <summary>Etapa pendente ou em andamento de uma estação (leitura), com a OP e o produto que o operador precisa ver.</summary>
public sealed record EtapaDaEstacao(
    int EtapaId,
    int OrdemProducaoId,
    string NumeroOp,
    Guid CodigoQr,
    string PedidoNumero,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    DateOnly? PrevisaoConclusao,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    StatusEtapa Status,
    DateTime? IniciadaEm,
    int? OperadorId,
    bool Liberada);

/// <summary>
/// Etapa da fila da estação. <paramref name="Liberada"/> diz se as etapas anteriores da OP já terminaram (RN05):
/// só então ela pode ser iniciada; as demais aparecem para o operador saber o que vem.
/// </summary>
public sealed record EtapaDaEstacaoDto(
    int EtapaId,
    int OrdemProducaoId,
    string NumeroOp,
    Guid CodigoQr,
    string PedidoNumero,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    DateOnly? PrevisaoConclusao,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    string Status,
    DateTime? IniciadaEm,
    int? OperadorId,
    bool Liberada)
{
    public static EtapaDaEstacaoDto De(EtapaDaEstacao e) => new(
        e.EtapaId,
        e.OrdemProducaoId,
        e.NumeroOp,
        e.CodigoQr,
        e.PedidoNumero,
        e.ProdutoId,
        e.ProdutoCodigo,
        e.ProdutoNome,
        e.Quantidade,
        e.PrevisaoConclusao,
        e.EstacaoId,
        e.EstacaoNome,
        e.Sequencia,
        e.Status.ToString(),
        e.IniciadaEm,
        e.OperadorId,
        e.Liberada);
}
