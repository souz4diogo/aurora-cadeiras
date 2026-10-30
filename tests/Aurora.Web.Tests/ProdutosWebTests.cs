using System.Net;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public partial class ProdutosWebTests(CenarioDoPainel cenario)
{
    /// <summary>Roteiro digitado no teste de posições: Qualidade (1), Corte (2) e Estofaria (3).</summary>
    private static readonly string[] OrdemDoRoteiroDigitado = ["Qualidade", "Corte", "Estofaria"];

    private static readonly string[] NomesDasEstacoes = ["Corte", "Montagem da Estrutura", "Estofaria", "Montagem Final", "Qualidade"];

    private static string NovoCodigo() => ("P" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    private static List<KeyValuePair<string, string>> CamposDoProduto(string codigo, string nome, string preco = "350,75") =>
    [
        new("Codigo", codigo),
        new("Nome", nome),
        new("LinhaProdutoId", "1"),
        new("PrecoVenda", preco),
    ];

    /// <summary>Campos do editor de roteiro: cada tupla é (estação, marcada, posição).</summary>
    private static List<KeyValuePair<string, string>> CamposDoRoteiro(string nomeDoProduto, params (int Estacao, bool Incluir, string Posicao)[] linhas)
    {
        var campos = new List<KeyValuePair<string, string>> { new("ProdutoNome", nomeDoProduto) };
        for (var i = 0; i < NomesDasEstacoes.Length; i++)
        {
            var linha = linhas.FirstOrDefault(l => l.Estacao == i + 1);
            campos.Add(new($"Estacoes[{i}].EstacaoId", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            campos.Add(new($"Estacoes[{i}].Nome", NomesDasEstacoes[i]));
            if (linha.Incluir)
            {
                campos.Add(new($"Estacoes[{i}].Incluir", "true"));
            }

            campos.Add(new($"Estacoes[{i}].Posicao", linha.Posicao ?? ""));
        }

        return campos;
    }

    private static List<KeyValuePair<string, string>> CamposDaFicha(string nomeDoProduto, params (string Materia, int Estacao, string Quantidade)[] itens)
    {
        var campos = new List<KeyValuePair<string, string>> { new("ProdutoNome", nomeDoProduto) };
        for (var i = 0; i < itens.Length; i++)
        {
            campos.Add(new($"Itens[{i}].MateriaPrimaId", itens[i].Materia));
            campos.Add(new($"Itens[{i}].EstacaoId", itens[i].Estacao.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            campos.Add(new($"Itens[{i}].Quantidade", itens[i].Quantidade));
        }

        return campos;
    }

    [GeneratedRegex(@"<option value=""(\d+)"">MP-001 - ")]
    private static partial Regex OpcaoDaMateriaPrimaSeed();

    private static string IdDaMateriaPrimaDoSeed(string html) => OpcaoDaMateriaPrimaSeed().Match(html).Groups[1].Value;

    /// <summary>Cria um produto pelo painel e devolve (id, nome); o produto nasce sem roteiro nem ficha.</summary>
    private static async Task<(string Id, string Nome)> CriarProdutoAsync(Navegador gerente)
    {
        var codigo = NovoCodigo();
        var nome = $"Cadeira {codigo}";
        var envio = await gerente.EnviarAsync("/Produtos/Novo", "/Produtos/Novo", CamposDoProduto(codigo, nome));
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        var id = Regex.Match(Navegador.Destino(envio), @"/Produtos/Detalhes/(\d+)").Groups[1].Value;
        Assert.NotEqual("", id);
        return (id, nome);
    }

    [Fact]
    public async Task Gerente_cadastra_produto_e_cai_nos_detalhes_com_aviso_de_roteiro_e_ficha_pendentes()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var (id, nome) = await CriarProdutoAsync(gerente);
        var detalhes = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Detalhes/{id}"));

        Assert.Contains(nome, detalhes, StringComparison.Ordinal);
        Assert.Contains("350,75", detalhes, StringComparison.Ordinal);
        Assert.Contains("ainda não tem roteiro", detalhes, StringComparison.Ordinal);
        Assert.Contains("ainda não tem ficha técnica", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_mostra_os_produtos_do_seed_com_preco_em_reais()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var lista = Navegador.Decodificar(await gerente.PaginaAsync("/Produtos?busca=CE-100"));

        Assert.Contains("CE-100", lista, StringComparison.Ordinal);
        Assert.Contains("1.290,00", lista, StringComparison.Ordinal);
        Assert.Contains("Novo produto", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Codigo_duplicado_volta_ao_formulario_com_a_mensagem_da_api()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var resposta = await gerente.EnviarAsync("/Produtos/Novo", "/Produtos/Novo", CamposDoProduto("CE-100", "Outro produto"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Já existe um produto com este código.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preco_com_mais_de_duas_casas_decimais_e_apontado_pela_api()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var resposta = await gerente.EnviarAsync("/Produtos/Novo", "/Produtos/Novo", CamposDoProduto(NovoCodigo(), "Produto", preco: "10,999"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("PrecoVenda", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Roteiro_segue_a_posicao_informada_e_aparece_nos_detalhes_nessa_ordem()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        var editor = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Roteiro/{id}"));

        // Qualidade na posição 1 e Corte na 2: a ordem digitada vale, não a ordem da lista
        var envio = await gerente.EnviarAsync(
            $"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}",
            CamposDoRoteiro(nome, (5, true, "1"), (1, true, "2"), (3, true, "3")));
        var detalhes = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Detalhes/{id}"));

        Assert.All(NomesDasEstacoes, estacao => Assert.Contains(estacao, editor, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        var ordem = OrdemDoRoteiroDigitado.Select(e => detalhes.IndexOf($">{e}</li>", StringComparison.Ordinal)).ToArray();
        Assert.All(ordem, posicao => Assert.True(posicao > 0));
        Assert.Equal(ordem.Order(), ordem);
    }

    [Fact]
    public async Task Roteiro_sem_nenhuma_estacao_marcada_e_recusado_sem_chamar_a_api()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);

        var resposta = await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Marque ao menos uma estação para o roteiro.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Posicao_fora_do_limite_e_apontada_na_linha()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);

        var resposta = await gerente.EnviarAsync(
            $"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "0")));

        Assert.Contains("A posição deve ficar entre 1 e 99.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ficha_tecnica_sem_roteiro_orienta_a_definir_o_roteiro_primeiro()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, _) = await CriarProdutoAsync(gerente);

        var editor = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}"));

        Assert.Contains("ainda não tem roteiro", editor, StringComparison.Ordinal);
        Assert.Contains("Defina o roteiro primeiro.", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Salvar ficha técnica", editor, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ficha_tecnica_e_gravada_com_quantidades_em_portugues_e_aparece_nos_detalhes()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1"), (2, true, "2")));
        var editor = await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}");
        var madeira = IdDaMateriaPrimaDoSeed(editor);

        var envio = await gerente.EnviarAsync(
            $"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}",
            CamposDaFicha(nome, (madeira, 1, "0,025"), (madeira, 2, "1,5")));
        var detalhes = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Detalhes/{id}"));

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("MP-001", detalhes, StringComparison.Ordinal);
        Assert.Contains("0,025", detalhes, StringComparison.Ordinal);
        Assert.Contains("1,5", detalhes, StringComparison.Ordinal);
        Assert.Contains("Corte", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editor_da_ficha_traz_a_linha_modelo_e_o_script_sem_codigo_embutido()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1")));

        var editor = await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}");
        using var script = await gerente.GetAsync("/js/linhas-dinamicas.js");

        Assert.Contains("<template data-modelo>", editor, StringComparison.Ordinal);
        Assert.Contains("Itens[__INDICE__].MateriaPrimaId", editor, StringComparison.Ordinal);
        Assert.Contains("data-adicionar-linha", editor, StringComparison.Ordinal);
        Assert.Contains("data-remover-linha", editor, StringComparison.Ordinal);
        Assert.Contains("id=\"anuncio-linhas\"", editor, StringComparison.Ordinal);
        Assert.Matches(@"<script src=""/js/linhas-dinamicas\.js", editor);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", editor);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
    }

    [Fact]
    public async Task Substituir_a_ficha_troca_tudo_e_enviar_sem_linhas_limpa_a_ficha()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1"), (2, true, "2")));
        var madeira = IdDaMateriaPrimaDoSeed(await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}"));
        await gerente.EnviarAsync($"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}", CamposDaFicha(nome, (madeira, 1, "1"), (madeira, 2, "2")));

        await gerente.EnviarAsync($"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}", CamposDaFicha(nome, (madeira, 2, "3")));
        var umaLinha = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Detalhes/{id}"));
        await gerente.EnviarAsync($"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}", CamposDaFicha(nome));
        var limpa = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos/Detalhes/{id}"));

        Assert.Equal(1, Regex.Count(umaLinha, "MP-001 - "));
        Assert.Contains("ainda não tem ficha técnica", limpa, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ficha_com_quantidade_zero_e_linha_sem_material_sao_apontadas_e_os_dados_digitados_continuam()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1")));
        var madeira = IdDaMateriaPrimaDoSeed(await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}"));

        var resposta = await gerente.EnviarAsync(
            $"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}",
            CamposDaFicha(nome, (madeira, 1, "0"), ("", 1, "abc")));

        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("A quantidade deve ser maior que zero.", texto, StringComparison.Ordinal);
        Assert.Contains("Escolha a matéria-prima.", texto, StringComparison.Ordinal);
        Assert.Contains("value=\"abc\"", texto, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{madeira}\" selected", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Estacao_fora_do_roteiro_enviada_a_forca_mostra_a_regra_da_api_no_resumo()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1")));
        var madeira = IdDaMateriaPrimaDoSeed(await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}"));

        var resposta = await gerente.EnviarAsync(
            $"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}", CamposDaFicha(nome, (madeira, 4, "1")));

        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("não faz parte do roteiro do produto", texto, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retirar_do_roteiro_uma_estacao_usada_pela_ficha_mostra_a_regra_da_api()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1"), (2, true, "2")));
        var madeira = IdDaMateriaPrimaDoSeed(await gerente.PaginaAsync($"/Produtos/FichaTecnica/{id}"));
        await gerente.EnviarAsync($"/Produtos/FichaTecnica/{id}", $"/Produtos/FichaTecnica/{id}", CamposDaFicha(nome, (madeira, 2, "1")));

        var resposta = await gerente.EnviarAsync($"/Produtos/Roteiro/{id}", $"/Produtos/Roteiro/{id}", CamposDoRoteiro(nome, (1, true, "1")));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("ajuste a ficha antes de retirá-la do roteiro", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_atualiza_o_preco_e_inativar_tira_o_produto_do_filtro_de_ativos()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, nome) = await CriarProdutoAsync(gerente);
        var codigo = nome["Cadeira ".Length..];

        var edicao = await gerente.EnviarAsync($"/Produtos/Editar/{id}", $"/Produtos/Editar/{id}", CamposDoProduto(codigo, nome, preco: "1290,50"));
        var lista = await gerente.PaginaAsync($"/Produtos?busca={codigo}");
        await gerente.EnviarComTokenDaPaginaAsync(lista, $"/Produtos/AlterarAtivo/{id}", [new("ativo", "false")]);
        var ativos = Navegador.Decodificar(await gerente.PaginaAsync($"/Produtos?busca={codigo}&ativo=true"));

        Assert.Equal(HttpStatusCode.Redirect, edicao.StatusCode);
        Assert.Contains("1.290,50", Navegador.Decodificar(lista), StringComparison.Ordinal);
        Assert.Contains("Nenhum produto encontrado.", ativos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operador_consulta_produtos_e_detalhes_sem_nenhum_botao_de_escrita_e_nao_edita()
    {
        using var operador = await cenario.EntrarComoAsync("Operador");

        var lista = Navegador.Decodificar(await operador.PaginaAsync("/Produtos?busca=CE-100"));
        var listaDeProdutos = Navegador.Decodificar(await operador.PaginaAsync("/Produtos"));
        var id = Navegador.IdDaPrimeiraDetalhe(listaDeProdutos);
        var detalhes = Navegador.Decodificar(await operador.PaginaAsync($"/Produtos/Detalhes/{id}"));
        var novo = await operador.GetAsync("/Produtos/Novo");
        var roteiro = await operador.GetAsync($"/Produtos/Roteiro/{id}");

        Assert.Contains("CE-100", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("Novo produto", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("Editar", detalhes, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(novo), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(roteiro), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalhes_de_produto_inexistente_mostram_a_pagina_de_nao_encontrado()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        using var resposta = await gerente.GetAsync("/Produtos/Detalhes/987654");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
