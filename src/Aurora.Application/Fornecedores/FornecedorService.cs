using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Fornecedores;

public class FornecedorService(
    IFornecedorRepository fornecedores,
    IUnitOfWork unitOfWork,
    IValidator<FornecedorRequest> validador)
{
    public async Task<Pagina<FornecedorDto>> ListarAsync(
        string? busca, bool? ativo, bool? comunidadeTradicional, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await fornecedores.ListarAsync(busca?.Trim(), ativo, comunidadeTradicional, pagina, tamanho, ct);
        return new Pagina<FornecedorDto>(itens.Select(FornecedorDto.De).ToList(), total, pagina, tamanho);
    }

    public async Task<FornecedorDto> ObterAsync(int id, CancellationToken ct = default) =>
        FornecedorDto.De(await ObterOuFalharAsync(id, ct));

    public async Task<FornecedorDto> CriarAsync(FornecedorRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var cnpj = Texto.SomenteDigitos(request.Cnpj);
        await GarantirCnpjDisponivelAsync(cnpj, null, ct);

        var fornecedor = new Fornecedor(
            request.RazaoSocial.Trim(),
            cnpj,
            Texto.OuNulo(request.Email)?.ToLowerInvariant(),
            Texto.OuNulo(request.Telefone),
            request.ComunidadeTradicional);
        fornecedores.Adicionar(fornecedor);
        await unitOfWork.SalvarAsync(ct);
        return FornecedorDto.De(fornecedor);
    }

    public async Task<FornecedorDto> AtualizarAsync(int id, FornecedorRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var fornecedor = await ObterOuFalharAsync(id, ct);
        var cnpj = Texto.SomenteDigitos(request.Cnpj);
        await GarantirCnpjDisponivelAsync(cnpj, id, ct);

        fornecedor.Atualizar(
            request.RazaoSocial.Trim(),
            cnpj,
            Texto.OuNulo(request.Email)?.ToLowerInvariant(),
            Texto.OuNulo(request.Telefone),
            request.ComunidadeTradicional);
        await unitOfWork.SalvarAsync(ct);
        return FornecedorDto.De(fornecedor);
    }

    public async Task<FornecedorDto> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        var fornecedor = await ObterOuFalharAsync(id, ct);
        fornecedor.DefinirAtivo(ativo);
        await unitOfWork.SalvarAsync(ct);
        return FornecedorDto.De(fornecedor);
    }

    private async Task<Fornecedor> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await fornecedores.ObterPorIdAsync(id, ct)
        ?? throw new RegraDeNegocioException("FORNECEDOR_NAO_ENCONTRADO", "Fornecedor não encontrado.", TipoErro.NaoEncontrado);

    private async Task GarantirCnpjDisponivelAsync(string cnpj, int? ignorarId, CancellationToken ct)
    {
        if (await fornecedores.CnpjExisteAsync(cnpj, ignorarId, ct))
        {
            throw new RegraDeNegocioException("DOCUMENTO_DUPLICADO", "Já existe um fornecedor com este CNPJ.");
        }
    }
}
