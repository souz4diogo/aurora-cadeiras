namespace Aurora.Database.Tests;

/// <summary>Monta dados isolados (códigos únicos) e chama as procedures como a API faria.</summary>
public sealed class Cenario(Db db, Usuarios usuarios)
{
    public const int Corte = 1;
    public const int Estrutura = 2;
    public const int Estofaria = 3;
    public const int MontagemFinal = 4;
    public const int Qualidade = 5;

    public Db Db => db;

    public Usuarios Usuarios => usuarios;

    private static string Codigo(string prefixo) => prefixo + Guid.NewGuid().ToString("N")[..10];

    public async Task<int> CriarMateriaAsync(decimal saldo, decimal minimo = 0)
    {
        var id = await db.InteiroAsync(
            "INSERT dbo.MateriaPrima (Codigo, Nome, Unidade, EstoqueMinimo, CustoUnitario) " +
            "VALUES (@c, N'Material de teste', 'un', @min, 10); SELECT CAST(SCOPE_IDENTITY() AS int);",
            usuarios.Almoxarife,
            ("@c", Codigo("MT-")),
            ("@min", minimo));

        if (saldo > 0)
        {
            await db.ExecutarAsync(
                "EXEC dbo.sp_AjustarEstoque @id, @saldo, N'Saldo de teste'",
                usuarios.Almoxarife,
                ("@id", id),
                ("@saldo", saldo));
        }

        return id;
    }

    /// <summary>Produto com roteiro de 5 estações e a ficha informada (estação, material, quantidade por unidade).</summary>
    public async Task<int> CriarProdutoAsync(params (int Estacao, int Material, decimal Quantidade)[] ficha)
    {
        var id = await db.InteiroAsync(
            "INSERT dbo.Produto (Codigo, Nome, LinhaProdutoId, PrecoVenda) " +
            "VALUES (@c, N'Produto de teste', 1, 100); SELECT CAST(SCOPE_IDENTITY() AS int);",
            null,
            ("@c", Codigo("PT-")));

        await db.ExecutarAsync(
            "INSERT dbo.RoteiroProduto (ProdutoId, EstacaoId, Sequencia) " +
            "SELECT @p, EstacaoId, EstacaoId FROM dbo.EstacaoProducao WHERE GeraEtapa = 1",
            null,
            ("@p", id));

        foreach (var (estacao, material, quantidade) in ficha)
        {
            await db.ExecutarAsync(
                "INSERT dbo.FichaTecnicaItem (ProdutoId, MateriaPrimaId, EstacaoId, Quantidade) VALUES (@p, @m, @e, @q)",
                null,
                ("@p", id),
                ("@m", material),
                ("@e", estacao),
                ("@q", quantidade));
        }

        return id;
    }

    public async Task<int> CriarPedidoAsync(int produtoId, int quantidade, bool confirmar = true)
    {
        var pedidoId = await db.InteiroAsync(
            "INSERT dbo.Pedido (ClienteId, VendedorId, PrazoEntrega) " +
            "SELECT TOP (1) ClienteId, @v, DATEADD(DAY, 10, CAST(SYSUTCDATETIME() AS date)) FROM dbo.Cliente; " +
            "SELECT CAST(SCOPE_IDENTITY() AS int);",
            usuarios.Vendedor,
            ("@v", usuarios.Vendedor));

        await db.ExecutarAsync(
            "INSERT dbo.PedidoItem (PedidoId, ProdutoId, Quantidade, PrecoUnitario) VALUES (@p, @prod, @q, 100); " +
            "UPDATE dbo.Pedido SET ValorTotal = @q * 100 WHERE PedidoId = @p;",
            usuarios.Vendedor,
            ("@p", pedidoId),
            ("@prod", produtoId),
            ("@q", quantidade));

        if (confirmar)
        {
            await ConfirmarAsync(pedidoId);
        }

        return pedidoId;
    }

    public Task ConfirmarAsync(int pedidoId) =>
        db.ExecutarAsync("EXEC dbo.sp_ConfirmarPedido @p", usuarios.Vendedor, ("@p", pedidoId));

    public Task<int> OpDoPedidoAsync(int pedidoId) =>
        db.InteiroAsync(
            "SELECT op.OrdemProducaoId FROM dbo.OrdemProducao op " +
            "JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId WHERE pi.PedidoId = @p",
            null,
            ("@p", pedidoId));

    /// <summary>Id da etapa da estação informada na OP.</summary>
    public Task<int> EtapaAsync(int opId, int estacao) =>
        db.InteiroAsync(
            "SELECT OrdemProducaoEtapaId FROM dbo.OrdemProducaoEtapa WHERE OrdemProducaoId = @op AND EstacaoId = @e",
            null,
            ("@op", opId),
            ("@e", estacao));

    public Task IniciarAsync(int etapaId, Guid? requisicao = null) =>
        db.ExecutarAsync(
            "EXEC dbo.sp_IniciarEtapa @e, @r, @d",
            usuarios.Operador,
            ("@e", etapaId),
            ("@r", requisicao ?? Guid.NewGuid()),
            ("@d", DateTime.UtcNow));

    public Task FinalizarAsync(int etapaId, Guid? requisicao = null) =>
        db.ExecutarAsync(
            "EXEC dbo.sp_FinalizarEtapa @e, @r, @d",
            usuarios.Operador,
            ("@e", etapaId),
            ("@r", requisicao ?? Guid.NewGuid()),
            ("@d", DateTime.UtcNow));

    /// <summary>Inicia e finaliza as etapas de Corte até Montagem Final e inicia a Qualidade.</summary>
    public async Task ProduzirAteQualidadeAsync(int opId)
    {
        foreach (var estacao in new[] { Corte, Estrutura, Estofaria, MontagemFinal })
        {
            var etapa = await EtapaAsync(opId, estacao);
            await IniciarAsync(etapa);
            await FinalizarAsync(etapa);
        }

        await IniciarAsync(await EtapaAsync(opId, Qualidade));
    }

    /// <summary>Refaz as etapas reabertas (da Estofaria à Qualidade) depois de uma reprovação.</summary>
    public async Task ProduzirDepoisDoRetrabalhoAsync(int opId)
    {
        foreach (var estacao in new[] { Estofaria, MontagemFinal })
        {
            var etapa = await EtapaAsync(opId, estacao);
            await IniciarAsync(etapa);
            await FinalizarAsync(etapa);
        }

        await IniciarAsync(await EtapaAsync(opId, Qualidade));
    }

    public Task InspecionarAsync(int opId, string resultado, int? estacaoRetrabalho = null) =>
        db.ExecutarAsync(
            "EXEC dbo.sp_RegistrarInspecao @op, @r, @e, N'Observação de teste'",
            usuarios.Inspetor,
            ("@op", opId),
            ("@r", resultado),
            ("@e", estacaoRetrabalho));

    public Task<string?> StatusPedidoAsync(int pedidoId) =>
        db.TextoAsync("SELECT Status FROM dbo.Pedido WHERE PedidoId = @p", null, ("@p", pedidoId));

    public Task<decimal> SaldoAsync(int materialId) =>
        db.DecimalAsync("SELECT EstoqueAtual FROM dbo.MateriaPrima WHERE MateriaPrimaId = @m", null, ("@m", materialId));
}
