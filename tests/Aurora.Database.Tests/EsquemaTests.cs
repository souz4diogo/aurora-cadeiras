namespace Aurora.Database.Tests;

[Collection("banco")]
public class EsquemaTests(BancoFixture banco)
{
    private Db Db => banco.Db;

    [Fact]
    public async Task Scripts_criam_as_25_tabelas()
    {
        var total = await Db.InteiroAsync("SELECT COUNT(*) FROM sys.tables WHERE name <> 'SchemaVersions'");

        Assert.Equal(25, total);
    }

    [Fact]
    public async Task Seed_carrega_cadastros_basicos()
    {
        Assert.Equal(7, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.Perfil"));
        Assert.Equal(6, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.EstacaoProducao"));
        Assert.Equal(6, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.Produto WHERE Codigo LIKE 'C[EJS]-%'"));
        Assert.Equal(14, await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.MateriaPrima WHERE Codigo LIKE 'MP-%'"));
        Assert.True(await Db.InteiroAsync("SELECT COUNT(*) FROM dbo.Fornecedor WHERE ComunidadeTradicional = 1") >= 2);
    }

    [Fact]
    public async Task Seed_nao_inclui_estofaria_no_roteiro_da_linha_jantar()
    {
        var etapasJantar = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.RoteiroProduto WHERE ProdutoId = (SELECT ProdutoId FROM dbo.Produto WHERE Codigo = 'CJ-100')");
        var estofariaJantar = await Db.InteiroAsync(
            "SELECT COUNT(*) FROM dbo.RoteiroProduto WHERE EstacaoId = 3 AND ProdutoId = (SELECT ProdutoId FROM dbo.Produto WHERE Codigo = 'CJ-100')");

        Assert.Equal(4, etapasJantar);
        Assert.Equal(0, estofariaJantar);
    }

    [Fact]
    public async Task Saldo_inicial_do_seed_vem_de_movimentos_de_ajuste()
    {
        var saldo = await Db.DecimalAsync("SELECT EstoqueAtual FROM dbo.MateriaPrima WHERE Codigo = 'MP-001'");
        var movimentos = await Db.DecimalAsync(
            "SELECT SUM(Quantidade) FROM dbo.MovimentoEstoque WHERE MateriaPrimaId = (SELECT MateriaPrimaId FROM dbo.MateriaPrima WHERE Codigo = 'MP-001')");

        Assert.Equal(20m, saldo);
        Assert.Equal(saldo, movimentos);
    }

    [Fact]
    public async Task Numeracao_dos_documentos_segue_o_padrao()
    {
        var produto = await new Cenario(Db, banco.Usuarios).CriarProdutoAsync();
        var cenario = new Cenario(Db, banco.Usuarios);
        var pedido = await cenario.CriarPedidoAsync(produto, 1, confirmar: false);

        var numero = await Db.TextoAsync("SELECT Numero FROM dbo.Pedido WHERE PedidoId = @p", null, ("@p", pedido));

        Assert.Matches(@"^PV-\d{4}-\d{5}$", numero);
    }
}
