namespace Aurora.Database.Tests;

[Collection("banco")]
public class EstoqueETriggersTests(BancoFixture banco)
{
    private Db Db => banco.Db;

    private Cenario NovoCenario() => new(Db, banco.Usuarios);

    [Fact]
    public async Task MovimentoEstoque_nao_pode_ser_alterado_nem_apagado()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 10);
        var movimento = await Db.InteiroAsync(
            "SELECT CAST(MIN(MovimentoEstoqueId) AS int) FROM dbo.MovimentoEstoque WHERE MateriaPrimaId = @m", null, ("@m", material));

        var erroUpdate = await Db.ErroAsync(
            "UPDATE dbo.MovimentoEstoque SET Quantidade = 999 WHERE MovimentoEstoqueId = @i", null, ("@i", movimento));
        var erroDelete = await Db.ErroAsync(
            "DELETE dbo.MovimentoEstoque WHERE MovimentoEstoqueId = @i", null, ("@i", movimento));

        Assert.Equal(51009, erroUpdate);
        Assert.Equal(51009, erroDelete);
        Assert.Equal(10m, await c.SaldoAsync(material));
    }

    [Fact]
    public async Task Saldo_de_materia_prima_nunca_fica_negativo()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 3);

        var erro = await Db.ErroAsync(
            "INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem) VALUES (@m, 'Saida', -10, 'Producao')",
            null, ("@m", material));

        Assert.Equal(547, erro);
        Assert.Equal(3m, await c.SaldoAsync(material));
    }

    [Fact]
    public async Task Ajuste_cria_movimento_com_a_diferenca_e_exige_motivo()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 10);

        await Db.ExecutarAsync("EXEC dbo.sp_AjustarEstoque @m, 7, N'Contagem física'", banco.Usuarios.Almoxarife, ("@m", material));
        var erroSemMotivo = await Db.ErroAsync("EXEC dbo.sp_AjustarEstoque @m, 5, N'  '", banco.Usuarios.Almoxarife, ("@m", material));

        Assert.Equal(7m, await c.SaldoAsync(material));
        Assert.Equal(-3m, await Db.DecimalAsync(
            "SELECT Quantidade FROM dbo.MovimentoEstoque WHERE MateriaPrimaId = @m AND Motivo = N'Contagem física'", null, ("@m", material)));
        Assert.Equal(51010, erroSemMotivo);
    }

    [Fact]
    public async Task Estoque_no_minimo_gera_um_unico_alerta_aberto_RN08()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 10, minimo: 5);
        Assert.Equal(0, await AlertasAbertosAsync(material));

        await Db.ExecutarAsync("EXEC dbo.sp_AjustarEstoque @m, 5, N'Consumo'", banco.Usuarios.Almoxarife, ("@m", material));
        await Db.ExecutarAsync("EXEC dbo.sp_AjustarEstoque @m, 3, N'Consumo'", banco.Usuarios.Almoxarife, ("@m", material));

        Assert.Equal(1, await AlertasAbertosAsync(material));
        Assert.Equal(1, await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.vw_EstoqueCritico WHERE MateriaPrimaId = @m", null, ("@m", material)));
    }

    [Fact]
    public async Task Iniciar_etapa_que_cruza_o_minimo_dispara_alerta()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 10, minimo: 5);
        var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 1m));
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 6));

        await c.IniciarAsync(await c.EtapaAsync(op, Cenario.Corte));

        Assert.Equal(1, await AlertasAbertosAsync(material));
    }

    [Fact]
    public async Task Recebimento_de_compra_da_entrada_no_estoque_e_atualiza_o_status()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 2);
        var (compra, item) = await CriarCompraEmitidaAsync(material, quantidade: 10, custo: 5m);

        await Db.ExecutarAsync("EXEC dbo.sp_ReceberItemCompra @i, 4", banco.Usuarios.Almoxarife, ("@i", item));
        Assert.Equal(6m, await c.SaldoAsync(material));
        Assert.Equal("ParcialmenteRecebido", await StatusCompraAsync(compra));

        await Db.ExecutarAsync("EXEC dbo.sp_ReceberItemCompra @i, 6", banco.Usuarios.Almoxarife, ("@i", item));
        Assert.Equal(12m, await c.SaldoAsync(material));
        Assert.Equal("Recebido", await StatusCompraAsync(compra));
        Assert.Equal(5m, await Db.DecimalAsync("SELECT CustoUnitario FROM dbo.MateriaPrima WHERE MateriaPrimaId = @m", null, ("@m", material)));
    }

    [Fact]
    public async Task Recebimento_acima_da_quantidade_comprada_falha_51007()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 0);
        var (_, item) = await CriarCompraEmitidaAsync(material, quantidade: 10, custo: 5m);

        var erro = await Db.ErroAsync("EXEC dbo.sp_ReceberItemCompra @i, 11", banco.Usuarios.Almoxarife, ("@i", item));

        Assert.Equal(51007, erro);
        Assert.Equal(0m, await c.SaldoAsync(material));
    }

    [Fact]
    public async Task Auditoria_registra_alteracoes_do_pedido_com_o_usuario_da_sessao()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1);

        var linha = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = 'Pedido' AND Registro = CAST(@p AS varchar(50)) " +
            "AND Acao = 'U' AND UsuarioId = @u AND Antes LIKE '%\"Status\":\"Aberto\"%' AND Depois LIKE '%\"Status\":\"Confirmado\"%'",
            null,
            ("@p", pedido), ("@u", banco.Usuarios.Vendedor));

        Assert.Equal(1, linha);
    }

    [Fact]
    public async Task Auditoria_de_usuario_nunca_grava_a_senha()
    {
        await Db.ExecutarAsync(
            "INSERT dbo.Usuario (Nome, Email, SenhaHash, PerfilId) VALUES (N'Senha Teste', 'senha.teste@teste.local', 'HASH-SECRETO-123', 1)");

        var vazou = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = 'Usuario' AND (Depois LIKE '%HASH-SECRETO-123%' OR Depois LIKE '%SenhaHash%')");
        var registrou = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = 'Usuario' AND Depois LIKE '%senha.teste@teste.local%'");

        Assert.Equal(0, vazou);
        Assert.Equal(1, registrou);
    }

    [Fact]
    public async Task Views_de_acompanhamento_e_painel_refletem_a_producao()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1);
        var op = await c.OpDoPedidoAsync(pedido);
        await c.IniciarAsync(await c.EtapaAsync(op, Cenario.Corte));

        var eventos = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.vw_AcompanhamentoPedido WHERE PedidoId = @p", null, ("@p", pedido));
        var noPainel = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.vw_PainelProducao WHERE OrdemProducaoId = @o AND StatusEtapa = 'EmAndamento'", null, ("@o", op));

        Assert.True(eventos >= 2);
        Assert.Equal(1, noPainel);
    }

    private Task<int> AlertasAbertosAsync(int material) =>
        Db.InteiroAsync("SELECT COUNT(*) FROM dbo.AlertaEstoque WHERE MateriaPrimaId = @m AND LidoEm IS NULL", null, ("@m", material));

    private Task<string?> StatusCompraAsync(int compra) =>
        Db.TextoAsync("SELECT Status FROM dbo.PedidoCompra WHERE PedidoCompraId = @c", null, ("@c", compra));

    private async Task<(int Compra, int Item)> CriarCompraEmitidaAsync(int material, decimal quantidade, decimal custo)
    {
        var compra = await Db.InteiroAsync(
            "INSERT dbo.PedidoCompra (FornecedorId, CompradorId, Status) " +
            "SELECT TOP (1) FornecedorId, @u, 'Emitido' FROM dbo.Fornecedor; SELECT CAST(SCOPE_IDENTITY() AS int);",
            banco.Usuarios.Almoxarife,
            ("@u", banco.Usuarios.Almoxarife));
        var item = await Db.InteiroAsync(
            "INSERT dbo.PedidoCompraItem (PedidoCompraId, MateriaPrimaId, Quantidade, CustoUnitario) VALUES (@c, @m, @q, @custo); " +
            "SELECT CAST(SCOPE_IDENTITY() AS int);",
            banco.Usuarios.Almoxarife,
            ("@c", compra), ("@m", material), ("@q", quantidade), ("@custo", custo));
        return (compra, item);
    }
}
