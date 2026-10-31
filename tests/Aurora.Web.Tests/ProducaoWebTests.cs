using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed partial class ProducaoWebTests(CenarioDoPainel cenario)
{
    [GeneratedRegex(@"OP-\d{4}-\d{5}")]
    private static partial Regex NumeroDaOrdem();

    private static async Task<int> PrimeiraEtapaAsync(HttpClient api, string ordemId)
    {
        var ordem = await api.GetFromJsonAsync<OrdemDeTeste>($"/api/v1/ordens-producao/{ordemId}");
        return ordem!.Etapas[0].Id;
    }

    private static Task<HttpResponseMessage> IniciarAsync(HttpClient api, int etapaId) =>
        api.PostAsJsonAsync($"/api/v1/etapas/{etapaId}/iniciar", new { ClientRequestId = Guid.NewGuid(), DataHoraCliente = DateTimeOffset.UtcNow });

    [Fact]
    public async Task Confirmar_pede_confirmacao_depois_gera_a_ordem_e_mostra_a_producao_no_pedido()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (id, numero) = await ProdutoDeProducaoPedidoAbertoAsync(vendedor, produto);

        var antes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));
        var confirmacao = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Confirmar/{id}"));
        var envio = await vendedor.EnviarAsync($"/Pedidos/Confirmar/{id}", $"/Pedidos/Confirmar/{id}", []);
        var depois = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Contains("Confirmar pedido", antes, StringComparison.Ordinal);
        Assert.Contains($"Confirmar pedido {numero}?", confirmacao, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("As ordens de produção foram geradas.", depois, StringComparison.Ordinal);
        Assert.Contains("Em produção", depois, StringComparison.Ordinal);
        Assert.Matches(NumeroDaOrdem(), depois);
        Assert.Contains("0 de 3 etapas", depois, StringComparison.Ordinal);
        Assert.Contains("<td>Corte</td>", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirmar pedido", depois, StringComparison.Ordinal);
        Assert.DoesNotContain("Editar pedido", depois, StringComparison.Ordinal);
        Assert.Contains("Cancelar pedido", depois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_ja_confirmado_nao_abre_a_confirmacao_e_o_usuario_e_avisado()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (id, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 2);

        var tentativa = await vendedor.GetAsync($"/Pedidos/Confirmar/{id}");
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync(Navegador.Destino(tentativa)));
        var repetida = await vendedor.EnviarAsync($"/Pedidos/Detalhes/{id}", $"/Pedidos/Confirmar/{id}", []);

        Assert.Equal(HttpStatusCode.Redirect, tentativa.StatusCode);
        Assert.Contains("não pode ser confirmado", detalhes, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Contains("Somente pedidos abertos podem ser confirmados", Navegador.Decodificar(await repetida.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Produto_sem_roteiro_mostra_a_regra_da_procedure_no_formulario_e_o_pedido_continua_aberto()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var admin = await cenario.Api.ClienteAutenticadoAsync("Administrador");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var semRoteiro = await admin.PostAsJsonAsync("/api/v1/produtos", new { Codigo = ("S" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), Nome = "Sem roteiro", LinhaProdutoId = 1, PrecoVenda = 10m });
        var produtoSemRoteiro = (await semRoteiro.Content.ReadFromJsonAsync<IdDeTeste>())!.Id;
        var envio = await vendedor.EnviarAsync(
            "/Pedidos/Novo", "/Pedidos/Novo",
            PedidosWebTests.Campos(produto.ClienteId.ToString(System.Globalization.CultureInfo.InvariantCulture), PedidosWebTests.Prazo(), (produtoSemRoteiro.ToString(System.Globalization.CultureInfo.InvariantCulture), "1")));
        var id = Regex.Match(Navegador.Destino(envio), @"\d+$").Value;

        var resposta = await vendedor.EnviarAsync($"/Pedidos/Confirmar/{id}", $"/Pedidos/Confirmar/{id}", []);
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Produto sem roteiro de produção cadastrado", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Contains("Aberto", detalhes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gerente_ve_a_ordem_no_quadro_na_coluna_da_estacao_atual_e_abre_o_detalhe()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, pedidoNumero) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);

        var quadro = Navegador.Decodificar(await gerente.PaginaAsync("/Producao"));
        var detalhe = Navegador.Decodificar(await gerente.PaginaAsync($"/Producao/Detalhes/{ordemId}"));

        foreach (var estacao in new[] { "Corte", "Montagem da Estrutura", "Estofaria", "Montagem Final", "Qualidade" })
        {
            Assert.Contains($"<span>{estacao}</span>", quadro, StringComparison.Ordinal);
        }

        // O cartão da ordem fica dentro da coluna do Corte, que vem antes da coluna da Montagem da Estrutura
        var colunaDoCorte = quadro[quadro.IndexOf("<span>Corte</span>", StringComparison.Ordinal)..quadro.IndexOf("<span>Montagem da Estrutura</span>", StringComparison.Ordinal)];
        Assert.Contains($"/Producao/Detalhes/{ordemId}\"", colunaDoCorte, StringComparison.Ordinal);
        Assert.Contains($"{produto.Codigo} × 3", colunaDoCorte, StringComparison.Ordinal);
        Assert.Contains($"Pedido {pedidoNumero}", colunaDoCorte, StringComparison.Ordinal);
        Assert.Contains("Aguardando início", colunaDoCorte, StringComparison.Ordinal);

        Assert.Contains("Planejada", detalhe, StringComparison.Ordinal);
        Assert.Contains(pedidoNumero, detalhe, StringComparison.Ordinal);
        Assert.Contains(produto.Codigo, detalhe, StringComparison.Ordinal);
        Assert.Contains("Imprimir etiqueta com QR code", detalhe, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"row\">Qualidade</th>", detalhe, StringComparison.Ordinal);
        Assert.Contains("Nenhum apontamento ainda.", detalhe, StringComparison.Ordinal);
        Assert.Contains("Nenhuma perda registrada.", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Etapa_iniciada_pela_api_aparece_em_andamento_no_quadro_com_apontamento_e_perda_no_detalhe()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        using var operador = await cenario.Api.ClienteAutenticadoAsync("Operador");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 4);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);
        var etapaId = await PrimeiraEtapaAsync(operador, ordemId);

        (await IniciarAsync(operador, etapaId)).EnsureSuccessStatusCode();
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{etapaId}/perdas", new { Tipo = "Refugo", Quantidade = 1, Motivo = "Madeira rachada" }))
            .EnsureSuccessStatusCode();
        var quadro = Navegador.Decodificar(await gerente.PaginaAsync("/Producao"));
        var detalhe = Navegador.Decodificar(await gerente.PaginaAsync($"/Producao/Detalhes/{ordemId}"));

        Assert.Matches(@"Em andamento há \d+ min", quadro);
        Assert.Contains("Em andamento", detalhe, StringComparison.Ordinal);
        Assert.Contains("<td>Início</td>", detalhe, StringComparison.Ordinal);
        Assert.Contains("Madeira rachada", detalhe, StringComparison.Ordinal);
        Assert.Contains("Refugo", detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("Nenhum apontamento ainda.", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_de_ordens_filtra_por_situacao_e_estacao()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);
        var link = $"/Producao/Detalhes/{ordemId}\"";

        var planejadas = await gerente.PaginaAsync("/Producao/Ordens?status=Planejada&pageSize=100");
        var emAndamento = await gerente.PaginaAsync("/Producao/Ordens?status=EmAndamento");
        var noCorte = await gerente.PaginaAsync("/Producao/Ordens?estacaoId=1");
        var naQualidade = await gerente.PaginaAsync("/Producao/Ordens?estacaoId=5");

        Assert.Contains("name=\"estacaoId\"", planejadas, StringComparison.Ordinal);
        Assert.Contains(link, planejadas + noCorte, StringComparison.Ordinal);
        Assert.DoesNotContain(link, emAndamento, StringComparison.Ordinal);
        Assert.DoesNotContain(link, naQualidade, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Etiqueta_traz_o_qr_code_em_svg_o_numero_e_o_botao_de_imprimir_sem_codigo_embutido()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(vendedor, pedidoId);

        var etiqueta = Navegador.Decodificar(await gerente.PaginaAsync($"/Producao/Etiqueta/{ordemId}"));
        using var cabecalhos = await gerente.GetAsync($"/Producao/Etiqueta/{ordemId}");

        Assert.Matches(NumeroDaOrdem(), etiqueta);
        Assert.Contains("<svg", etiqueta, StringComparison.Ordinal);
        Assert.Contains("role=\"img\"", etiqueta, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"QR code da ordem de produção OP-", etiqueta, StringComparison.Ordinal);
        Assert.Contains("viewBox=", etiqueta, StringComparison.Ordinal);
        Assert.Contains("data-imprimir", etiqueta, StringComparison.Ordinal);
        Assert.Contains(produto.Codigo, etiqueta, StringComparison.Ordinal);
        Assert.DoesNotContain("<?xml", etiqueta, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", etiqueta, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", etiqueta);
        Assert.Contains("img-src 'self' data:", string.Join(';', cabecalhos.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vendedor_abre_a_ordem_do_proprio_pedido_e_nao_a_de_outro_vendedor()
    {
        using var dono = await cenario.EntrarComoAsync("Vendedor");
        using var outro = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(dono, quantidade: 1);
        var ordemId = await ProdutoDeProducao.IdDaOrdemAsync(dono, pedidoId);

        var doDono = await dono.GetAsync($"/Producao/Detalhes/{ordemId}");
        var doOutro = await outro.GetAsync($"/Producao/Detalhes/{ordemId}");
        var etiquetaDoOutro = await outro.GetAsync($"/Producao/Etiqueta/{ordemId}");
        var quadroDoOutro = await outro.PaginaAsync("/Producao");

        Assert.Equal(HttpStatusCode.OK, doDono.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, doOutro.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, etiquetaDoOutro.StatusCode);
        Assert.DoesNotContain($"/Producao/Detalhes/{ordemId}\"", quadroDoOutro, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Operador")]
    [InlineData("Almoxarife")]
    public async Task Perfis_sem_acesso_a_producao_sao_barrados(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var quadro = await navegador.GetAsync("/Producao");
        var ordens = await navegador.GetAsync("/Producao/Ordens");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(quadro), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(ordens), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Almoxarife_ve_o_pedido_sem_a_secao_de_producao_e_o_gerente_nao_confirma()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 1);

        var doAlmoxarife = Navegador.Decodificar(await almoxarife.PaginaAsync($"/Pedidos/Detalhes/{pedidoId}"));
        var confirmacaoDoGerente = await gerente.GetAsync($"/Pedidos/Confirmar/{pedidoId}");

        Assert.DoesNotContain("titulo-producao", doAlmoxarife, StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(confirmacaoDoGerente), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedido_totalmente_atendido_pelo_estoque_de_produto_fica_pronto_sem_ordem()
    {
        using var vendedor = await cenario.EntrarComoAsync("Vendedor");
        var produto = await ProdutoDeProducao.CriarAsync(cenario.Api);
        await produto.DarEntradaDeProdutoAcabadoAsync(cenario.Api, 10);

        var (pedidoId, _) = await produto.PedidoConfirmadoAsync(vendedor, quantidade: 4);
        var detalhes = Navegador.Decodificar(await vendedor.PaginaAsync($"/Pedidos/Detalhes/{pedidoId}"));

        Assert.Contains("Pronto", detalhes, StringComparison.Ordinal);
        Assert.Contains("nenhuma ordem de produção foi necessária", detalhes, StringComparison.Ordinal);
        Assert.DoesNotContain("titulo-producao", detalhes, StringComparison.Ordinal);
    }

    private static async Task<(string Id, string Numero)> ProdutoDeProducaoPedidoAbertoAsync(Navegador vendedor, ProdutoDeProducao produto)
    {
        var envio = await vendedor.EnviarAsync(
            "/Pedidos/Novo", "/Pedidos/Novo",
            PedidosWebTests.Campos(produto.ClienteId.ToString(System.Globalization.CultureInfo.InvariantCulture), PedidosWebTests.Prazo(),
                (produto.ProdutoId.ToString(System.Globalization.CultureInfo.InvariantCulture), "2")));
        var id = Regex.Match(Navegador.Destino(envio), @"/Pedidos/Detalhes/(\d+)").Groups[1].Value;
        var detalhes = await vendedor.PaginaAsync($"/Pedidos/Detalhes/{id}");
        return (id, Regex.Match(detalhes, @"PV-\d{4}-\d{5}").Value);
    }

    private sealed record IdDeTeste(int Id);

    private sealed record EtapaDeTeste(int Id);

    private sealed record OrdemDeTeste(IReadOnlyList<EtapaDeTeste> Etapas);
}
