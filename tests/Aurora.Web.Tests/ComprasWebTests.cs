using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed partial class ComprasWebTests(CenarioDoPainel cenario)
{
    [GeneratedRegex(@"/Compras/Detalhes/(\d+)")]
    private static partial Regex LinkDaCompra();

    [GeneratedRegex(@"name=""itemId"" value=""(\d+)""")]
    private static partial Regex ItemParaReceber();

    private static string Prazo() => DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<int> NovoFornecedorAsync()
    {
        using var admin = await cenario.Api.ClienteAutenticadoAsync("Administrador");
        var resposta = await admin.PostAsJsonAsync("/api/v1/fornecedores", new
        {
            RazaoSocial = "Fornecedor " + Guid.NewGuid().ToString("N")[..8],
            Cnpj = DadosDeTeste.Digitos(14),
            Email = (string?)null,
            Telefone = (string?)null,
            ComunidadeTradicional = false,
        });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<Criado>())!.Id;
    }

    private static List<KeyValuePair<string, string>> Campos(int? fornecedorId, int materiaPrimaId, string quantidade, string custo = "") =>
    [
        new("FornecedorId", fornecedorId?.ToString(CultureInfo.InvariantCulture) ?? ""),
        new("DataPrevista", Prazo()),
        new("Itens[0].MateriaPrimaId", materiaPrimaId.ToString(CultureInfo.InvariantCulture)),
        new("Itens[0].Quantidade", quantidade),
        new("Itens[0].CustoUnitario", custo),
    ];

    private async Task<(string Id, ProdutoDeProducao Material)> CriarCompraAsync(Navegador almoxarife, string quantidade = "10")
    {
        var material = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 100m);
        var fornecedor = await NovoFornecedorAsync();
        var envio = await almoxarife.EnviarAsync("/Compras/Novo", "/Compras/Novo", Campos(fornecedor, material.MateriaPrimaId, quantidade));
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        var id = LinkDaCompra().Match(Navegador.Destino(envio)).Groups[1].Value;
        Assert.NotEqual("", id);
        return (id, material);
    }

    [Fact]
    public async Task Almoxarife_cria_emite_e_recebe_em_duas_remessas_e_o_estoque_acompanha()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, material) = await CriarCompraAsync(almoxarife, "10,5");

        var criado = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}"));
        Assert.Contains("Pedido de compra", criado, StringComparison.Ordinal);
        Assert.Contains("Solicitado", criado, StringComparison.Ordinal);
        Assert.Contains("Emitir pedido", criado, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"itemId\"", criado, StringComparison.Ordinal);

        var emissao = await almoxarife.EnviarAsync($"/Compras/Detalhes/{id}", $"/Compras/Emitir/{id}", []);
        Assert.Equal(HttpStatusCode.Redirect, emissao.StatusCode);
        var emitido = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}"));
        Assert.Contains("Emitido", emitido, StringComparison.Ordinal);
        var itemId = ItemParaReceber().Match(emitido).Groups[1].Value;
        Assert.NotEqual("", itemId);

        var primeira = await almoxarife.EnviarAsync(
            $"/Compras/Detalhes/{id}", $"/Compras/Receber/{id}", [new("itemId", itemId), new("quantidade", "4,5")]);
        var depoisDaPrimeira = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(primeira)));
        Assert.Contains("Parcialmente recebido", depoisDaPrimeira, StringComparison.Ordinal);
        Assert.Contains("Recebido: 4,5 m2", depoisDaPrimeira, StringComparison.Ordinal);

        var segunda = await almoxarife.EnviarAsync(
            $"/Compras/Detalhes/{id}", $"/Compras/Receber/{id}", [new("itemId", itemId), new("quantidade", "6")]);
        var completo = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(segunda)));
        var extrato = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Movimentos?materiaPrimaId={material.MateriaPrimaId}"));
        var saldos = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque?busca={material.MateriaPrimaCodigo}"));

        Assert.Contains(">Recebido<", completo, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"itemId\"", completo, StringComparison.Ordinal);
        Assert.Contains("+4,5", extrato, StringComparison.Ordinal);
        Assert.Contains("+6", extrato, StringComparison.Ordinal);
        Assert.Contains("<td class=\"numero\">110,5</td>", saldos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Do_alerta_de_estoque_critico_ate_o_saldo_reposto()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var material = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 5m, estoqueMinimo: 5m);
        var fornecedor = await NovoFornecedorAsync();

        var alertas = Navegador.Decodificar(await almoxarife.PaginaAsync("/Estoque/Alertas"));
        var link = $"/Compras/Novo?materiaPrimaId={material.MateriaPrimaId}";
        var formulario = Navegador.Decodificar(await almoxarife.PaginaAsync(link));
        var envio = await almoxarife.EnviarAsync(link, "/Compras/Novo", Campos(fornecedor, material.MateriaPrimaId, "20"));
        var id = LinkDaCompra().Match(Navegador.Destino(envio)).Groups[1].Value;
        await almoxarife.EnviarAsync($"/Compras/Detalhes/{id}", $"/Compras/Emitir/{id}", []);
        var itemId = ItemParaReceber().Match(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}")).Groups[1].Value;
        await almoxarife.EnviarAsync($"/Compras/Detalhes/{id}", $"/Compras/Receber/{id}", [new("itemId", itemId), new("quantidade", "20")]);
        var saldos = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque?busca={material.MateriaPrimaCodigo}"));

        Assert.Contains($"/Compras/Novo?materiaPrimaId={material.MateriaPrimaId}", alertas, StringComparison.Ordinal);
        Assert.Contains($"value=\"{material.MateriaPrimaId}\" selected=\"selected\"", formulario, StringComparison.Ordinal);
        Assert.Contains("<td class=\"numero\">25</td>", saldos, StringComparison.Ordinal);
        Assert.Contains(">Normal<", saldos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Receber_mais_do_que_falta_ou_quantidade_vazia_e_recusado_na_propria_tela()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, _) = await CriarCompraAsync(almoxarife, "10");
        await almoxarife.EnviarAsync($"/Compras/Detalhes/{id}", $"/Compras/Emitir/{id}", []);
        var itemId = ItemParaReceber().Match(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}")).Groups[1].Value;

        var excedente = await almoxarife.EnviarAsync(
            $"/Compras/Detalhes/{id}", $"/Compras/Receber/{id}", [new("itemId", itemId), new("quantidade", "10,001")]);
        var vazio = await almoxarife.EnviarAsync(
            $"/Compras/Detalhes/{id}", $"/Compras/Receber/{id}", [new("itemId", itemId), new("quantidade", "")]);

        Assert.Equal(HttpStatusCode.OK, excedente.StatusCode);
        Assert.Contains("alert-danger", await excedente.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, vazio.StatusCode);
        Assert.Contains("Informe a quantidade que chegou", Navegador.Decodificar(await vazio.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.DoesNotContain("Parcialmente", Navegador.Decodificar(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelar_pede_confirmacao_e_depois_nao_permite_mais_receber()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, _) = await CriarCompraAsync(almoxarife);

        var confirmacao = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Compras/Cancelar/{id}"));
        var cancelamento = await almoxarife.EnviarAsync($"/Compras/Cancelar/{id}", $"/Compras/Cancelar/{id}", []);
        var depois = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Compras/Detalhes/{id}"));

        Assert.Contains("Sim, cancelar o pedido de compra", confirmacao, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, cancelamento.StatusCode);
        Assert.Contains(">Cancelado<", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("Emitir pedido", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar pedido", depois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formulario_sem_fornecedor_ou_sem_itens_volta_com_as_mensagens()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var material = await ProdutoDeProducao.CriarAsync(cenario.Api);

        var semFornecedor = await almoxarife.EnviarAsync("/Compras/Novo", "/Compras/Novo", Campos(null, material.MateriaPrimaId, "0"));
        var semItens = await almoxarife.EnviarAsync(
            "/Compras/Novo", "/Compras/Novo", [new("FornecedorId", (await NovoFornecedorAsync()).ToString(CultureInfo.InvariantCulture))]);

        var texto = Navegador.Decodificar(await semFornecedor.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, semFornecedor.StatusCode);
        Assert.Contains("Escolha o fornecedor.", texto, StringComparison.Ordinal);
        Assert.Contains("A quantidade deve ser maior que zero.", texto, StringComparison.Ordinal);
        Assert.Contains("Adicione ao menos um item ao pedido de compra.", Navegador.Decodificar(await semItens.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_consulta_mas_nao_ve_nem_usa_as_acoes_do_almoxarife()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, _) = await CriarCompraAsync(almoxarife);

        var lista = Navegador.Decodificar(await gerente.PaginaAsync("/Compras"));
        var detalhes = Navegador.Decodificar(await gerente.PaginaAsync($"/Compras/Detalhes/{id}"));
        var novo = await gerente.GetAsync("/Compras/Novo");
        var emitir = await gerente.EnviarAsync($"/Compras/Detalhes/{id}", $"/Compras/Emitir/{id}", []);

        Assert.DoesNotContain("Novo pedido de compra", lista, StringComparison.Ordinal);
        Assert.Contains("Solicitado", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("Emitir pedido", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar pedido", detalhes, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(novo), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(emitir), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Operador")]
    [InlineData("Inspetor")]
    public async Task Perfis_sem_acesso_a_compras_vao_para_acesso_negado(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var lista = await navegador.GetAsync("/Compras");
        var detalhes = await navegador.GetAsync("/Compras/Detalhes/1");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(lista), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(detalhes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_filtra_por_status_e_mostra_a_compra_criada()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, _) = await CriarCompraAsync(almoxarife);

        var solicitados = Navegador.Decodificar(await almoxarife.PaginaAsync("/Compras?status=Solicitado"));
        var recebidos = Navegador.Decodificar(await almoxarife.PaginaAsync("/Compras?status=Recebido"));

        Assert.Contains($"/Compras/Detalhes/{id}\"", solicitados, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Compras/Detalhes/{id}\"", recebidos, StringComparison.Ordinal);
    }

    private sealed record Criado(int Id);
}
