using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Usuarios;

public sealed record UsuarioResposta(int Id, string Nome, string Email, string Perfil, bool Ativo);

public interface IUsuariosApi
{
    /// <summary>Todos os usuários (até 2.000), em ordem alfabética, para preencher listas de escolha.</summary>
    Task<IReadOnlyList<UsuarioResposta>> ListarTodosAsync(CancellationToken ct = default);
}

public sealed class UsuariosApi(HttpClient http) : ApiClientBase(http), IUsuariosApi
{
    public Task<IReadOnlyList<UsuarioResposta>> ListarTodosAsync(CancellationToken ct = default) =>
        TodasAsPaginasAsync<UsuarioResposta>("api/v1/usuarios", [], ct);
}
