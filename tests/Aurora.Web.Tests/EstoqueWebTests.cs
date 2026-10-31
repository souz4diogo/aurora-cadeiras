using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed partial class EstoqueWebTests(CenarioDoPainel cenario)
{
    [GeneratedRegex(@"/Estoque/LerAlerta/(\d+)")]
    private static partial Regex AcaoDeLerAlerta();

    private static List<KeyValuePair<string, string>> Ajuste(int materiaPrimaId, string quantidade, string motivo) =>
    [
        new("MateriaPrimaId", materiaPrimaId.ToString(CultureInfo.InvariantCulture)),
        new("NovaQuantidade", quantidade),
        new("Motivo", motivo),
    ];

    [Fact]
    public async Task Saldos_mostram_o_critico_com_texto_e_filtram_por_criticidade_e_busca()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var critica = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 5m, estoqueMinimo: 5m);
        var folgada = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 50m, estoqueMinimo: 5m);

        async Task<string> ListarAsync(string criticidade, ProdutoDeProducao material) =>
            Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque?critico={criticidade}&busca={material.MateriaPrimaCodigo}"));

        var criticaNosCriticos = await ListarAsync("true", critica);
        var criticaNosNormais = await ListarAsync("false", critica);
        var folgadaNosNormais = await ListarAsync("false", folgada);
        var folgadaNosCriticos = await ListarAsync("true", folgada);

        Assert.Contains(critica.MateriaPrimaCodigo, criticaNosCriticos, StringComparison.Ordinal);
        Assert.Contains(">Crítico<", criticaNosCriticos, StringComparison.Ordinal);
        Assert.Contains("Nenhuma matéria-prima encontrada.", criticaNosNormais, StringComparison.Ordinal);
        Assert.Contains(folgada.MateriaPrimaCodigo, folgadaNosNormais, StringComparison.Ordinal);
        Assert.Contains(">Normal<", folgadaNosNormais, StringComparison.Ordinal);
        Assert.Contains("<td class=\"numero\">50</td>", folgadaNosNormais, StringComparison.Ordinal);
        Assert.Contains("Nenhuma matéria-prima encontrada.", folgadaNosCriticos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Almoxarife_ajusta_o_saldo_e_o_movimento_aparece_no_extrato_com_sinal_e_motivo()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 10m);
        var formulario = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Ajustar?materiaPrimaId={produto.MateriaPrimaId}"));

        var envio = await almoxarife.EnviarAsync(
            "/Estoque/Ajustar", "/Estoque/Ajustar", Ajuste(produto.MateriaPrimaId, "7,5", "Contagem física de outubro"));
        var saldos = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(envio)));
        var extrato = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Movimentos?materiaPrimaId={produto.MateriaPrimaId}"));

        Assert.Contains("saldo 10 m2", formulario, StringComparison.Ordinal);
        Assert.Contains("selected=\"selected\"", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains($"{produto.MateriaPrimaNome} ajustada de 10 para 7,5 m2 (-2,5).", saldos, StringComparison.Ordinal);
        Assert.Contains("Contagem física de outubro", extrato, StringComparison.Ordinal);
        Assert.Contains("-2,5", extrato, StringComparison.Ordinal);
        Assert.Contains("<td>Ajuste</td>", extrato, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ajuste_sem_motivo_ou_com_saldo_negativo_e_apontado_sem_chamar_a_api()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 10m);

        var semMotivo = await almoxarife.EnviarAsync("/Estoque/Ajustar", "/Estoque/Ajustar", Ajuste(produto.MateriaPrimaId, "5", ""));
        var negativo = await almoxarife.EnviarAsync("/Estoque/Ajustar", "/Estoque/Ajustar", Ajuste(produto.MateriaPrimaId, "-1", "Motivo"));
        var vazio = await almoxarife.EnviarAsync("/Estoque/Ajustar", "/Estoque/Ajustar", []);

        Assert.Equal(HttpStatusCode.OK, semMotivo.StatusCode);
        Assert.Contains("Informe o motivo do ajuste.", Navegador.Decodificar(await semMotivo.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Contains("O saldo contado não pode ser negativo.", Navegador.Decodificar(await negativo.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        var textoVazio = Navegador.Decodificar(await vazio.Content.ReadAsStringAsync());
        Assert.Contains("Escolha a matéria-prima.", textoVazio, StringComparison.Ordinal);
        Assert.Contains("Informe o saldo contado.", textoVazio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ajustar_para_o_mesmo_saldo_avisa_que_nada_foi_alterado()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 10m);

        var envio = await almoxarife.EnviarAsync("/Estoque/Ajustar", "/Estoque/Ajustar", Ajuste(produto.MateriaPrimaId, "10", "Conferência"));
        var pagina = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(envio)));

        Assert.Contains("nada foi alterado", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apenas_o_almoxarife_ajusta_o_saldo_o_gerente_e_o_administrador_so_consultam()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var admin = await cenario.EntrarComoAsync("Administrador");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var doGerente = await gerente.GetAsync("/Estoque/Ajustar");
        var doAdmin = await admin.GetAsync("/Estoque/Ajustar");
        var saldosDoGerente = Navegador.Decodificar(await gerente.PaginaAsync("/Estoque"));
        var saldosDoAlmoxarife = Navegador.Decodificar(await almoxarife.PaginaAsync("/Estoque"));

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(doGerente), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(doAdmin), StringComparison.Ordinal);
        Assert.DoesNotContain("Ajustar saldo", saldosDoGerente, StringComparison.Ordinal);
        Assert.Contains("Ajustar saldo", saldosDoAlmoxarife, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("Operador")]
    public async Task Perfis_sem_acesso_ao_estoque_sao_barrados(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        foreach (var rota in new[] { "/Estoque", "/Estoque/Produtos", "/Estoque/Movimentos", "/Estoque/Alertas" })
        {
            var resposta = await navegador.GetAsync(rota);
            Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(resposta), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Produto_acabado_mostra_total_reservado_e_disponivel_depois_de_confirmar_um_pedido()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        await produto.DarEntradaDeProdutoAcabadoAsync(cenario.Api, 10);
        await produto.PedidoConfirmadoAsync(vendedor, quantidade: 4);

        var pagina = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Produtos?busca={produto.Codigo}"));

        Assert.Matches($@"<th scope=""row"">{produto.Codigo}</th>\s*<td>[^<]*</td>\s*<td class=""numero"">10</td>\s*<td class=""numero"">4</td>\s*<td class=""numero"">6</td>", pagina);
    }

    [Fact]
    public async Task Alerta_aberto_pela_producao_aparece_para_o_gerente_sem_botao_e_o_almoxarife_marca_como_lido()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        using var operador = await cenario.Api.ClienteAutenticadoAsync("Operador");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 20m, estoqueMinimo: 15m, consumoPorUnidade: 2m);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);
        var ordem = (await operador.GetFromJsonAsync<OrdemDeTeste>($"/api/v1/ordens-producao/{ordemId}"))!;
        (await operador.PostAsJsonAsync(
                $"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", new { ClientRequestId = Guid.NewGuid(), DataHoraCliente = DateTimeOffset.UtcNow }))
            .EnsureSuccessStatusCode();

        var doGerente = await PaginaDoAlertaAsync(gerente, produto.MateriaPrimaCodigo);
        var doAlmoxarife = await PaginaDoAlertaAsync(almoxarife, produto.MateriaPrimaCodigo);
        Assert.NotNull(doGerente);
        Assert.NotNull(doAlmoxarife);
        var envio = await almoxarife.EnviarComTokenDaPaginaAsync(doAlmoxarife, AcaoDaLeitura(doAlmoxarife, produto.MateriaPrimaCodigo), []);
        var depois = Navegador.Decodificar(await almoxarife.PaginaAsync(Navegador.Destino(envio)));

        Assert.DoesNotContain("Marcar como lido", doGerente, StringComparison.Ordinal);
        Assert.Contains("Marcar como lido", doAlmoxarife, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains($"Alerta de {produto.MateriaPrimaNome} marcado como lido.", depois, StringComparison.Ordinal);
        Assert.Null(await PaginaDoAlertaAsync(almoxarife, produto.MateriaPrimaCodigo));
    }

    [Fact]
    public async Task Gerente_nao_marca_alerta_como_lido()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var pagina = await gerente.PaginaAsync("/Estoque/Alertas");

        var resposta = await gerente.EnviarComTokenDaPaginaAsync(pagina, "/Estoque/LerAlerta/1", []);

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(resposta), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Extrato_filtra_por_tipo_origem_e_material()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        using var operador = await cenario.Api.ClienteAutenticadoAsync("Operador");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api, saldo: 50m, consumoPorUnidade: 2m);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);
        var ordem = (await operador.GetFromJsonAsync<OrdemDeTeste>($"/api/v1/ordens-producao/{ordemId}"))!;
        (await operador.PostAsJsonAsync(
                $"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", new { ClientRequestId = Guid.NewGuid(), DataHoraCliente = DateTimeOffset.UtcNow }))
            .EnsureSuccessStatusCode();
        var material = produto.MateriaPrimaId.ToString(CultureInfo.InvariantCulture);

        var consumo = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Movimentos?materiaPrimaId={material}&origem=Producao"));
        var entradas = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Estoque/Movimentos?materiaPrimaId={material}&tipo=Saida&origem=Ajuste"));

        Assert.Contains("<td>Saída</td>", consumo, StringComparison.Ordinal);
        Assert.Contains("<td>Produção</td>", consumo, StringComparison.Ordinal);
        Assert.Contains("-6", consumo, StringComparison.Ordinal);
        Assert.Contains("Nenhum movimento encontrado.", entradas, StringComparison.Ordinal);
    }

    /// <summary>Procura o alerta do material nas páginas da lista (a lista é paginada e o banco de teste é compartilhado).</summary>
    private static async Task<string?> PaginaDoAlertaAsync(Navegador navegador, string codigoDaMateria)
    {
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var html = await navegador.PaginaAsync($"/Estoque/Alertas?page={pagina}");
            if (html.Contains(codigoDaMateria, StringComparison.Ordinal))
            {
                return html;
            }

            if (!html.Contains("rel=\"next\"", StringComparison.Ordinal))
            {
                return null;
            }
        }

        return null;
    }

    private static string AcaoDaLeitura(string html, string codigoDaMateria)
    {
        var linha = Regex.Match(Navegador.Decodificar(html), $@"{codigoDaMateria}.*?(/Estoque/LerAlerta/\d+)", RegexOptions.Singleline);
        Assert.True(linha.Success, "A tela de alertas não tem o alerta do material.");
        return linha.Groups[1].Value;
    }

    private sealed record EtapaDeTeste(int Id);

    private sealed record OrdemDeTeste(IReadOnlyList<EtapaDeTeste> Etapas);
}
