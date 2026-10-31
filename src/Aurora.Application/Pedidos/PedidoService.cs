using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Pedidos;

public class PedidoService(
    IPedidoRepository pedidos,
    IClienteRepository clientes,
    IProdutoRepository produtos,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    IRelogio relogio,
    IValidator<PedidoRequest> validador)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.AgoraUtc);

    /// <summary>O vendedor enxerga e altera só os próprios pedidos; os demais perfis com acesso enxergam todos.</summary>
    private int? RestricaoDeVendedor =>
        usuarioAtual.Perfil == PerfilUsuario.Vendedor ? usuarioAtual.UsuarioId : null;

    public async Task<Pagina<PedidoResumoDto>> ListarAsync(
        StatusPedido? status,
        int? clienteId,
        DateOnly? de,
        DateOnly? ate,
        bool? atrasados,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDePedidos(RestricaoDeVendedor, status, clienteId, de, ate, atrasados, Hoje);
        var (itens, total) = await pedidos.ListarAsync(filtro, pagina, tamanho, ct);
        return new Pagina<PedidoResumoDto>(itens.Select(i => PedidoResumoDto.De(i, Hoje)).ToList(), total, pagina, tamanho);
    }

    public async Task<PedidoDto> ObterAsync(int id, CancellationToken ct = default) =>
        await MontarDtoAsync(await ObterOuFalharAsync(id, ct), ct);

    public async Task<PedidoDto> CriarAsync(PedidoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var vendedorId = usuarioAtual.UsuarioId
            ?? throw new RegraDeNegocioException("NAO_AUTENTICADO", "Não autenticado.", TipoErro.NaoAutenticado);

        await GarantirClienteAtivoAsync(request.ClienteId, ct);
        var itens = await MontarItensAsync(request.Itens, pedidoAtual: null, ct);

        var pedido = new Pedido(request.ClienteId, vendedorId, request.PrazoEntrega, Texto.OuNulo(request.Observacao), itens);
        pedidos.Adicionar(pedido);
        await unitOfWork.SalvarAsync(ct);
        return await MontarDtoAsync(pedido, ct);
    }

    public async Task<PedidoDto> AtualizarAsync(int id, PedidoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var pedido = await ObterOuFalharAsync(id, ct);

        pedido.AtualizarDados(request.ClienteId, request.PrazoEntrega, Texto.OuNulo(request.Observacao));
        await GarantirClienteAtivoAsync(request.ClienteId, ct);
        pedido.SubstituirItens(await MontarItensAsync(request.Itens, pedido, ct));

        await unitOfWork.SalvarAsync(ct);
        return await MontarDtoAsync(pedido, ct);
    }

    public async Task<PedidoDto> CancelarAsync(int id, CancellationToken ct = default)
    {
        var pedido = await ObterOuFalharAsync(id, ct);
        await pedidos.CancelarAsync(pedido.Id, ct);
        return await MontarDtoAsync(pedido, ct);
    }

    private async Task<Pedido> ObterOuFalharAsync(int id, CancellationToken ct)
    {
        var pedido = await pedidos.ObterPorIdAsync(id, ct);
        if (pedido is null || (RestricaoDeVendedor is int vendedorId && pedido.VendedorId != vendedorId))
        {
            throw new RegraDeNegocioException("PEDIDO_NAO_ENCONTRADO", "Pedido não encontrado.", TipoErro.NaoEncontrado);
        }

        return pedido;
    }

    private async Task GarantirClienteAtivoAsync(int clienteId, CancellationToken ct)
    {
        var cliente = await clientes.ObterPorIdAsync(clienteId, ct)
            ?? throw new RegraDeNegocioException("CLIENTE_INEXISTENTE", "O cliente informado não existe.", TipoErro.EntidadeNaoProcessavel);
        if (!cliente.Ativo)
        {
            throw new RegraDeNegocioException("CLIENTE_INATIVO", "O cliente informado está inativo.", TipoErro.EntidadeNaoProcessavel);
        }
    }

    /// <summary>
    /// Resolve os produtos e o preço de cada item: item que já estava no pedido mantém o preço (e pode continuar
    /// mesmo com o produto inativo); item novo usa o preço de venda atual e exige produto ativo.
    /// </summary>
    private async Task<List<ItemDePedido>> MontarItensAsync(
        IReadOnlyList<PedidoItemRequest> itens, Pedido? pedidoAtual, CancellationToken ct)
    {
        var encontrados = (await produtos.ObterPorIdsAsync(itens.Select(i => i.ProdutoId).ToList(), ct))
            .ToDictionary(p => p.Id);
        var jaNoPedido = pedidoAtual?.Itens.ToDictionary(i => i.ProdutoId) ?? [];

        var resultado = new List<ItemDePedido>(itens.Count);
        foreach (var item in itens)
        {
            if (!encontrados.TryGetValue(item.ProdutoId, out var produto))
            {
                throw new RegraDeNegocioException(
                    "PRODUTO_INEXISTENTE", $"O produto {item.ProdutoId} não existe.", TipoErro.EntidadeNaoProcessavel);
            }

            if (jaNoPedido.TryGetValue(item.ProdutoId, out var itemExistente))
            {
                resultado.Add(new ItemDePedido(item.ProdutoId, item.Quantidade, itemExistente.PrecoUnitario));
                continue;
            }

            if (!produto.Ativo)
            {
                throw new RegraDeNegocioException(
                    "PRODUTO_INATIVO", $"O produto {produto.Codigo} está inativo.", TipoErro.EntidadeNaoProcessavel);
            }

            resultado.Add(new ItemDePedido(item.ProdutoId, item.Quantidade, produto.PrecoVenda));
        }

        return resultado;
    }

    private async Task<PedidoDto> MontarDtoAsync(Pedido pedido, CancellationToken ct)
    {
        var cliente = await clientes.ObterPorIdAsync(pedido.ClienteId, ct);
        var catalogo = (await produtos.ObterPorIdsAsync(pedido.Itens.Select(i => i.ProdutoId).ToList(), ct))
            .ToDictionary(p => p.Id);

        var itens = pedido.Itens
            .Select(i => (Item: i, Produto: catalogo[i.ProdutoId]))
            .OrderBy(x => x.Produto.Codigo, StringComparer.Ordinal)
            .Select(x => new PedidoItemDto(
                x.Item.Id, x.Produto.Id, x.Produto.Codigo, x.Produto.Nome, x.Item.Quantidade, x.Item.PrecoUnitario, x.Item.Subtotal))
            .ToList();

        return new PedidoDto(
            pedido.Id,
            pedido.Numero,
            pedido.ClienteId,
            cliente?.Nome ?? "",
            pedido.VendedorId,
            pedido.DataPedido,
            pedido.PrazoEntrega,
            pedido.Status.ToString(),
            pedido.Observacao,
            pedido.ValorTotal,
            pedido.EstaAtrasado(Hoje),
            itens);
    }
}
