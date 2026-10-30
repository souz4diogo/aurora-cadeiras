using System.Net;

namespace Aurora.Web.Api.Comum;

/// <summary>Resposta de erro da API (ProblemDetails com o campo extra "code") convertida em exceção.</summary>
public sealed class ApiException(
    HttpStatusCode status,
    string codigo,
    string titulo,
    string detalhe,
    IReadOnlyDictionary<string, string[]> erros) : Exception(detalhe)
{
    public HttpStatusCode Status { get; } = status;

    /// <summary>Código estável da regra (ex.: RN04, CODIGO_DUPLICADO, VALIDACAO).</summary>
    public string Codigo { get; } = codigo;

    public string Titulo { get; } = titulo;

    public string Detalhe { get; } = detalhe;

    /// <summary>Erros de validação por campo (nome do campo na API, em PascalCase).</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; } = erros;

    /// <summary>
    /// Falha que o usuário corrige na própria tela (campo inválido, regra violada, duplicidade): a tela volta ao
    /// formulário com a mensagem. As demais viram páginas de erro.
    /// </summary>
    public bool EhFalhaDeNegocio => Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity;

    public bool EhNaoEncontrado => Status == HttpStatusCode.NotFound;

    public bool EhSemPermissao => Status == HttpStatusCode.Forbidden;
}
