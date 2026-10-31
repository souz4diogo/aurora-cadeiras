using System.Globalization;
using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;
using Aurora.Infrastructure.Seguranca;

namespace Aurora.Api.Seguranca;

public sealed class HttpCurrentUser(IHttpContextAccessor acessor) : ICurrentUser
{
    public int? UsuarioId
    {
        get
        {
            var sub = acessor.HttpContext?.User.FindFirst(NomesDeClaims.UsuarioId)?.Value;
            return int.TryParse(sub, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public PerfilUsuario? Perfil
    {
        get
        {
            var perfil = acessor.HttpContext?.User.FindFirst(NomesDeClaims.Perfil)?.Value;
            return Enum.TryParse<PerfilUsuario>(perfil, out var valor) && Enum.IsDefined(valor) ? valor : null;
        }
    }
}
