using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Application.MateriasPrimas;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Estoque;

public class EstoqueService(
    IEstoqueRepository estoque,
    IMateriaPrimaRepository materiasPrimas,
    IValidator<AjusteDeEstoqueRequest> validador)
{
    public async Task<Pagina<MovimentoEstoqueDto>> ListarMovimentosAsync(
        int? materiaPrimaId,
        int? produtoId,
        TipoMovimento? tipo,
        OrigemMovimento? origem,
        DateOnly? de,
        DateOnly? ate,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDeMovimentos(materiaPrimaId, produtoId, tipo, origem, de, ate);
        var (itens, total) = await estoque.ListarMovimentosAsync(filtro, pagina, tamanho, ct);
        return new Pagina<MovimentoEstoqueDto>(itens.Select(MovimentoEstoqueDto.De).ToList(), total, pagina, tamanho);
    }

    /// <summary>Corrige o saldo de uma matéria-prima (RF14): o livro-razão não é editado, ganha um movimento de ajuste.</summary>
    public async Task<AjusteDeEstoqueDto> AjustarAsync(AjusteDeEstoqueRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var antes = await ObterAsync(request.MateriaPrimaId, ct);
        await estoque.AjustarMateriaPrimaAsync(request.MateriaPrimaId, request.NovaQuantidade, request.Motivo.Trim(), ct);
        var depois = await ObterAsync(request.MateriaPrimaId, ct);

        return new AjusteDeEstoqueDto(MateriaPrimaDto.De(depois), antes.EstoqueAtual, depois.EstoqueAtual - antes.EstoqueAtual);
    }

    private async Task<MateriaPrima> ObterAsync(int materiaPrimaId, CancellationToken ct) =>
        (await materiasPrimas.ObterPorIdsAsync([materiaPrimaId], ct)).SingleOrDefault()
        ?? throw new RegraDeNegocioException(
            "MATERIA_PRIMA_NAO_ENCONTRADA", "Matéria-prima não encontrada.", TipoErro.NaoEncontrado);
}
