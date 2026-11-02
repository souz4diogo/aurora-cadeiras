using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Expedicoes;

public class ExpedicaoService(
    IExpedicaoRepository expedicoes,
    ICurrentUser usuarioAtual,
    IValidator<ExpedirRequest> validador)
{
    /// <summary>O vendedor enxerga só as expedições dos próprios pedidos; os demais perfis com acesso enxergam todas.</summary>
    private int? RestricaoDeVendedor =>
        usuarioAtual.Perfil == PerfilUsuario.Vendedor ? usuarioAtual.UsuarioId : null;

    /// <summary>
    /// Expede o pedido (RF19). Só pedido Pronto sai (RN07, 409): a procedure libera a reserva, baixa o produto
    /// acabado e muda o pedido para Expedido na mesma transação.
    /// </summary>
    public async Task<ExpedicaoDto> ExpedirAsync(int pedidoId, ExpedirRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        await expedicoes.ExpedirAsync(
            pedidoId, Texto.OuNulo(request.Transportadora), Texto.OuNulo(request.CodigoRastreio), ct);

        var expedicao = await expedicoes.ObterPorPedidoAsync(pedidoId, ct)
            ?? throw new RegraDeNegocioException("PEDIDO_NAO_ENCONTRADO", "Pedido não encontrado.", TipoErro.NaoEncontrado);
        return ExpedicaoDto.De(expedicao);
    }

    public async Task<Pagina<ExpedicaoDto>> ListarAsync(
        DateOnly? de, DateOnly? ate, int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDeExpedicoes(RestricaoDeVendedor, de, ate);
        var (itens, total) = await expedicoes.ListarAsync(filtro, pagina, tamanho, ct);
        return new Pagina<ExpedicaoDto>(itens.Select(ExpedicaoDto.De).ToList(), total, pagina, tamanho);
    }
}
