using Aurora.Application.Abstracoes;
using Aurora.Application.Estoque;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class EstoqueServiceTests
{
    private readonly Mock<IEstoqueRepository> _estoque = new();
    private readonly Mock<IMateriaPrimaRepository> _materiasPrimas = new();

    private EstoqueService Servico() => new(_estoque.Object, _materiasPrimas.Object, new AjusteDeEstoqueRequestValidator());

    private static MateriaPrima MateriaPrimaComSaldo(decimal saldo)
    {
        var materia = new MateriaPrima("MP-001", "Madeira", UnidadeDeMedida.MetroCubico, 2m, 100m, null);
        typeof(MateriaPrima).GetProperty(nameof(MateriaPrima.EstoqueAtual))!.SetValue(materia, saldo);
        return materia;
    }

    /// <summary>Devolve o saldo antigo na primeira leitura e o novo depois do ajuste.</summary>
    private void SaldoAntesEDepois(decimal antes, decimal depois) =>
        _materiasPrimas.SetupSequence(r => r.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync([MateriaPrimaComSaldo(antes)])
            .ReturnsAsync([MateriaPrimaComSaldo(depois)]);

    [Fact]
    public async Task Ajustar_chama_a_procedure_e_devolve_o_saldo_anterior_e_a_diferenca()
    {
        SaldoAntesEDepois(antes: 20m, depois: 7.5m);

        var resposta = await Servico().AjustarAsync(new AjusteDeEstoqueRequest(1, 7.5m, " Contagem física "));

        Assert.Equal(20m, resposta.SaldoAnterior);
        Assert.Equal(-12.5m, resposta.Diferenca);
        Assert.Equal(7.5m, resposta.MateriaPrima.EstoqueAtual);
        _estoque.Verify(r => r.AjustarMateriaPrimaAsync(1, 7.5m, "Contagem física", default), Times.Once);
    }

    [Fact]
    public async Task Ajustar_para_o_mesmo_saldo_devolve_diferenca_zero()
    {
        SaldoAntesEDepois(antes: 5m, depois: 5m);

        var resposta = await Servico().AjustarAsync(new AjusteDeEstoqueRequest(1, 5m, "Conferência"));

        Assert.Equal(0m, resposta.Diferenca);
    }

    [Fact]
    public async Task Ajustar_materia_prima_inexistente_devolve_nao_encontrado_sem_chamar_a_procedure()
    {
        _materiasPrimas.Setup(r => r.ObterPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), default)).ReturnsAsync([]);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().AjustarAsync(new AjusteDeEstoqueRequest(9, 1m, "Motivo")));

        Assert.Equal("MATERIA_PRIMA_NAO_ENCONTRADA", erro.Codigo);
        Assert.Equal(TipoErro.NaoEncontrado, erro.Tipo);
        _estoque.Verify(r => r.AjustarMateriaPrimaAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData(0, 1, "Motivo")]
    [InlineData(1, -1, "Motivo")]
    [InlineData(1, 1.0005, "Motivo")]
    [InlineData(1, 1, "")]
    [InlineData(1, 1, "   ")]
    public async Task Ajustar_com_entrada_invalida_lanca_validacao(int materiaPrimaId, double quantidade, string motivo)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().AjustarAsync(new AjusteDeEstoqueRequest(materiaPrimaId, (decimal)quantidade, motivo)));

        _estoque.Verify(r => r.AjustarMateriaPrimaAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Motivo_com_mais_de_200_caracteres_lanca_validacao()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => Servico().AjustarAsync(new AjusteDeEstoqueRequest(1, 1m, new string('m', 201))));
    }

    [Fact]
    public async Task Listar_movimentos_normaliza_a_paginacao_e_identifica_o_tipo_do_item()
    {
        var momento = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        _estoque.Setup(r => r.ListarMovimentosAsync(It.IsAny<FiltroDeMovimentos>(), 1, 100, default)).ReturnsAsync((
            [
                new MovimentoEstoqueResumo(2, momento, TipoMovimento.Saida, OrigemMovimento.Producao, 7, 4, null, "MP-004", "Tecido", -3m, 9, "Carlos", null),
                new MovimentoEstoqueResumo(1, momento, TipoMovimento.Entrada, OrigemMovimento.ProducaoConcluida, 7, null, 12, "CE-100", "Cadeira", 1m, null, null, null),
            ], 2));

        var pagina = await Servico().ListarMovimentosAsync(null, null, null, null, null, null, 0, 9999);

        Assert.Equal(100, pagina.PageSize);
        Assert.Equal(["MateriaPrima", "Produto"], pagina.Items.Select(i => i.ItemTipo));
        Assert.Equal([4, 12], pagina.Items.Select(i => i.ItemId));
        Assert.Equal("Saida", pagina.Items[0].Tipo);
        Assert.Equal("ProducaoConcluida", pagina.Items[1].Origem);
    }
}
