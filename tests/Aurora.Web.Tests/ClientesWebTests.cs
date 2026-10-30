using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public class ClientesWebTests(CenarioDoPainel cenario)
{
    private static List<KeyValuePair<string, string>> Campos(string nome, string documento, string? email = null, string cidade = "Campinas", string uf = "SP") =>
    [
        new("Nome", nome),
        new("CpfCnpj", documento),
        new("Email", email ?? ""),
        new("Telefone", ""),
        new("Cidade", cidade),
        new("Uf", uf),
    ];

    private static async Task<string> TextoAsync(HttpResponseMessage resposta) =>
        Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());

    private static string NovoNome() => $"Cliente {Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task Vendedor_cadastra_cliente_e_ve_a_confirmacao_e_o_documento_formatado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var nome = NovoNome();
        var cpf = DadosDeTeste.Digitos(11);

        var envio = await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(nome, cpf, "contato@lima.com"));
        var lista = await vendedor.PaginaAsync($"/Clientes?busca={cpf}");

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Equal("/Clientes", Navegador.Destino(envio));
        var texto = Navegador.Decodificar(lista);
        Assert.Contains(nome, texto, StringComparison.Ordinal);
        Assert.Contains($"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}", texto, StringComparison.Ordinal);
        Assert.Contains("Campinas/SP", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmacao_de_sucesso_aparece_uma_vez_em_regiao_anunciada_por_leitor_de_tela()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var nome = NovoNome();
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(nome, DadosDeTeste.Digitos(11)));

        var primeira = Navegador.Decodificar(await vendedor.PaginaAsync("/Clientes"));
        var segunda = Navegador.Decodificar(await vendedor.PaginaAsync("/Clientes"));

        Assert.Contains($"Cliente {nome} cadastrado.", primeira, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", primeira, StringComparison.Ordinal);
        Assert.DoesNotContain("cadastrado.", segunda, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Documento_duplicado_volta_ao_formulario_com_a_mensagem_da_api_e_os_dados_digitados()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cpf = DadosDeTeste.Digitos(11);
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(NovoNome(), cpf));

        var resposta = await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos("Outro Nome", cpf, cidade: "Santos"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = await TextoAsync(resposta);
        Assert.Contains("Já existe um cliente com este CPF/CNPJ.", texto, StringComparison.Ordinal);
        Assert.Contains("value=\"Outro Nome\"", texto, StringComparison.Ordinal);
        Assert.Contains("value=\"Santos\"", texto, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Campo_obrigatorio_vazio_e_apontado_sem_chamar_a_api()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var resposta = await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos("", DadosDeTeste.Digitos(11)));

        var texto = await TextoAsync(resposta);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe o nome.", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erro_de_validacao_da_api_aparece_no_campo_ligado_por_aria_describedby()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var resposta = await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(NovoNome(), "12345"));

        var texto = await TextoAsync(resposta);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe um documento com 11 ou 14 dígitos.", texto, StringComparison.Ordinal);
        Assert.Matches("aria-describedby=\"CpfCnpj-dica CpfCnpj-erro\"", texto);
        Assert.Matches("id=\"CpfCnpj-erro\"", texto);
    }

    [Fact]
    public async Task Editar_atualiza_o_cliente_e_mostra_o_nome_novo_na_lista()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cpf = DadosDeTeste.Digitos(11);
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(NovoNome(), cpf));
        var lista = await vendedor.PaginaAsync($"/Clientes?busca={cpf}");
        var id = System.Text.RegularExpressions.Regex.Match(lista, @"/Clientes/Editar/(\d+)").Groups[1].Value;
        var novoNome = NovoNome() + " (editado)";

        var formulario = Navegador.Decodificar(await vendedor.PaginaAsync($"/Clientes/Editar/{id}"));
        var envio = await vendedor.EnviarAsync($"/Clientes/Editar/{id}", $"/Clientes/Editar/{id}", Campos(novoNome, cpf));
        var depois = Navegador.Decodificar(await vendedor.PaginaAsync($"/Clientes?busca={cpf}"));

        Assert.Contains($"value=\"{cpf}\"", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains(novoNome, depois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_cliente_inexistente_mostra_a_pagina_de_nao_encontrado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        using var resposta = await vendedor.GetAsync("/Clientes/Editar/987654");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Contains("Registro não encontrado", await TextoAsync(resposta), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inativar_e_ativar_alteram_a_situacao_na_lista_e_no_filtro()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cpf = DadosDeTeste.Digitos(11);
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(NovoNome(), cpf));
        var lista = await vendedor.PaginaAsync($"/Clientes?busca={cpf}");
        var id = System.Text.RegularExpressions.Regex.Match(lista, @"/Clientes/Editar/(\d+)").Groups[1].Value;

        var inativar = await vendedor.EnviarComTokenDaPaginaAsync(lista, $"/Clientes/AlterarAtivo/{id}", [new("ativo", "false")]);
        var ativos = Navegador.Decodificar(await vendedor.PaginaAsync($"/Clientes?busca={cpf}&ativo=true"));
        var inativos = Navegador.Decodificar(await vendedor.PaginaAsync($"/Clientes?busca={cpf}&ativo=false"));

        Assert.Equal(HttpStatusCode.Redirect, inativar.StatusCode);
        Assert.Contains("Nenhum cliente encontrado.", ativos, StringComparison.Ordinal);
        Assert.Contains(">Inativo<", inativos, StringComparison.Ordinal);
        Assert.Contains("Ativar", inativos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_consulta_a_lista_mas_nao_ve_os_botoes_de_escrita_nem_consegue_cadastrar()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var lista = Navegador.Decodificar(await gerente.PaginaAsync("/Clientes"));
        var tentativa = await gerente.GetAsync("/Clientes/Novo");

        Assert.DoesNotContain("Novo cliente", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("Inativar", lista, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, tentativa.StatusCode);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(tentativa), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Almoxarife_nao_acessa_clientes_e_ve_a_pagina_de_acesso_negado()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var resposta = await almoxarife.GetAsync("/Clientes");
        var pagina = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(resposta)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("Acesso negado", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cadastro_sem_o_token_anti_csrf_e_recusado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var resposta = await vendedor.EnviarSemTokenAsync("/Clientes/Novo", Campos(NovoNome(), DadosDeTeste.Digitos(11)));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Busca_filtra_a_lista_por_nome()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var nome = NovoNome();
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(nome, DadosDeTeste.Digitos(11)));
        await vendedor.EnviarAsync("/Clientes/Novo", "/Clientes/Novo", Campos(NovoNome(), DadosDeTeste.Digitos(11)));

        var encontrados = Navegador.Decodificar(await vendedor.PaginaAsync($"/Clientes?busca={Uri.EscapeDataString(nome)}"));

        Assert.Contains(nome, encontrados, StringComparison.Ordinal);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(encontrados, "/Clientes/Editar/"));
    }
}
