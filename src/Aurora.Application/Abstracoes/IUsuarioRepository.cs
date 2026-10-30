using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);

    Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<bool> EmailExisteAsync(string email, int? ignorarId, CancellationToken ct = default);

    Task<bool> ClienteExisteAsync(int clienteId, CancellationToken ct = default);

    Task<(IReadOnlyList<Usuario> Itens, int Total)> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(Usuario usuario);
}
