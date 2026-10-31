using System.Globalization;
using Aurora.Application.Abstracoes;
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
}
