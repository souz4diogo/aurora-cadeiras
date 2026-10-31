using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public static class TransacaoExtensions
{
    /// <summary>
    /// Executa a operação numa transação própria (tudo ou nada). A estratégia de retentativa do EF exige que a
    /// transação manual fique dentro dela, e a operação inteira é repetida se a conexão cair no meio.
    /// </summary>
    public static Task ExecutarEmTransacaoAsync(this AuroraDbContext db, Func<Task> operacao, CancellationToken ct = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            await operacao();
            await transacao.CommitAsync(ct);
        });
}
