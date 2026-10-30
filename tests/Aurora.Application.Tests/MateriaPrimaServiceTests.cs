using Aurora.Application.Abstracoes;
using Aurora.Application.MateriasPrimas;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class MateriaPrimaServiceTests
{
    private readonly Mock<IMateriaPrimaRepository> _materiasPrimas = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private MateriaPrimaService Servico() => new(_materiasPrimas.Object, _uow.Object, new MateriaPrimaRequestValidator());

    private static MateriaPrima NovaMateriaPrima() =>
        new("MP-001", "Madeira", UnidadeDeMedida.MetroCubico, 2m, 4500m, null);

    [Fact]
    public async Task Criar_converte_a_unidade_e_salva_com_saldo_zerado()
    {
        var resposta = await Servico().CriarAsync(new MateriaPrimaRequest(" mp-010 ", "Tecido", "m", 10m, 25.5m, null));

        Assert.Equal("MP-010", resposta.Codigo);
        Assert.Equal("m", resposta.Unidade);
        Assert.Equal(0m, resposta.EstoqueAtual);
        _materiasPrimas.Verify(r => r.Adicionar(It.Is<MateriaPrima>(m => m.Unidade == UnidadeDeMedida.Metro)), Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Criar_com_codigo_existente_devolve_conflito()
    {
        _materiasPrimas.Setup(r => r.CodigoExisteAsync("MP-001", null, default)).ReturnsAsync(true);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new MateriaPrimaRequest("MP-001", "Madeira", "m3", 1m, 1m, null)));

        Assert.Equal("CODIGO_DUPLICADO", erro.Codigo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Criar_com_fornecedor_inexistente_devolve_422()
    {
        _materiasPrimas.Setup(r => r.FornecedorExisteAsync(99, default)).ReturnsAsync(false);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new MateriaPrimaRequest("MP-020", "Cola", "l", 1m, 1m, 99)));

        Assert.Equal("FORNECEDOR_INEXISTENTE", erro.Codigo);
        Assert.Equal(TipoErro.EntidadeNaoProcessavel, erro.Tipo);
    }

    [Theory]
    [InlineData("MP-1", "Cola", "kg", 1, 1)]
    [InlineData("MP-1", "Cola", "l", -1, 1)]
    [InlineData("MP-1", "Cola", "l", 1, -1)]
    [InlineData("", "Cola", "l", 1, 1)]
    [InlineData("MP-1", "", "l", 1, 1)]
    public async Task Criar_com_entrada_invalida_lanca_validacao(string codigo, string nome, string unidade, int minimo, int custo)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new MateriaPrimaRequest(codigo, nome, unidade, minimo, custo, null)));
    }

    [Fact]
    public async Task Atualizar_nao_altera_o_saldo()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdAsync(3, default)).ReturnsAsync(NovaMateriaPrima());

        var resposta = await Servico().AtualizarAsync(3, new MateriaPrimaRequest("MP-001", "Madeira de lei", "m3", 5m, 4800m, null));

        Assert.Equal("Madeira de lei", resposta.Nome);
        Assert.Equal(5m, resposta.EstoqueMinimo);
        Assert.Equal(0m, resposta.EstoqueAtual);
        _materiasPrimas.Verify(r => r.CodigoExisteAsync("MP-001", 3, default), Times.Once);
    }

    [Fact]
    public async Task Trocar_a_unidade_de_materia_prima_em_uso_devolve_conflito_e_nao_salva()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdAsync(3, default)).ReturnsAsync(NovaMateriaPrima());
        _materiasPrimas.Setup(r => r.EmUsoAsync(It.IsAny<int>(), default)).ReturnsAsync(true);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().AtualizarAsync(3, new MateriaPrimaRequest("MP-001", "Madeira", "m2", 2m, 4500m, null)));

        Assert.Equal("UNIDADE_EM_USO", erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Manter_a_mesma_unidade_vale_mesmo_em_uso_e_nem_consulta_o_uso()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdAsync(3, default)).ReturnsAsync(NovaMateriaPrima());
        _materiasPrimas.Setup(r => r.EmUsoAsync(It.IsAny<int>(), default)).ReturnsAsync(true);

        var resposta = await Servico().AtualizarAsync(3, new MateriaPrimaRequest("MP-001", "Madeira de lei", "m3", 2m, 4500m, null));

        Assert.Equal("m3", resposta.Unidade);
        _materiasPrimas.Verify(r => r.EmUsoAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Antes_do_primeiro_uso_a_unidade_pode_ser_corrigida()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdAsync(3, default)).ReturnsAsync(NovaMateriaPrima());
        _materiasPrimas.Setup(r => r.EmUsoAsync(It.IsAny<int>(), default)).ReturnsAsync(false);

        var resposta = await Servico().AtualizarAsync(3, new MateriaPrimaRequest("MP-001", "Madeira", "m2", 2m, 4500m, null));

        Assert.Equal("m2", resposta.Unidade);
    }

    [Fact]
    public async Task Obter_inexistente_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(404));

        Assert.Equal("MATERIA_PRIMA_NAO_ENCONTRADA", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Fact]
    public async Task Alterar_ativo_inativa_a_materia_prima()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdAsync(1, default)).ReturnsAsync(NovaMateriaPrima());

        var resposta = await Servico().AlterarAtivoAsync(1, false);

        Assert.False(resposta.Ativo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }
}
