using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.MateriasPrimas;

public class MateriaPrimaService(
    IMateriaPrimaRepository materiasPrimas,
    IUnitOfWork unitOfWork,
    IValidator<MateriaPrimaRequest> validador)
{
    public async Task<Pagina<MateriaPrimaDto>> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await materiasPrimas.ListarAsync(busca?.Trim(), ativo, pagina, tamanho, ct);
        return new Pagina<MateriaPrimaDto>(itens.Select(MateriaPrimaDto.De).ToList(), total, pagina, tamanho);
    }

    public async Task<MateriaPrimaDto> ObterAsync(int id, CancellationToken ct = default) =>
        MateriaPrimaDto.De(await ObterOuFalharAsync(id, ct));

    public async Task<MateriaPrimaDto> CriarAsync(MateriaPrimaRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var codigo = NormalizarCodigo(request.Codigo);
        await GarantirCodigoDisponivelAsync(codigo, null, ct);
        await GarantirFornecedorAsync(request.FornecedorPreferencialId, ct);

        var materiaPrima = new MateriaPrima(
            codigo,
            request.Nome.Trim(),
            UnidadesDeMedida.DeCodigo(request.Unidade),
            request.EstoqueMinimo,
            request.CustoUnitario,
            request.FornecedorPreferencialId);
        materiasPrimas.Adicionar(materiaPrima);
        await unitOfWork.SalvarAsync(ct);
        return MateriaPrimaDto.De(materiaPrima);
    }

    public async Task<MateriaPrimaDto> AtualizarAsync(int id, MateriaPrimaRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var materiaPrima = await ObterOuFalharAsync(id, ct);
        var codigo = NormalizarCodigo(request.Codigo);
        await GarantirCodigoDisponivelAsync(codigo, id, ct);
        await GarantirFornecedorAsync(request.FornecedorPreferencialId, ct);

        materiaPrima.Atualizar(
            codigo,
            request.Nome.Trim(),
            UnidadesDeMedida.DeCodigo(request.Unidade),
            request.EstoqueMinimo,
            request.CustoUnitario,
            request.FornecedorPreferencialId);
        await unitOfWork.SalvarAsync(ct);
        return MateriaPrimaDto.De(materiaPrima);
    }

    public async Task<MateriaPrimaDto> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        var materiaPrima = await ObterOuFalharAsync(id, ct);
        materiaPrima.DefinirAtivo(ativo);
        await unitOfWork.SalvarAsync(ct);
        return MateriaPrimaDto.De(materiaPrima);
    }

    private static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private async Task<MateriaPrima> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await materiasPrimas.ObterPorIdAsync(id, ct)
        ?? throw new RegraDeNegocioException(
            "MATERIA_PRIMA_NAO_ENCONTRADA", "Matéria-prima não encontrada.", TipoErro.NaoEncontrado);

    private async Task GarantirCodigoDisponivelAsync(string codigo, int? ignorarId, CancellationToken ct)
    {
        if (await materiasPrimas.CodigoExisteAsync(codigo, ignorarId, ct))
        {
            throw new RegraDeNegocioException("CODIGO_DUPLICADO", "Já existe uma matéria-prima com este código.");
        }
    }

    private async Task GarantirFornecedorAsync(int? fornecedorId, CancellationToken ct)
    {
        if (fornecedorId is not null && !await materiasPrimas.FornecedorExisteAsync(fornecedorId.Value, ct))
        {
            throw new RegraDeNegocioException(
                "FORNECEDOR_INEXISTENTE", "O fornecedor informado não existe.", TipoErro.EntidadeNaoProcessavel);
        }
    }
}
