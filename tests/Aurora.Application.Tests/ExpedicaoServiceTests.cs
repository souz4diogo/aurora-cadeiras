using Aurora.Application.Abstracoes;
using Aurora.Application.Expedicoes;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class ExpedicaoServiceTests
{
    private readonly Mock<IExpedicaoRepository> _expedicoes = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();

    public ExpedicaoServiceTests()
    {
        _usuarioAtual.SetupGet(u => u.UsuarioId).Returns(9);
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(PerfilUsuario.Almoxarife);
    }

    private ExpedicaoService Servico() => new(_expedicoes.Object, _usuarioAtual.Object, new ExpedirRequestValidator());

    private static ExpedicaoResumo Resumo(string? transportadora = null, string? rastreio = null) => new(
        3, 31, "PED-2026-00031", "Móveis Lima", 9, "Ana", transportadora, rastreio, new DateTime(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Expedir_apara_os_textos_e_troca_vazio_por_nulo_antes_de_chamar_a_procedure()
    {
        _expedicoes.Setup(r => r.ObterPorPedidoAsync(31, default)).ReturnsAsync(Resumo("Rota Sul", null));

        var resposta = await Servico().ExpedirAsync(31, new ExpedirRequest("  Rota Sul  ", "   "));

        _expedicoes.Verify(r => r.ExpedirAsync(31, "Rota Sul", null, default), Times.Once);
        Assert.Equal("PED-2026-00031", resposta.PedidoNumero);
        Assert.Equal("Rota Sul", resposta.Transportadora);
    }

    [Fact]
    public async Task Pedido_nao_pronto_propaga_o_erro_RN07_da_procedure_e_nao_consulta_a_expedicao()
    {
        _expedicoes.Setup(r => r.ExpedirAsync(31, null, null, default))
            .ThrowsAsync(new RegraDeNegocioException("RN07", "Pedido não está pronto para expedição.", TipoErro.Conflito));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().ExpedirAsync(31, new ExpedirRequest(null, null)));

        Assert.Equal("RN07", erro.Codigo);
        _expedicoes.Verify(r => r.ObterPorPedidoAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Theory]
    [InlineData(81, 0)]
    [InlineData(0, 41)]
    public async Task Texto_acima_do_limite_lanca_validacao_sem_chamar_a_procedure(int transportadora, int rastreio)
    {
        var pedido = new ExpedirRequest(new string('t', transportadora), new string('r', rastreio));

        await Assert.ThrowsAsync<ValidationException>(() => Servico().ExpedirAsync(31, pedido));

        _expedicoes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Textos_no_limite_sao_aceitos()
    {
        _expedicoes.Setup(r => r.ObterPorPedidoAsync(31, default)).ReturnsAsync(Resumo());

        await Servico().ExpedirAsync(31, new ExpedirRequest(new string('t', 80), new string('r', 40)));

        _expedicoes.Verify(r => r.ExpedirAsync(31, new string('t', 80), new string('r', 40), default), Times.Once);
    }

    [Fact]
    public async Task Sem_expedicao_gravada_depois_da_procedure_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().ExpedirAsync(31, new ExpedirRequest(null, null)));

        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Theory]
    [InlineData(PerfilUsuario.Vendedor, 9)]
    [InlineData(PerfilUsuario.Almoxarife, null)]
    [InlineData(PerfilUsuario.GerenteProducao, null)]
    public async Task Listar_restringe_so_o_vendedor_aos_proprios_pedidos(PerfilUsuario perfil, int? esperado)
    {
        _usuarioAtual.SetupGet(u => u.Perfil).Returns(perfil);
        FiltroDeExpedicoes? usado = null;
        _expedicoes.Setup(r => r.ListarAsync(It.IsAny<FiltroDeExpedicoes>(), 1, 20, default))
            .Callback<FiltroDeExpedicoes, int, int, CancellationToken>((f, _, _, _) => usado = f)
            .ReturnsAsync(([Resumo()], 1));

        var pagina = await Servico().ListarAsync(null, null, 1, 20);

        Assert.Equal(esperado, usado!.VendedorId);
        Assert.Single(pagina.Items);
    }
}
