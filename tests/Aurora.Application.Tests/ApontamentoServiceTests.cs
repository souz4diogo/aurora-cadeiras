using Aurora.Application.Abstracoes;
using Aurora.Application.Producao;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class ApontamentoServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Hora = new(Agora.AddMinutes(-5), TimeSpan.Zero);

    private readonly Mock<IProducaoRepository> _producao = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public ApontamentoServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Operador);
        _producao.Setup(r => r.ObterEtapaAsync(It.IsAny<int>(), default))
            .ReturnsAsync((int id, CancellationToken _) => Etapa(id));
    }

    private static EtapaResumo Etapa(int id) => new(
        id, 12, "OP-2026-00012", StatusOrdemProducao.EmAndamento, 10, 2, "Montagem", 2, StatusEtapa.Pendente, null, null, null, null);

    private ApontamentoService Servico()
    {
        var etapas = new EtapaService(
            _producao.Object,
            _uow.Object,
            _usuarioAtual.Object,
            new ApontamentoRequestValidator(_relogio.Object),
            new PerdaRequestValidator());
        return new ApontamentoService(etapas, _producao.Object, _usuarioAtual.Object, new SincronizacaoRequestValidator());
    }

    private static ApontamentoOfflineItem Item(string tipo, Guid? id = null, int etapaId = 341, DateTimeOffset? hora = null) =>
        new(id ?? Guid.NewGuid(), tipo, etapaId, hora ?? Hora);

    [Fact]
    public async Task Inicio_e_fim_chamam_as_procedures_certas_com_a_hora_do_aparelho_em_utc()
    {
        var inicio = Item("Inicio");
        var fim = Item("fim"); // sem diferenciar maiúsculas

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([inicio, fim]));

        Assert.Equal(["Aplicado", "Aplicado"], resposta.Resultados.Select(r => r.Status));
        _producao.Verify(r => r.IniciarEtapaAsync(341, inicio.ClientRequestId, Hora.UtcDateTime, default), Times.Once);
        _producao.Verify(r => r.FinalizarEtapaAsync(341, fim.ClientRequestId, Hora.UtcDateTime, default), Times.Once);
    }

    [Fact]
    public async Task Item_ja_registrado_devolve_JaProcessado_sem_chamar_a_procedure()
    {
        var item = Item("Inicio");
        _producao.Setup(r => r.ApontamentoRegistradoAsync(item.ClientRequestId, default)).ReturnsAsync(true);

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([item]));

        var resultado = Assert.Single(resposta.Resultados);
        Assert.Equal(StatusDeSincronizacao.JaProcessado, resultado.Status);
        Assert.Null(resultado.Code);
        _producao.Verify(r => r.IniciarEtapaAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Regra_de_negocio_rejeita_so_o_item_e_o_lote_continua_na_ordem()
    {
        var recusado = Item("Inicio", etapaId: 1);
        var aceito = Item("Inicio", etapaId: 2);
        _producao.Setup(r => r.IniciarEtapaAsync(1, recusado.ClientRequestId, It.IsAny<DateTime>(), default))
            .ThrowsAsync(new RegraDeNegocioException("RN05", "Etapa anterior não concluída.", TipoErro.Conflito));

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([recusado, aceito]));

        Assert.Equal([recusado.ClientRequestId, aceito.ClientRequestId], resposta.Resultados.Select(r => r.ClientRequestId));
        Assert.Equal(StatusDeSincronizacao.Rejeitado, resposta.Resultados[0].Status);
        Assert.Equal("RN05", resposta.Resultados[0].Code);
        Assert.Equal("Etapa anterior não concluída.", resposta.Resultados[0].Mensagem);
        Assert.Equal(StatusDeSincronizacao.Aplicado, resposta.Resultados[1].Status);
    }

    [Fact]
    public async Task Etapa_inexistente_e_rejeitada_com_o_codigo_da_etapa()
    {
        _producao.Setup(r => r.ObterEtapaAsync(999, default)).ReturnsAsync((EtapaResumo?)null);

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([Item("Inicio", etapaId: 999)]));

        Assert.Equal("ETAPA_NAO_ENCONTRADA", Assert.Single(resposta.Resultados).Code);
    }

    [Theory]
    [InlineData("Pausa")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Tipo_desconhecido_e_rejeitado_com_VALIDACAO_sem_tocar_no_banco(string? tipo)
    {
        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([Item(tipo!)]));

        Assert.Equal("VALIDACAO", Assert.Single(resposta.Resultados).Code);
        _producao.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Hora_do_aparelho_no_futuro_e_rejeitada_com_VALIDACAO()
    {
        var futuro = new DateTimeOffset(Agora.AddHours(3), TimeSpan.Zero);

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([Item("Inicio", hora: futuro)]));

        var resultado = Assert.Single(resposta.Resultados);
        Assert.Equal(StatusDeSincronizacao.Rejeitado, resultado.Status);
        Assert.Equal("VALIDACAO", resultado.Code);
    }

    [Fact]
    public async Task Identificador_vazio_e_rejeitado_com_VALIDACAO()
    {
        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([Item("Inicio", Guid.Empty)]));

        Assert.Equal("VALIDACAO", Assert.Single(resposta.Resultados).Code);
    }

    [Fact]
    public async Task Somente_o_operador_finaliza_etapa()
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Inspetor);
        var item = Item("Fim");

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([item]));

        var resultado = Assert.Single(resposta.Resultados);
        Assert.Equal("PERFIL_SEM_PERMISSAO", resultado.Code);
        _producao.Verify(r => r.FinalizarEtapaAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Item_nulo_e_rejeitado_sem_derrubar_o_lote()
    {
        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest([null!, Item("Inicio")]));

        Assert.Equal(StatusDeSincronizacao.Rejeitado, resposta.Resultados[0].Status);
        Assert.Equal(StatusDeSincronizacao.Aplicado, resposta.Resultados[1].Status);
    }

    [Fact]
    public async Task Lote_vazio_nulo_ou_grande_demais_lanca_validacao()
    {
        var grande = Enumerable.Range(0, SincronizacaoRequestValidator.TamanhoMaximoDoLote + 1).Select(_ => Item("Inicio")).ToList();

        await Assert.ThrowsAsync<ValidationException>(() => Servico().SincronizarAsync(new SincronizacaoRequest([])));
        await Assert.ThrowsAsync<ValidationException>(() => Servico().SincronizarAsync(new SincronizacaoRequest(null!)));
        await Assert.ThrowsAsync<ValidationException>(() => Servico().SincronizarAsync(new SincronizacaoRequest(grande)));
    }

    [Fact]
    public async Task Lote_no_limite_e_aceito()
    {
        var lote = Enumerable.Range(0, SincronizacaoRequestValidator.TamanhoMaximoDoLote).Select(_ => Item("Inicio")).ToList();

        var resposta = await Servico().SincronizarAsync(new SincronizacaoRequest(lote));

        Assert.Equal(SincronizacaoRequestValidator.TamanhoMaximoDoLote, resposta.Resultados.Count);
    }
}
