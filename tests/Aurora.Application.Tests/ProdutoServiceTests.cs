using Aurora.Application.Abstracoes;
using Aurora.Application.Produtos;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class ProdutoServiceTests
{
    private readonly Mock<IProdutoRepository> _produtos = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ProdutoService Servico() => new(_produtos.Object, _uow.Object, new ProdutoRequestValidator());

    private static Produto NovoProduto() => new("CE-100", "Cadeira", LinhaProduto.Escritorio, 100m);

    [Fact]
    public async Task Criar_normaliza_o_codigo_e_salva()
    {
        var resposta = await Servico().CriarAsync(new ProdutoRequest(" ce-300 ", " Cadeira Nova ", 1, 450.50m));

        Assert.Equal("CE-300", resposta.Codigo);
        Assert.Equal("Cadeira Nova", resposta.Nome);
        Assert.Equal("Escritorio", resposta.LinhaProduto);
        Assert.True(resposta.Ativo);
        _produtos.Verify(r => r.Adicionar(It.Is<Produto>(p => p.Codigo == "CE-300")), Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Criar_com_codigo_existente_devolve_conflito_e_nao_salva()
    {
        _produtos.Setup(r => r.CodigoExisteAsync("CE-100", null, default)).ReturnsAsync(true);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new ProdutoRequest("ce-100", "Cadeira", 1, 10m)));

        Assert.Equal("CODIGO_DUPLICADO", erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("", "Cadeira", (byte)1, 10)]
    [InlineData("CE 100", "Cadeira", (byte)1, 10)]
    [InlineData("CE-100", "", (byte)1, 10)]
    [InlineData("CE-100", "Cadeira", (byte)9, 10)]
    [InlineData("CE-100", "Cadeira", (byte)1, -1)]
    public async Task Criar_com_entrada_invalida_lanca_validacao(string codigo, string nome, byte linha, int preco)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new ProdutoRequest(codigo, nome, linha, preco)));

        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public void Codigo_nulo_e_uma_falha_de_validacao_e_nao_uma_excecao()
    {
        var resultado = new ProdutoRequestValidator().Validate(new ProdutoRequest(null!, "Cadeira", 1, 10m));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(ProdutoRequest.Codigo));
    }

    [Fact]
    public async Task Criar_rejeita_preco_com_mais_de_duas_casas_decimais()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new ProdutoRequest("CE-100", "Cadeira", 1, 10.999m)));
    }

    [Fact]
    public async Task Atualizar_ignora_o_proprio_produto_na_checagem_de_codigo()
    {
        var produto = NovoProduto();
        _produtos.Setup(r => r.ObterPorIdAsync(7, default)).ReturnsAsync(produto);

        var resposta = await Servico().AtualizarAsync(7, new ProdutoRequest("ce-100", "Cadeira Renovada", 2, 120m));

        Assert.Equal("Cadeira Renovada", resposta.Nome);
        Assert.Equal("Jantar", resposta.LinhaProduto);
        _produtos.Verify(r => r.CodigoExisteAsync("CE-100", 7, default), Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Atualizar_produto_inexistente_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().AtualizarAsync(99, new ProdutoRequest("CE-100", "Cadeira", 1, 10m)));

        Assert.Equal("PRODUTO_NAO_ENCONTRADO", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Fact]
    public async Task Alterar_ativo_inativa_o_produto()
    {
        _produtos.Setup(r => r.ObterPorIdAsync(1, default)).ReturnsAsync(NovoProduto());

        var resposta = await Servico().AlterarAtivoAsync(1, false);

        Assert.False(resposta.Ativo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Listar_normaliza_pagina_e_tamanho()
    {
        _produtos.Setup(r => r.ListarAsync("cadeira", true, 1, 100, default))
            .ReturnsAsync(([NovoProduto()], 1));

        var pagina = await Servico().ListarAsync(" cadeira ", true, 0, 5000);

        Assert.Equal(1, pagina.Page);
        Assert.Equal(100, pagina.PageSize);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Items);
    }
}
