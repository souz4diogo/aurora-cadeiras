using System.Net.Http.Json;
using Microsoft.Data.SqlClient;

namespace Aurora.Web.Tests.Infraestrutura;

/// <summary>
/// Produto novo com roteiro Corte, Montagem da Estrutura e Qualidade, cujo Corte consome uma matéria-prima nova com
/// saldo, mais um cliente. Cada teste monta o seu para não gastar o estoque do seed nem interferir nos outros.
/// </summary>
public sealed record ProdutoDeProducao(
    int ProdutoId, string Codigo, int MateriaPrimaId, string MateriaPrimaCodigo, string MateriaPrimaNome, int ClienteId)
{
    private const byte Corte = 1;
    private const byte MontagemDaEstrutura = 2;
    private const byte Qualidade = 5;

    public static async Task<ProdutoDeProducao> CriarAsync(
        ApiHost api, decimal saldo = 100m, decimal estoqueMinimo = 1m, decimal consumoPorUnidade = 2m)
    {
        using var admin = await api.ClienteAutenticadoAsync("Administrador");
        using var almoxarife = await api.ClienteAutenticadoAsync("Almoxarife");
        var sufixo = Guid.NewGuid().ToString("N");
        var codigoDaMateria = ("M" + sufixo)[..12].ToUpperInvariant();
        var codigoDoProduto = ("P" + sufixo)[..12].ToUpperInvariant();

        var materia = await PostarAsync(admin, "/api/v1/materias-primas", new
        {
            Codigo = codigoDaMateria,
            Nome = "Insumo " + codigoDaMateria,
            Unidade = "m2",
            EstoqueMinimo = estoqueMinimo,
            CustoUnitario = 10m,
            FornecedorPreferencialId = (int?)null,
        });
        (await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new { MateriaPrimaId = materia.Id, NovaQuantidade = saldo, Motivo = "Saldo inicial do teste" }))
            .EnsureSuccessStatusCode();

        var produto = await PostarAsync(admin, "/api/v1/produtos", new { Codigo = codigoDoProduto, Nome = "Cadeira " + codigoDoProduto, LinhaProdutoId = 1, PrecoVenda = 100m });
        (await admin.PutAsJsonAsync($"/api/v1/produtos/{produto.Id}/roteiro", new { Estacoes = new List<int> { Corte, MontagemDaEstrutura, Qualidade } }))
            .EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync(
                $"/api/v1/produtos/{produto.Id}/ficha-tecnica",
                new { Itens = new[] { new { MateriaPrimaId = materia.Id, EstacaoId = Corte, Quantidade = consumoPorUnidade } } }))
            .EnsureSuccessStatusCode();

        var cpf = DadosDeTeste.Digitos(11);
        var cliente = await PostarAsync(admin, "/api/v1/clientes", new { Nome = "Cliente " + cpf, CpfCnpj = cpf });

        return new ProdutoDeProducao(produto.Id, codigoDoProduto, materia.Id, codigoDaMateria, "Insumo " + codigoDaMateria, cliente.Id);
    }

    /// <summary>Dá entrada de produto acabado direto no livro-razão, como a inspeção aprovada fará (fase 4).</summary>
    public async Task DarEntradaDeProdutoAcabadoAsync(ApiHost api, int quantidade)
    {
        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "INSERT dbo.MovimentoEstoque (ProdutoId, Tipo, Quantidade, Origem, Motivo) VALUES (@produto, 'Entrada', @quantidade, 'ProducaoConcluida', N'Teste')";
        comando.Parameters.AddWithValue("@produto", ProdutoId);
        comando.Parameters.AddWithValue("@quantidade", quantidade);
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Cria e confirma um pedido do produto pelo painel; devolve (id, número) do pedido.</summary>
    public async Task<(string Id, string Numero)> PedidoConfirmadoAsync(Navegador vendedor, int quantidade)
    {
        var envio = await vendedor.EnviarAsync(
            "/Pedidos/Novo", "/Pedidos/Novo",
            PedidosWebTests.Campos(ClienteId.ToString(System.Globalization.CultureInfo.InvariantCulture), PedidosWebTests.Prazo(),
                (ProdutoId.ToString(System.Globalization.CultureInfo.InvariantCulture), quantidade.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var id = System.Text.RegularExpressions.Regex.Match(Navegador.Destino(envio), @"/Pedidos/Detalhes/(\d+)").Groups[1].Value;
        Assert.NotEqual("", id);
        var confirmacao = await vendedor.EnviarAsync($"/Pedidos/Confirmar/{id}", $"/Pedidos/Confirmar/{id}", []);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, confirmacao.StatusCode);
        var detalhes = await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}");
        return (id, System.Text.RegularExpressions.Regex.Match(detalhes, @"PV-\d{4}-\d{5}").Value);
    }

    /// <summary>Id da primeira ordem de produção do pedido, lido do link na página de detalhes do pedido.</summary>
    public static async Task<string> IdDaOrdemAsync(Navegador navegador, string pedidoId)
    {
        var detalhes = await navegador.PaginaAsync($"/Pedidos/Detalhes/{pedidoId}");
        var ordem = System.Text.RegularExpressions.Regex.Match(detalhes, @"/Producao/Detalhes/(\d+)");
        Assert.True(ordem.Success, "O pedido confirmado não mostra a ordem de produção.");
        return ordem.Groups[1].Value;
    }

    private static async Task<Criado> PostarAsync(HttpClient cliente, string rota, object corpo)
    {
        var resposta = await cliente.PostAsJsonAsync(rota, corpo);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<Criado>())!;
    }

    private sealed record Criado(int Id);
}
