using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.MateriasPrimas;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) do estoque: consulta para Administrador, Gerente e Almoxarife; ajuste só do Almoxarife.</summary>
[Collection("api")]
public class PermissoesEstoqueTests(ApiFactory api)
{
    private static readonly string[] Todos =
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor", "Cliente"];

    public static TheoryData<string, bool> Consulta => Matriz(["Administrador", "GerenteProducao", "Almoxarife"]);

    public static TheoryData<string, bool> Ajuste => Matriz(["Almoxarife"]);

    private static TheoryData<string, bool> Matriz(string[] permitidos)
    {
        var dados = new TheoryData<string, bool>();
        foreach (var perfil in Todos)
        {
            dados.Add(perfil, permitidos.Contains(perfil));
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(Consulta))]
    public async Task Consulta_de_movimentos(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.GetAsync("/api/v1/estoque/movimentos");

        if (permitido)
        {
            resposta.AssertStatus(HttpStatusCode.OK);
            return;
        }

        resposta.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await resposta.CodigoAsync());
    }

    [Theory]
    [MemberData(nameof(Ajuste))]
    public async Task Ajuste_de_estoque(string perfil, bool permitido)
    {
        using var admin = await api.ComoAdministradorAsync();
        var materia = (await admin.GetFromJsonAsync<Pagina<MateriaPrimaDto>>("/api/v1/materias-primas?busca=MP-001"))!.Items.Single(m => m.Codigo == "MP-001");
        using var cliente = await api.ComoAsync(perfil);

        // Reajusta para o saldo que já existe: o teste não muda os dados do seed
        var resposta = await cliente.PostAsJsonAsync(
            "/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materia.Id, materia.EstoqueAtual, "Conferência de permissão"));

        if (permitido)
        {
            resposta.AssertStatus(HttpStatusCode.OK);
            return;
        }

        resposta.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("/api/v1/estoque/movimentos")]
    public async Task Rotas_sem_token_devolvem_401(string rota)
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync(rota);

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }
}
