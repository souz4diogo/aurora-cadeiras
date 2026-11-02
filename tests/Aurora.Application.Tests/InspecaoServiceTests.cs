using Aurora.Application.Abstracoes;
using Aurora.Application.Qualidade;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class InspecaoServiceTests
{
    private readonly Mock<IQualidadeRepository> _qualidade = new();

    private InspecaoService Servico() => new(_qualidade.Object, new InspecaoRequestValidator());

    private static InspecaoResumo Resumo(ResultadoInspecao resultado, byte? retrabalho = null) => new(
        7, 12, "OP-2026-00012", "PED-2026-00031", "Cadeira Ergonômica", resultado, retrabalho, retrabalho is null ? null : "Montagem da Estrutura",
        null, 9, "Marta", new DateTime(2026, 10, 30, 15, 0, 0, DateTimeKind.Utc),
        resultado == ResultadoInspecao.Aprovada ? StatusOrdemProducao.Concluida : StatusOrdemProducao.EmAndamento,
        resultado == ResultadoInspecao.Aprovada ? StatusPedido.Pronto : StatusPedido.EmProducao);

    [Fact]
    public async Task Aprovada_descarta_a_estacao_de_retrabalho_e_normaliza_a_observacao()
    {
        _qualidade.Setup(r => r.ObterUltimaDaOrdemAsync(12, default)).ReturnsAsync(Resumo(ResultadoInspecao.Aprovada));

        var resposta = await Servico().RegistrarAsync(12, new InspecaoRequest(" aprovada ", 2, "  ok  "));

        _qualidade.Verify(r => r.RegistrarInspecaoAsync(12, ResultadoInspecao.Aprovada, null, "ok", default), Times.Once);
        Assert.Equal("Aprovada", resposta.Resultado);
        Assert.Equal("Pronto", resposta.StatusPedido);
    }

    [Fact]
    public async Task Reprovada_repassa_a_estacao_de_retrabalho_e_a_regra_RN06_fica_na_procedure()
    {
        _qualidade.Setup(r => r.ObterUltimaDaOrdemAsync(12, default)).ReturnsAsync(Resumo(ResultadoInspecao.Reprovada, 2));

        var resposta = await Servico().RegistrarAsync(12, new InspecaoRequest("Reprovada", 2, "Costura torta"));

        _qualidade.Verify(r => r.RegistrarInspecaoAsync(12, ResultadoInspecao.Reprovada, 2, "Costura torta", default), Times.Once);
        Assert.Equal((byte?)2, resposta.EstacaoRetrabalhoId);
        Assert.Equal("EmAndamento", resposta.StatusOrdem);
    }

    [Fact]
    public async Task Reprovada_sem_estacao_chega_a_procedure_que_devolve_o_erro_da_regra()
    {
        _qualidade.Setup(r => r.RegistrarInspecaoAsync(12, ResultadoInspecao.Reprovada, null, null, default))
            .ThrowsAsync(new RegraDeNegocioException("RN06", "Inspeção reprovada exige a estação de retrabalho.", TipoErro.EntidadeNaoProcessavel));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RegistrarAsync(12, new InspecaoRequest("Reprovada", null, null)));

        Assert.Equal("RN06", erro.Codigo);
    }

    [Theory]
    [InlineData("Talvez")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Resultado_invalido_lanca_validacao_sem_chamar_a_procedure(string? resultado)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().RegistrarAsync(12, new InspecaoRequest(resultado!, null, null)));

        _qualidade.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Observacao_acima_do_limite_lanca_validacao()
    {
        var longa = new string('x', 301);

        await Assert.ThrowsAsync<ValidationException>(() => Servico().RegistrarAsync(12, new InspecaoRequest("Aprovada", null, longa)));
    }

    [Fact]
    public async Task Sem_inspecao_gravada_depois_da_procedure_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RegistrarAsync(12, new InspecaoRequest("Aprovada", null, null)));

        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Fact]
    public async Task Listar_normaliza_a_paginacao_e_repassa_os_filtros()
    {
        FiltroDeInspecoes? usado = null;
        _qualidade.Setup(r => r.ListarAsync(It.IsAny<FiltroDeInspecoes>(), 1, 100, default))
            .Callback<FiltroDeInspecoes, int, int, CancellationToken>((f, _, _, _) => usado = f)
            .ReturnsAsync(([Resumo(ResultadoInspecao.Aprovada)], 1));

        var pagina = await Servico().ListarAsync(12, ResultadoInspecao.Aprovada, null, null, pagina: 0, tamanho: 5000);

        Assert.Equal(new FiltroDeInspecoes(12, ResultadoInspecao.Aprovada, null, null), usado);
        Assert.Equal(1, pagina.Page);
        Assert.Equal(100, pagina.PageSize);
        Assert.Equal(1, pagina.Total);
    }
}
