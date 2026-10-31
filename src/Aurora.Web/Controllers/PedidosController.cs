using System.Globalization;
using Aurora.Web.Api.Clientes;
using Aurora.Web.Api.Pedidos;
using Aurora.Web.Api.Produtos;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Pedidos;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDePedidos)]
public class PedidosController(IPedidosApi pedidos, IClientesApi clientes, IProdutosApi produtos) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? status, int? clienteId, DateOnly? de, DateOnly? ate, bool? atrasados, int page = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroDePedidos(string.IsNullOrWhiteSpace(status) ? null : status, clienteId, de, ate, atrasados);

        // O Almoxarife consulta pedidos mas não clientes (docs/06 §3): sem permissão, o filtro por cliente não aparece
        IReadOnlyList<SelectListItem>? opcoesDeCliente = null;
        if (User.IsInRole(Perfis.Administrador) || User.IsInRole(Perfis.GerenteProducao) || User.IsInRole(Perfis.Vendedor))
        {
            opcoesDeCliente = (await clientes.ListarAtivosAsync(ct)).Select(ParaOpcao).ToList();
        }

        return View(new PedidosListaViewModel(await pedidos.ListarAsync(filtro, page, ct), filtro, opcoesDeCliente));
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct) => View(await pedidos.ObterAsync(id, ct));

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Novo(CancellationToken ct) =>
        await FormularioAsync(new PedidoFormViewModel { Itens = [new PedidoItemFormViewModel()] }, ct);

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Novo(PedidoFormViewModel modelo, CancellationToken ct)
    {
        ExigirItens(modelo);
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await pedidos.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Pedido {criado.Numero} criado.");
                return RedirectToAction(nameof(Detalhes), new { id = criado.Id });
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var pedido = await pedidos.ObterAsync(id, ct);
        if (!pedido.PodeEditar)
        {
            AvisarAtencao($"O pedido {pedido.Numero} está {Formatacao.StatusDoPedido(pedido.Status).ToLowerInvariant()} e não pode mais ser editado.");
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        return await FormularioAsync(PedidoFormViewModel.De(pedido), ct);
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Editar(int id, PedidoFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        ExigirItens(modelo);
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizado = await pedidos.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Pedido {atualizado.Numero} atualizado.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            () => FormularioAsync(modelo, ct));
    }

    /// <summary>Tela de confirmação: cancelar muda o pedido e libera reservas, então pede uma segunda confirmação.</summary>
    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Cancelar(int id, CancellationToken ct) => View(await pedidos.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ActionName("Cancelar")]
    public async Task<IActionResult> ConfirmarCancelamento(int id, CancellationToken ct) =>
        await TentarAsync(
            async () =>
            {
                var cancelado = await pedidos.CancelarAsync(id, ct);
                AvisarSucesso($"Pedido {cancelado.Numero} cancelado.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            async () => View(await pedidos.ObterAsync(id, ct)));

    private void ExigirItens(PedidoFormViewModel modelo)
    {
        if (modelo.Itens.Count == 0)
        {
            ModelState.AddModelError(nameof(PedidoFormViewModel.Itens), "Adicione ao menos um item ao pedido.");
        }
    }

    /// <summary>
    /// Exibe o formulário com os clientes e produtos ativos para escolher. Um cliente ou produto já gravado no pedido
    /// continua disponível mesmo que tenha sido inativado depois (marcado como inativo).
    /// </summary>
    private async Task<IActionResult> FormularioAsync(PedidoFormViewModel modelo, CancellationToken ct)
    {
        var clientesAtivos = (await clientes.ListarAtivosAsync(ct)).Select(ParaOpcao).ToList();
        if (modelo.ClienteId is int clienteId && clientesAtivos.All(c => c.Value != Identificador(clienteId)))
        {
            var atual = await clientes.ObterAsync(clienteId, ct);
            clientesAtivos.Add(new SelectListItem($"{atual.Nome} (inativo)", Identificador(clienteId)));
        }

        var produtosAtivos = (await produtos.ListarAtivosAsync(ct))
            .Select(p => new ProdutoOpcao(p.Id, $"{p.Codigo} - {p.Nome}", p.PrecoVenda))
            .ToList();
        foreach (var produtoId in modelo.Itens.Select(i => i.ProdutoId).OfType<int>().Distinct())
        {
            if (produtosAtivos.All(p => p.Id != produtoId))
            {
                var atual = await produtos.ObterAsync(produtoId, ct);
                produtosAtivos.Add(new ProdutoOpcao(atual.Id, $"{atual.Codigo} - {atual.Nome} (inativo)", atual.PrecoVenda));
            }
        }

        modelo.Clientes = clientesAtivos;
        modelo.Produtos = produtosAtivos;
        return View("Form", modelo);
    }

    private static SelectListItem ParaOpcao(ClienteResposta cliente) => new(cliente.Nome, Identificador(cliente.Id));

    private static string Identificador(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
