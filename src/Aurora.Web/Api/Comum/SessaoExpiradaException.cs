namespace Aurora.Web.Api.Comum;

/// <summary>A API recusou o token e a renovação não foi possível: o usuário precisa entrar de novo.</summary>
public sealed class SessaoExpiradaException : Exception
{
    public SessaoExpiradaException()
        : base("Sua sessão expirou. Entre novamente.")
    {
    }
}
