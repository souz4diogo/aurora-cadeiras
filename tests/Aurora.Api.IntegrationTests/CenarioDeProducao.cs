using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aurora.Application.Clientes;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.FichasTecnicas;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Pedidos;
using Aurora.Application.Producao;
using Aurora.Application.Produtos;
using Aurora.Application.Roteiros;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

/// <summary>
/// Dados novos para um teste de produção, sem tocar no seed: uma matéria-prima com saldo, um produto com roteiro
/// Corte, Montagem da Estrutura e Qualidade, cujo Corte consome a matéria-prima, e um cliente. Cada teste monta o
/// seu, então o saldo e o estoque de produto de um não interferem no outro.
/// </summary>
internal sealed class CenarioDeProducao
{
    public const byte Corte = 1;
    public const byte MontagemDaEstrutura = 2;
    public const byte Qualidade = 5;

    private static readonly DateOnly Prazo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    private readonly ApiFactory _api;

    private CenarioDeProducao(ApiFactory api, MateriaPrimaDto materia, ProdutoDto produto, ClienteDto cliente, decimal consumoPorUnidade)
    {
        _api = api;
        Materia = materia;
        Produto = produto;
        Cliente = cliente;
        ConsumoPorUnidade = consumoPorUnidade;
    }

    public MateriaPrimaDto Materia { get; }

    public ProdutoDto Produto { get; }

    public ClienteDto Cliente { get; }

    public decimal ConsumoPorUnidade { get; }

    public static async Task<CenarioDeProducao> CriarAsync(
        ApiFactory api, decimal saldo = 100m, decimal estoqueMinimo = 1m, decimal consumoPorUnidade = 2m)
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var sufixo = Guid.NewGuid().ToString("N");

        var materia = await CriarAsync<MateriaPrimaDto>(
            admin,
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("M" + sufixo)[..12].ToUpperInvariant(), "Insumo de Produção", "m2", estoqueMinimo, 10m, null));
        (await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materia.Id, saldo, "Saldo inicial do teste")))
            .AssertStatus(HttpStatusCode.OK);

        var produto = await CriarAsync<ProdutoDto>(
            admin, "/api/v1/produtos", new ProdutoRequest(("P" + sufixo)[..12].ToUpperInvariant(), "Cadeira de Produção", 1, 100m));
        (await admin.PutAsJsonAsync(
                $"/api/v1/produtos/{produto.Id}/roteiro", new RoteiroRequest(new[] { Corte, MontagemDaEstrutura, Qualidade })))
            .AssertStatus(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync(
                $"/api/v1/produtos/{produto.Id}/ficha-tecnica",
                new FichaTecnicaRequest([new FichaTecnicaItemRequest(materia.Id, Corte, consumoPorUnidade)])))
            .AssertStatus(HttpStatusCode.OK);

        var cliente = await CriarAsync<ClienteDto>(
            admin, "/api/v1/clientes", new ClienteRequest("Cliente de Produção", DadosDeTeste.Digitos(11), null, null, null, null));

        return new CenarioDeProducao(api, materia, produto, cliente, consumoPorUnidade);
    }

    public async Task<PedidoDto> PedidoAbertoAsync(HttpClient vendedor, int quantidade) =>
        await CriarAsync<PedidoDto>(
            vendedor, "/api/v1/pedidos", new PedidoRequest(Cliente.Id, Prazo, null, [new PedidoItemRequest(Produto.Id, quantidade)]));

    /// <summary>Cria o pedido e o confirma: com estoque de produto zerado, gera uma OP para a quantidade toda.</summary>
    public async Task<PedidoDto> PedidoConfirmadoAsync(HttpClient vendedor, int quantidade)
    {
        var pedido = await PedidoAbertoAsync(vendedor, quantidade);
        var resposta = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null);
        resposta.AssertStatus(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<PedidoDto>())!;
    }

    /// <summary>A OP gerada para o pedido, já com as etapas.</summary>
    public static async Task<OrdemProducaoDto> OrdemDoPedidoAsync(HttpClient gerente, int pedidoId)
    {
        var lista = (await gerente.GetFromJsonAsync<Pagina<OrdemProducaoResumoDto>>($"/api/v1/ordens-producao?pedidoId={pedidoId}"))!;
        var resumo = Assert.Single(lista.Items);
        return (await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{resumo.Id}"))!;
    }

    public async Task<decimal> SaldoAsync()
    {
        using var almoxarife = await _api.ComoAsync("Almoxarife");
        return (await almoxarife.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{Materia.Id}"))!.EstoqueAtual;
    }

    /// <summary>Dá entrada de produto acabado direto no livro-razão, como a inspeção aprovada fará (fase 4).</summary>
    public async Task DarEntradaDeProdutoAcabadoAsync(int quantidade)
    {
        await using var conexao = new SqlConnection(_api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "INSERT dbo.MovimentoEstoque (ProdutoId, Tipo, Quantidade, Origem, Motivo) VALUES (@produto, 'Entrada', @quantidade, 'ProducaoConcluida', N'Teste')";
        comando.Parameters.AddWithValue("@produto", Produto.Id);
        comando.Parameters.AddWithValue("@quantidade", quantidade);
        await comando.ExecuteNonQueryAsync();
    }

    public static ApontamentoRequest NovoApontamento(Guid? requisicao = null) =>
        new(requisicao ?? Guid.NewGuid(), DateTimeOffset.UtcNow);

    public async Task<HttpClient> NovoVendedorAsync()
    {
        var (usuario, senha) = await _api.CriarUsuarioAsync("Vendedor");
        var token = await _api.LoginAsync(usuario.Email, senha);
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return cliente;
    }

    private static async Task<T> CriarAsync<T>(HttpClient cliente, string rota, object corpo)
    {
        var resposta = await cliente.PostAsJsonAsync(rota, corpo);
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<T>())!;
    }
}
