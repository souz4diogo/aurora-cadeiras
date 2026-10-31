using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;
using Microsoft.Data.SqlClient;

namespace Aurora.Web.Tests;

[Collection("painel")]
public partial class PedidosWebTests(CenarioDoPainel cenario)
{
    private static string Prazo(int dias = 30) => DateTime.UtcNow.AddDays(dias).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"<option value=""(\d+)"" data-preco=""[\d.]+""[^>]*>(CE-100|CJ-100) - ")]
    private static partial Regex OpcaoDeProdutoDoSeed();

    [GeneratedRegex(@"PV-\d{4}-\d{5}")]
    private static partial Regex NumeroDoPedido();

    [GeneratedRegex(@"/Pedidos/Detalhes/(\d+)")]
    private static partial Regex LinkDoPedido();

    private static List<KeyValuePair<string, string>> Campos(string cliente, string prazo, params (string Produto, string Quantidade)[] itens)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("Numero", ""),
            new("ClienteId", cliente),
            new("PrazoEntrega", prazo),
            new("Observacao", "Entrega parcelada"),
        };
        for (var i = 0; i < itens.Length; i++)
        {
            campos.Add(new($"Itens[{i}].ProdutoId", itens[i].Produto));
            campos.Add(new($"Itens[{i}].Quantidade", itens[i].Quantidade));
        }

        return campos;
    }

    /// <summary>Cria um cliente pelo painel e devolve o id.</summary>
    private static async Task<string> NovoClienteAsync(Navegador navegador)
    {
        var cpf = DadosDeTeste.Digitos(11);
        await navegador.EnviarAsync(
            "/Clientes/Novo", "/Clientes/Novo",
            [new("Nome", $"Cliente {cpf}"), new("CpfCnpj", cpf), new("Email", ""), new("Telefone", ""), new("Cidade", ""), new("Uf", "")]);
        return Navegador.IdDaPrimeiraEdicao(await navegador.PaginaAsync($"/Clientes?busca={cpf}"), "Clientes");
    }

    /// <summary>Ids dos produtos CE-100 e CJ-100 (seed), lidos da lista de escolha do formulário.</summary>
    private static async Task<(string Ce100, string Cj100)> ProdutosDoSeedAsync(Navegador navegador)
    {
        var formulario = await navegador.PaginaAsync("/Pedidos/Novo");
        // O produto aparece na linha inicial e na linha modelo: vale a primeira ocorrência
        var opcoes = OpcaoDeProdutoDoSeed().Matches(formulario)
            .GroupBy(m => m.Groups[2].Value)
            .ToDictionary(g => g.Key, g => g.First().Groups[1].Value);
        return (opcoes["CE-100"], opcoes["CJ-100"]);
    }

    /// <summary>Cria um pedido de 2 CE-100 + 1 CJ-100 e devolve (id, número).</summary>
    private static async Task<(string Id, string Numero)> NovoPedidoAsync(Navegador vendedor)
    {
        var cliente = await NovoClienteAsync(vendedor);
        var (ce100, cj100) = await ProdutosDoSeedAsync(vendedor);
        var envio = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(), (ce100, "2"), (cj100, "1")));
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        var id = Regex.Match(Navegador.Destino(envio), @"/Pedidos/Detalhes/(\d+)").Groups[1].Value;
        var detalhes = await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}");
        return (id, NumeroDoPedido().Match(detalhes).Value);
    }

    [Fact]
    public async Task Vendedor_cria_pedido_e_ve_numero_itens_status_e_total_calculado_pela_api()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cliente = await NovoClienteAsync(vendedor);
        var (ce100, cj100) = await ProdutosDoSeedAsync(vendedor);

        var envio = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(), (ce100, "2"), (cj100, "1")));
        var id = Regex.Match(Navegador.Destino(envio), @"/Pedidos/Detalhes/(\d+)").Groups[1].Value;
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Matches(NumeroDoPedido(), detalhes);
        Assert.Contains("Aberto", detalhes, StringComparison.Ordinal);
        Assert.Contains("CE-100", detalhes, StringComparison.Ordinal);
        Assert.Contains("CJ-100", detalhes, StringComparison.Ordinal);
        Assert.Contains("3.060,00", detalhes, StringComparison.Ordinal); // 2 x 1.290,00 + 1 x 480,00
        Assert.Contains("Entrega parcelada", detalhes, StringComparison.Ordinal);
        Assert.Contains("Editar pedido", detalhes, StringComparison.Ordinal);
        Assert.Contains("Cancelar pedido", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formulario_traz_precos_total_estimado_linha_modelo_e_scripts_sem_codigo_embutido()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var formulario = await vendedor.PaginaAsync("/Pedidos/Novo");
        using var scriptDoTotal = await vendedor.GetAsync("/js/total-do-pedido.js");

        Assert.Matches(@"data-preco=""1290\.00""", formulario);
        Assert.Contains("id=\"total-pedido\"", formulario, StringComparison.Ordinal);
        Assert.Contains("<template data-modelo>", formulario, StringComparison.Ordinal);
        Assert.Contains("data-adicionar-linha", formulario, StringComparison.Ordinal);
        Assert.Matches(@"<script src=""/js/total-do-pedido\.js", formulario);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", formulario);
        Assert.Matches(@"type=""date""", formulario);
        Assert.Equal(HttpStatusCode.OK, scriptDoTotal.StatusCode);
    }

    [Fact]
    public async Task Campos_obrigatorios_em_branco_e_quantidade_fora_do_limite_sao_apontados_sem_chamar_a_api()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        var resposta = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos("", "", ("", "0")));

        var texto = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Escolha o cliente.", texto, StringComparison.Ordinal);
        Assert.Contains("Informe o prazo de entrega.", texto, StringComparison.Ordinal);
        Assert.Contains("Escolha o produto.", texto, StringComparison.Ordinal);
        Assert.Contains("A quantidade deve ficar entre 1 e 100.000.", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_sem_nenhum_item_e_recusado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cliente = await NovoClienteAsync(vendedor);

        var resposta = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo()));

        Assert.Contains("Adicione ao menos um item ao pedido.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Regras_da_api_prazo_no_passado_e_produto_repetido_aparecem_no_formulario_com_os_dados_mantidos()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cliente = await NovoClienteAsync(vendedor);
        var (ce100, _) = await ProdutosDoSeedAsync(vendedor);

        var passado = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(-10), (ce100, "1")));
        var repetido = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(), (ce100, "1"), (ce100, "2")));

        var textoDoPassado = Navegador.Decodificar(await passado.Content.ReadAsStringAsync());
        var textoDoRepetido = Navegador.Decodificar(await repetido.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, passado.StatusCode);
        Assert.Contains("O prazo de entrega não pode estar no passado.", textoDoPassado, StringComparison.Ordinal);
        Assert.Contains($"value=\"{Prazo(-10)}\"", textoDoPassado, StringComparison.Ordinal);
        Assert.Contains("O mesmo produto não pode aparecer duas vezes", textoDoRepetido, StringComparison.Ordinal);
        Assert.Contains("value=\"2\"", textoDoRepetido, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_troca_quantidade_e_itens_e_recalcula_o_total()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var (id, _) = await NovoPedidoAsync(vendedor);
        var formulario = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Editar/{id}"));
        var (ce100, _) = await ProdutosDoSeedAsync(vendedor);
        var cliente = Regex.Match(formulario, @"<option selected=""selected"" value=""(\d+)"">").Groups[1].Value;
        Assert.NotEqual("", cliente);

        var envio = await vendedor.EnviarAsync($"/Pedidos/Editar/{id}", $"/Pedidos/Editar/{id}", Campos(cliente, Prazo(45), (ce100, "5")));
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Contains("CE-100", formulario, StringComparison.Ordinal);
        Assert.Contains("value=\"2\"", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("6.450,00", detalhes, StringComparison.Ordinal); // 5 x 1.290,00
        Assert.DoesNotContain("CJ-100", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelar_pede_confirmacao_depois_cancela_e_some_com_os_botoes_de_edicao()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var (id, numero) = await NovoPedidoAsync(vendedor);

        var confirmacao = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Cancelar/{id}"));
        var envio = await vendedor.EnviarAsync($"/Pedidos/Cancelar/{id}", $"/Pedidos/Cancelar/{id}", []);
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Contains($"Cancelar pedido {numero}?", confirmacao, StringComparison.Ordinal);
        Assert.Contains("Esta ação não pode ser desfeita.", confirmacao, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("Cancelado", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("Editar pedido", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar pedido", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_cancelado_nao_abre_para_edicao_e_o_usuario_e_avisado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var (id, _) = await NovoPedidoAsync(vendedor);
        await vendedor.EnviarAsync($"/Pedidos/Cancelar/{id}", $"/Pedidos/Cancelar/{id}", []);

        var tentativa = await vendedor.GetAsync($"/Pedidos/Editar/{id}");
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync(Navegador.Destino(tentativa)));

        Assert.Equal(HttpStatusCode.Redirect, tentativa.StatusCode);
        Assert.Contains("não pode mais ser editado", detalhes, StringComparison.Ordinal);
        Assert.Contains("data-alerta=\"aviso\"", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelar_de_novo_um_pedido_ja_cancelado_mostra_a_regra_da_procedure()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var (id, _) = await NovoPedidoAsync(vendedor);
        await vendedor.EnviarAsync($"/Pedidos/Cancelar/{id}", $"/Pedidos/Cancelar/{id}", []);

        var resposta = await vendedor.EnviarAsync($"/Pedidos/Cancelar/{id}", $"/Pedidos/Cancelar/{id}", []);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("não permite o cancelamento", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vendedor_nao_abre_pedido_de_outro_vendedor_mas_o_gerente_abre()
    {
        using var dono = await cenario.EntrarComoAsync("Vendedor");
        using var outro = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var (id, numero) = await NovoPedidoAsync(dono);

        using var doOutro = await outro.GetAsync($"/Pedidos/Detalhes/{id}");
        var edicaoDoOutro = await outro.GetAsync($"/Pedidos/Editar/{id}");
        var listaDoOutro = await outro.PaginaAsync("/Pedidos");
        var listaDoGerente = await gerente.PaginaAsync("/Pedidos?pageSize=100");

        Assert.Equal(HttpStatusCode.NotFound, doOutro.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edicaoDoOutro.StatusCode);
        Assert.DoesNotContain(numero, listaDoOutro, StringComparison.Ordinal);
        Assert.Contains($"/Pedidos/Detalhes/{id}\"", listaDoGerente, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_e_almoxarife_consultam_sem_botoes_de_escrita_e_nao_criam_nem_cancelam()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var (id, _) = await NovoPedidoAsync(vendedor);

        var listaDoGerente = Navegador.Decodificar(await gerente.PaginaAsync("/Pedidos"));
        var detalhesDoGerente = Navegador.Decodificar(await gerente.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        var listaDoAlmoxarife = Navegador.Decodificar(await almoxarife.PaginaAsync("/Pedidos"));
        var novoDoAlmoxarife = await almoxarife.GetAsync("/Pedidos/Novo");
        var cancelarDoGerente = await gerente.GetAsync($"/Pedidos/Cancelar/{id}");

        Assert.DoesNotContain("Novo pedido", listaDoGerente, StringComparison.Ordinal);
        Assert.DoesNotContain("Editar pedido", detalhesDoGerente, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar pedido", detalhesDoGerente, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"clienteId\"", listaDoAlmoxarife, StringComparison.Ordinal); // sem permissão em clientes, sem filtro
        Assert.Contains("name=\"clienteId\"", listaDoGerente, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(novoDoAlmoxarife), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(cancelarDoGerente), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operador_nao_acessa_pedidos()
    {
        using var operador = await cenario.EntrarComoAsync("Operador");

        var resposta = await operador.GetAsync("/Pedidos");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(resposta), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_filtra_por_status_cliente_e_periodo_e_marca_os_atrasados()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var cliente = await NovoClienteAsync(vendedor);
        var (ce100, _) = await ProdutosDoSeedAsync(vendedor);
        var envioA = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(), (ce100, "1")));
        var envioB = await vendedor.EnviarAsync("/Pedidos/Novo", "/Pedidos/Novo", Campos(cliente, Prazo(), (ce100, "1")));
        var idA = Regex.Match(Navegador.Destino(envioA), @"\d+$").Value;
        var idB = Regex.Match(Navegador.Destino(envioB), @"\d+$").Value;
        await vendedor.EnviarAsync($"/Pedidos/Cancelar/{idB}", $"/Pedidos/Cancelar/{idB}", []);

        // Simula um pedido confirmado e vencido (a confirmação em si é da fase 3)
        await using (var conexao = new SqlConnection(cenario.Api.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "UPDATE dbo.Pedido SET Status = 'Confirmado', PrazoEntrega = DATEADD(DAY, -3, CAST(SYSUTCDATETIME() AS date)) WHERE PedidoId = @id";
            comando.Parameters.AddWithValue("@id", int.Parse(idA, CultureInfo.InvariantCulture));
            await comando.ExecuteNonQueryAsync();
        }

        var hoje = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var doCliente = await vendedor.PaginaAsync($"/Pedidos?clienteId={cliente}");
        var cancelados = await vendedor.PaginaAsync($"/Pedidos?clienteId={cliente}&status=Cancelado");
        var atrasados = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos?clienteId={cliente}&atrasados=true"));
        var depoisDeHoje = await vendedor.PaginaAsync($"/Pedidos?clienteId={cliente}&de={DateTime.UtcNow.AddDays(2):yyyy-MM-dd}");
        var hojeNoPeriodo = await vendedor.PaginaAsync($"/Pedidos?clienteId={cliente}&de={hoje}&ate={hoje}");

        Assert.Equal(2, LinkDoPedido().Count(doCliente));
        Assert.Equal([idB], LinkDoPedido().Matches(cancelados).Select(m => m.Groups[1].Value));
        Assert.Equal([idA], LinkDoPedido().Matches(atrasados).Select(m => m.Groups[1].Value));
        Assert.Contains("Atrasado", atrasados, StringComparison.Ordinal);
        Assert.Contains("Confirmado", atrasados, StringComparison.Ordinal);
        Assert.Empty(LinkDoPedido().Matches(depoisDeHoje));
        Assert.Equal(2, LinkDoPedido().Count(hojeNoPeriodo));
    }

    [Fact]
    public async Task Pedido_inexistente_mostra_a_pagina_de_nao_encontrado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");

        using var resposta = await vendedor.GetAsync("/Pedidos/Detalhes/987654");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
