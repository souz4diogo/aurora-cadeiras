using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Tests;

public class PedidoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 30);

    private static Pedido NovoPedido(params ItemDePedido[] itens) =>
        new(clienteId: 3, vendedorId: 9, prazoEntrega: Hoje.AddDays(10), observacao: null, itens);

    private static void ForcarStatus(Pedido pedido, StatusPedido status) =>
        typeof(Pedido).GetProperty(nameof(Pedido.Status))!.SetValue(pedido, status);

    [Fact]
    public void Pedido_novo_nasce_aberto_com_o_valor_total_dos_itens()
    {
        var pedido = NovoPedido(new ItemDePedido(1, 2, 100m), new ItemDePedido(2, 3, 50.5m));

        Assert.Equal(StatusPedido.Aberto, pedido.Status);
        Assert.Equal(2 * 100m + 3 * 50.5m, pedido.ValorTotal);
        Assert.Equal(2, pedido.Itens.Count);
    }

    [Fact]
    public void Pedido_sem_itens_e_recusado()
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() => NovoPedido());

        Assert.Equal("PEDIDO_SEM_ITENS", erro.Codigo);
        Assert.Equal(TipoErro.Validacao, erro.Tipo);
    }

    [Fact]
    public void Substituir_itens_mantem_o_mesmo_produto_remove_o_ausente_e_inclui_o_novo()
    {
        var pedido = NovoPedido(new ItemDePedido(1, 2, 100m), new ItemDePedido(2, 1, 40m));
        var itemDoProduto1 = pedido.Itens.Single(i => i.ProdutoId == 1);

        pedido.SubstituirItens([new ItemDePedido(1, 5, 90m), new ItemDePedido(3, 1, 10m)]);

        Assert.Equal([1, 3], pedido.Itens.Select(i => i.ProdutoId).Order());
        Assert.Same(itemDoProduto1, pedido.Itens.Single(i => i.ProdutoId == 1));
        Assert.Equal(5, itemDoProduto1.Quantidade);
        Assert.Equal(90m, itemDoProduto1.PrecoUnitario);
        Assert.Equal(5 * 90m + 10m, pedido.ValorTotal);
    }

    [Fact]
    public void Substituir_itens_por_lista_vazia_e_recusado_e_preserva_os_itens()
    {
        var pedido = NovoPedido(new ItemDePedido(1, 2, 100m));

        Assert.Throws<RegraDeNegocioException>(() => pedido.SubstituirItens([]));

        Assert.Single(pedido.Itens);
        Assert.Equal(200m, pedido.ValorTotal);
    }

    [Theory]
    [InlineData(StatusPedido.Confirmado)]
    [InlineData(StatusPedido.EmProducao)]
    [InlineData(StatusPedido.Pronto)]
    [InlineData(StatusPedido.Expedido)]
    [InlineData(StatusPedido.Cancelado)]
    public void So_e_possivel_editar_enquanto_aberto(StatusPedido status)
    {
        var pedido = NovoPedido(new ItemDePedido(1, 1, 10m));
        ForcarStatus(pedido, status);

        var dados = Assert.Throws<RegraDeNegocioException>(() => pedido.AtualizarDados(4, Hoje, null));
        var itens = Assert.Throws<RegraDeNegocioException>(() => pedido.SubstituirItens([new ItemDePedido(2, 1, 10m)]));

        Assert.Equal("STATUS_INVALIDO", dados.Codigo);
        Assert.Equal("STATUS_INVALIDO", itens.Codigo);
        Assert.Equal(TipoErro.Conflito, dados.Tipo);
    }

    [Fact]
    public void Atualizar_dados_altera_cliente_prazo_e_observacao()
    {
        var pedido = NovoPedido(new ItemDePedido(1, 1, 10m));

        pedido.AtualizarDados(4, Hoje.AddDays(20), "Entrega parcelada");

        Assert.Equal(4, pedido.ClienteId);
        Assert.Equal(Hoje.AddDays(20), pedido.PrazoEntrega);
        Assert.Equal("Entrega parcelada", pedido.Observacao);
    }

    [Theory]
    [InlineData(StatusPedido.Aberto, 5, false)]
    [InlineData(StatusPedido.Confirmado, 5, true)]
    [InlineData(StatusPedido.EmProducao, 5, true)]
    [InlineData(StatusPedido.Pronto, 5, true)]
    [InlineData(StatusPedido.Confirmado, -5, false)]
    [InlineData(StatusPedido.Expedido, 5, false)]
    [InlineData(StatusPedido.Cancelado, 5, false)]
    public void Atraso_so_vale_para_pedido_em_andamento_com_prazo_vencido(StatusPedido status, int diasDeVencimento, bool esperado)
    {
        var pedido = new Pedido(1, 1, Hoje.AddDays(-diasDeVencimento), null, [new ItemDePedido(1, 1, 10m)]);
        ForcarStatus(pedido, status);

        Assert.Equal(esperado, pedido.EstaAtrasado(Hoje));
    }
}
