using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Auditoria;
using Aurora.Application.Comum;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Usuarios;

namespace Aurora.Api.IntegrationTests;

/// <summary>Histórico de alterações (RNF10): os triggers gravam e a rota <c>/auditoria</c> mostra quem mudou o quê.</summary>
[Collection("api")]
public sealed class AuditoriaTests(ApiFactory api)
{
    private static async Task<Pagina<AuditoriaDto>> ConsultarAsync(HttpClient admin, string consulta)
    {
        var resposta = await admin.GetAsync("/api/v1/auditoria" + consulta);
        resposta.AssertStatus(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<Pagina<AuditoriaDto>>())!;
    }

    private static async Task<MateriaPrimaDto> NovaMateriaAsync(HttpClient cliente)
    {
        var codigo = "AU" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var resposta = await cliente.PostAsJsonAsync(
            "/api/v1/materias-primas", new MateriaPrimaRequest(codigo, "Material " + codigo, "m2", 5m, 10m, null));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
    }

    [Fact]
    public async Task Alterar_uma_materia_prima_mostra_quem_mudou_e_o_antes_e_depois()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var admin = await api.ComoAdministradorAsync();
        var materia = await NovaMateriaAsync(almoxarife);

        (await almoxarife.PutAsJsonAsync(
                $"/api/v1/materias-primas/{materia.Id}",
                new MateriaPrimaRequest(materia.Codigo, materia.Nome, "m2", 8m, 10m, null)))
            .AssertStatus(HttpStatusCode.OK);
        var historico = await ConsultarAsync(admin, $"?tabela=MateriaPrima&registro={materia.Id}");

        Assert.Equal(2, historico.Total);
        var alteracao = historico.Items[0];
        var inclusao = historico.Items[1];
        Assert.Equal("Alteração", alteracao.Acao);
        Assert.Equal("Inclusão", inclusao.Acao);
        Assert.NotNull(alteracao.UsuarioNome);
        Assert.Equal(alteracao.UsuarioId, inclusao.UsuarioId);
        var campo = Assert.Single(alteracao.Alteracoes, c => c.Campo == "EstoqueMinimo");
        Assert.Equal("5", campo.Antes?.TrimEnd('0').TrimEnd('.'));
        Assert.Equal("8", campo.Depois?.TrimEnd('0').TrimEnd('.'));
        Assert.DoesNotContain(alteracao.Alteracoes, c => c.Campo == "Nome");
        Assert.All(inclusao.Alteracoes, c => Assert.Null(c.Antes));
    }

    [Fact]
    public async Task Auditoria_de_usuario_nunca_traz_senha_nem_token()
    {
        using var admin = await api.ComoAdministradorAsync();
        var (usuario, _) = await api.CriarUsuarioAsync("Vendedor");

        var historico = await ConsultarAsync(admin, $"?tabela=Usuario&registro={usuario.Id}");

        var inclusao = Assert.Single(historico.Items);
        Assert.Contains(inclusao.Alteracoes, c => c.Campo == "Email" && c.Depois == usuario.Email);
        Assert.DoesNotContain(inclusao.Alteracoes, c => c.Campo.Contains("Senha", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inclusao.Alteracoes, c => c.Campo.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filtra_por_usuario_e_por_periodo()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var admin = await api.ComoAdministradorAsync();
        var materia = await NovaMateriaAsync(almoxarife);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var autor = (await ConsultarAsync(admin, $"?tabela=MateriaPrima&registro={materia.Id}")).Items.Single().UsuarioId;

        var doAutor = await ConsultarAsync(admin, $"?tabela=MateriaPrima&registro={materia.Id}&usuarioId={autor}");
        var deOutro = await ConsultarAsync(admin, $"?tabela=MateriaPrima&registro={materia.Id}&usuarioId=987654");
        var hojeNoPeriodo = await ConsultarAsync(admin, $"?registro={materia.Id}&tabela=MateriaPrima&de={hoje:yyyy-MM-dd}&ate={hoje:yyyy-MM-dd}");
        var ontem = await ConsultarAsync(admin, $"?registro={materia.Id}&tabela=MateriaPrima&ate={hoje.AddDays(-1):yyyy-MM-dd}");

        Assert.Single(doAutor.Items);
        Assert.Empty(deOutro.Items);
        Assert.Single(hojeNoPeriodo.Items);
        Assert.Empty(ontem.Items);
    }

    [Fact]
    public async Task Paginacao_respeita_o_tamanho_e_a_ordem_do_mais_recente()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var admin = await api.ComoAdministradorAsync();
        await NovaMateriaAsync(almoxarife);
        await NovaMateriaAsync(almoxarife);

        var primeira = await ConsultarAsync(admin, "?tabela=MateriaPrima&page=1&pageSize=1");
        var segunda = await ConsultarAsync(admin, "?tabela=MateriaPrima&page=2&pageSize=1");

        Assert.Single(primeira.Items);
        Assert.True(primeira.Total >= 2);
        Assert.True(primeira.Items[0].Id > segunda.Items[0].Id);
    }

    [Theory]
    [InlineData("?tabela=Auditoria")]
    [InlineData("?tabela=Usuario%3B%20DROP%20TABLE%20Usuario")]
    [InlineData("?de=2026-02-01&ate=2026-01-01")]
    public async Task Filtro_invalido_responde_400(string consulta)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/auditoria" + consulta);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("FILTRO_INVALIDO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Lista_as_tabelas_auditadas()
    {
        using var admin = await api.ComoAdministradorAsync();

        var tabelas = await admin.GetFromJsonAsync<string[]>("/api/v1/auditoria/tabelas");

        Assert.Equal(["MateriaPrima", "OrdemProducao", "Pedido", "Usuario"], tabelas!);
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("GerenteProducao", false)]
    [InlineData("Operador", false)]
    [InlineData("Vendedor", false)]
    [InlineData("Almoxarife", false)]
    [InlineData("Inspetor", false)]
    [InlineData("Cliente", false)]
    public async Task So_o_administrador_consulta_a_auditoria(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var historico = await cliente.GetAsync("/api/v1/auditoria");
        var tabelas = await cliente.GetAsync("/api/v1/auditoria/tabelas");

        if (permitido)
        {
            historico.AssertStatus(HttpStatusCode.OK);
            tabelas.AssertStatus(HttpStatusCode.OK);
            return;
        }

        historico.AssertStatus(HttpStatusCode.Forbidden);
        tabelas.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await historico.CodigoAsync());
    }

    [Fact]
    public async Task Sem_login_devolve_401()
    {
        using var anonimo = api.CreateClient();

        (await anonimo.GetAsync("/api/v1/auditoria")).AssertStatus(HttpStatusCode.Unauthorized);
        (await anonimo.GetAsync("/api/v1/auditoria/tabelas")).AssertStatus(HttpStatusCode.Unauthorized);
    }
}
