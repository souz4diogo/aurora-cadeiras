using Aurora.Application.Abstracoes;

namespace Aurora.Api.Seguranca;

public sealed class HttpCurrentUser(IHttpContextAccessor acessor) : ICurrentUser
{
    public int? UsuarioId
    {
        get
        {
            var sub = acessor.HttpContext?.User.FindFirst("sub")?.Value;
            return int.TryParse(sub, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }
}
