using System.ComponentModel.DataAnnotations;

namespace Aurora.Web.Models.Pedidos;

public sealed class PedidoItemFormViewModel
{
    [Required(ErrorMessage = "Escolha o produto.")]
    [Display(Name = "Produto")]
    public int? ProdutoId { get; set; }

    [Range(1, 100000, ErrorMessage = "A quantidade deve ficar entre 1 e 100.000.")]
    [Display(Name = "Quantidade")]
    public int Quantidade { get; set; } = 1;
}
