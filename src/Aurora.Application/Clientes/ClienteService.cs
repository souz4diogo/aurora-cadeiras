using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Clientes;

public class ClienteService(
    IClienteRepository clientes,
    IUnitOfWork unitOfWork,
    IValidator<ClienteRequest> validador)
{
    public async Task<Pagina<ClienteDto>> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await clientes.ListarAsync(busca?.Trim(), ativo, pagina, tamanho, ct);
        return new Pagina<ClienteDto>(itens.Select(ClienteDto.De).ToList(), total, pagina, tamanho);
    }

    public async Task<ClienteDto> ObterAsync(int id, CancellationToken ct = default) =>
        ClienteDto.De(await ObterOuFalharAsync(id, ct));

    public async Task<ClienteDto> CriarAsync(ClienteRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var documento = Texto.SomenteDigitos(request.CpfCnpj);
        await GarantirDocumentoDisponivelAsync(documento, null, ct);

        var cliente = new Cliente(
            request.Nome.Trim(),
            documento,
            Texto.OuNulo(request.Email)?.ToLowerInvariant(),
            Texto.OuNulo(request.Telefone),
            Texto.OuNulo(request.Cidade),
            Texto.OuNulo(request.Uf)?.ToUpperInvariant());
        clientes.Adicionar(cliente);
        await unitOfWork.SalvarAsync(ct);
        return ClienteDto.De(cliente);
    }

    public async Task<ClienteDto> AtualizarAsync(int id, ClienteRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var cliente = await ObterOuFalharAsync(id, ct);
        var documento = Texto.SomenteDigitos(request.CpfCnpj);
        await GarantirDocumentoDisponivelAsync(documento, id, ct);

        cliente.Atualizar(
            request.Nome.Trim(),
            documento,
            Texto.OuNulo(request.Email)?.ToLowerInvariant(),
            Texto.OuNulo(request.Telefone),
            Texto.OuNulo(request.Cidade),
            Texto.OuNulo(request.Uf)?.ToUpperInvariant());
        await unitOfWork.SalvarAsync(ct);
        return ClienteDto.De(cliente);
    }

    public async Task<ClienteDto> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        var cliente = await ObterOuFalharAsync(id, ct);
        cliente.DefinirAtivo(ativo);
        await unitOfWork.SalvarAsync(ct);
        return ClienteDto.De(cliente);
    }

    private async Task<Cliente> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await clientes.ObterPorIdAsync(id, ct)
        ?? throw new RegraDeNegocioException("CLIENTE_NAO_ENCONTRADO", "Cliente não encontrado.", TipoErro.NaoEncontrado);

    private async Task GarantirDocumentoDisponivelAsync(string documento, int? ignorarId, CancellationToken ct)
    {
        if (await clientes.DocumentoExisteAsync(documento, ignorarId, ct))
        {
            throw new RegraDeNegocioException("DOCUMENTO_DUPLICADO", "Já existe um cliente com este CPF/CNPJ.");
        }
    }
}
