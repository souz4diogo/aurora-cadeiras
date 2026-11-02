using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Usuarios;

public sealed record UsuarioResposta(
    int Id, string Nome, string Email, byte PerfilId, string Perfil, int? ClienteId, bool Ativo, DateTime? UltimoLoginEm);

public sealed record PerfilResposta(byte Id, string Nome);

/// <summary>A senha provisória só existe nesta resposta: a API guarda apenas o hash.</summary>
public sealed record UsuarioComSenhaProvisoriaResposta(UsuarioResposta Usuario, string SenhaProvisoria);

public sealed record UsuarioRequisicao(string Nome, string Email, byte PerfilId, int? ClienteId);

public interface IUsuariosApi
{
    Task<Pagina<UsuarioResposta>> ListarAsync(string? busca, int pagina, CancellationToken ct = default);

    /// <summary>Todos os usuários (até 2.000), para preencher listas de escolha.</summary>
    Task<IReadOnlyList<UsuarioResposta>> ListarTodosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PerfilResposta>> ListarPerfisAsync(CancellationToken ct = default);

    Task<UsuarioResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<UsuarioComSenhaProvisoriaResposta> CriarAsync(UsuarioRequisicao dados, CancellationToken ct = default);

    Task<UsuarioResposta> AtualizarAsync(int id, UsuarioRequisicao dados, CancellationToken ct = default);

    /// <summary>Inativar revoga as sessões do usuário.</summary>
    Task<UsuarioResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);

    Task<UsuarioComSenhaProvisoriaResposta> RedefinirSenhaAsync(int id, CancellationToken ct = default);
}

public sealed class UsuariosApi(HttpClient http) : ApiClientBase(http), IUsuariosApi
{
    public Task<Pagina<UsuarioResposta>> ListarAsync(string? busca, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<UsuarioResposta>>(
            "api/v1/usuarios" + Consulta(("busca", busca), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<IReadOnlyList<UsuarioResposta>> ListarTodosAsync(CancellationToken ct = default) =>
        TodasAsPaginasAsync<UsuarioResposta>("api/v1/usuarios", [], ct);

    public Task<IReadOnlyList<PerfilResposta>> ListarPerfisAsync(CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<PerfilResposta>>("api/v1/perfis", ct);

    public Task<UsuarioResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<UsuarioResposta>($"api/v1/usuarios/{id}", ct);

    public Task<UsuarioComSenhaProvisoriaResposta> CriarAsync(UsuarioRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<UsuarioComSenhaProvisoriaResposta>("api/v1/usuarios", dados, ct);

    public Task<UsuarioResposta> AtualizarAsync(int id, UsuarioRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<UsuarioResposta>($"api/v1/usuarios/{id}", dados, ct);

    public Task<UsuarioResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default) =>
        HttpPatchAsync<UsuarioResposta>($"api/v1/usuarios/{id}/ativo", new { Ativo = ativo }, ct);

    public Task<UsuarioComSenhaProvisoriaResposta> RedefinirSenhaAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<UsuarioComSenhaProvisoriaResposta>($"api/v1/usuarios/{id}/redefinir-senha", new { }, ct);
}
