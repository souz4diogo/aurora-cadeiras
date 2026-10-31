using Aurora.Application.Abstracoes;
using Aurora.Application.Roteiros;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class RoteiroServiceTests
{
    private readonly Mock<IProdutoRepository> _produtos = new();
    private readonly Mock<IRoteiroRepository> _roteiros = new();
    private readonly Mock<IFichaTecnicaRepository> _fichas = new();
    private readonly Mock<IEstacaoRepository> _estacoes = new();

    public RoteiroServiceTests()
    {
        _produtos.Setup(r => r.ObterPorIdAsync(7, default))
            .ReturnsAsync(new Produto("CE-100", "Cadeira", LinhaProduto.Escritorio, 100m));
        _estacoes.Setup(r => r.ListarAsync(default)).ReturnsAsync(
        [
            Estacao(1, "Corte", 1, true),
            Estacao(2, "Montagem", 2, true),
            Estacao(6, "Expedicao", 6, false),
        ]);
        _roteiros.Setup(r => r.ListarPorProdutoAsync(7, default)).ReturnsAsync([]);
        _fichas.Setup(r => r.ListarPorProdutoAsync(7, default)).ReturnsAsync([]);
    }

    private RoteiroService Servico() =>
        new(_produtos.Object, _roteiros.Object, _fichas.Object, _estacoes.Object, new RoteiroRequestValidator());

    private static EstacaoProducao Estacao(byte id, string nome, byte ordem, bool geraEtapa)
    {
        var estacao = (EstacaoProducao)Activator.CreateInstance(typeof(EstacaoProducao), nonPublic: true)!;
        typeof(EstacaoProducao).GetProperty(nameof(EstacaoProducao.Id))!.SetValue(estacao, id);
        typeof(EstacaoProducao).GetProperty(nameof(EstacaoProducao.Nome))!.SetValue(estacao, nome);
        typeof(EstacaoProducao).GetProperty(nameof(EstacaoProducao.Ordem))!.SetValue(estacao, ordem);
        typeof(EstacaoProducao).GetProperty(nameof(EstacaoProducao.GeraEtapa))!.SetValue(estacao, geraEtapa);
        return estacao;
    }

    [Fact]
    public async Task Substituir_numera_a_sequencia_pela_posicao_na_lista()
    {
        IReadOnlyList<RoteiroProduto>? gravado = null;
        _roteiros.Setup(r => r.SubstituirAsync(7, It.IsAny<IReadOnlyList<RoteiroProduto>>(), default))
            .Callback<int, IReadOnlyList<RoteiroProduto>, CancellationToken>((_, etapas, _) => gravado = etapas)
            .Returns(Task.CompletedTask);

        await Servico().SubstituirAsync(7, new RoteiroRequest(new byte[] { 2, 1 }));

        Assert.Equal([(2, 1), (1, 2)], gravado!.Select(e => ((int)e.EstacaoId, (int)e.Sequencia)));
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)42)]
    public async Task Estacao_que_nao_existe_ou_nao_gera_etapa_e_rejeitada(byte estacao)
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().SubstituirAsync(7, new RoteiroRequest(new byte[] { 1, estacao })));

        Assert.Equal("ESTACAO_INVALIDA", erro.Codigo);
        Assert.Equal(TipoErro.EntidadeNaoProcessavel, erro.Tipo);
        _roteiros.Verify(r => r.SubstituirAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RoteiroProduto>>(), default), Times.Never);
    }

    [Fact]
    public async Task Nao_retira_do_roteiro_uma_estacao_usada_pela_ficha()
    {
        _fichas.Setup(r => r.ListarPorProdutoAsync(7, default)).ReturnsAsync([new FichaTecnicaItem(7, 1, 2, 1m)]);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().SubstituirAsync(7, new RoteiroRequest(new byte[] { 1 })));

        Assert.Equal("ROTEIRO_INCOMPATIVEL_COM_FICHA", erro.Codigo);
        _roteiros.Verify(r => r.SubstituirAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RoteiroProduto>>(), default), Times.Never);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 1, 1 })]
    public async Task Roteiro_vazio_ou_repetido_lanca_validacao(byte[] estacoes)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().SubstituirAsync(7, new RoteiroRequest(estacoes)));
    }

    [Fact]
    public async Task Produto_inexistente_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(404));

        Assert.Equal("PRODUTO_NAO_ENCONTRADO", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }
}
