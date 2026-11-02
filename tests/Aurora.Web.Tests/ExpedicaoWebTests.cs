using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed class ExpedicaoWebTests(CenarioDoPainel cenario)
{
    /// <summary>Pedido Pronto: o estoque de produto acabado atende tudo, então a confirmação já reserva e não gera OP.</summary>
    private async Task<(string Id, string Numero)> PedidoProntoAsync(Navegador vendedor)
    {
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        await produto.DarEntradaDeProdutoAcabadoAsync(cenario.Api, 5);
        var (id, numero) = await produto.PedidoConfirmadoAsync(vendedor, 2);
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        Assert.Contains(">Pronto<", detalhes, StringComparison.Ordinal);
        return (id, numero);
    }

    [Fact]
    public async Task Almoxarife_expede_o_pedido_pronto_e_ele_vai_para_o_historico()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, numero) = await PedidoProntoAsync(vendedor);

        var fila = Navegador.Decodificar(await almoxarife.PaginaAsync("/Expedicao"));
        var detalhes = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        var formulario = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Expedicao/Expedir/{id}"));
        var envio = await almoxarife.EnviarAsync(
            $"/Expedicao/Expedir/{id}", $"/Expedicao/Expedir/{id}",
            [new("Transportadora", "Rápido Sul"), new("CodigoRastreio", "BR123456789")]);
        var historico = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(envio)));
        var depois = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        var filaDepois = Navegador.Decodificar(await almoxarife.PaginaAsync("/Expedicao"));

        Assert.Contains(numero, fila, StringComparison.Ordinal);
        Assert.Contains($"/Expedicao/Expedir/{id}\"", detalhes, StringComparison.Ordinal);
        Assert.Contains("Confirmar expedição", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.StartsWith("/Expedicao/Historico", Navegador.Destino(envio), StringComparison.Ordinal);
        Assert.Contains($"Pedido {numero} expedido.", historico, StringComparison.Ordinal);
        Assert.Contains("Rápido Sul", historico, StringComparison.Ordinal);
        Assert.Contains("BR123456789", historico, StringComparison.Ordinal);
        Assert.Contains(">Expedido<", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("Expedir pedido", depois, StringComparison.Ordinal);
        Assert.DoesNotContain(numero, filaDepois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transportadora_e_rastreio_sao_opcionais()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, numero) = await PedidoProntoAsync(vendedor);

        var envio = await almoxarife.EnviarAsync($"/Expedicao/Expedir/{id}", $"/Expedicao/Expedir/{id}", []);
        var historico = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(envio)));

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains($"Pedido {numero} expedido.", historico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rastreio_comprido_demais_e_apontado_sem_expedir()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, _) = await PedidoProntoAsync(vendedor);

        var envio = await almoxarife.EnviarAsync(
            $"/Expedicao/Expedir/{id}", $"/Expedicao/Expedir/{id}", [new("CodigoRastreio", new string('9', 41))]);

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Contains("O código de rastreio aceita até 40 caracteres.", Navegador.Decodificar(await envio.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Contains(">Pronto<", Navegador.Decodificar(await almoxarife.PaginaAsync($"/Pedidos/Detalhes/{id}")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_que_nao_esta_pronto_nao_abre_o_formulario_de_expedicao()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (id, _) = await produto.PedidoConfirmadoAsync(vendedor, 1);

        var resposta = await almoxarife.GetAsync($"/Expedicao/Expedir/{id}");
        var detalhes = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal($"/Pedidos/Detalhes/{id}", Navegador.Destino(resposta));
        Assert.DoesNotContain("Expedir pedido", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vendedor_ve_a_expedicao_mas_nao_expede_e_o_operador_nem_entra()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var operador = await cenario.EntrarComoAsync("Operador");
        var (id, numero) = await PedidoProntoAsync(vendedor);

        var fila = Navegador.Decodificar(await vendedor.PaginaAsync("/Expedicao"));
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        var formulario = await vendedor.GetAsync($"/Expedicao/Expedir/{id}");
        var doOperador = await operador.GetAsync("/Expedicao");

        Assert.Contains(numero, fila, StringComparison.Ordinal);
        Assert.DoesNotContain("/Expedicao/Expedir/", fila, StringComparison.Ordinal);
        Assert.DoesNotContain("Expedir pedido", detalhes, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(formulario), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(doOperador), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhes_do_pedido_mostram_a_linha_do_tempo()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (id, _) = await produto.PedidoConfirmadoAsync(vendedor, 1);

        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Contains("Linha do tempo", detalhes, StringComparison.Ordinal);
        Assert.Contains("<time", detalhes, StringComparison.Ordinal);
    }
}
