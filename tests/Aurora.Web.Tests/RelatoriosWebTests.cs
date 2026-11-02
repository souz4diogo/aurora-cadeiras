using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed class RelatoriosWebTests(CenarioDoPainel cenario)
{
    [Fact]
    public async Task Gerente_baixa_o_relatorio_de_pedidos_em_pdf_e_em_excel()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        using var pdf = await gerente.GetAsync("/Relatorios/Baixar?tipo=pedidos&formato=pdf");
        using var excel = await gerente.GetAsync("/Relatorios/Baixar?tipo=pedidos&formato=xlsx");
        var bytesDoPdf = await pdf.Content.ReadAsByteArrayAsync();
        var bytesDoExcel = await excel.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytesDoPdf, 0, 4));
        Assert.StartsWith("relatorio-pedidos-", pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        Assert.EndsWith(".pdf", pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
        Assert.Contains("no-store", pdf.Headers.CacheControl?.ToString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Content.Headers.ContentType?.MediaType);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytesDoExcel, 0, 2));
        Assert.EndsWith(".xlsx", excel.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tela_lista_os_relatorios_com_os_filtros_de_cada_um_e_os_dois_formatos()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var pagina = Navegador.Decodificar(await gerente.PaginaAsync("/Relatorios"));

        Assert.Contains("id=\"relatorio-pedidos\"", pagina, StringComparison.Ordinal);
        Assert.Contains("id=\"relatorio-estoque\"", pagina, StringComparison.Ordinal);
        Assert.Contains("id=\"de-pedidos\"", pagina, StringComparison.Ordinal);
        Assert.Contains("id=\"status-pedidos\"", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"de-estoque\"", pagina, StringComparison.Ordinal);
        Assert.Contains("id=\"ano-compras-comunitarias\"", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"pdf\"", pagina, StringComparison.Ordinal);
        Assert.Contains("value=\"xlsx\"", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Periodo_invertido_volta_a_tela_com_o_aviso_da_api()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var resposta = await gerente.GetAsync("/Relatorios/Baixar?tipo=pedidos&formato=pdf&de=2026-12-31&ate=2026-01-01");
        var tela = Navegador.Decodificar(await gerente.PaginaAsync(Navegador.Destino(resposta)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Relatorios", Navegador.Destino(resposta));
        Assert.Contains("A data inicial não pode ser depois da data final.", tela, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vendedor_ve_so_os_relatorios_do_perfil_e_a_api_recusa_os_outros()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var pagina = Navegador.Decodificar(await vendedor.PaginaAsync("/Relatorios"));
        using var proibido = await vendedor.GetAsync("/Relatorios/Baixar?tipo=estoque&formato=pdf");

        Assert.Contains("id=\"relatorio-pedidos\"", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"relatorio-estoque\"", pagina, StringComparison.Ordinal);
        Assert.NotEqual("application/pdf", proibido.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual(HttpStatusCode.OK, proibido.StatusCode);
    }

    [Theory]
    [InlineData("Operador")]
    [InlineData("Inspetor")]
    public async Task Perfis_sem_relatorios_vao_para_acesso_negado(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var lista = await navegador.GetAsync("/Relatorios");
        var download = await navegador.GetAsync("/Relatorios/Baixar?tipo=pedidos&formato=pdf");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(lista), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(download), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sem_login_o_download_pede_autenticacao()
    {
        using var anonimo = cenario.Painel.NovoNavegador();

        var resposta = await anonimo.GetAsync("/Relatorios/Baixar?tipo=pedidos&formato=pdf");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.StartsWith("/Conta/Login", Navegador.Destino(resposta), StringComparison.Ordinal);
    }
}
