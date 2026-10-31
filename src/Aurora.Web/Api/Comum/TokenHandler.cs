using System.Net;
using System.Net.Http.Headers;
using Aurora.Web.Api.Autenticacao;
using Aurora.Web.Seguranca;

namespace Aurora.Web.Api.Comum;

/// <summary>
/// Anexa o Bearer do usuário logado a cada chamada à API e renova o token: antes de expirar e, se mesmo assim a
/// API responder 401, uma vez (repetindo a requisição). Se a renovação falhar, lança <see cref="SessaoExpiradaException"/>.
/// </summary>
public sealed class TokenHandler(IHttpContextAccessor acessor, IAuthApi auth, TimeProvider relogio) : DelegatingHandler
{
    private static readonly TimeSpan Antecedencia = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var contexto = acessor.HttpContext
            ?? throw new InvalidOperationException("Chamada à API fora de uma requisição web.");
        var tokens = await SessaoDeTokens.LerAsync(contexto) ?? throw new SessaoExpiradaException();

        if (tokens.ExpiraEm - relogio.GetUtcNow() <= Antecedencia)
        {
            tokens = await RenovarAsync(contexto, tokens, cancellationToken);
        }

        // O conteúdo só pode ser lido uma vez; guarda uma cópia para poder repetir a requisição após renovar o token
        var copia = await ClonarAsync(request, cancellationToken);
        var resposta = await EnviarAsync(request, tokens.AccessToken, cancellationToken);
        if (resposta.StatusCode != HttpStatusCode.Unauthorized)
        {
            return resposta;
        }

        resposta.Dispose();
        tokens = await RenovarAsync(contexto, tokens, cancellationToken);
        return await EnviarAsync(copia, tokens.AccessToken, cancellationToken);
    }

    private async Task<TokensDaSessao> RenovarAsync(HttpContext contexto, TokensDaSessao atuais, CancellationToken ct)
    {
        TokenResposta renovado;
        try
        {
            renovado = await auth.RenovarAsync(atuais.RefreshToken, ct);
        }
        catch (ApiException)
        {
            throw new SessaoExpiradaException();
        }

        await SessaoDeTokens.GravarAsync(contexto, SessaoDeTokens.CriarUsuario(renovado.Usuario), renovado, relogio.GetUtcNow());
        return (await SessaoDeTokens.LerAsync(contexto))!;
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage requisicao, string accessToken, CancellationToken ct)
    {
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return base.SendAsync(requisicao, ct);
    }

    private static async Task<HttpRequestMessage> ClonarAsync(HttpRequestMessage original, CancellationToken ct)
    {
        var copia = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var cabecalho in original.Headers)
        {
            copia.Headers.TryAddWithoutValidation(cabecalho.Key, cabecalho.Value);
        }

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync(ct);
            copia.Content = new ByteArrayContent(bytes);
            foreach (var cabecalho in original.Content.Headers)
            {
                copia.Content.Headers.TryAddWithoutValidation(cabecalho.Key, cabecalho.Value);
            }
        }

        return copia;
    }
}
