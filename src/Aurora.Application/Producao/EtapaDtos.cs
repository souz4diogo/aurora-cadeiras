using Aurora.Domain.Enums;

namespace Aurora.Application.Producao;

/// <summary>Etapa (leitura) com a estação, o operador e os dados da OP que a validação precisa.</summary>
public sealed record EtapaResumo(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    StatusOrdemProducao StatusOp,
    int QuantidadeOp,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    StatusEtapa Status,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm,
    int? OperadorId,
    string? OperadorNome);

public sealed record EtapaDto(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    string Status,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm,
    int? OperadorId,
    string? OperadorNome)
{
    public static EtapaDto De(EtapaResumo e) => new(
        e.Id,
        e.OrdemProducaoId,
        e.NumeroOp,
        e.EstacaoId,
        e.EstacaoNome,
        e.Sequencia,
        e.Status.ToString(),
        e.IniciadaEm,
        e.ConcluidaEm,
        e.OperadorId,
        e.OperadorNome);
}

/// <summary>
/// Início ou fim de etapa. O <paramref name="ClientRequestId"/> identifica a tentativa: reenviá-lo (por exemplo,
/// após uma queda de rede) não repete o efeito. <paramref name="DataHoraCliente"/> é a hora da ação no aparelho.
/// </summary>
public sealed record ApontamentoRequest(Guid ClientRequestId, DateTimeOffset DataHoraCliente);

/// <summary>Perda numa etapa; <paramref name="Tipo"/> é "Refugo" ou "Retrabalho".</summary>
public sealed record PerdaRequest(string Tipo, int Quantidade, string Motivo);
