using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Tests;

public class PedidoCompraTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 2);

    private static PedidoCompra Novo(params ItemDePedidoCompra[] itens) =>
        new(fornecedorId: 3, compradorId: 9, dataPrevista: null, itens.Length == 0 ? [new(1, 10m, 2m)] : itens);

    /// <summary>O banco é quem muda o status e o recebido; nos testes simulamos esse estado.</summary>
    private static PedidoCompra Em(StatusPedidoCompra status, decimal recebido = 0m, DateOnly? dataPrevista = null)
    {
        var pedido = Novo(new ItemDePedidoCompra(1, 10m, 2m));
        typeof(PedidoCompra).GetProperty(nameof(PedidoCompra.Status))!.SetValue(pedido, status);
        typeof(PedidoCompra).GetProperty(nameof(PedidoCompra.DataPrevista))!.SetValue(pedido, dataPrevista);
        typeof(PedidoCompraItem).GetProperty(nameof(PedidoCompraItem.QuantidadeRecebida))!.SetValue(pedido.Itens.Single(), recebido);
        return pedido;
    }

    [Fact]
    public void Nasce_solicitado_com_o_total_dos_itens()
    {
        var pedido = Novo(new(1, 10m, 2.5m), new(2, 4m, 0.125m));

        Assert.Equal(StatusPedidoCompra.Solicitado, pedido.Status);
        Assert.Equal((10m * 2.5m) + (4m * 0.125m), pedido.ValorTotal);
        Assert.Equal(2, pedido.Itens.Count);
        Assert.All(pedido.Itens, i => Assert.Equal(i.Quantidade, i.Saldo));
    }

    [Fact]
    public void Sem_itens_e_recusado()
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() => new PedidoCompra(1, 1, null, []));

        Assert.Equal("PEDIDO_COMPRA_SEM_ITENS", erro.Codigo);
        Assert.Equal(TipoErro.Validacao, erro.Tipo);
    }

    [Fact]
    public void A_mesma_materia_prima_duas_vezes_e_recusada()
    {
        var erro = Assert.Throws<RegraDeNegocioException>(() => Novo(new(1, 1m, 1m), new(1, 2m, 1m)));

        Assert.Equal("MATERIA_PRIMA_REPETIDA", erro.Codigo);
    }

    [Theory]
    [InlineData(StatusPedidoCompra.Solicitado, true, true, false)]
    [InlineData(StatusPedidoCompra.Emitido, false, true, true)]
    [InlineData(StatusPedidoCompra.ParcialmenteRecebido, false, false, true)] // já recebeu algo: o material está no estoque
    [InlineData(StatusPedidoCompra.Recebido, false, false, false)]
    [InlineData(StatusPedidoCompra.Cancelado, false, false, false)]
    public void O_que_pode_ser_feito_depende_da_situacao(StatusPedidoCompra status, bool emitir, bool cancelar, bool receber)
    {
        var recebido = status is StatusPedidoCompra.ParcialmenteRecebido ? 3m : status is StatusPedidoCompra.Recebido ? 10m : 0m;
        var pedido = Em(status, recebido);

        Assert.Equal(emitir, pedido.PodeEmitir);
        Assert.Equal(cancelar, pedido.PodeCancelar);
        Assert.Equal(receber, pedido.PodeReceber);
    }

    [Fact]
    public void Emitido_com_algo_ja_recebido_nao_cancela()
    {
        // Estado que só existe numa corrida: a regra vale mesmo assim
        Assert.False(Em(StatusPedidoCompra.Emitido, recebido: 1m).PodeCancelar);
    }

    [Theory]
    [InlineData(StatusPedidoCompra.Emitido, true)]
    [InlineData(StatusPedidoCompra.ParcialmenteRecebido, true)]
    [InlineData(StatusPedidoCompra.Solicitado, false)] // ainda não foi ao fornecedor: não há o que cobrar
    [InlineData(StatusPedidoCompra.Recebido, false)]
    [InlineData(StatusPedidoCompra.Cancelado, false)]
    public void So_esta_atrasado_o_que_o_fornecedor_ainda_deve_entregar(StatusPedidoCompra status, bool atrasado)
    {
        var pedido = Em(status, dataPrevista: Hoje.AddDays(-1));

        Assert.Equal(atrasado, pedido.EstaAtrasado(Hoje));
    }

    [Fact]
    public void Sem_data_prevista_ou_no_prazo_nao_esta_atrasado()
    {
        Assert.False(Em(StatusPedidoCompra.Emitido, dataPrevista: null).EstaAtrasado(Hoje));
        Assert.False(Em(StatusPedidoCompra.Emitido, dataPrevista: Hoje).EstaAtrasado(Hoje));
    }
}
