using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Produtos;

public class ProdutoService(
    IProdutoRepository produtos,
    IUnitOfWork unitOfWork,
    IValidator<ProdutoRequest> validador)
{
    public async Task<Pagina<ProdutoDto>> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await produtos.ListarAsync(busca?.Trim(), ativo, pagina, tamanho, ct);
        return new Pagina<ProdutoDto>(itens.Select(ProdutoDto.De).ToList(), total, pagina, tamanho);
    }

    public async Task<ProdutoDto> ObterAsync(int id, CancellationToken ct = default) =>
        ProdutoDto.De(await ObterOuFalharAsync(id, ct));

    public async Task<ProdutoDto> CriarAsync(ProdutoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var codigo = NormalizarCodigo(request.Codigo);
        await GarantirCodigoDisponivelAsync(codigo, null, ct);

        var produto = new Produto(codigo, request.Nome.Trim(), (LinhaProduto)request.LinhaProdutoId, request.PrecoVenda);
        produtos.Adicionar(produto);
        await unitOfWork.SalvarAsync(ct);
        return ProdutoDto.De(produto);
    }

    public async Task<ProdutoDto> AtualizarAsync(int id, ProdutoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var produto = await ObterOuFalharAsync(id, ct);
        var codigo = NormalizarCodigo(request.Codigo);
        await GarantirCodigoDisponivelAsync(codigo, id, ct);

        produto.Atualizar(codigo, request.Nome.Trim(), (LinhaProduto)request.LinhaProdutoId, request.PrecoVenda);
        await unitOfWork.SalvarAsync(ct);
        return ProdutoDto.De(produto);
    }

    public async Task<ProdutoDto> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        var produto = await ObterOuFalharAsync(id, ct);
        produto.DefinirAtivo(ativo);
        await unitOfWork.SalvarAsync(ct);
        return ProdutoDto.De(produto);
    }

    private static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private async Task<Produto> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await produtos.ObterPorIdAsync(id, ct)
        ?? throw new RegraDeNegocioException("PRODUTO_NAO_ENCONTRADO", "Produto não encontrado.", TipoErro.NaoEncontrado);

    private async Task GarantirCodigoDisponivelAsync(string codigo, int? ignorarId, CancellationToken ct)
    {
        if (await produtos.CodigoExisteAsync(codigo, ignorarId, ct))
        {
            throw new RegraDeNegocioException("CODIGO_DUPLICADO", "Já existe um produto com este código.");
        }
    }
}
