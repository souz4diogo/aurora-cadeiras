namespace Aurora.Database.Tests;

[Collection("banco")]
public class SegurancaTests(BancoFixture banco)
{
    private Db Db => banco.Db;

    private async Task<Db> UsuarioDoPapelAsync(string papel)
    {
        var nome = "teste_" + Guid.NewGuid().ToString("N")[..8];
        await Db.ExecutarAsync($"CREATE USER [{nome}] WITHOUT LOGIN; ALTER ROLE [{papel}] ADD MEMBER [{nome}];");
        return Db.Como(nome);
    }

    [Fact]
    public async Task Aplicacao_nao_altera_estoque_diretamente()
    {
        var app = await UsuarioDoPapelAsync("role_aurora_app");
        var material = await new Cenario(Db, banco.Usuarios).CriarMateriaAsync(saldo: 5);

        var insert = await app.ErroAsync(
            "INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem) VALUES (@m, 'Entrada', 1, 'Compra')",
            null, ("@m", material));
        var update = await app.ErroAsync("UPDATE dbo.OrdemProducaoEtapa SET Status = 'Concluida'");
        var auditoria = await app.ErroAsync("DELETE dbo.Auditoria");

        Assert.Equal(229, insert);
        Assert.Equal(229, update);
        Assert.Equal(229, auditoria);
    }

    [Fact]
    public async Task Fluxo_completo_funciona_pelo_papel_da_aplicacao_via_procedures_e_triggers()
    {
        var app = await UsuarioDoPapelAsync("role_aurora_app");
        var c = new Cenario(app, banco.Usuarios);
        var material = await c.CriarMateriaAsync(saldo: 20);
        var produto = await c.CriarProdutoAsync((Cenario.Corte, material, 1m));
        var pedido = await c.CriarPedidoAsync(produto, 2);
        var op = await c.OpDoPedidoAsync(pedido);

        await c.ProduzirAteQualidadeAsync(op);
        await c.InspecionarAsync(op, "Aprovada");
        await app.ExecutarAsync("EXEC dbo.sp_Expedir @p", banco.Usuarios.Almoxarife, ("@p", pedido));

        Assert.Equal("Expedido", await c.StatusPedidoAsync(pedido));
        Assert.Equal(18m, await c.SaldoAsync(material));
    }

    [Fact]
    public async Task Relatorios_enxergam_apenas_as_views()
    {
        var leitor = await UsuarioDoPapelAsync("role_aurora_readonly");

        var view = await leitor.InteiroAsync("SELECT COUNT(*) FROM dbo.vw_EstoqueCritico");
        var tabela = await leitor.ErroAsync("SELECT COUNT(*) FROM dbo.Pedido");

        Assert.True(view >= 0);
        Assert.Equal(229, tabela);
    }

    [Fact]
    public async Task Dados_pessoais_aparecem_mascarados_sem_permissao_de_unmask()
    {
        var nome = "teste_" + Guid.NewGuid().ToString("N")[..8];
        await Db.ExecutarAsync($"CREATE USER [{nome}] WITHOUT LOGIN; GRANT SELECT ON dbo.Cliente TO [{nome}];");
        var original = await Db.TextoAsync("SELECT TOP (1) Email FROM dbo.Cliente ORDER BY ClienteId");

        var visto = await Db.Como(nome).TextoAsync("SELECT TOP (1) Email FROM dbo.Cliente ORDER BY ClienteId");

        Assert.NotEqual(original, visto);
        Assert.Contains("XXXX", visto, StringComparison.Ordinal);
    }
}
