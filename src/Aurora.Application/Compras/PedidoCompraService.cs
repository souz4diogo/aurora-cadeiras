using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Compras;

/// <summary>
/// Pedidos de compra de matéria-prima (RF16) e seu recebimento (RF17). Criar é CRUD simples; emitir e cancelar são
/// instruções atômicas e condicionais; receber roda em procedure (atualiza item, estoque, custo e status juntos).
/// </summary>
public class PedidoCompraService(
    IPedidoCompraRepository compras,
    IFornecedorRepository fornecedores,
    IMateriaPrimaRepository materiasPrimas,
    IUsuarioRepository usuarios,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    IRelogio relogio,
    IValidator<PedidoCompraRequest> validadorDoPedido,
    IValidator<ReceberItemRequest> validadorDoRecebimento)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.AgoraUtc);

    public async Task<Pagina<PedidoCompraResumoDto>> ListarAsync(
        StatusPedidoCompra? status,
        int? fornecedorId,
        DateOnly? de,
        DateOnly? ate,
        bool? atrasados,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDePedidosCompra(status, fornecedorId, de, ate, atrasados, Hoje);
        var (itens, total) = await compras.ListarAsync(filtro, pagina, tamanho, ct);
        return new Pagina<PedidoCompraResumoDto>(itens.Select(i => PedidoCompraResumoDto.De(i, Hoje)).ToList(), total, pagina, tamanho);
    }

    public async Task<PedidoCompraDto> ObterAsync(int id, CancellationToken ct = default) =>
        await MontarDtoAsync(await ObterOuFalharAsync(id, ct), ct);

    public async Task<PedidoCompraDto> CriarAsync(PedidoCompraRequest request, CancellationToken ct = default)
    {
        await validadorDoPedido.ValidateAndThrowAsync(request, ct);
        var compradorId = usuarioAtual.UsuarioId
            ?? throw new RegraDeNegocioException("NAO_AUTENTICADO", "Não autenticado.", TipoErro.NaoAutenticado);

        await GarantirFornecedorAtivoAsync(request.FornecedorId, ct);
        var itens = await MontarItensAsync(request.Itens, ct);

        var pedido = new PedidoCompra(request.FornecedorId, compradorId, request.DataPrevista, itens);
        compras.Adicionar(pedido);
        await unitOfWork.SalvarAsync(ct);
        return await MontarDtoAsync(pedido, ct);
    }

    /// <summary>Envia a solicitação ao fornecedor (Solicitado → Emitido).</summary>
    public async Task<PedidoCompraDto> EmitirAsync(int id, CancellationToken ct = default)
    {
        var pedido = await ObterOuFalharAsync(id, ct);
        if (!pedido.PodeEmitir || !await compras.TentarEmitirAsync(id, ct))
        {
            throw new RegraDeNegocioException("STATUS_INVALIDO", "Só uma solicitação ainda não emitida pode ser emitida.");
        }

        return await ObterAsync(id, ct);
    }

    /// <summary>Cancela o pedido enquanto nada foi recebido; depois do primeiro recebimento o material já está no estoque.</summary>
    public async Task<PedidoCompraDto> CancelarAsync(int id, CancellationToken ct = default)
    {
        var pedido = await ObterOuFalharAsync(id, ct);
        if (!pedido.PodeCancelar || !await compras.TentarCancelarAsync(id, ct))
        {
            throw new RegraDeNegocioException(
                "STATUS_INVALIDO", "O pedido não pode mais ser cancelado: já foi recebido, em parte ou por inteiro, ou já foi cancelado.");
        }

        return await ObterAsync(id, ct);
    }

    /// <summary>
    /// Registra o que chegou de um item (RF17): dá entrada no estoque e, quando todos os itens completam, o pedido vira
    /// Recebido. Receber acima do pedido é recusado (422), e só pedido Emitido ou parcial recebe (409).
    /// </summary>
    public async Task<PedidoCompraDto> ReceberItemAsync(int itemId, ReceberItemRequest request, CancellationToken ct = default)
    {
        await validadorDoRecebimento.ValidateAndThrowAsync(request, ct);
        var pedidoId = await compras.ObterPedidoDoItemAsync(itemId, ct)
            ?? throw new RegraDeNegocioException(
                "ITEM_DE_COMPRA_NAO_ENCONTRADO", "Item do pedido de compra não encontrado.", TipoErro.NaoEncontrado);

        await compras.ReceberItemAsync(itemId, request.Quantidade, ct);
        return await ObterAsync(pedidoId, ct);
    }

    private async Task<PedidoCompra> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await compras.ObterAsync(id, ct)
        ?? throw new RegraDeNegocioException(
            "PEDIDO_COMPRA_NAO_ENCONTRADO", "Pedido de compra não encontrado.", TipoErro.NaoEncontrado);

    private async Task GarantirFornecedorAtivoAsync(int fornecedorId, CancellationToken ct)
    {
        var fornecedor = await fornecedores.ObterPorIdAsync(fornecedorId, ct)
            ?? throw new RegraDeNegocioException(
                "FORNECEDOR_INEXISTENTE", "O fornecedor informado não existe.", TipoErro.EntidadeNaoProcessavel);
        if (!fornecedor.Ativo)
        {
            throw new RegraDeNegocioException(
                "FORNECEDOR_INATIVO", "O fornecedor informado está inativo.", TipoErro.EntidadeNaoProcessavel);
        }
    }

    /// <summary>Confere que cada matéria-prima existe e está ativa; sem custo informado, vale o custo atual dela.</summary>
    private async Task<List<ItemDePedidoCompra>> MontarItensAsync(IReadOnlyList<PedidoCompraItemRequest> itens, CancellationToken ct)
    {
        var encontradas = (await materiasPrimas.ObterPorIdsAsync(itens.Select(i => i.MateriaPrimaId).ToList(), ct))
            .ToDictionary(m => m.Id);

        var resultado = new List<ItemDePedidoCompra>(itens.Count);
        foreach (var item in itens)
        {
            if (!encontradas.TryGetValue(item.MateriaPrimaId, out var materia))
            {
                throw new RegraDeNegocioException(
                    "MATERIA_PRIMA_INEXISTENTE", $"A matéria-prima {item.MateriaPrimaId} não existe.", TipoErro.EntidadeNaoProcessavel);
            }

            if (!materia.Ativo)
            {
                throw new RegraDeNegocioException(
                    "MATERIA_PRIMA_INATIVA", $"A matéria-prima {materia.Codigo} está inativa.", TipoErro.EntidadeNaoProcessavel);
            }

            resultado.Add(new ItemDePedidoCompra(item.MateriaPrimaId, item.Quantidade, item.CustoUnitario ?? materia.CustoUnitario));
        }

        return resultado;
    }

    private async Task<PedidoCompraDto> MontarDtoAsync(PedidoCompra pedido, CancellationToken ct)
    {
        var fornecedor = await fornecedores.ObterPorIdAsync(pedido.FornecedorId, ct);
        var comprador = await usuarios.ObterPorIdAsync(pedido.CompradorId, ct);
        var materias = (await materiasPrimas.ObterPorIdsAsync(pedido.Itens.Select(i => i.MateriaPrimaId).ToList(), ct))
            .ToDictionary(m => m.Id);

        var itens = pedido.Itens
            .Select(i => (Item: i, Materia: materias[i.MateriaPrimaId]))
            .OrderBy(x => x.Materia.Codigo, StringComparer.Ordinal)
            .Select(x => new PedidoCompraItemDto(
                x.Item.Id,
                x.Materia.Id,
                x.Materia.Codigo,
                x.Materia.Nome,
                x.Materia.Unidade.ParaCodigo(),
                x.Item.Quantidade,
                x.Item.CustoUnitario,
                x.Item.QuantidadeRecebida,
                x.Item.Saldo,
                x.Item.Subtotal))
            .ToList();

        return new PedidoCompraDto(
            pedido.Id,
            pedido.Numero,
            pedido.FornecedorId,
            fornecedor?.RazaoSocial ?? "",
            fornecedor?.ComunidadeTradicional ?? false,
            pedido.CompradorId,
            comprador?.Nome,
            pedido.Status.ToString(),
            pedido.DataPrevista,
            pedido.CriadoEm,
            pedido.ValorTotal,
            pedido.EstaAtrasado(Hoje),
            pedido.PodeEmitir,
            pedido.PodeCancelar,
            pedido.PodeReceber,
            itens);
    }
}
