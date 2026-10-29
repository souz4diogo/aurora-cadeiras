using Aurora.Api.Extensions;
using Aurora.Api.Middlewares;
using Aurora.Api.Seguranca;
using Aurora.Application;
using Aurora.Application.Abstracoes;
using Aurora.Infrastructure;
using Aurora.Infrastructure.Persistencia;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(opcoes => opcoes.AddServerHeader = false);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAuroraApplication();
builder.Services.AddAuroraInfrastructure(builder.Configuration);
builder.Services.AddAuroraAuth();
builder.Services.AddAuroraRateLimit();
builder.Services.AddAuroraSwagger();
builder.Services.AddAuroraHealth();
builder.Services.AddControllers();
builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
{
    var problema = contexto.ProblemDetails;
    problema.Extensions["traceId"] = contexto.HttpContext.TraceIdentifier;
    problema.Extensions.TryAdd("code", problema.Status switch
    {
        StatusCodes.Status401Unauthorized => "NAO_AUTENTICADO",
        StatusCodes.Status403Forbidden => "SEM_PERMISSAO",
        StatusCodes.Status404NotFound => "NAO_ENCONTRADO",
        StatusCodes.Status429TooManyRequests => "LIMITE_EXCEDIDO",
        _ => "ERRO",
    });
});

builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IConfiguration>((opcoes, configuracao) => opcoes.AddDefaultPolicy(politica =>
    {
        var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];
        if (origens.Length > 0)
        {
            politica.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod();
        }
    }));

var app = builder.Build();

app.UseMiddleware<TratamentoDeErrosMiddleware>();
app.UseMiddleware<CabecalhosDeSegurancaMiddleware>();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.MapSwagger().AllowAnonymous();
    app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "Aurora Cadeiras API v1"));
}

app.MapControllers();
app.MapAuroraHealth();

try
{
    await app.Services.SemearAdministradorAsync();
}
catch (Exception ex)
{
    LogMensagens.FalhaAoSemearAdministrador(app.Logger, ex);
}

app.Run();

public partial class Program;


