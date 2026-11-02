using System.Net;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed partial class PainelWebTests(CenarioDoPainel cenario)
{
    [GeneratedRegex(@"Pedidos em aberto</p>\s*<p class=""display-6 mb-1"">(\d+)</p>")]
    private static partial Regex CartaoDePedidosEmAberto();

    private static int PedidosEmAberto(string html) =>
        int.Parse(CartaoDePedidosEmAberto().Match(html).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("GerenteProducao")]
    [InlineData("Administrador")]
    public async Task Gerencia_ve_cartoes_graficos_e_tabelas_acessiveis(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var pagina = Navegador.Decodificar(await navegador.PaginaAsync("/Painel"));

        Assert.Contains("Painel gerencial", pagina, StringComparison.Ordinal);
        foreach (var cartao in new[] { "Pedidos em aberto", "Pedidos atrasados", "Prontos para expedir", "OPs em andamento", "Materiais críticos", "Compras a receber" })
        {
            Assert.Contains(cartao, pagina, StringComparison.Ordinal);
        }

        Assert.Contains("<svg class=\"grafico\"", pagina, StringComparison.Ordinal);
        Assert.Contains("<title id=\"grafico-titulo\">Ordens de produção por estação</title>", pagina, StringComparison.Ordinal);
        Assert.Contains("<caption class=\"visually-hidden\">Produção por estação</caption>", pagina, StringComparison.Ordinal);
        Assert.Contains("Pedidos atrasados", pagina, StringComparison.Ordinal);
        foreach (var estacao in new[] { "Corte", "Qualidade" })
        {
            Assert.Contains($"<th scope=\"row\">{estacao}</th>", pagina, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Pagina_nao_traz_script_nem_estilo_embutido_por_causa_da_politica_de_conteudo()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var pagina = await gerente.PaginaAsync("/Painel");

        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", pagina);
        Assert.DoesNotMatch(@"<[^>]+\sstyle=", pagina);
    }

    [Fact]
    public async Task Cartao_de_pedidos_em_aberto_cresce_quando_um_pedido_e_confirmado()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var antes = PedidosEmAberto(await gerente.PaginaAsync("/Painel"));
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);

        await produto.PedidoConfirmadoAsync(vendedor, 1);

        Assert.Equal(antes + 1, PedidosEmAberto(await gerente.PaginaAsync("/Painel")));
    }

    [Fact]
    public async Task Periodo_do_consumo_aceita_so_as_janelas_conhecidas()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var sete = await gerente.PaginaAsync("/Painel?dias=7");
        var invalido = await gerente.PaginaAsync("/Painel?dias=999");

        Assert.Matches(@"<option value=""7"" selected=""selected"">", sete);
        Assert.Matches(@"<option value=""30"" selected=""selected"">", invalido);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Almoxarife")]
    [InlineData("Operador")]
    [InlineData("Inspetor")]
    public async Task Demais_perfis_vao_para_acesso_negado(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var resposta = await navegador.GetAsync("/Painel");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(resposta), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_oferece_o_painel_so_para_a_gerencia()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var doGerente = await gerente.PaginaAsync("/");
        var doAlmoxarife = await almoxarife.PaginaAsync("/");

        Assert.Contains("href=\"/Painel\"", doGerente, StringComparison.Ordinal);
        Assert.Contains("href=\"/Relatorios\"", doGerente, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Painel\"", doAlmoxarife, StringComparison.Ordinal);
        Assert.Contains("href=\"/Compras\"", doAlmoxarife, StringComparison.Ordinal);
        Assert.Contains("href=\"/Expedicao\"", doAlmoxarife, StringComparison.Ordinal);
    }
}
