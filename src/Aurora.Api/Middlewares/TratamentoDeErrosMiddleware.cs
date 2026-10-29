using System.Text.Json;
using Aurora.Domain.Excecoes;
using Aurora.Infrastructure.Erros;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Api.Middlewares;

/// <summary>
/// Converte exceções de regra, validação e erros das procedures (51001-51010) em ProblemDetails (RFC 7807)
/// com o campo extra "code". Erros inesperados viram 500 sem detalhes técnicos.
/// </summary>
public sealed partial class TratamentoDeErrosMiddleware(RequestDelegate proximo, ILogger<TratamentoDeErrosMiddleware> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> Titulos = new(StringComparer.Ordinal)
    {
        ["RN01"] = "Pedido não está confirmado",
        ["RN04"] = "Saldo insuficiente de matéria-prima",
        ["RN05"] = "Etapa anterior não concluída",
        ["RN06"] = "Inspeção inválida",
        ["RN07"] = "Pedido não está pronto para expedição",
        ["STATUS_INVALIDO"] = "Status atual não permite a operação",
        ["RECEBIMENTO_EXCEDENTE"] = "Recebimento acima da quantidade comprada",
        ["NAO_ENCONTRADO"] = "Registro não encontrado",
        ["MOVIMENTO_IMUTAVEL"] = "Movimento de estoque é imutável",
        ["PARAMETRO_INVALIDO"] = "Parâmetro inválido",
        ["REGISTRO_DUPLICADO"] = "Registro duplicado",
        ["VIOLACAO_DE_INTEGRIDADE"] = "Violação de integridade",
        ["CREDENCIAIS_INVALIDAS"] = "Credenciais inválidas",
        ["CONTA_BLOQUEADA"] = "Conta bloqueada",
        ["REFRESH_INVALIDO"] = "Sessão inválida",
        ["EMAIL_DUPLICADO"] = "E-mail já cadastrado",
        ["USUARIO_NAO_ENCONTRADO"] = "Usuário não encontrado",
        ["CLIENTE_INEXISTENTE"] = "Cliente inexistente",
        ["VALIDACAO"] = "Dados inválidos",
    };

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await proximo(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            contexto.Response.StatusCode = 499;
        }
        catch (ValidationException ex)
        {
            var erros = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await Escrever(contexto, StatusCodes.Status400BadRequest, "VALIDACAO", "Um ou mais campos são inválidos.", erros);
        }
        catch (RegraDeNegocioException ex)
        {
            await Escrever(contexto, Status(ex.Tipo), ex.Codigo, ex.Message);
        }
        catch (Exception ex) when (Traduzir(ex) is { } regra)
        {
            await Escrever(contexto, Status(regra.Tipo), regra.Codigo, regra.Message);
        }
        catch (Exception ex)
        {
            RegistrarErro(ex, contexto.Request.Method, contexto.Request.Path);
            await Escrever(contexto, StatusCodes.Status500InternalServerError, "ERRO_INTERNO", "Ocorreu um erro inesperado.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado em {Metodo} {Caminho}")]
    private partial void RegistrarErro(Exception ex, string metodo, PathString caminho);

    private static RegraDeNegocioException? Traduzir(Exception ex) => ex switch
    {
        SqlException sql => SqlErroTradutor.Traduzir(sql),
        DbUpdateException { InnerException: SqlException sql } => SqlErroTradutor.Traduzir(sql),
        _ => null,
    };

    private static int Status(TipoErro tipo) => tipo switch
    {
        TipoErro.Validacao => StatusCodes.Status400BadRequest,
        TipoErro.NaoAutenticado => StatusCodes.Status401Unauthorized,
        TipoErro.NaoEncontrado => StatusCodes.Status404NotFound,
        TipoErro.EntidadeNaoProcessavel => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status409Conflict,
    };

    private static async Task Escrever(
        HttpContext contexto, int status, string codigo, string detalhe, IDictionary<string, string[]>? erros = null)
    {
        if (contexto.Response.HasStarted)
        {
            return;
        }

        var problema = new ProblemDetails
        {
            Type = $"https://aurora.local/erros/{codigo.ToLowerInvariant().Replace('_', '-')}",
            Title = Titulos.GetValueOrDefault(codigo, detalhe),
            Status = status,
            Detail = detalhe,
        };
        problema.Extensions["code"] = codigo;
        problema.Extensions["traceId"] = contexto.TraceIdentifier;
        if (erros is not null)
        {
            problema.Extensions["errors"] = erros;
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(problema, Json, "application/problem+json");
    }
}

