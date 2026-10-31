using Aurora.Web.Api.Pedidos;
using Aurora.Web.Api.Producao;

namespace Aurora.Web.Models.Pedidos;

/// <param name="Ordens">OPs geradas pela confirmação; nulo quando o perfil não consulta produção (a seção some).</param>
public sealed record PedidoDetalhesViewModel(PedidoResposta Pedido, IReadOnlyList<OrdemProducaoResumoResposta>? Ordens);
