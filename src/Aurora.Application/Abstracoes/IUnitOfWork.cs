namespace Aurora.Application.Abstracoes;

public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);
}
