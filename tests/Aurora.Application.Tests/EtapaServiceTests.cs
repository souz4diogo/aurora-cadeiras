using Aurora.Application.Abstracoes;
using Aurora.Application.Producao;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class EtapaServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Requisicao = Guid.Parse("b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10");

    private readonly Mock<IProducaoRepository> _producao = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public EtapaServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
    }

    private EtapaService Servico() => new(
        _producao.Object, _uow.Object, _usuarioAtual.Object,
        new ApontamentoRequestValidator(_relogio.Object), new PerdaRequestValidator());

    private static EtapaResumo Etapa(StatusEtapa status = StatusEtapa.Pendente, int quantidadeOp = 10) => new(
        341, 12, "OP-2026-00012", StatusOrdemProducao.EmAndamento, quantidadeOp, 2, "Montagem", 2, status, null, null, null, null);

    private void EtapaNoBanco(EtapaResumo etapa) =>
        _producao.Setup(r => r.ObterEtapaAsync(etapa.Id, default)).ReturnsAsync(etapa);

    private static ApontamentoRequest Apontamento(DateTimeOffset? hora = null) =>
        new(Requisicao, hora ?? new DateTimeOffset(Agora, TimeSpan.Zero));

    [Fact]
    public async Task Iniciar_executa_a_procedure_com_o_identificador_da_requisicao_em_utc_e_devolve_o_novo_estado()
    {
        _producao.SetupSequence(r => r.ObterEtapaAsync(341, default))
            .ReturnsAsync(Etapa())
            .ReturnsAsync(Etapa(StatusEtapa.EmAndamento));
        var horaLocal = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(-3));

        var resposta = await Servico().IniciarAsync(341, Apontamento(horaLocal));

        _producao.Verify(r => r.IniciarEtapaAsync(341, Requisicao, new DateTime(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc), default), Times.Once);
        Assert.Equal("EmAndamento", resposta.Status);
        Assert.Equal("Montagem", resposta.EstacaoNome);
    }

    [Fact]
    public async Task Iniciar_etapa_inexistente_devolve_nao_encontrado_sem_chamar_a_procedure()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().IniciarAsync(999, Apontamento()));

        Assert.Equal("ETAPA_NAO_ENCONTRADA", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        _producao.Verify(
            r => r.IniciarEtapaAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Finalizar_executa_a_procedure_de_finalizacao()
    {
        _producao.SetupSequence(r => r.ObterEtapaAsync(341, default))
            .ReturnsAsync(Etapa(StatusEtapa.EmAndamento))
            .ReturnsAsync(Etapa(StatusEtapa.Concluida));

        var resposta = await Servico().FinalizarAsync(341, Apontamento());

        _producao.Verify(r => r.FinalizarEtapaAsync(341, Requisicao, Agora, default), Times.Once);
        Assert.Equal("Concluida", resposta.Status);
    }

    [Fact]
    public async Task Apontamento_com_identificador_vazio_ou_hora_invalida_lanca_validacao()
    {
        EtapaNoBanco(Etapa());
        var semIdentificador = new ApontamentoRequest(Guid.Empty, new DateTimeOffset(Agora, TimeSpan.Zero));
        var semHora = new ApontamentoRequest(Requisicao, default);
        var noFuturo = Apontamento(new DateTimeOffset(Agora.AddHours(2), TimeSpan.Zero));

        foreach (var pedido in new[] { semIdentificador, semHora, noFuturo })
        {
            await Assert.ThrowsAsync<ValidationException>(() => Servico().IniciarAsync(341, pedido));
        }

        _producao.Verify(
            r => r.IniciarEtapaAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Apontamento_com_o_relogio_do_aparelho_um_pouco_adiantado_e_aceito()
    {
        EtapaNoBanco(Etapa());

        await Servico().IniciarAsync(341, Apontamento(new DateTimeOffset(Agora.AddMinutes(5), TimeSpan.Zero)));

        _producao.Verify(
            r => r.IniciarEtapaAsync(341, Requisicao, Agora.AddMinutes(5), default), Times.Once);
    }

    [Fact]
    public async Task Registrar_perda_grava_com_o_usuario_logado_e_o_tipo_sem_diferenciar_maiusculas()
    {
        EtapaNoBanco(Etapa(StatusEtapa.EmAndamento));
        RegistroPerda? gravada = null;
        _producao.Setup(r => r.AdicionarPerda(It.IsAny<RegistroPerda>())).Callback<RegistroPerda>(p => gravada = p);

        var resposta = await Servico().RegistrarPerdaAsync(341, new PerdaRequest(" refugo ", 2, " Peça rachada "));

        Assert.Equal(TipoPerda.Refugo, gravada!.Tipo);
        Assert.Equal(9, gravada.UsuarioId);
        Assert.Equal("Peça rachada", gravada.Motivo);
        Assert.Equal("Refugo", resposta.Tipo);
        Assert.Equal("Montagem", resposta.EstacaoNome);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Theory]
    [InlineData(StatusEtapa.Pendente)]
    [InlineData(StatusEtapa.Concluida)]
    public async Task Perda_so_e_aceita_em_etapa_em_andamento(StatusEtapa status)
    {
        EtapaNoBanco(Etapa(status));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RegistrarPerdaAsync(341, new PerdaRequest("Refugo", 1, "Motivo")));

        Assert.Equal("STATUS_INVALIDO", erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Perda_maior_que_a_quantidade_da_ordem_e_rejeitada()
    {
        EtapaNoBanco(Etapa(StatusEtapa.EmAndamento, quantidadeOp: 10));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RegistrarPerdaAsync(341, new PerdaRequest("Retrabalho", 11, "Motivo")));

        Assert.Equal("PERDA_ACIMA_DA_QUANTIDADE", erro.Codigo);
        Assert.Equal(TipoErro.EntidadeNaoProcessavel, erro.Tipo);
    }

    [Theory]
    [InlineData("Quebra", 1, "Motivo")]
    [InlineData("", 1, "Motivo")]
    [InlineData("Refugo", 0, "Motivo")]
    [InlineData("Refugo", -3, "Motivo")]
    [InlineData("Refugo", 1, "")]
    [InlineData("Refugo", 1, "   ")]
    [InlineData("1", 1, "Motivo")]
    public async Task Perda_invalida_lanca_validacao(string tipo, int quantidade, string motivo)
    {
        EtapaNoBanco(Etapa(StatusEtapa.EmAndamento));

        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().RegistrarPerdaAsync(341, new PerdaRequest(tipo, quantidade, motivo)));

        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Perda_sem_usuario_autenticado_devolve_nao_autenticado()
    {
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns((int?)null);
        EtapaNoBanco(Etapa(StatusEtapa.EmAndamento));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RegistrarPerdaAsync(341, new PerdaRequest("Refugo", 1, "Motivo")));

        Assert.Equal(TipoErro.NaoAutenticado, erro.Tipo);
    }
}
