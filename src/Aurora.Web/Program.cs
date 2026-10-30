using Aurora.Web.Extensions;
using Aurora.Web.Filters;
using Aurora.Web.Middlewares;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(opcoes => opcoes.AddServerHeader = false);

builder.Services.AddAuroraAutenticacaoWeb(builder.Configuration);
builder.Services.AddAuroraApi(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddControllersWithViews(opcoes =>
{
    opcoes.Filters.Add<ApiExceptionFilter>();
    opcoes.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

app.UseMiddleware<CabecalhosDeSegurancaMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
