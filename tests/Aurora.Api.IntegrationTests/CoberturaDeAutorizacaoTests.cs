using System.Reflection;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Api.IntegrationTests;

/// <summary>
/// RNF02: controle de acesso por perfil em TODAS as rotas. A política de fallback já exige login, mas só estes testes
/// impedem que uma rota nova nasça sem dizer quem pode usá-la, ou com o nome de um perfil digitado errado (o que
/// negaria o acesso a todos, em silêncio).
/// </summary>
[Collection("api")]
public class CoberturaDeAutorizacaoTests(ApiFactory api)
{
    /// <summary>Únicas ações anônimas: entrar e renovar a sessão (docs/06 §4.1).</summary>
    private static readonly HashSet<string> AnonimasPermitidas = ["Auth.Login", "Auth.Refresh"];

    /// <summary>Exigem só estar logado, sem perfil específico: dados da própria sessão.</summary>
    private static readonly HashSet<string> ApenasLogadas = ["Auth.Logout", "Auth.Me"];

    private IEnumerable<(string Nome, MethodInfo Metodo, Type Controller)> Acoes() =>
        api.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Select(a => (
                Nome: $"{a.ControllerName}.{a.ActionName}",
                Metodo: a.MethodInfo,
                Controller: a.ControllerTypeInfo.AsType()));

    private static IEnumerable<AuthorizeAttribute> Autorizacoes(MethodInfo metodo, Type controller) =>
        metodo.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    private static bool Anonima(MethodInfo metodo, Type controller) =>
        metodo.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
        || controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    [Fact]
    public void A_api_tem_acoes_para_verificar()
    {
        Assert.True(Acoes().Count() > 50, "O teste só vale se enxergar as ações da API.");
    }

    [Fact]
    public void Toda_acao_declara_os_perfis_que_podem_usa_la_ou_esta_em_uma_lista_curta_de_excecoes()
    {
        var semControle = new List<string>();
        foreach (var (nome, metodo, controller) in Acoes())
        {
            if (Anonima(metodo, controller))
            {
                if (!AnonimasPermitidas.Contains(nome))
                {
                    semControle.Add($"{nome}: anônima sem estar na lista de exceções");
                }

                continue;
            }

            if (ApenasLogadas.Contains(nome))
            {
                continue;
            }

            if (!Autorizacoes(metodo, controller).Any(a => !string.IsNullOrWhiteSpace(a.Roles)))
            {
                semControle.Add($"{nome}: sem [Authorize(Roles = ...)]");
            }
        }

        Assert.True(semControle.Count == 0, "Ações sem controle de perfil:\n" + string.Join("\n", semControle));
    }

    [Fact]
    public void Todo_perfil_citado_nas_regras_existe()
    {
        var conhecidos = Enum.GetNames<PerfilUsuario>().ToHashSet(StringComparer.Ordinal);
        var invalidos = new List<string>();
        foreach (var (nome, metodo, controller) in Acoes())
        {
            foreach (var regra in Autorizacoes(metodo, controller).Select(a => a.Roles).Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                foreach (var perfil in regra!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!conhecidos.Contains(perfil))
                    {
                        invalidos.Add($"{nome}: perfil desconhecido \"{perfil}\"");
                    }
                }
            }
        }

        Assert.True(invalidos.Count == 0, string.Join("\n", invalidos));
    }

    [Fact]
    public void As_listas_de_excecao_nao_ficam_desatualizadas()
    {
        var nomes = Acoes().Select(a => a.Nome).ToHashSet();

        Assert.All(AnonimasPermitidas.Concat(ApenasLogadas), excecao => Assert.Contains(excecao, nomes));
    }
}
