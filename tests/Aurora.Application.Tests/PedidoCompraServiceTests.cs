using Aurora.Application.Abstracoes;
using Aurora.Application.Compras;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class PedidoCompraServiceTests
{
    private static readonly DateTime Agora = new(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPedidoCompraRepository> _compras = new();
    private readonly Mock<IFornecedorRepository> _fornecedores = new();
    private readonly Mock<IMateriaPrimaRepository> _materias = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public PedidoCompraServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _fornecedores.Setup(r => r.ObterPorIdAsync(3, default))
            .ReturnsAsync(new Fornecedor("Madeireira Tupi", "12345678000190", null, null, true));
        _materias.Setup(r => r.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.Select(id => Materia(id, custo: 7.5m)).ToList());
    }

    private PedidoCompraService Servico() => new(
        _compras.Object, _fornecedores.Object, _materias.Object, _usuarios.Object, _uow.Object, _usuarioAtual.Object,
        _relogio.Object, new PedidoCompraRequestValidator(_relogio.Object), new ReceberItemRequestValidator());

    private static MateriaPrima Materia(int id, decimal custo = 5m, bool ativa = true)
    {
        var materia = new MateriaPrima("MAT-" + id, "Material " + id, UnidadeDeMedida.MetroQuadrado, 1m, custo, null);
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(materia, id);
        materia.DefinirAtivo(ativa);
        return materia;
    }

    private static PedidoCompra PedidoNoBanco(StatusPedidoCompra status = StatusPedidoCompra.Solicitado, decimal recebido = 0m)
    {
        var pedido = new PedidoCompra(3, 9, null, [new ItemDePedidoCompra(1, 10m, 2m)]);
        typeof(PedidoCompra).GetProperty(nameof(PedidoCompra.Id))!.SetValue(pedido, 55);
        typeof(PedidoCompra).GetProperty(nameof(PedidoCompra.Numero))!.SetValue(pedido, "PC-2026-00055");
        typeof(PedidoCompra).GetProperty(nameof(PedidoCompra.Status))!.SetValue(pedido, status);
        typeof(PedidoCompraItem).GetProperty(nameof(PedidoCompraItem.QuantidadeRecebida))!.SetValue(pedido.Itens.Single(), recebido);
        return pedido;
    }

    private void PedidoExiste(PedidoCompra pedido) => _compras.Setup(r => r.ObterAsync(pedido.Id, default)).ReturnsAsync(pedido);

    [Fact]
    public async Task Criar_usa_o_usuario_logado_como_comprador_e_o_custo_atual_quando_o_item_nao_traz_custo()
    {
        PedidoCompra? gravado = null;
        _compras.Setup(r => r.Adicionar(It.IsAny<PedidoCompra>())).Callback<PedidoCompra>(p => gravado = p);

        var resposta = await Servico().CriarAsync(new PedidoCompraRequest(3, null, [new(1, 4m, null), new(2, 1m, 3.25m)]));

        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
        Assert.Equal(9, gravado!.CompradorId);
        Assert.Equal(7.5m, gravado.Itens.Single(i => i.MateriaPrimaId == 1).CustoUnitario); // custo atual do material
        Assert.Equal(3.25m, gravado.Itens.Single(i => i.MateriaPrimaId == 2).CustoUnitario); // o informado vence
        Assert.Equal("Solicitado", resposta.Status);
        Assert.True(resposta.ComunidadeTradicional);
        Assert.Equal((4m * 7.5m) + 3.25m, resposta.ValorTotal);
    }

    [Fact]
    public async Task Criar_sem_usuario_autenticado_devolve_nao_autenticado()
    {
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns((int?)null);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoCompraRequest(3, null, [new(1, 1m, 1m)])));

        Assert.Equal(TipoErro.NaoAutenticado, erro.Tipo);
        _compras.Verify(r => r.Adicionar(It.IsAny<PedidoCompra>()), Times.Never);
    }

    [Fact]
    public async Task Criar_com_fornecedor_inexistente_ou_inativo_devolve_422_sem_gravar()
    {
        var inativo = new Fornecedor("Antigo", "98765432000100", null, null, false);
        inativo.DefinirAtivo(false);
        _fornecedores.Setup(r => r.ObterPorIdAsync(4, default)).ReturnsAsync(inativo);
        _fornecedores.Setup(r => r.ObterPorIdAsync(5, default)).ReturnsAsync((Fornecedor?)null);

        var deInativo = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoCompraRequest(4, null, [new(1, 1m, 1m)])));
        var deInexistente = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoCompraRequest(5, null, [new(1, 1m, 1m)])));

        Assert.Equal("FORNECEDOR_INATIVO", deInativo.Codigo);
        Assert.Equal("FORNECEDOR_INEXISTENTE", deInexistente.Codigo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Criar_com_materia_prima_inativa_ou_inexistente_devolve_422()
    {
        _materias.Setup(r => r.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync([Materia(1, ativa: false)]);

        var inativa = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoCompraRequest(3, null, [new(1, 1m, 1m)])));
        var inexistente = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new PedidoCompraRequest(3, null, [new(99, 1m, 1m)])));

        Assert.Equal("MATERIA_PRIMA_INATIVA", inativa.Codigo);
        Assert.Equal("MATERIA_PRIMA_INEXISTENTE", inexistente.Codigo);
    }

    [Fact]
    public async Task Pedido_invalido_nem_consulta_o_banco()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().CriarAsync(new PedidoCompraRequest(3, null, [])));

        _fornecedores.VerifyNoOtherCalls();
        _compras.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Emitir_pede_a_atualizacao_atomica_e_devolve_o_pedido_atualizado()
    {
        var antes = PedidoNoBanco(StatusPedidoCompra.Solicitado);
        var depois = PedidoNoBanco(StatusPedidoCompra.Emitido);
        _compras.SetupSequence(r => r.ObterAsync(55, default)).ReturnsAsync(antes).ReturnsAsync(depois);
        _compras.Setup(r => r.TentarEmitirAsync(55, default)).ReturnsAsync(true);

        var resposta = await Servico().EmitirAsync(55);

        Assert.Equal("Emitido", resposta.Status);
        _compras.Verify(r => r.TentarEmitirAsync(55, default), Times.Once);
    }

    [Fact]
    public async Task Emitir_pedido_que_nao_esta_solicitado_devolve_409_sem_tentar()
    {
        PedidoExiste(PedidoNoBanco(StatusPedidoCompra.Emitido));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().EmitirAsync(55));

        Assert.Equal("STATUS_INVALIDO", erro.Codigo);
        _compras.Verify(r => r.TentarEmitirAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Perder_a_corrida_ao_cancelar_devolve_409_mesmo_com_a_leitura_dizendo_que_podia()
    {
        PedidoExiste(PedidoNoBanco(StatusPedidoCompra.Emitido));
        _compras.Setup(r => r.TentarCancelarAsync(55, default)).ReturnsAsync(false); // um recebimento chegou primeiro

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().CancelarAsync(55));

        Assert.Equal("STATUS_INVALIDO", erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
    }

    [Fact]
    public async Task Cancelar_depois_de_receber_algo_devolve_409_sem_tentar()
    {
        PedidoExiste(PedidoNoBanco(StatusPedidoCompra.ParcialmenteRecebido, recebido: 3m));

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().CancelarAsync(55));

        _compras.Verify(r => r.TentarCancelarAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Pedido_inexistente_devolve_404_em_todas_as_operacoes()
    {
        foreach (var operacao in new Func<Task>[]
                 {
                     () => Servico().ObterAsync(1),
                     () => Servico().EmitirAsync(1),
                     () => Servico().CancelarAsync(1),
                 })
        {
            var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(operacao);
            Assert.Equal("PEDIDO_COMPRA_NAO_ENCONTRADO", erro.Codigo);
            Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        }
    }

    [Fact]
    public async Task Receber_chama_a_procedure_e_devolve_o_pedido_como_ficou()
    {
        _compras.Setup(r => r.ObterPedidoDoItemAsync(8, default)).ReturnsAsync(55);
        PedidoExiste(PedidoNoBanco(StatusPedidoCompra.ParcialmenteRecebido, recebido: 4.125m));

        var resposta = await Servico().ReceberItemAsync(8, new ReceberItemRequest(4.125m));

        _compras.Verify(r => r.ReceberItemAsync(8, 4.125m, default), Times.Once);
        Assert.Equal("ParcialmenteRecebido", resposta.Status);
        Assert.Equal(4.125m, resposta.Itens.Single().QuantidadeRecebida);
    }

    [Fact]
    public async Task Receber_item_inexistente_devolve_404_sem_chamar_a_procedure()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ReceberItemAsync(8, new ReceberItemRequest(1m)));

        Assert.Equal("ITEM_DE_COMPRA_NAO_ENCONTRADO", erro.Codigo);
        _compras.Verify(r => r.ReceberItemAsync(It.IsAny<int>(), It.IsAny<decimal>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Receber_quantidade_invalida_nem_chega_ao_banco(double quantidade)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().ReceberItemAsync(8, new ReceberItemRequest((decimal)quantidade)));

        _compras.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Listar_normaliza_a_paginacao_e_calcula_o_atraso_pela_data_de_hoje()
    {
        FiltroDePedidosCompra? usado = null;
        var resumo = new PedidoCompraResumo(
            1, "PC-2026-00001", 3, "Madeireira", StatusPedidoCompra.Emitido, new DateOnly(2026, 11, 1), Agora, 150m, 2);
        _compras.Setup(r => r.ListarAsync(It.IsAny<FiltroDePedidosCompra>(), 1, 100, default))
            .Callback<FiltroDePedidosCompra, int, int, CancellationToken>((f, _, _, _) => usado = f)
            .ReturnsAsync(([resumo], 1));

        var pagina = await Servico().ListarAsync(StatusPedidoCompra.Emitido, 3, null, null, null, pagina: 0, tamanho: 9999);

        Assert.Equal(new DateOnly(2026, 11, 2), usado!.Hoje);
        Assert.Equal(StatusPedidoCompra.Emitido, usado.Status);
        Assert.Equal(1, pagina.Page);
        Assert.Equal(100, pagina.PageSize);
        Assert.True(Assert.Single(pagina.Items).Atrasado);
    }

    [Theory]
    [InlineData(0.0005, false)] // 4 casas
    [InlineData(0.001, true)]
    [InlineData(999999999.999, true)]
    [InlineData(1000000000, false)] // estouraria decimal(12,3)
    public void Quantidade_do_item_respeita_a_coluna_decimal_12_3(double quantidade, bool valida)
    {
        var resultado = new PedidoCompraItemRequestValidator().Validate(new PedidoCompraItemRequest(1, (decimal)quantidade, 1m));

        Assert.Equal(valida, resultado.IsValid);
    }
}
