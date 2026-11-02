using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed class AuditoriaWebTests(CenarioDoPainel cenario)
{
    [Fact]
    public async Task Administrador_ve_a_inclusao_da_materia_prima_com_os_campos_e_o_autor()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var material = await ProdutoDeProducao.CriarAsync(cenario.Api);

        var pagina = Navegador.Decodificar(await admin.PaginaAsync($"/Auditoria?tabela=MateriaPrima&registro={material.MateriaPrimaId}"));

        Assert.Contains("Auditoria", pagina, StringComparison.Ordinal);
        Assert.Contains(">Inclusão<", pagina, StringComparison.Ordinal);
        Assert.Contains("<details>", pagina, StringComparison.Ordinal);
        Assert.Contains($"<th scope=\"row\">Codigo</th>", pagina, StringComparison.Ordinal);
        Assert.Contains(material.MateriaPrimaCodigo, pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("Fora da API", pagina, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"visually-hidden\">Alterações registradas", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filtro_sem_resultado_mostra_a_mensagem_e_o_filtro_continua_preenchido()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");

        var pagina = Navegador.Decodificar(await admin.PaginaAsync("/Auditoria?tabela=Pedido&registro=999999999"));

        Assert.Contains("Nenhuma alteração encontrada para esses filtros.", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"999999999\"", pagina, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Pedido\" selected=\"selected\">", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Periodo_invertido_volta_ao_filtro_limpo_com_o_aviso_da_api()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");

        var resposta = await admin.GetAsync("/Auditoria?de=2026-12-31&ate=2026-01-01");
        var tela = Navegador.Decodificar(await admin.PaginaAsync(Navegador.Destino(resposta)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Auditoria", Navegador.Destino(resposta));
        Assert.Contains("A data inicial não pode ser depois da data final.", tela, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GerenteProducao")]
    [InlineData("Almoxarife")]
    [InlineData("Vendedor")]
    [InlineData("Operador")]
    [InlineData("Inspetor")]
    public async Task Demais_perfis_vao_para_acesso_negado_e_nao_veem_o_menu(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var resposta = await navegador.GetAsync("/Auditoria");
        var inicio = await navegador.PaginaAsync("/");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(resposta), StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Auditoria\"", inicio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_do_administrador_oferece_a_auditoria()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");

        var inicio = await admin.PaginaAsync("/");

        Assert.Contains("href=\"/Auditoria\"", inicio, StringComparison.Ordinal);
    }
}
