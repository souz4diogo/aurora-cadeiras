using Aurora.Application.Abstracoes;
using Aurora.Application.Clientes;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class ClienteServiceTests
{
    private readonly Mock<IClienteRepository> _clientes = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ClienteService Servico() => new(_clientes.Object, _uow.Object, new ClienteRequestValidator());

    [Fact]
    public async Task Criar_normaliza_documento_email_e_uf_e_salva()
    {
        var request = new ClienteRequest(" Móveis Lima ", "12.345.678/0001-95", " Contato@Lima.com ", " (11) 99999-0000 ", " Campinas ", "sp");

        var resposta = await Servico().CriarAsync(request);

        Assert.Equal("Móveis Lima", resposta.Nome);
        Assert.Equal("12345678000195", resposta.CpfCnpj);
        Assert.Equal("contato@lima.com", resposta.Email);
        Assert.Equal("Campinas", resposta.Cidade);
        Assert.Equal("SP", resposta.Uf);
        Assert.True(resposta.Ativo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Criar_com_campos_opcionais_em_branco_grava_nulo()
    {
        var resposta = await Servico().CriarAsync(new ClienteRequest("Maria", "12345678901", "  ", "", null, " "));

        Assert.Null(resposta.Email);
        Assert.Null(resposta.Telefone);
        Assert.Null(resposta.Cidade);
        Assert.Null(resposta.Uf);
    }

    [Fact]
    public async Task Criar_com_documento_existente_devolve_conflito()
    {
        _clientes.Setup(r => r.DocumentoExisteAsync("12345678901", null, default)).ReturnsAsync(true);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().CriarAsync(new ClienteRequest("Maria", "123.456.789-01", null, null, null, null)));

        Assert.Equal("DOCUMENTO_DUPLICADO", erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("", "12345678901", null, null)]
    [InlineData("Maria", "123", null, null)]
    [InlineData("Maria", "1234567890123", null, null)]
    [InlineData("Maria", "abc", null, null)]
    [InlineData("Maria", "12345678901", "nao-e-email", null)]
    [InlineData("Maria", "12345678901", null, "SAO")]
    [InlineData("Maria", "12345678901", null, "1A")]
    public async Task Criar_com_entrada_invalida_lanca_validacao(string nome, string documento, string? email, string? uf)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().CriarAsync(new ClienteRequest(nome, documento, email, null, null, uf)));

        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }

    [Fact]
    public async Task Atualizar_ignora_o_proprio_cliente_na_checagem_do_documento()
    {
        _clientes.Setup(r => r.ObterPorIdAsync(4, default))
            .ReturnsAsync(new Cliente("Maria", "12345678901", null, null, null, null));

        var resposta = await Servico().AtualizarAsync(4, new ClienteRequest("Maria Souza", "12345678901", null, null, "Santos", "sp"));

        Assert.Equal("Maria Souza", resposta.Nome);
        Assert.Equal("SP", resposta.Uf);
        _clientes.Verify(r => r.DocumentoExisteAsync("12345678901", 4, default), Times.Once);
    }

    [Fact]
    public async Task Obter_inexistente_devolve_nao_encontrado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => Servico().ObterAsync(99));

        Assert.Equal("CLIENTE_NAO_ENCONTRADO", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
    }

    [Fact]
    public async Task Alterar_ativo_inativa_o_cliente()
    {
        _clientes.Setup(r => r.ObterPorIdAsync(1, default))
            .ReturnsAsync(new Cliente("Maria", "12345678901", null, null, null, null));

        var resposta = await Servico().AlterarAtivoAsync(1, false);

        Assert.False(resposta.Ativo);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }
}
