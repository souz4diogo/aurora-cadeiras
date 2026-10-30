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
builder.Services.AddAuroraProblemDetails();
builder.Services.AddAuroraCors();
builder.Services.AddAuroraForwardedHeaders();

var app = builder.Build();

app.UseForwardedHeaders();
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

app.UseAuroraSwagger();
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


