using Aurora.Application.Abstracoes;
using Aurora.Application.Producao;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using Moq;

namespace Aurora.Application.Tests;

public class OrdemProducaoServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProducaoRepository> _producao = new();
    private readonly Mock<IEstacaoRepository> _estacoes = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public OrdemProducaoServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.GerenteProducao);
    }

    private OrdemProducaoService Servico() => new(_producao.Object, _estacoes.Object, _usuarioAtual.Object, _relogio.Object);

    private static EstacaoProducao Estacao(byte id, string nome, byte ordem, bool geraEtapa = true)
    {
        var estacao = (EstacaoProducao)Activator.CreateInstance(typeof(EstacaoProducao), nonPublic: true)!;
        void Definir(string propriedade, object valor) =>
            typeof(EstacaoProducao).GetProperty(propriedade)!.SetValue(estacao, valor);
        Definir(nameof(EstacaoProducao.Id), id);
        Definir(nameof(EstacaoProducao.Nome), nome);
        Definir(nameof(EstacaoProducao.Ordem), ordem);
        Definir(nameof(EstacaoProducao.GeraEtapa), geraEtapa);
        return estacao;
    }

    private static OrdemProducaoResumo Ordem(
        int id,
        EtapaAtualResumo? atual,
        StatusOrdemProducao status = StatusOrdemProducao.EmAndamento,
        DateOnly? previsao = null) => new(
        id, $"OP-2026-{id:00000}", 1, "PV-2026-00001", 1, "CE-100", "Cadeira", 5, status,
        previsao ?? new DateOnly(2026, 11, 20), Agora.AddDays(-3), null, 4, 1, atual);

    private static EtapaAtualResumo Etapa(byte estacaoId, string nome, StatusEtapa status, DateTime? iniciada = null) =>
        new(estacaoId * 10, estacaoId, nome, estacaoId, status, iniciada);

    [Fact]
    public async Task Listar_restringe_o_vendedor_as_proprias_ordens_e_normaliza_a_pagina()
    {
        FiltroDeOrdens? filtroUsado = null;
        _producao.Setup(r => r.ListarOrdensAsync(It.IsAny<FiltroDeOrdens>(), It.IsAny<int>(), It.IsAny<int>(), default))
            .Callback<FiltroDeOrdens, int, int, CancellationToken>((f, _, _, _) => filtroUsado = f)
            .ReturnsAsync(([], 0));
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Vendedor);

        var pagina = await Servico().ListarAsync(StatusOrdemProducao.Planejada, 2, 7, true, 0, 5000);

        Assert.Equal(9, filtroUsado!.VendedorId);
        Assert.Equal(StatusOrdemProducao.Planejada, filtroUsado.Status);
        Assert.Equal((byte)2, filtroUsado.EstacaoId);
        Assert.Equal(7, filtroUsado.PedidoId);
        Assert.Equal(new DateOnly(2026, 10, 30), filtroUsado.Hoje);
        Assert.Equal(100, pagina.PageSize);
    }

    [Fact]
    public async Task Listar_calcula_atraso_e_minutos_na_etapa_com_o_relogio()
    {
        var iniciada = Agora.AddMinutes(-47);
        _producao.Setup(r => r.ListarOrdensAsync(It.IsAny<FiltroDeOrdens>(), 1, 20, default)).ReturnsAsync((
            [
                Ordem(1, Etapa(2, "Montagem", StatusEtapa.EmAndamento, iniciada), previsao: new DateOnly(2026, 10, 1)),
                Ordem(2, Etapa(2, "Montagem", StatusEtapa.Pendente)),
                Ordem(3, null, StatusOrdemProducao.Concluida, new DateOnly(2026, 10, 1)),
            ], 3));

        var pagina = await Servico().ListarAsync(null, null, null, null, 1, 20);

        Assert.Equal([true, false, false], pagina.Items.Select(i => i.Atrasada));
        Assert.Equal(47, pagina.Items[0].EtapaAtual!.MinutosNaEtapa);
        Assert.Null(pagina.Items[1].EtapaAtual!.MinutosNaEtapa);
        Assert.Null(pagina.Items[2].EtapaAtual);
    }

    [Fact]
    public async Task Quadro_agrupa_as_ordens_pela_estacao_atual_na_ordem_do_layout_e_ignora_estacao_sem_etapa()
    {
        _estacoes.Setup(r => r.ListarAsync(default)).ReturnsAsync(
        [
            Estacao(3, "Estofaria", 3),
            Estacao(1, "Corte", 1),
            Estacao(2, "Montagem", 2),
            Estacao(6, "Expedição", 6, geraEtapa: false),
        ]);
        _producao.Setup(r => r.ListarOrdensAsync(It.IsAny<FiltroDeOrdens>(), 1, OrdemProducaoService.LimiteDoQuadro, default)).ReturnsAsync((
            [
                Ordem(1, Etapa(2, "Montagem", StatusEtapa.EmAndamento, Agora.AddMinutes(-5))),
                Ordem(2, Etapa(1, "Corte", StatusEtapa.Pendente)),
                Ordem(3, Etapa(2, "Montagem", StatusEtapa.Pendente)),
                Ordem(4, null),
            ], 4));

        var quadro = await Servico().ObterQuadroAsync();

        Assert.Equal(["Corte", "Montagem", "Estofaria"], quadro.Colunas.Select(c => c.EstacaoNome));
        Assert.Equal([2], quadro.Colunas[0].Ordens.Select(o => o.Id));
        Assert.Equal([1, 3], quadro.Colunas[1].Ordens.Select(o => o.Id));
        Assert.Empty(quadro.Colunas[2].Ordens);
        Assert.Equal(4, quadro.TotalDeOrdensAbertas);
        Assert.False(quadro.Truncado);
    }

    [Fact]
    public async Task Quadro_pede_so_as_abertas_e_avisa_quando_ha_mais_do_que_cabe()
    {
        FiltroDeOrdens? filtroUsado = null;
        _estacoes.Setup(r => r.ListarAsync(default)).ReturnsAsync([Estacao(1, "Corte", 1)]);
        _producao.Setup(r => r.ListarOrdensAsync(It.IsAny<FiltroDeOrdens>(), 1, OrdemProducaoService.LimiteDoQuadro, default))
            .Callback<FiltroDeOrdens, int, int, CancellationToken>((f, _, _, _) => filtroUsado = f)
            .ReturnsAsync(([Ordem(1, Etapa(1, "Corte", StatusEtapa.Pendente))], 450));

        var quadro = await Servico().ObterQuadroAsync();

        Assert.True(filtroUsado!.SomenteAbertas);
        Assert.True(quadro.Truncado);
        Assert.Equal(450, quadro.TotalDeOrdensAbertas);
    }

    [Fact]
    public async Task Obter_ordem_inexistente_ou_de_outro_vendedor_devolve_nao_encontrado()
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Vendedor);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(5));

        Assert.Equal("ORDEM_PRODUCAO_NAO_ENCONTRADA", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        _producao.Verify(r => r.ObterOrdemAsync(5, 9, default), Times.Once);
    }

    [Fact]
    public async Task Obter_ordem_devolve_o_detalhe_com_o_codigo_do_qr()
    {
        var qr = Guid.NewGuid();
        _producao.Setup(r => r.ObterOrdemAsync(5, null, default)).ReturnsAsync(new OrdemProducaoDetalhe(
            Ordem(5, Etapa(1, "Corte", StatusEtapa.Pendente)), qr, "Móveis Lima", [], [], []));

        var ordem = await Servico().ObterAsync(5);

        Assert.Equal(qr, ordem.CodigoQr);
        Assert.Equal("Móveis Lima", ordem.ClienteNome);
        Assert.Equal("OP-2026-00005", ordem.Ordem.Numero);
    }
}
