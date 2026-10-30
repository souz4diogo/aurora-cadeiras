using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Usuarios;

public class UsuarioService(
    IUsuarioRepository usuarios,
    IRefreshTokenRepository refreshTokens,
    ISenhaHasher hasher,
    IGeradorDeSenha geradorDeSenha,
    IRelogio relogio,
    IUnitOfWork unitOfWork,
    IValidator<CriarUsuarioRequest> validadorCriar,
    IValidator<AtualizarUsuarioRequest> validadorAtualizar)
{
    public const int TamanhoMaximoDePagina = 100;

    public static IReadOnlyList<PerfilDto> ListarPerfis() =>
        Enum.GetValues<PerfilUsuario>().Select(p => new PerfilDto((byte)p, p.ToString())).ToList();

    public async Task<Pagina<UsuarioDto>> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct = default)
    {
        pagina = Math.Max(pagina, 1);
        tamanho = Math.Clamp(tamanho, 1, TamanhoMaximoDePagina);
        var (itens, total) = await usuarios.ListarAsync(busca?.Trim(), pagina, tamanho, ct);
        return new Pagina<UsuarioDto>(itens.Select(UsuarioDto.De).ToList(), total, pagina, tamanho);
    }

    public async Task<UsuarioDto> ObterAsync(int id, CancellationToken ct = default) =>
        UsuarioDto.De(await ObterOuFalharAsync(id, ct));

    public async Task<UsuarioComSenhaProvisoriaDto> CriarAsync(CriarUsuarioRequest request, CancellationToken ct = default)
    {
        await validadorCriar.ValidateAndThrowAsync(request, ct);
        var email = Normalizar(request.Email);

        await GarantirEmailDisponivelAsync(email, null, ct);
        await GarantirClienteAsync(request.PerfilId, request.ClienteId, ct);

        var senha = geradorDeSenha.GerarProvisoria();
        var usuario = new Usuario(request.Nome.Trim(), email, hasher.Hash(senha), (PerfilUsuario)request.PerfilId, request.ClienteId);
        usuarios.Adicionar(usuario);
        await unitOfWork.SalvarAsync(ct);

        return new UsuarioComSenhaProvisoriaDto(UsuarioDto.De(usuario), senha);
    }

    public async Task<UsuarioDto> AtualizarAsync(int id, AtualizarUsuarioRequest request, CancellationToken ct = default)
    {
        await validadorAtualizar.ValidateAndThrowAsync(request, ct);
        var usuario = await ObterOuFalharAsync(id, ct);
        var email = Normalizar(request.Email);

        await GarantirEmailDisponivelAsync(email, id, ct);
        await GarantirClienteAsync(request.PerfilId, request.ClienteId, ct);

        usuario.Atualizar(request.Nome.Trim(), email, (PerfilUsuario)request.PerfilId, request.ClienteId);
        await unitOfWork.SalvarAsync(ct);
        return UsuarioDto.De(usuario);
    }

    public async Task<UsuarioDto> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        var usuario = await ObterOuFalharAsync(id, ct);
        usuario.DefinirAtivo(ativo);

        if (!ativo)
        {
            await refreshTokens.RevogarTodosDoUsuarioAsync(id, relogio.AgoraUtc, ct);
        }

        await unitOfWork.SalvarAsync(ct);
        return UsuarioDto.De(usuario);
    }

    public async Task<UsuarioComSenhaProvisoriaDto> RedefinirSenhaAsync(int id, CancellationToken ct = default)
    {
        var usuario = await ObterOuFalharAsync(id, ct);
        var senha = geradorDeSenha.GerarProvisoria();
        usuario.TrocarSenha(hasher.Hash(senha));
        await refreshTokens.RevogarTodosDoUsuarioAsync(id, relogio.AgoraUtc, ct);
        await unitOfWork.SalvarAsync(ct);
        return new UsuarioComSenhaProvisoriaDto(UsuarioDto.De(usuario), senha);
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();

    private async Task<Usuario> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await usuarios.ObterPorIdAsync(id, ct)
        ?? throw new RegraDeNegocioException("USUARIO_NAO_ENCONTRADO", "Usuário não encontrado.", TipoErro.NaoEncontrado);

    private async Task GarantirEmailDisponivelAsync(string email, int? ignorarId, CancellationToken ct)
    {
        if (await usuarios.EmailExisteAsync(email, ignorarId, ct))
        {
            throw new RegraDeNegocioException("EMAIL_DUPLICADO", "Já existe um usuário com este e-mail.");
        }
    }

    private async Task GarantirClienteAsync(byte perfilId, int? clienteId, CancellationToken ct)
    {
        if (perfilId == (byte)PerfilUsuario.Cliente
            && clienteId is not null
            && !await usuarios.ClienteExisteAsync(clienteId.Value, ct))
        {
            throw new RegraDeNegocioException("CLIENTE_INEXISTENTE", "O cliente informado não existe.", TipoErro.EntidadeNaoProcessavel);
        }
    }
}
