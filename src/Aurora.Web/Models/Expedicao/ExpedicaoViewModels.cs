using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Expedicoes;
using Aurora.Web.Api.Pedidos;

namespace Aurora.Web.Models.Expedicao;

/// <summary>Pedidos Prontos esperando para sair.</summary>
public sealed record ProntosViewModel(Pagina<PedidoResumoResposta> Pagina);

/// <summary>Histórico das expedições já feitas.</summary>
public sealed record HistoricoDeExpedicoesViewModel(Pagina<ExpedicaoResposta> Pagina);

public sealed class ExpedirFormViewModel
{
    /// <summary>Pedido mostrado na confirmação; preenchido pelo controller a cada exibição.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public PedidoResposta? Pedido { get; set; }

    [StringLength(80, ErrorMessage = "A transportadora aceita até 80 caracteres.")]
    [Display(Name = "Transportadora")]
    public string? Transportadora { get; set; }

    [StringLength(40, ErrorMessage = "O código de rastreio aceita até 40 caracteres.")]
    [Display(Name = "Código de rastreio")]
    public string? CodigoRastreio { get; set; }

    public ExpedirRequisicao ParaRequisicao() => new(
        string.IsNullOrWhiteSpace(Transportadora) ? null : Transportadora.Trim(),
        string.IsNullOrWhiteSpace(CodigoRastreio) ? null : CodigoRastreio.Trim());
}
