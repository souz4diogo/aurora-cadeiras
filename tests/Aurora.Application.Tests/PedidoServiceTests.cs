using Aurora.Application.Abstracoes;
using Aurora.Application.Pedidos;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class PedidoServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Prazo = new(2026, 12, 15);

    private readonly Mock<IPedidoRepository> _pedidos = new();
    private readonly Mock<IClienteRepository> _clientes = new();
    private readonly Mock<IProdutoRepository> _produtos = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public PedidoServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Vendedor);
        _clientes.Setup(r => r.ObterPorIdAsync(3, default)).ReturnsAsync(new Cliente("Móveis Lima", "12345678901", null, null, null, null));
    }

    private PedidoService Servico() => new(
        _pedidos.Object, _clientes.Object, _produtos.Object, _uow.Object, _usuarioAtual.Object, _relogio.Object,
        new PedidoRequestValidator(_relogio.Object));

    private static Produto Produto(int id, string codigo, decimal preco, bool ativo = true)
    {
        var produto = new Produto(codigo, "Cadeira " + codigo, LinhaProduto.Escritorio, preco);
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(produto, id);
        produto.DefinirAtivo(ativo);
        return produto;
    }

    private void CatalogoCom(params Produto[] produtos) =>
        _produtos.Setup(r => r.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), default)).ReturnsAsync(produtos);

    private static PedidoRequest Request(params PedidoItemRequest[] itens) => new(3, Prazo, " Entrega parcelada ", itens);

    private static Pedido PedidoDoVendedor(int vendedorId, params ItemDePedido[] itens) =>
        new(3, vendedorId, Prazo, null, itens);

    [Fact]
    public async Task Criar_usa_o_preco_atual_do_produto_o_vendedor_logado_e_calcula_o_total()
    {
        CatalogoCom(Produto(1, "CE-100", 1290m), Produto(2, "CJ-100", 480m));

        var resposta = await Servico().CriarAsync(Request(new PedidoItemRequest(1, 2), new PedidoItemRequest(2, 3)));

        Assert.Equal(2 * 1290m + 3 * 480m, resposta.ValorTotal);
        Assert.Equal("Aberto", resposta.Status);
        Assert.Equal(9, resposta.VendedorId);
        Assert.Equal("Móveis Lima", resposta.ClienteNome);
        Assert.Equal("Entrega parcelada", resposta.Observacao);
        _pedidos.Verify(r => r.Adicionar(It.Is<Pedido>(p => p.VendedorId == 9 && p.Itens.Count == 2)), Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Criar_sem_usuario_autenticado_devolve_nao_autenticado()
    {
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns((int?)null);
        CatalogoCom(Produto(1, "CE-100", 100m));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().CriarAsync(Request(new PedidoItemRequest(1, 1))));

        Assert.Equal(TipoErro.NaoAutenticado, erro.Tipo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task Criar_com_quantidade_fora_do_limite_lanca_validacao(int quantidade)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().CriarAsync(Request(new PedidoItemRequest(1, quantidade))));
    }

    [Fact]
    public async Task Criar_sem_itens_ou_com_produto_repetido_lanca_validacao()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().CriarAsync(Request()));
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(Request(new PedidoItemRequest(1, 1), new PedidoItemRequest(1, 2))));
    }

    [Fact]
    public async Task Criar_com_prazo_no_passado_lanca_validacao_mas_aceita_um_dia_de_folga_de_fuso()
    {
        CatalogoCom(Produto(1, "CE-100", 100m));
        var ontem = DateOnly.FromDateTime(Agora).AddDays(-1);
        var anteontem = ontem.AddDays(-1);

        await Servico().CriarAsync(new PedidoRequest(3, ontem, null, [new PedidoItemRequest(1, 1)]));
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new PedidoRequest(3, anteontem, null, [new PedidoItemRequest(1, 1)])));
    }

    [Fact]
    public async Task Criar_com_cliente_inexistente_ou_inativo_devolve_422()
    {
        CatalogoCom(Produto(1, "CE-100", 100m));
        var inativo = new Cliente("Inativo", "12345678901", null, null, null, null);
        inativo.DefinirAtivo(false);
        _clientes.Setup(r => r.ObterPorIdAsync(4, default)).ReturnsAsync(inativo);

        var inexistente = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoRequest(99, Prazo, null, [new PedidoItemRequest(1, 1)])));
        var desativado = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoRequest(4, Prazo, null, [new PedidoItemRequest(1, 1)])));

        Assert.Equal("CLIENTE_INEXISTENTE", inexistente.Codigo);
        Assert.Equal("CLIENTE_INATIVO", desativado.Codigo);
        Assert.Equal(TipoErro.EntidadeNaoProcessavel, desativado.Tipo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Criar_com_produto_inexistente_ou_inativo_devolve_422()
    {
        CatalogoCom(Produto(1, "CE-100", 100m), Produto(2, "CE-200", 100m, ativo: false));

        var inexistente = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(Request(new PedidoItemRequest(1, 1), new PedidoItemRequest(77, 1))));
        var inativo = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(Request(new PedidoItemRequest(2, 1))));

        Assert.Equal("PRODUTO_INEXISTENTE", inexistente.Codigo);
        Assert.Equal("PRODUTO_INATIVO", inativo.Codigo);
    }

    [Fact]
    public async Task Atualizar_mantem_o_preco_do_item_que_ja_estava_e_usa_o_preco_atual_no_item_novo()
    {
        // O produto 1 subiu de 100 para 150 depois que o pedido foi criado
        CatalogoCom(Produto(1, "CE-100", 150m), Produto(2, "CJ-100", 480m));
        var pedido = PedidoDoVendedor(9, new ItemDePedido(1, 2, 100m));
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(pedido);

        var resposta = await Servico().AtualizarAsync(5, Request(new PedidoItemRequest(1, 4), new PedidoItemRequest(2, 1)));

        Assert.Equal(100m, resposta.Itens.Single(i => i.ProdutoId == 1).PrecoUnitario);
        Assert.Equal(480m, resposta.Itens.Single(i => i.ProdutoId == 2).PrecoUnitario);
        Assert.Equal(4 * 100m + 480m, resposta.ValorTotal);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Atualizar_item_que_ja_estava_no_pedido_vale_mesmo_com_o_produto_inativo()
    {
        CatalogoCom(Produto(1, "CE-100", 150m, ativo: false));
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(PedidoDoVendedor(9, new ItemDePedido(1, 2, 100m)));

        var resposta = await Servico().AtualizarAsync(5, Request(new PedidoItemRequest(1, 3)));

        Assert.Equal(300m, resposta.ValorTotal);
    }

    [Fact]
    public async Task Vendedor_nao_enxerga_nem_altera_pedido_de_outro_vendedor()
    {
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(PedidoDoVendedor(77, new ItemDePedido(1, 1, 10m)));
        CatalogoCom(Produto(1, "CE-100", 10m));

        var leitura = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(5));
        var edicao = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().AtualizarAsync(5, Request(new PedidoItemRequest(1, 1))));
        var cancelamento = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().CancelarAsync(5));

        Assert.All([leitura, edicao, cancelamento], e =>
        {
            Assert.Equal("PEDIDO_NAO_ENCONTRADO", e.Codigo);
            Assert.Equal(TipoErro.NaoEncontrado, e.Tipo);
        });
        _pedidos.Verify(r => r.CancelarAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Gerente_enxerga_pedido_de_qualquer_vendedor()
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.GerenteProducao);
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(PedidoDoVendedor(77, new ItemDePedido(1, 1, 10m)));
        CatalogoCom(Produto(1, "CE-100", 10m));

        var resposta = await Servico().ObterAsync(5);

        Assert.Equal(77, resposta.VendedorId);
    }

    [Fact]
    public async Task Listar_restringe_o_vendedor_aos_proprios_pedidos_e_o_gerente_nao()
    {
        FiltroDePedidos? filtroUsado = null;
        _pedidos.Setup(r => r.ListarAsync(It.IsAny<FiltroDePedidos>(), It.IsAny<int>(), It.IsAny<int>(), default))
            .Callback<FiltroDePedidos, int, int, CancellationToken>((f, _, _, _) => filtroUsado = f)
            .ReturnsAsync(([], 0));

        await Servico().ListarAsync(StatusPedido.Aberto, null, null, null, null, 1, 20);
        var comoVendedor = filtroUsado;
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.GerenteProducao);
        await Servico().ListarAsync(null, null, null, null, null, 1, 20);

        Assert.Equal(9, comoVendedor!.VendedorId);
        Assert.Equal(StatusPedido.Aberto, comoVendedor.Status);
        Assert.Null(filtroUsado!.VendedorId);
        Assert.Equal(new DateOnly(2026, 10, 30), filtroUsado.Hoje);
    }

    [Fact]
    public async Task Listar_marca_como_atrasado_so_o_pedido_em_andamento_com_prazo_vencido()
    {
        var vencido = new DateOnly(2026, 10, 1);
        PedidoResumo Resumo(int id, StatusPedido status, DateOnly prazo) =>
            new(id, $"PV-2026-{id:00000}", 3, "Cliente", 9, Agora, prazo, status, 10m, 1);
        _pedidos.Setup(r => r.ListarAsync(It.IsAny<FiltroDePedidos>(), 1, 20, default)).ReturnsAsync((
            [Resumo(1, StatusPedido.Confirmado, vencido), Resumo(2, StatusPedido.Aberto, vencido), Resumo(3, StatusPedido.Pronto, Prazo)], 3));

        var pagina = await Servico().ListarAsync(null, null, null, null, null, 1, 20);

        Assert.Equal([true, false, false], pagina.Items.Select(i => i.Atrasado));
        Assert.Equal("Confirmado", pagina.Items[0].Status);
    }

    [Fact]
    public async Task Confirmar_delega_a_procedure_e_devolve_o_pedido()
    {
        CatalogoCom(Produto(1, "CE-100", 10m));
        var pedido = PedidoDoVendedor(9, new ItemDePedido(1, 1, 10m));
        typeof(Pedido).GetProperty(nameof(Pedido.Id))!.SetValue(pedido, 5);
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(pedido);

        var resposta = await Servico().ConfirmarAsync(5);

        _pedidos.Verify(r => r.ConfirmarAsync(5, default), Times.Once);
        Assert.Equal(5, resposta.Id);
    }

    [Fact]
    public async Task Vendedor_nao_confirma_pedido_de_outro_vendedor()
    {
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(PedidoDoVendedor(77, new ItemDePedido(1, 1, 10m)));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ConfirmarAsync(5));

        Assert.Equal("PEDIDO_NAO_ENCONTRADO", erro.Codigo);
        _pedidos.Verify(r => r.ConfirmarAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Cancelar_delega_ao_repositorio_o_cancelamento_pela_procedure()
    {
        CatalogoCom(Produto(1, "CE-100", 10m));
        var pedido = PedidoDoVendedor(9, new ItemDePedido(1, 1, 10m));
        typeof(Pedido).GetProperty(nameof(Pedido.Id))!.SetValue(pedido, 5);
        _pedidos.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync(pedido);

        var resposta = await Servico().CancelarAsync(5);

        _pedidos.Verify(r => r.CancelarAsync(5, default), Times.Once);
        Assert.Equal(5, resposta.Id);
    }
}
