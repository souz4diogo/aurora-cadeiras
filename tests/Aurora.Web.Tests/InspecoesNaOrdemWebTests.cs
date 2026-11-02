using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed class InspecoesNaOrdemWebTests(CenarioDoPainel cenario)
{
    private async Task<string> OrdemDeUmPedidoNovoAsync()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, 1);
        return await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);
    }

    [Theory]
    [InlineData("GerenteProducao")]
    [InlineData("Administrador")]
    [InlineData("Inspetor")]
    public async Task Perfis_de_qualidade_veem_a_secao_de_inspecoes_da_ordem(string perfil)
    {
        var ordemId = await OrdemDeUmPedidoNovoAsync();
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var pagina = Navegador.Decodificar(await navegador.PaginaAsync($"/Producao/Detalhes/{ordemId}"));

        Assert.Contains("Inspeções de qualidade", pagina, StringComparison.Ordinal);
        Assert.Contains("Nenhuma inspeção registrada.", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vendedor_nao_abre_a_ordem_de_pedido_de_outro_vendedor()
    {
        var ordemId = await OrdemDeUmPedidoNovoAsync();
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        // O vendedor só enxerga OPs dos próprios pedidos; esta é de outro vendedor, então a API responde 404
        var resposta = await vendedor.GetAsync($"/Producao/Detalhes/{ordemId}");

        Assert.NotEqual(System.Net.HttpStatusCode.OK, resposta.StatusCode);
    }
}
