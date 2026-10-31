namespace Aurora.Web.Api.Producao;

public sealed record OrdemProducaoResumoResposta(
    int Id,
    string Numero,
    int PedidoId,
    string PedidoNumero,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    string Status,
    DateOnly? PrevisaoConclusao,
    bool Atrasada,
    DateTime CriadaEm,
    DateTime? ConcluidaEm,
    int TotalDeEtapas,
    int EtapasConcluidas,
    EtapaAtualResposta? EtapaAtual);
