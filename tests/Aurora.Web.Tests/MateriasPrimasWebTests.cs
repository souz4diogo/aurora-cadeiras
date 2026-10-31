using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public class MateriasPrimasWebTests(CenarioDoPainel cenario)
{
    private static List<KeyValuePair<string, string>> Campos(
        string codigo, string nome, string unidade = "m3", string minimo = "2,5", string custo = "1234,56", string fornecedor = "") =>
    [
        new("Codigo", codigo),
        new("Nome", nome),
        new("Unidade", unidade),
        new("EstoqueMinimo", minimo),
        new("CustoUnitario", custo),
        new("FornecedorPreferencialId", fornecedor),
    ];

    private static string NovoCodigo() => ("W" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    [Fact]
    public async Task Almoxarife_cadastra_materia_prima_com_numeros_em_portugues_e_a_lista_mostra_os_valores_formatados()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var codigo = NovoCodigo();

        var envio = await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Madeira de Teste"));
        var lista = Navegador.Decodificar(await almoxarife.PaginaAsync($"/MateriasPrimas?busca={codigo}"));

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains(codigo, lista, StringComparison.Ordinal);
        Assert.Contains("2,5 m3", lista, StringComparison.Ordinal);
        Assert.Contains("1.234,56", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Materia_prima_nova_aparece_como_critica_pois_o_saldo_zero_esta_abaixo_do_minimo()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var codigo = NovoCodigo();
        await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Insumo Crítico"));

        var lista = Navegador.Decodificar(await almoxarife.PaginaAsync($"/MateriasPrimas?busca={codigo}"));

        Assert.Contains(">Crítico<", lista, StringComparison.Ordinal);
        Assert.DoesNotContain(">Normal<", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Codigo_duplicado_volta_ao_formulario_com_a_mensagem_da_api_e_a_lista_de_fornecedores()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var codigo = NovoCodigo();
        await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Primeira"));

        var resposta = await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Segunda"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Contains("Já existe uma matéria-prima com este código.", texto, StringComparison.Ordinal);
        Assert.Contains("value=\"Segunda\"", texto, StringComparison.Ordinal);
        Assert.Matches("<option[^>]*value=\"\"[^>]*>Nenhum</option>", texto);
    }

    [Fact]
    public async Task Numero_invalido_e_recusado_em_portugues_sem_chamar_a_api()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var resposta = await almoxarife.EnviarAsync(
            "/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(NovoCodigo(), "Insumo", minimo: "abc"));

        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("O valor 'abc' não é válido", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valor_negativo_e_recusado()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var resposta = await almoxarife.EnviarAsync(
            "/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(NovoCodigo(), "Insumo", custo: "-5"));

        Assert.Contains("O custo unitário não pode ser negativo.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fornecedor_preferencial_e_escolhido_numa_lista_e_volta_selecionado_na_edicao()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var cnpj = DadosDeTeste.Digitos(14);
        var razao = $"Fornecedor {Guid.NewGuid():N}"[..22];
        await almoxarife.EnviarAsync(
            "/Fornecedores/Novo", "/Fornecedores/Novo",
            [new("RazaoSocial", razao), new("Cnpj", cnpj), new("Email", ""), new("Telefone", ""), new("ComunidadeTradicional", "false")]);
        var fornecedorId = Navegador.IdDaPrimeiraEdicao(await almoxarife.PaginaAsync($"/Fornecedores?busca={cnpj}"), "Fornecedores");
        var codigo = NovoCodigo();

        var formulario = Navegador.Decodificar(await almoxarife.PaginaAsync("/MateriasPrimas/Novo"));
        await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Com fornecedor", fornecedor: fornecedorId));
        var materiaId = Navegador.IdDaPrimeiraEdicao(await almoxarife.PaginaAsync($"/MateriasPrimas?busca={codigo}"), "MateriasPrimas");
        var edicao = Navegador.Decodificar(await almoxarife.PaginaAsync($"/MateriasPrimas/Editar/{materiaId}"));

        Assert.Contains(razao, formulario, StringComparison.Ordinal);
        Assert.Matches($"<option selected=\"selected\" value=\"{fornecedorId}\">", edicao);
    }

    [Fact]
    public async Task Edicao_mostra_os_numeros_sem_zeros_a_mais_e_atualiza_a_materia_prima()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var codigo = NovoCodigo();
        await almoxarife.EnviarAsync("/MateriasPrimas/Novo", "/MateriasPrimas/Novo", Campos(codigo, "Para editar"));
        var id = Navegador.IdDaPrimeiraEdicao(await almoxarife.PaginaAsync($"/MateriasPrimas?busca={codigo}"), "MateriasPrimas");

        var formulario = Navegador.Decodificar(await almoxarife.PaginaAsync($"/MateriasPrimas/Editar/{id}"));
        var envio = await almoxarife.EnviarAsync(
            $"/MateriasPrimas/Editar/{id}", $"/MateriasPrimas/Editar/{id}", Campos(codigo, "Nome editado", minimo: "10"));
        var depois = Navegador.Decodificar(await almoxarife.PaginaAsync($"/MateriasPrimas?busca={codigo}"));

        Assert.Contains("value=\"2,5\"", formulario, StringComparison.Ordinal);
        Assert.Contains("value=\"1234,56\"", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("Nome editado", depois, StringComparison.Ordinal);
        Assert.Contains("10 m3", depois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_consulta_sem_botoes_de_escrita_e_vendedor_nao_acessa()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var lista = Navegador.Decodificar(await gerente.PaginaAsync("/MateriasPrimas"));
        var vendedorNaLista = await vendedor.GetAsync("/MateriasPrimas");

        Assert.DoesNotContain("Nova matéria-prima", lista, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(vendedorNaLista), StringComparison.Ordinal);
    }
}
