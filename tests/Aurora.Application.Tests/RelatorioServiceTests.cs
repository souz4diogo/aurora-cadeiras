using Aurora.Application.Abstracoes;
using Aurora.Application.Painel;
using Aurora.Application.Relatorios;
using Aurora.Application.Relatorios.Geradores;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using Moq;

namespace Aurora.Application.Tests;

public class RelatorioServiceTests
{
    private static readonly DateTime Agora = new(2026, 11, 2, 15, 30, 0, DateTimeKind.Utc);
    private static readonly FiltroDeRelatorio SemFiltro = new(null, null, null, null);

    private readonly Mock<IRelatoriosRepository> _repositorio = new();
    private readonly Mock<IExportadorFactory> _fabrica = new();
    private readonly Mock<IExportadorDeRelatorio> _exportador = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public RelatorioServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.GerenteProducao);
        _exportador.SetupGet(e => e.Formato).Returns("pdf");
        _exportador.SetupGet(e => e.TipoDeConteudo).Returns("application/pdf");
        _exportador.SetupGet(e => e.Extensao).Returns("pdf");
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Returns([1, 2, 3]);
        _fabrica.SetupGet(f => f.FormatosDisponiveis).Returns(["pdf", "xlsx"]);
        _fabrica.Setup(f => f.Criar("pdf")).Returns(_exportador.Object);
        _repositorio.Setup(r => r.ListarEstoqueDeMateriasAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDeEstoqueDeMateria>([], 0));
        _repositorio.Setup(r => r.ListarEstoqueDeProdutosAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDeEstoqueDeProduto>([], 0));
    }

    private RelatorioService Servico() => new(
        [
            new RelatorioDePedidos(_repositorio.Object, _usuarioAtual.Object),
            new RelatorioDeProducao(_repositorio.Object),
            new RelatorioDeEstoque(_repositorio.Object),
            new RelatorioDeConsumo(_repositorio.Object),
            new RelatorioDeComprasComunitarias(_repositorio.Object),
        ],
        _fabrica.Object,
        _usuarioAtual.Object,
        _relogio.Object);

    [Fact]
    public async Task Gera_o_arquivo_com_o_tipo_de_conteudo_e_um_nome_com_tipo_data_e_hora()
    {
        var arquivo = await Servico().GerarAsync("estoque", "pdf", SemFiltro);

        Assert.Equal([1, 2, 3], arquivo.Conteudo);
        Assert.Equal("application/pdf", arquivo.TipoDeConteudo);
        Assert.Equal("relatorio-estoque-20261102-1530.pdf", arquivo.NomeDoArquivo);
        _exportador.Verify(e => e.Exportar(It.Is<Relatorio>(r => r.Titulo == "Relatório de estoque" && r.Secoes.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task O_tipo_nao_diferencia_maiusculas()
    {
        var arquivo = await Servico().GerarAsync("ESTOQUE", "pdf", SemFiltro);

        Assert.StartsWith("relatorio-estoque-", arquivo.NomeDoArquivo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tipo_desconhecido_devolve_404_antes_de_qualquer_consulta()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().GerarAsync("inexistente", "pdf", SemFiltro));

        Assert.Equal("RELATORIO_INEXISTENTE", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        _repositorio.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PerfilUsuario.Vendedor, "estoque")]
    [InlineData(PerfilUsuario.Almoxarife, "pedidos")]
    [InlineData(PerfilUsuario.Operador, "estoque")]
    [InlineData(PerfilUsuario.Cliente, "pedidos")]
    [InlineData(PerfilUsuario.Vendedor, "consumo")]
    public async Task Perfil_fora_da_matriz_devolve_403_sem_consultar_o_banco(PerfilUsuario perfil, string tipo)
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(perfil);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().GerarAsync(tipo, "pdf", SemFiltro));

        Assert.Equal(TipoErro.Proibido, erro.Tipo);
        Assert.Equal("SEM_PERMISSAO", erro.Codigo);
        _repositorio.VerifyNoOtherCalls();
        _exportador.Verify(e => e.Exportar(It.IsAny<Relatorio>()), Times.Never);
    }

    [Fact]
    public async Task Sem_perfil_reconhecido_ninguem_gera()
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns((PerfilUsuario?)null);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().GerarAsync("estoque", "pdf", SemFiltro));

        Assert.Equal(TipoErro.Proibido, erro.Tipo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("docx")]
    public async Task Formato_ausente_ou_desconhecido_devolve_400_listando_os_formatos_validos(string? formato)
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().GerarAsync("estoque", formato, SemFiltro));

        Assert.Equal("FORMATO_INVALIDO", erro.Codigo);
        Assert.Equal(TipoErro.Validacao, erro.Tipo);
        Assert.Contains("pdf ou xlsx", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PerfilUsuario.Administrador, 5)]
    [InlineData(PerfilUsuario.GerenteProducao, 5)]
    [InlineData(PerfilUsuario.Vendedor, 1)]
    [InlineData(PerfilUsuario.Almoxarife, 1)]
    [InlineData(PerfilUsuario.Operador, 0)]
    [InlineData(PerfilUsuario.Inspetor, 0)]
    [InlineData(PerfilUsuario.Cliente, 0)]
    public void Lista_so_o_que_o_perfil_pode_gerar(PerfilUsuario perfil, int quantidade)
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(perfil);

        Assert.Equal(quantidade, Servico().ListarDisponiveis().Count);
    }

    [Fact]
    public async Task Pedidos_do_vendedor_vao_ao_banco_restritos_ao_proprio_id()
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Vendedor);
        _repositorio.Setup(r => r.ListarPedidosAsync(9, null, null, null, It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDePedido>([], 0));

        await Servico().GerarAsync("pedidos", "pdf", SemFiltro);

        _repositorio.Verify(r => r.ListarPedidosAsync(9, null, null, null, It.IsAny<int>(), default), Times.Once);
        _exportador.Verify(e => e.Exportar(It.Is<Relatorio>(x => x.Filtros.Contains("Somente os seus pedidos"))), Times.Once);
    }

    [Fact]
    public async Task Pedidos_do_gerente_nao_sao_restritos()
    {
        _repositorio.Setup(r => r.ListarPedidosAsync(null, null, null, null, It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDePedido>([], 0));

        await Servico().GerarAsync("pedidos", "pdf", SemFiltro);

        _repositorio.Verify(r => r.ListarPedidosAsync(null, null, null, null, It.IsAny<int>(), default), Times.Once);
    }

    [Fact]
    public async Task Pedidos_calcula_o_atraso_so_dos_pedidos_em_andamento_e_totaliza()
    {
        var hoje = new DateOnly(2026, 11, 2);
        LinhaDePedido Pedido(string numero, StatusPedido status, DateOnly prazo, decimal valor) =>
            new(numero, "Cliente", "Vendedor", Agora, prazo, status, 2, valor);
        _repositorio.Setup(r => r.ListarPedidosAsync(null, null, null, null, It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDePedido>(
            [
                Pedido("A", StatusPedido.EmProducao, hoje.AddDays(-3), 100m), // atrasado 3 dias
                Pedido("B", StatusPedido.Expedido, hoje.AddDays(-30), 50m), // já saiu: não é atraso
                Pedido("C", StatusPedido.Confirmado, hoje.AddDays(5), 25m), // no prazo
                Pedido("D", StatusPedido.Aberto, hoje.AddDays(-1), 10m), // rascunho: não conta
            ],
            4));
        Relatorio? gerado = null;
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Callback<Relatorio>(r => gerado = r).Returns([1]);

        await Servico().GerarAsync("pedidos", "pdf", SemFiltro);

        var secao = Assert.Single(gerado!.Secoes);
        Assert.Equal([3, 0, 0, 0], secao.Linhas.Select(l => (int)l[8]!));
        Assert.Equal(185m, secao.Totais![7]);
        Assert.Equal(8, secao.Totais[6]);
        Assert.Null(secao.Aviso);
    }

    [Fact]
    public async Task Quando_o_banco_tem_mais_linhas_que_o_limite_o_relatorio_avisa()
    {
        _repositorio.Setup(r => r.ListarPedidosAsync(null, null, null, null, It.IsAny<int>(), default))
            .ReturnsAsync(new ResultadoLimitado<LinhaDePedido>([], 12_345));
        Relatorio? gerado = null;
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Callback<Relatorio>(r => gerado = r).Returns([1]);

        await Servico().GerarAsync("pedidos", "pdf", SemFiltro);

        Assert.Contains("12.345", gerado!.Secoes[0].Aviso, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Inventado")]
    [InlineData("99")]
    [InlineData("1")]
    public async Task Situacao_desconhecida_de_pedido_devolve_400(string status)
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().GerarAsync("pedidos", "pdf", new FiltroDeRelatorio(null, null, status, null)));

        Assert.Equal("FILTRO_INVALIDO", erro.Codigo);
    }

    [Fact]
    public async Task Periodo_invertido_devolve_400_em_pedidos_e_em_consumo()
    {
        var invertido = new FiltroDeRelatorio(new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 1), null, null);

        foreach (var tipo in new[] { "pedidos", "consumo" })
        {
            var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().GerarAsync(tipo, "pdf", invertido));
            Assert.Equal("FILTRO_INVALIDO", erro.Codigo);
        }
    }

    [Fact]
    public async Task Consumo_sem_periodo_usa_os_ultimos_30_dias_incluindo_hoje()
    {
        DateOnly? de = null;
        DateOnly? ate = null;
        _repositorio.Setup(r => r.ListarConsumoPorMateriaAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), default))
            .Callback<DateOnly, DateOnly, int, CancellationToken>((d, a, _, _) => (de, ate) = (d, a))
            .ReturnsAsync([]);
        _repositorio.Setup(r => r.ListarConsumoPorProdutoAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>(), default))
            .ReturnsAsync([]);

        await Servico().GerarAsync("consumo", "pdf", SemFiltro);

        Assert.Equal(new DateOnly(2026, 11, 2), ate);
        Assert.Equal(new DateOnly(2026, 10, 4), de); // 30 dias contando hoje
    }

    [Fact]
    public async Task Estoque_marca_como_critico_o_saldo_no_minimo_ou_abaixo_e_soma_o_valor_em_estoque()
    {
        _repositorio.Setup(r => r.ListarEstoqueDeMateriasAsync(It.IsAny<int>(), default)).ReturnsAsync(
            new ResultadoLimitado<LinhaDeEstoqueDeMateria>(
            [
                new("A", "No mínimo", "m", 10m, 10m, 2m),
                new("B", "Abaixo", "m", 3m, 10m, 5m),
                new("C", "Folgado", "m", 11m, 10m, 1m),
            ],
            3));
        Relatorio? gerado = null;
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Callback<Relatorio>(r => gerado = r).Returns([1]);

        await Servico().GerarAsync("estoque", "pdf", SemFiltro);

        var materias = gerado!.Secoes[0];
        Assert.Equal(["Crítico", "Crítico", "Normal"], materias.Linhas.Select(l => (string)l[5]!));
        Assert.Equal((10m * 2m) + (3m * 5m) + (11m * 1m), materias.Totais![7]);
        Assert.Equal("2 críticos", materias.Totais[5]);
    }

    [Fact]
    public async Task Compras_comunitarias_calcula_o_percentual_geral_pelo_total_e_nao_pela_media_dos_meses()
    {
        _repositorio.Setup(r => r.ListarComprasComunitariasAsync(2026, default)).ReturnsAsync(
        [
            new LinhaDeComprasComunitarias(2026, 9, 100m, 1000m, 10m), // 10%
            new LinhaDeComprasComunitarias(2026, 10, 900m, 1000m, 90m), // 90%
        ]);
        Relatorio? gerado = null;
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Callback<Relatorio>(r => gerado = r).Returns([1]);

        await Servico().GerarAsync("compras-comunitarias", "pdf", new FiltroDeRelatorio(null, null, null, 2026));

        var totais = gerado!.Secoes[0].Totais!;
        Assert.Equal(1000m, totais[1]);
        Assert.Equal(2000m, totais[2]);
        Assert.Equal(50m, totais[3]); // 1000 de 2000
        Assert.Equal("setembro de 2026", gerado.Secoes[0].Linhas[0][0]);
    }

    [Fact]
    public async Task Compras_comunitarias_sem_nenhum_recebimento_nao_divide_por_zero()
    {
        _repositorio.Setup(r => r.ListarComprasComunitariasAsync(null, default)).ReturnsAsync([]);
        Relatorio? gerado = null;
        _exportador.Setup(e => e.Exportar(It.IsAny<Relatorio>())).Callback<Relatorio>(r => gerado = r).Returns([1]);

        await Servico().GerarAsync("compras-comunitarias", "pdf", SemFiltro);

        Assert.Equal(0m, gerado!.Secoes[0].Totais![3]);
    }
}

public class PainelServiceTests
{
    private static readonly DateTime Agora = new(2026, 11, 2, 15, 30, 0, DateTimeKind.Utc);

    private readonly Mock<IPainelRepository> _painel = new();
    private readonly Mock<IEstacaoRepository> _estacoes = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public PainelServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.GerenteProducao);
    }

    private PainelService Servico() => new(_painel.Object, _estacoes.Object, _usuarioAtual.Object, _relogio.Object);

    private static Aurora.Domain.Entidades.EstacaoProducao Estacao(byte id, string nome, byte ordem, bool geraEtapa)
    {
        var estacao = (Aurora.Domain.Entidades.EstacaoProducao)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Aurora.Domain.Entidades.EstacaoProducao));
        foreach (var (nomeDaPropriedade, valor) in new (string, object)[] { ("Id", id), ("Nome", nome), ("Ordem", ordem), ("GeraEtapa", geraEtapa) })
        {
            typeof(Aurora.Domain.Entidades.EstacaoProducao).GetProperty(nomeDaPropriedade)!.SetValue(estacao, valor);
        }

        return estacao;
    }

    [Fact]
    public async Task Producao_completa_as_estacoes_sem_ordem_com_zeros_e_deixa_de_fora_as_que_nao_geram_etapa()
    {
        _estacoes.Setup(r => r.ListarAsync(default)).ReturnsAsync(
        [
            Estacao(3, "Estofaria", 3, true),
            Estacao(1, "Corte", 1, true),
            Estacao(6, "Expedicao", 6, false),
            Estacao(2, "Montagem", 2, true),
        ]);
        _painel.Setup(r => r.ListarProducaoPorEstacaoAsync(default)).ReturnsAsync(
            [new ProducaoDaEstacaoDto(2, "Montagem", 2, 4, 1, 3, 2, 37.5)]);

        var lista = await Servico().ObterProducaoPorEstacaoAsync();

        Assert.Equal(["Corte", "Montagem", "Estofaria"], lista.Select(e => e.Estacao));
        Assert.Equal(4, lista[1].OrdensNaEstacao);
        Assert.Equal(37.5, lista[1].TempoMedioNaEtapaMinutos);
        Assert.Equal(0, lista[0].OrdensNaEstacao);
        Assert.Null(lista[0].TempoMedioNaEtapaMinutos);
    }

    [Theory]
    [InlineData(PerfilUsuario.Vendedor, 9)]
    [InlineData(PerfilUsuario.GerenteProducao, null)]
    [InlineData(PerfilUsuario.Administrador, null)]
    public async Task Pedidos_atrasados_restringem_so_o_vendedor_aos_proprios(PerfilUsuario perfil, int? esperado)
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(perfil);
        _painel.Setup(r => r.ListarPedidosAtrasadosAsync(esperado, 1, 20, default)).ReturnsAsync(([], 0));

        await Servico().ListarPedidosAtrasadosAsync(1, 20);

        _painel.Verify(r => r.ListarPedidosAtrasadosAsync(esperado, 1, 20, default), Times.Once);
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData(7, 7)]
    [InlineData(0, 1)]
    [InlineData(-9, 1)]
    [InlineData(5000, 365)]
    public async Task Janela_de_consumo_e_limitada_a_1_ate_365_dias_contando_hoje(int? dias, int esperado)
    {
        DateOnly? de = null;
        DateOnly? ate = null;
        _painel.Setup(r => r.ListarConsumoAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), PainelService.MateriaisNoGrafico, default))
            .Callback<DateOnly, DateOnly, int, CancellationToken>((d, a, _, _) => (de, ate) = (d, a))
            .ReturnsAsync([]);

        await Servico().ObterConsumoAsync(dias);

        Assert.Equal(new DateOnly(2026, 11, 2), ate);
        Assert.Equal(esperado, ate!.Value.DayNumber - de!.Value.DayNumber + 1);
    }

    [Fact]
    public async Task Resumo_usa_o_relogio_do_sistema()
    {
        _painel.Setup(r => r.ObterResumoAsync(Agora, default)).ReturnsAsync(new ResumoDoPainelDto(1, 2, 3, 4, 5, 6, 7, 8, 9, Agora));

        var resumo = await Servico().ObterResumoAsync();

        Assert.Equal(Agora, resumo.AtualizadoEm);
        Assert.Equal(7, resumo.MateriaisCriticos);
    }
}
