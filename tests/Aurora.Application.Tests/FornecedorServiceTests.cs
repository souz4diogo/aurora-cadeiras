using Aurora.Application.Abstracoes;
using Aurora.Application.Fornecedores;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class FornecedorServiceTests
{
    private readonly Mock<IFornecedorRepository> _fornecedores = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private FornecedorService Servico() => new(_fornecedores.Object, _uow.Object, new FornecedorRequestValidator());

    [Fact]
    public async Task Criar_normaliza_o_cnpj_e_preserva_a_marca_de_comunidade_tradicional()
    {
        var resposta = await Servico().CriarAsync(
            new FornecedorRequest(" Cooperativa Aruanã ", "11.222.333/0001-81", "Contato@Aruana.org", null, true));

        Assert.Equal("Cooperativa Aruanã", resposta.RazaoSocial);
        Assert.Equal("11222333000181", resposta.Cnpj);
        Assert.Equal("contato@aruana.org", resposta.Email);
        Assert.True(resposta.ComunidadeTradicional);
        Assert.True(resposta.Ativo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Criar_com_cnpj_existente_devolve_conflito()
    {
        _fornecedores.Setup(r => r.CnpjExisteAsync("11222333000181", null, default)).ReturnsAsync(true);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new FornecedorRequest("Outra", "11222333000181", null, null, false)));

        Assert.Equal("DOCUMENTO_DUPLICADO", erro.Codigo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("", "11222333000181")]
    [InlineData("Fornecedor", "12345678901")]
    [InlineData("Fornecedor", "")]
    public async Task Criar_com_entrada_invalida_lanca_validacao(string razaoSocial, string cnpj)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new FornecedorRequest(razaoSocial, cnpj, null, null, false)));
    }

    [Fact]
    public async Task Atualizar_pode_marcar_o_fornecedor_como_comunidade_tradicional()
    {
        _fornecedores.Setup(r => r.ObterPorIdAsync(2, default))
            .ReturnsAsync(new Fornecedor("Artesãs", "11222333000181", null, null, false));

        var resposta = await Servico().AtualizarAsync(2, new FornecedorRequest("Artesãs", "11222333000181", null, null, true));

        Assert.True(resposta.ComunidadeTradicional);
        _fornecedores.Verify(r => r.CnpjExisteAsync("11222333000181", 2, default), Times.Once);
    }

    [Fact]
    public async Task Obter_inexistente_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(7));

        Assert.Equal("FORNECEDOR_NAO_ENCONTRADO", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Fact]
    public async Task Listar_repassa_os_filtros_e_normaliza_a_paginacao()
    {
        _fornecedores.Setup(r => r.ListarAsync("aru", true, true, 1, 100, default))
            .ReturnsAsync(([new Fornecedor("Aruanã", "11222333000181", null, null, true)], 1));

        var pagina = await Servico().ListarAsync(" aru ", true, true, -3, 9999);

        Assert.Single(pagina.Items);
        Assert.Equal(1, pagina.Page);
        Assert.Equal(100, pagina.PageSize);
    }
}
