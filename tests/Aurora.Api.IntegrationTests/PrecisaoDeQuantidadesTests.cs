using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Compras;
using Aurora.Application.Estoque;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;

namespace Aurora.Api.IntegrationTests;

/// <summary>
/// Quantidades de matéria-prima têm 3 casas decimais (decimal(12,3)). O parâmetro decimal enviado sem precisão
/// chegava ao SQL Server com 2 casas e arredondava em silêncio (4,125 virava 4,13): estes testes travam isso.
/// </summary>
[Collection("api")]
public class PrecisaoDeQuantidadesTests(ApiFactory api)
{
    private static async Task<MateriaPrimaDto> NovaMateriaAsync(HttpClient admin)
    {
        var resposta = await admin.PostAsJsonAsync(
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("P" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), "Material fracionado", "m3", 0m, 1m, null));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
    }

    private static async Task<decimal> SaldoAsync(HttpClient cliente, int materiaId) =>
        (await cliente.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{materiaId}"))!.EstoqueAtual;

    [Theory]
    [InlineData("12.345")]
    [InlineData("0.001")]
    [InlineData("7.999")]
    public async Task Ajuste_de_estoque_guarda_as_tres_casas_decimais(string texto)
    {
        var quantidade = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaAsync(admin);

        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materia.Id, quantidade, "Contagem fracionada"));

        resposta.AssertStatus(HttpStatusCode.OK);
        Assert.Equal(quantidade, await SaldoAsync(almoxarife, materia.Id));
    }

    [Theory]
    [InlineData("4.125")]
    [InlineData("0.001")]
    public async Task Recebimento_guarda_as_tres_casas_decimais_e_o_excedente_de_um_milesimo_e_recusado(string texto)
    {
        var quantidade = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaAsync(admin);
        var fornecedorResposta = await admin.PostAsJsonAsync(
            "/api/v1/fornecedores", new FornecedorRequest("Fornecedor fracionado", DadosDeTeste.Digitos(14), null, null, false));
        var fornecedor = (await fornecedorResposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
        var pedidoResposta = await almoxarife.PostAsJsonAsync(
            "/api/v1/pedidos-compra", new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, quantidade, 1m)]));
        var pedido = (await pedidoResposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        (await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/emitir", content: null)).AssertStatus(HttpStatusCode.OK);
        var item = pedido.Itens[0].Id;

        var tudo = await almoxarife.PostAsJsonAsync($"/api/v1/pedidos-compra/itens/{item}/receber", new ReceberItemRequest(quantidade));
        var umMilesimoAMais = await almoxarife.PostAsJsonAsync($"/api/v1/pedidos-compra/itens/{item}/receber", new ReceberItemRequest(0.001m));

        tudo.AssertStatus(HttpStatusCode.OK);
        Assert.Equal(quantidade, await SaldoAsync(almoxarife, materia.Id));
        umMilesimoAMais.AssertStatus(HttpStatusCode.Conflict); // o pedido já está Recebido
    }
}
