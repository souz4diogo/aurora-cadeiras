namespace Aurora.Database.Tests;

[Collection("banco")]
public class ProducaoTests(BancoFixture banco)
{
    private Db Db => banco.Db;

    private Cenario NovoCenario() => new(Db, banco.Usuarios);

    [Fact]
    public async Task Confirmar_pedido_gera_op_com_as_etapas_do_roteiro()
    {
        var c = NovoCenario();
        var produto = await c.CriarProdutoAsync();

        var pedido = await c.CriarPedidoAsync(produto, 3);

        var op = await c.OpDoPedidoAsync(pedido);
        Assert.Equal("EmProducao", await c.StatusPedidoAsync(pedido));
        Assert.Equal(5, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.OrdemProducaoEtapa WHERE OrdemProducaoId = @o", null, ("@o", op)));
        Assert.Equal(3, await Db.InteiroAsync("SELECT Quantidade FROM dbo.OrdemProducao WHERE OrdemProducaoId = @o", null, ("@o", op)));
    }

    [Fact]
    public async Task Gerar_ordens_de_pedido_nao_confirmado_falha_RN01_51003()
    {
        var c = NovoCenario();
        var produto = await c.CriarProdutoAsync();
        var pedido = await c.CriarPedidoAsync(produto, 1, confirmar: false);

        var erro = await Db.ErroAsync("EXEC dbo.sp_GerarOrdensProducao @p", banco.Usuarios.Vendedor, ("@p", pedido));

        Assert.Equal(51003, erro);
    }

    [Fact]
    public async Task Confirmar_pedido_ja_confirmado_falha_51005()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1);

        var erro = await Db.ErroAsync("EXEC dbo.sp_ConfirmarPedido @p", banco.Usuarios.Vendedor, ("@p", pedido));

        Assert.Equal(51005, erro);
    }

    [Fact]
    public async Task Produto_acabado_disponivel_e_reservado_em_vez_de_gerar_op_RN02()
    {
        var c = NovoCenario();
        var produto = await c.CriarProdutoAsync();
        await Db.ExecutarAsync("INSERT dbo.EstoqueProduto (ProdutoId, Quantidade, Reservado) VALUES (@p, 10, 0)", null, ("@p", produto));

        var pedido = await c.CriarPedidoAsync(produto, 4);

        Assert.Equal("Pronto", await c.StatusPedidoAsync(pedido));
        Assert.Equal(0, await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.OrdemProducao WHERE ProdutoId = @p", null, ("@p", produto)));
        Assert.Equal(4, await Db.InteiroAsync("SELECT Reservado FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
    }

    [Fact]
    public async Task Estoque_parcial_reserva_o_disponivel_e_produz_o_restante_RN02()
    {
        var c = NovoCenario();
        var produto = await c.CriarProdutoAsync();
        await Db.ExecutarAsync("INSERT dbo.EstoqueProduto (ProdutoId, Quantidade, Reservado) VALUES (@p, 10, 0)", null, ("@p", produto));

        var pedido = await c.CriarPedidoAsync(produto, 15);

        var op = await c.OpDoPedidoAsync(pedido);
        Assert.Equal("EmProducao", await c.StatusPedidoAsync(pedido));
        Assert.Equal(5, await Db.InteiroAsync("SELECT Quantidade FROM dbo.OrdemProducao WHERE OrdemProducaoId = @o", null, ("@o", op)));
        Assert.Equal(10, await Db.InteiroAsync("SELECT Reservado FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
    }

    [Fact]
    public async Task Iniciar_etapa_baixa_a_materia_prima_conforme_a_ficha_tecnica_RN03()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 100);
        var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 2m));
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 5));

        await c.IniciarAsync(await c.EtapaAsync(op, Cenario.Corte));

        Assert.Equal(90m, await c.SaldoAsync(material));
        var movimento = await Db.DecimalAsync(
            "SELECT Quantidade FROM dbo.MovimentoEstoque WHERE MateriaPrimaId = @m AND Origem = 'Producao'", null, ("@m", material));
        Assert.Equal(-10m, movimento);
    }

    [Fact]
    public async Task Sem_saldo_suficiente_a_etapa_nao_inicia_RN04_51001()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 5);
        var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 2m));
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 5));
        var etapa = await c.EtapaAsync(op, Cenario.Corte);

        var erro = await Db.ErroAsync(
            "EXEC dbo.sp_IniciarEtapa @e, @r, @d",
            banco.Usuarios.Operador,
            ("@e", etapa), ("@r", Guid.NewGuid()), ("@d", DateTime.UtcNow));

        Assert.Equal(51001, erro);
        Assert.Equal(5m, await c.SaldoAsync(material));
        Assert.Equal("Pendente", await Db.TextoAsync("SELECT Status FROM dbo.OrdemProducaoEtapa WHERE OrdemProducaoEtapaId = @e", null, ("@e", etapa)));
        Assert.Equal(0, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.ApontamentoProducao WHERE OrdemProducaoEtapaId = @e", null, ("@e", etapa)));
    }

    [Fact]
    public async Task Etapa_fora_de_ordem_falha_RN05_51002()
    {
        var c = NovoCenario();
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1));
        var segunda = await c.EtapaAsync(op, Cenario.Estrutura);

        var erro = await Db.ErroAsync(
            "EXEC dbo.sp_IniciarEtapa @e, @r, @d",
            banco.Usuarios.Operador,
            ("@e", segunda), ("@r", Guid.NewGuid()), ("@d", DateTime.UtcNow));

        Assert.Equal(51002, erro);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_clientRequestId_nao_duplica_a_baixa()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 100);
        var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 2m));
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 5));
        var etapa = await c.EtapaAsync(op, Cenario.Corte);
        var requisicao = Guid.NewGuid();

        await c.IniciarAsync(etapa, requisicao);
        await c.IniciarAsync(etapa, requisicao);

        Assert.Equal(90m, await c.SaldoAsync(material));
        Assert.Equal(1, await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.ApontamentoProducao WHERE ClientRequestId = @r", null, ("@r", requisicao)));
    }

    [Fact]
    public async Task Inicios_simultaneos_disputando_o_mesmo_saldo_permitem_apenas_um()
    {
        for (var rodada = 0; rodada < 25; rodada++)
        {
            var c = NovoCenario();
            var material = await c.CriarMateriaAsync(saldo: 1);
            var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 1m));
            var etapaA = await c.EtapaAsync(await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 1)), Cenario.Corte);
            var etapaB = await c.EtapaAsync(await c.OpDoPedidoAsync(await c.CriarPedidoAsync(produto, 1)), Cenario.Corte);

            async Task<int> TentarAsync(int etapa)
            {
                try
                {
                    await c.IniciarAsync(etapa);
                    return 0;
                }
                catch (Microsoft.Data.SqlClient.SqlException ex)
                {
                    return ex.Number;
                }
            }

            var resultados = await Task.WhenAll(Task.Run(() => TentarAsync(etapaA)), Task.Run(() => TentarAsync(etapaB)));

            var detalhe = $"rodada {rodada}: erros = [{string.Join(", ", resultados)}]";
            Assert.True(resultados.Count(r => r == 0) == 1, detalhe);
            Assert.True(resultados.Count(r => r == 51001) == 1, detalhe);
            Assert.Equal(0m, await c.SaldoAsync(material));
        }
    }

    [Fact]
    public async Task Confirmacoes_simultaneas_de_produtos_novos_nao_geram_deadlock()
    {
        var c = NovoCenario();
        var pedidos = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            pedidos.Add(await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1, confirmar: false));
        }

        await Task.WhenAll(pedidos.Select(p => Task.Run(() => c.ConfirmarAsync(p))));

        foreach (var pedido in pedidos)
        {
            Assert.Equal("EmProducao", await c.StatusPedidoAsync(pedido));
        }
    }

    [Fact]
    public async Task Finalizar_a_etapa_de_qualidade_so_e_possivel_por_inspecao_51005()
    {
        var c = NovoCenario();
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1));
        await c.ProduzirAteQualidadeAsync(op);
        var qualidade = await c.EtapaAsync(op, Cenario.Qualidade);

        var erro = await Db.ErroAsync(
            "EXEC dbo.sp_FinalizarEtapa @e, @r, @d",
            banco.Usuarios.Inspetor,
            ("@e", qualidade), ("@r", Guid.NewGuid()), ("@d", DateTime.UtcNow));

        Assert.Equal(51005, erro);
    }

    [Fact]
    public async Task Fluxo_completo_do_pedido_a_expedicao()
    {
        var c = NovoCenario();
        var material = await c.CriarMateriaAsync(saldo: 50);
        var produto = await c.CriarProdutoAsync((Cenario.Estrutura, material, 1m));
        var pedido = await c.CriarPedidoAsync(produto, 4);
        var op = await c.OpDoPedidoAsync(pedido);

        await c.ProduzirAteQualidadeAsync(op);
        await c.InspecionarAsync(op, "Aprovada");

        Assert.Equal("Concluida", await Db.TextoAsync("SELECT Status FROM dbo.OrdemProducao WHERE OrdemProducaoId = @o", null, ("@o", op)));
        Assert.Equal("Pronto", await c.StatusPedidoAsync(pedido));
        Assert.Equal(46m, await c.SaldoAsync(material));
        Assert.Equal(4, await Db.InteiroAsync("SELECT Quantidade FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
        Assert.Equal(4, await Db.InteiroAsync("SELECT Reservado FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));

        await Db.ExecutarAsync(
            "EXEC dbo.sp_Expedir @p, N'Transportadora Teste', 'BR123'", banco.Usuarios.Almoxarife, ("@p", pedido));

        Assert.Equal("Expedido", await c.StatusPedidoAsync(pedido));
        Assert.Equal(0, await Db.InteiroAsync("SELECT Quantidade FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
        Assert.Equal(0, await Db.InteiroAsync("SELECT Reservado FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
        Assert.Equal(1, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.Expedicao WHERE PedidoId = @p", null, ("@p", pedido)));
    }

    [Fact]
    public async Task Expedir_pedido_nao_pronto_falha_RN07_51004()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1);

        var erro = await Db.ErroAsync("EXEC dbo.sp_Expedir @p", banco.Usuarios.Almoxarife, ("@p", pedido));

        Assert.Equal(51004, erro);
        Assert.Equal("EmProducao", await c.StatusPedidoAsync(pedido));
    }

    [Fact]
    public async Task Reprovacao_reabre_as_etapas_a_partir_da_estacao_de_retrabalho_RN06()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 2);
        var op = await c.OpDoPedidoAsync(pedido);
        await c.ProduzirAteQualidadeAsync(op);

        await c.InspecionarAsync(op, "Reprovada", Cenario.Estofaria);

        var status = await Db.TextoAsync(
            "SELECT STRING_AGG(Status, ',') WITHIN GROUP (ORDER BY Sequencia) FROM dbo.OrdemProducaoEtapa WHERE OrdemProducaoId = @o",
            null, ("@o", op));
        Assert.Equal("Concluida,Concluida,Pendente,Pendente,Pendente", status);
        Assert.Equal("EmAndamento", await Db.TextoAsync("SELECT Status FROM dbo.OrdemProducao WHERE OrdemProducaoId = @o", null, ("@o", op)));
        Assert.Equal("EmProducao", await c.StatusPedidoAsync(pedido));
        Assert.Equal(1, await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.RegistroPerda rp JOIN dbo.OrdemProducaoEtapa e ON e.OrdemProducaoEtapaId = rp.OrdemProducaoEtapaId " +
            "WHERE e.OrdemProducaoId = @o AND rp.Tipo = 'Retrabalho'", null, ("@o", op)));
        Assert.Equal(1, await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.Inspecao WHERE OrdemProducaoId = @o AND Resultado = 'Reprovada'", null, ("@o", op)));

        // A OP volta a poder ser produzida e aprovada
        await c.ProduzirDepoisDoRetrabalhoAsync(op);
        await c.InspecionarAsync(op, "Aprovada");
        Assert.Equal("Pronto", await c.StatusPedidoAsync(pedido));
    }

    [Fact]
    public async Task Reprovacao_sem_estacao_de_retrabalho_falha_51006()
    {
        var c = NovoCenario();
        var op = await c.OpDoPedidoAsync(await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1));
        await c.ProduzirAteQualidadeAsync(op);

        var erro = await Db.ErroAsync(
            "EXEC dbo.sp_RegistrarInspecao @o, 'Reprovada'", banco.Usuarios.Inspetor, ("@o", op));

        Assert.Equal(51006, erro);
    }

    [Fact]
    public async Task Cancelar_pedido_em_producao_libera_reserva_e_cancela_ops()
    {
        var c = NovoCenario();
        var produto = await c.CriarProdutoAsync();
        await Db.ExecutarAsync("INSERT dbo.EstoqueProduto (ProdutoId, Quantidade, Reservado) VALUES (@p, 10, 0)", null, ("@p", produto));
        var pedido = await c.CriarPedidoAsync(produto, 15);
        var op = await c.OpDoPedidoAsync(pedido);

        await Db.ExecutarAsync("EXEC dbo.sp_CancelarPedido @p", banco.Usuarios.Vendedor, ("@p", pedido));

        Assert.Equal("Cancelado", await c.StatusPedidoAsync(pedido));
        Assert.Equal("Cancelada", await Db.TextoAsync("SELECT Status FROM dbo.OrdemProducao WHERE OrdemProducaoId = @o", null, ("@o", op)));
        Assert.Equal(0, await Db.InteiroAsync("SELECT Reservado FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
        Assert.Equal(10, await Db.InteiroAsync("SELECT Quantidade FROM dbo.EstoqueProduto WHERE ProdutoId = @p", null, ("@p", produto)));
    }

    [Fact]
    public async Task Cancelar_pedido_expedido_falha_51005()
    {
        var c = NovoCenario();
        var pedido = await c.CriarPedidoAsync(await c.CriarProdutoAsync(), 1);
        await Db.ExecutarAsync("UPDATE dbo.Pedido SET Status = 'Expedido' WHERE PedidoId = @p", null, ("@p", pedido));

        var erro = await Db.ErroAsync("EXEC dbo.sp_CancelarPedido @p", banco.Usuarios.Vendedor, ("@p", pedido));

        Assert.Equal(51005, erro);
    }
}
