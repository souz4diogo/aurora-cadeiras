using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public class FornecedoresWebTests(CenarioDoPainel cenario)
{
    private static List<KeyValuePair<string, string>> Campos(string razaoSocial, string cnpj, bool comunidade = false) =>
    [
        new("RazaoSocial", razaoSocial),
        new("Cnpj", cnpj),
        new("Email", "vendas@fornecedor.local"),
        new("Telefone", ""),
        new("ComunidadeTradicional", comunidade ? "true" : "false"),
    ];

    private static string NovaRazao() => $"Fornecedor {Guid.NewGuid():N}"[..22];

    [Fact]
    public async Task Almoxarife_cadastra_fornecedor_de_comunidade_tradicional_e_a_lista_mostra_a_marca()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var razao = NovaRazao();
        var cnpj = DadosDeTeste.Digitos(14);

        var envio = await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(razao, cnpj, comunidade: true));
        var lista = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Fornecedores?busca={cnpj}"));

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains(razao, lista, StringComparison.Ordinal);
        Assert.Contains($"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}", lista, StringComparison.Ordinal);
        Assert.Contains("<td>Sim</td>", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filtro_de_comunidade_tradicional_separa_os_fornecedores()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var comunidade = NovaRazao();
        var comum = NovaRazao();
        await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(comunidade, DadosDeTeste.Digitos(14), comunidade: true));
        await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(comum, DadosDeTeste.Digitos(14)));

        await almoxarife.PaginaAsync("/Fornecedores"); // consome o aviso de sucesso, que cita o último fornecedor criado
        var soComunidades = Navegador.Decodificar(await almoxarife.PaginaAsync("/Fornecedores?comunidadeTradicional=true&pageSize=100"));
        var demais = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Fornecedores?comunidadeTradicional=false&busca={Uri.EscapeDataString(comum)}"));

        Assert.Contains(comunidade, soComunidades, StringComparison.Ordinal);
        Assert.DoesNotContain(comum, soComunidades, StringComparison.Ordinal);
        Assert.Contains(comum, demais, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cnpj_duplicado_volta_ao_formulario_com_a_mensagem_da_api()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var cnpj = DadosDeTeste.Digitos(14);
        await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(NovaRazao(), cnpj));

        var resposta = await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos("Outra Razão", cnpj));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Contains("Já existe um fornecedor com este CNPJ.", texto, StringComparison.Ordinal);
        Assert.Contains("value=\"Outra Razão\"", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cnpj_com_digitos_a_menos_e_apontado_no_campo()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var resposta = await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(NovaRazao(), "123"));

        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe um documento com 14 dígitos.", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_marca_o_fornecedor_como_comunidade_tradicional_e_inativar_o_tira_do_filtro_de_ativos()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var cnpj = DadosDeTeste.Digitos(14);
        var razao = NovaRazao();
        await almoxarife.EnviarAsync("/Fornecedores/Novo", "/Fornecedores/Novo", Campos(razao, cnpj));
        var lista = await almoxarife.PaginaAsync($"/Fornecedores?busca={cnpj}");
        var id = Navegador.IdDaPrimeiraEdicao(lista, "Fornecedores");

        await almoxarife.EnviarAsync($"/Fornecedores/Editar/{id}", $"/Fornecedores/Editar/{id}", Campos(razao, cnpj, comunidade: true));
        var marcado = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Fornecedores?busca={cnpj}"));
        await almoxarife.EnviarComTokenDaPaginaAsync(lista, $"/Fornecedores/AlterarAtivo/{id}", [new("ativo", "false")]);
        var ativos = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Fornecedores?busca={cnpj}&ativo=true"));

        Assert.Contains("<td>Sim</td>", marcado, StringComparison.Ordinal);
        Assert.Contains("Nenhum fornecedor encontrado.", ativos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_consulta_mas_nao_escreve_e_vendedor_nao_acessa()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var lista = Navegador.Decodificar(await gerente.PaginaAsync("/Fornecedores"));
        var escritaDoGerente = await gerente.GetAsync("/Fornecedores/Novo");
        var vendedorNaLista = await vendedor.GetAsync("/Fornecedores");

        Assert.DoesNotContain("Novo fornecedor", lista, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(escritaDoGerente), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(vendedorNaLista), StringComparison.Ordinal);
    }
}
