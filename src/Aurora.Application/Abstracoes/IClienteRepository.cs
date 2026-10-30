using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IClienteRepository
{
    Task<Cliente?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<bool> DocumentoExisteAsync(string cpfCnpj, int? ignorarId, CancellationToken ct = default);

    Task<(IReadOnlyList<Cliente> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(Cliente cliente);
}
