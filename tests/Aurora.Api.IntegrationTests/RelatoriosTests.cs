using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Application.Clientes;
using Aurora.Application.Compras;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Pedidos;
using Aurora.Application.Relatorios;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using UglyToad.PdfPig;

namespace Aurora.Api.IntegrationTests;

/// <summary>Relatórios em PDF e Excel (RF21): os arquivos são abertos de verdade e o conteúdo é conferido.</summary>
[Collection("api")]
public sealed partial class RelatoriosTests(ApiFactory api)
{
    [GeneratedRegex(@"^attachment; filename=relatorio-[a-z-]+-\d{8}-\d{4}\.(pdf|xlsx)", RegexOptions.IgnoreCase)]
    private static partial Regex NomeDeArquivo();

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<HttpResponseMessage> PedirAsync(HttpClient cliente, string tipo, string formato, string extras = "") =>
        await cliente.GetAsync($"/api/v1/relatorios/{tipo}?formato={formato}{extras}");

    private static async Task<byte[]> BaixarAsync(HttpClient cliente, string tipo, string formato, string extras = "")
    {
        var resposta = await PedirAsync(cliente, tipo, formato, extras);
        resposta.AssertStatus(HttpStatusCode.OK);
        return await resposta.Content.ReadAsByteArrayAsync();
    }

    /// <summary>Texto do PDF numa linha só, com o espaço sem quebra do pt-BR trocado por espaço comum.</summary>
    private static string TextoDoPdf(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        var texto = string.Join(" ", documento.GetPages().Select(p => p.Text));
        return Regex.Replace(texto.Replace(' ', ' '), @"\s+", " ");
    }

    private static XLWorkbook Excel(byte[] xlsx) => new(new MemoryStream(xlsx));

    private static async Task<PedidoDto> PedidoAsync(CenarioDeProducao cenario, HttpClient vendedor, int quantidade, bool confirmar = true) =>
        confirmar ? await cenario.PedidoConfirmadoAsync(vendedor, quantidade) : await cenario.PedidoAbertoAsync(vendedor, quantidade);

    [Theory]
    [InlineData("pedidos", "pdf", "application/pdf")]
    [InlineData("pedidos", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("producao", "pdf", "application/pdf")]
    [InlineData("producao", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("estoque", "pdf", "application/pdf")]
    [InlineData("estoque", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("consumo", "pdf", "application/pdf")]
    [InlineData("consumo", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("compras-comunitarias", "pdf", "application/pdf")]
    [InlineData("compras-comunitarias", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task Todo_relatorio_sai_nos_dois_formatos_como_download_sem_cache(string tipo, string formato, string tipoDeConteudo)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await PedirAsync(admin, tipo, formato);

        resposta.AssertStatus(HttpStatusCode.OK);
        Assert.Equal(tipoDeConteudo, resposta.Content.Headers.ContentType?.MediaType);
        Assert.Matches(NomeDeArquivo(), resposta.Content.Headers.ContentDisposition?.ToString() ?? "");
        Assert.Contains("no-store", resposta.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        var conteudo = await resposta.Content.ReadAsByteArrayAsync();
        if (formato == "pdf")
        {
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(conteudo, 0, 5));
            using var pdf = PdfDocument.Open(conteudo);
            Assert.True(pdf.NumberOfPages >= 1);
        }
        else
        {
            using var pasta = Excel(conteudo);
            Assert.NotEmpty(pasta.Worksheets);
        }
    }

    [Fact]
    public async Task Formato_aceita_maiusculas()
    {
        using var admin = await api.ComoAdministradorAsync();

        (await PedirAsync(admin, "estoque", "PDF")).AssertStatus(HttpStatusCode.OK);
        (await PedirAsync(admin, "ESTOQUE", "XLSX")).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pdf_de_pedidos_tem_titulo_numero_cliente_valor_em_reais_e_numeracao_de_paginas()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await PedidoAsync(cenario, vendedor, quantidade: 7);

        var texto = TextoDoPdf(await BaixarAsync(gerente, "pedidos", "pdf"));

        Assert.Contains("Relatório de pedidos", texto, StringComparison.Ordinal);
        Assert.Contains("Aurora Cadeiras", texto, StringComparison.Ordinal);
        Assert.Contains(pedido.Numero, texto, StringComparison.Ordinal);
        Assert.Contains(cenario.Cliente.Nome, texto, StringComparison.Ordinal);
        Assert.Contains("R$ 700,00", texto, StringComparison.Ordinal); // 7 × 100,00
        Assert.Contains("horário de Brasília", texto, StringComparison.Ordinal);
        Assert.Matches(@"Página 1 de \d+", texto);
    }

    [Fact]
    public async Task Pdf_acentua_corretamente_a_fonte_embutida_cobre_o_portugues()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var producao = TextoDoPdf(await BaixarAsync(gerente, "producao", "pdf"));
        var comunidades = TextoDoPdf(await BaixarAsync(gerente, "compras-comunitarias", "pdf"));

        Assert.Contains("Relatório de produção", producao, StringComparison.Ordinal);
        Assert.Contains("Compras de comunidades tradicionais", comunidades, StringComparison.Ordinal);
        Assert.DoesNotContain('�', producao); // nenhum caractere de substituição
    }

    [Fact]
    public async Task Excel_de_pedidos_tem_numeros_e_datas_de_verdade_cabecalho_fixo_e_filtro()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await PedidoAsync(cenario, vendedor, quantidade: 7);

        using var pasta = Excel(await BaixarAsync(gerente, "pedidos", "xlsx"));

        var planilha = pasta.Worksheet("Pedidos");
        var cabecalho = planilha.Row(5).Cells(1, 9).Select(c => c.GetString()).ToList();
        Assert.Equal(["Número", "Cliente", "Vendedor", "Data do pedido", "Prazo de entrega", "Situação", "Itens", "Valor total", "Dias de atraso"], cabecalho);
        var linha = planilha.RowsUsed().Single(r => r.Cell(1).GetString() == pedido.Numero);
        Assert.Equal(XLDataType.Number, linha.Cell(8).DataType); // valor é número, não texto
        Assert.Equal(700m, linha.Cell(8).GetValue<decimal>());
        Assert.Equal(XLDataType.DateTime, linha.Cell(4).DataType);
        Assert.Equal(XLDataType.DateTime, linha.Cell(5).DataType);
        Assert.Equal("Em produção", linha.Cell(6).GetString());
        Assert.Contains("R$", linha.Cell(8).Style.NumberFormat.Format, StringComparison.Ordinal);
        Assert.Equal(1, planilha.Cell(1, 1).Style.Font.Bold ? 1 : 0);
        Assert.True(planilha.AutoFilter.IsEnabled);
        Assert.Equal(5, planilha.SheetView.SplitRow); // cabeçalho fixo
    }

    [Fact]
    public async Task Filtro_de_situacao_e_de_periodo_restringem_os_pedidos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var aberto = await PedidoAsync(cenario, vendedor, quantidade: 1, confirmar: false);
        var confirmado = await PedidoAsync(cenario, vendedor, quantidade: 1);

        using var soAbertos = Excel(await BaixarAsync(gerente, "pedidos", "xlsx", "&status=Aberto"));
        using var doFuturo = Excel(await BaixarAsync(gerente, "pedidos", "xlsx", $"&de={Hoje.AddDays(5):yyyy-MM-dd}"));
        using var deHoje = Excel(await BaixarAsync(gerente, "pedidos", "xlsx", $"&de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"));

        static HashSet<string> Numeros(XLWorkbook p) =>
            p.Worksheet("Pedidos").RowsUsed().Where(r => r.RowNumber() > 5).Select(r => r.Cell(1).GetString()).ToHashSet();
        Assert.Contains(aberto.Numero, Numeros(soAbertos));
        Assert.DoesNotContain(confirmado.Numero, Numeros(soAbertos));
        Assert.DoesNotContain(aberto.Numero, Numeros(doFuturo));
        Assert.Contains(aberto.Numero, Numeros(deHoje));
        Assert.Contains(confirmado.Numero, Numeros(deHoje));
    }

    [Theory]
    [InlineData("&status=Inventado", "FILTRO_INVALIDO")]
    [InlineData("&de=2026-12-31&ate=2026-01-01", "FILTRO_INVALIDO")]
    public async Task Filtro_invalido_devolve_400_com_o_motivo(string extras, string codigo)
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var resposta = await PedirAsync(gerente, "pedidos", "pdf", extras);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal(codigo, await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Ano_fora_do_intervalo_devolve_400()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        (await PedirAsync(gerente, "compras-comunitarias", "pdf", "&ano=1999")).AssertStatus(HttpStatusCode.BadRequest);
        (await PedirAsync(gerente, "compras-comunitarias", "pdf", "&ano=2026")).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vendedor_recebe_so_os_proprios_pedidos_e_o_relatorio_diz_isso()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var dono = await cenario.NovoVendedorAsync();
        using var outro = await cenario.NovoVendedorAsync();
        var meu = await PedidoAsync(cenario, dono, quantidade: 1);
        var alheio = await PedidoAsync(cenario, outro, quantidade: 1);

        var xlsx = await BaixarAsync(dono, "pedidos", "xlsx");
        var pdf = TextoDoPdf(await BaixarAsync(dono, "pedidos", "pdf"));

        using var pasta = Excel(xlsx);
        var numeros = pasta.Worksheet("Pedidos").RowsUsed().Where(r => r.RowNumber() > 5).Select(r => r.Cell(1).GetString()).ToList();
        Assert.Contains(meu.Numero, numeros);
        Assert.DoesNotContain(alheio.Numero, numeros);
        Assert.Contains("Somente os seus pedidos", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(alheio.Numero, pdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Texto_que_parece_formula_fica_como_texto_no_excel()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var cenario = await CenarioDeProducao.CriarAsync(api);
        const string Malicioso = "=HYPERLINK(\"http://exemplo.invalido\",\"clique\")";
        var clienteResposta = await admin.PostAsJsonAsync(
            "/api/v1/clientes", new ClienteRequest(Malicioso, DadosDeTeste.Digitos(11), null, null, null, null));
        clienteResposta.AssertStatus(HttpStatusCode.Created);
        var cliente = (await clienteResposta.Content.ReadFromJsonAsync<ClienteDto>())!;
        var criado = await vendedor.PostAsJsonAsync(
            "/api/v1/pedidos", new PedidoRequest(cliente.Id, Hoje.AddDays(20), null, [new PedidoItemRequest(cenario.Produto.Id, 1)]));
        criado.AssertStatus(HttpStatusCode.Created);

        using var pasta = Excel(await BaixarAsync(gerente, "pedidos", "xlsx"));

        var celula = pasta.Worksheet("Pedidos").CellsUsed().First(c => c.GetString() == Malicioso);
        Assert.False(celula.HasFormula);
        Assert.Equal(XLDataType.Text, celula.DataType);
    }

    [Fact]
    public async Task Excel_de_estoque_tem_duas_planilhas_e_marca_o_material_critico()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        var criada = await admin.PostAsJsonAsync(
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("R" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), "Material do relatório", "m", 50m, 4m, null));
        var materia = (await criada.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;

        var xlsx = await BaixarAsync(gerente, "estoque", "xlsx");
        var pdf = TextoDoPdf(await BaixarAsync(gerente, "estoque", "pdf"));

        using var pasta = Excel(xlsx);
        Assert.Equal(["Matérias-primas", "Produto acabado"], pasta.Worksheets.Select(w => w.Name));
        var linha = pasta.Worksheet("Matérias-primas").RowsUsed().Single(r => r.Cell(1).GetString() == materia.Codigo);
        Assert.Equal("Crítico", linha.Cell(6).GetString()); // saldo 0, mínimo 50
        Assert.Equal(XLDataType.Number, linha.Cell(4).DataType);
        Assert.Contains(materia.Codigo, pdf, StringComparison.Ordinal);
        Assert.Contains("Produto acabado", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consumo_traz_o_material_consumido_e_nao_traz_de_periodo_sem_consumo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 1_000_000m, consumoPorUnidade: 5m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 8);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);

        using var hoje = Excel(await BaixarAsync(gerente, "consumo", "xlsx"));
        using var antigo = Excel(await BaixarAsync(gerente, "consumo", "xlsx", "&de=2020-01-01&ate=2020-01-31"));

        var porMateria = hoje.Worksheet("Por matéria-prima");
        var linha = porMateria.RowsUsed().Single(r => r.Cell(1).GetString() == cenario.Materia.Codigo);
        Assert.Equal(40m, linha.Cell(4).GetValue<decimal>()); // 8 × 5
        Assert.Equal(400m, linha.Cell(5).GetValue<decimal>()); // × custo 10
        Assert.DoesNotContain(antigo.Worksheet("Por matéria-prima").RowsUsed(), r => r.Cell(1).GetString() == cenario.Materia.Codigo);
        Assert.Contains(hoje.Worksheet("Custo por produto").RowsUsed(), r => r.Cell(1).GetString() == cenario.Produto.Codigo);
    }

    [Fact]
    public async Task Producao_traz_a_op_em_aberto_na_estacao_em_que_esta()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        using var pasta = Excel(await BaixarAsync(gerente, "producao", "xlsx"));

        var linha = pasta.Worksheet("Ordens de produção em aberto").RowsUsed().Single(r => r.Cell(1).GetString() == ordem.Ordem.Numero);
        Assert.Equal(cenario.Produto.Nome, linha.Cell(2).GetString());
        Assert.Equal(3, linha.Cell(3).GetValue<int>());
        Assert.Equal("Corte", linha.Cell(4).GetString());
        Assert.Equal("Aguardando", linha.Cell(5).GetString());
        Assert.Equal("Não", linha.Cell(8).GetString());
    }

    [Fact]
    public async Task Compras_comunitarias_mostra_a_parte_dos_fornecedores_de_comunidades()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var fornecedorResposta = await admin.PostAsJsonAsync(
            "/api/v1/fornecedores", new FornecedorRequest("Cooperativa Quilombola", DadosDeTeste.Digitos(14), null, null, true));
        var fornecedor = (await fornecedorResposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
        var materiaResposta = await admin.PostAsJsonAsync(
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("Q" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), "Fibra natural", "m", 1m, 1m, null));
        var materia = (await materiaResposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
        var pedidoResposta = await almoxarife.PostAsJsonAsync(
            "/api/v1/pedidos-compra", new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, 100m, 12.5m)]));
        var pedido = (await pedidoResposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        (await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/emitir", content: null)).AssertStatus(HttpStatusCode.OK);
        (await almoxarife.PostAsJsonAsync($"/api/v1/pedidos-compra/itens/{pedido.Itens[0].Id}/receber", new ReceberItemRequest(100m)))
            .AssertStatus(HttpStatusCode.OK);

        using var pasta = Excel(await BaixarAsync(gerente, "compras-comunitarias", "xlsx", $"&ano={DateTime.UtcNow.Year}"));

        var planilha = pasta.Worksheet("Recebimentos por mês");
        var mes = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(DateTime.UtcNow.Month);
        var linha = planilha.RowsUsed().Single(r => r.Cell(1).GetString() == $"{mes} de {DateTime.UtcNow.Year}");
        Assert.True(linha.Cell(2).GetValue<decimal>() >= 1250m); // 100 × 12,50 das comunidades
        Assert.True(linha.Cell(3).GetValue<decimal>() >= linha.Cell(2).GetValue<decimal>());
        Assert.InRange(linha.Cell(4).GetValue<decimal>(), 0.0001m, 1m); // o Excel guarda a fração (formato 0,00%)
        Assert.Contains("%", linha.Cell(4).Style.NumberFormat.Format, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tipo_desconhecido_devolve_404_e_formato_ausente_ou_invalido_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();

        var desconhecido = await PedirAsync(admin, "inexistente", "pdf");
        var semFormato = await admin.GetAsync("/api/v1/relatorios/estoque");
        var formatoInvalido = await PedirAsync(admin, "estoque", "docx");

        desconhecido.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("RELATORIO_INEXISTENTE", await desconhecido.CodigoAsync());
        semFormato.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("FORMATO_INVALIDO", await semFormato.CodigoAsync());
        formatoInvalido.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Contains("pdf", (await formatoInvalido.ProblemaAsync()).GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formato_fora_da_configuracao_fica_indisponivel()
    {
        await using var soPdf = api.WithWebHostBuilder(construtor => construtor.UseSetting("Relatorios:Formatos:0", "pdf"));
        var token = await api.LoginAsync(ApiFactory.EmailAdmin, ApiFactory.SenhaAdmin);
        using var admin = soPdf.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);

        var pdf = await PedirAsync(admin, "estoque", "pdf");
        var excel = await PedirAsync(admin, "estoque", "xlsx");

        pdf.AssertStatus(HttpStatusCode.OK);
        excel.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("FORMATO_INVALIDO", await excel.CodigoAsync());
    }

    [Theory]
    [InlineData("pedidos", "Administrador", true)]
    [InlineData("pedidos", "GerenteProducao", true)]
    [InlineData("pedidos", "Vendedor", true)]
    [InlineData("pedidos", "Almoxarife", false)]
    [InlineData("producao", "Administrador", true)]
    [InlineData("producao", "GerenteProducao", true)]
    [InlineData("producao", "Vendedor", false)]
    [InlineData("producao", "Almoxarife", false)]
    [InlineData("estoque", "Administrador", true)]
    [InlineData("estoque", "GerenteProducao", true)]
    [InlineData("estoque", "Almoxarife", true)]
    [InlineData("estoque", "Vendedor", false)]
    [InlineData("consumo", "GerenteProducao", true)]
    [InlineData("consumo", "Almoxarife", false)]
    [InlineData("consumo", "Vendedor", false)]
    [InlineData("compras-comunitarias", "Administrador", true)]
    [InlineData("compras-comunitarias", "Almoxarife", false)]
    [InlineData("compras-comunitarias", "Vendedor", false)]
    [InlineData("pedidos", "Operador", false)]
    [InlineData("estoque", "Inspetor", false)]
    [InlineData("pedidos", "Cliente", false)]
    public async Task Cada_tipo_so_atende_os_perfis_da_matriz(string tipo, string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await PedirAsync(cliente, tipo, "pdf");

        if (permitido)
        {
            resposta.AssertStatus(HttpStatusCode.OK);
            return;
        }

        resposta.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("Administrador", new[] { "pedidos", "producao", "estoque", "consumo", "compras-comunitarias" })]
    [InlineData("GerenteProducao", new[] { "pedidos", "producao", "estoque", "consumo", "compras-comunitarias" })]
    [InlineData("Vendedor", new[] { "pedidos" })]
    [InlineData("Almoxarife", new[] { "estoque" })]
    public async Task A_lista_de_relatorios_mostra_so_o_que_o_perfil_pode_gerar(string perfil, string[] esperados)
    {
        using var cliente = await api.ComoAsync(perfil);

        var lista = await cliente.GetFromJsonAsync<List<RelatorioDisponivelDto>>("/api/v1/relatorios");

        Assert.Equal(esperados.Order(), lista!.Select(r => r.Tipo).Order());
    }

    [Fact]
    public async Task A_lista_informa_os_filtros_de_cada_relatorio()
    {
        using var admin = await api.ComoAdministradorAsync();

        var lista = (await admin.GetFromJsonAsync<List<RelatorioDisponivelDto>>("/api/v1/relatorios"))!.ToDictionary(r => r.Tipo);

        Assert.True(lista["pedidos"].AceitaPeriodo && lista["pedidos"].AceitaStatus && !lista["pedidos"].AceitaAno);
        Assert.True(lista["consumo"].AceitaPeriodo && !lista["consumo"].AceitaStatus);
        Assert.True(lista["compras-comunitarias"].AceitaAno && !lista["compras-comunitarias"].AceitaPeriodo);
        Assert.True(!lista["producao"].AceitaPeriodo && !lista["estoque"].AceitaPeriodo);
    }
}
