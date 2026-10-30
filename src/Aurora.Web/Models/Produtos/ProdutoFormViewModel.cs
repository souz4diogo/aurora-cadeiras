using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Produtos;
using Aurora.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Produtos;

public sealed class ProdutoFormViewModel
{
    /// <summary>Preenchido na edição; nulo no cadastro.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(20, ErrorMessage = "O código aceita até 20 caracteres.")]
    [Display(Name = "Código")]
    public string Codigo { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, ErrorMessage = "O nome aceita até 120 caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = "";

    [Range(1, 255, ErrorMessage = "Escolha a linha do produto.")]
    [Display(Name = "Linha do produto")]
    public byte LinhaProdutoId { get; set; } = 1;

    [Range(0, 999999999, ErrorMessage = "O preço de venda não pode ser negativo.")]
    [Display(Name = "Preço de venda (R$)")]
    public decimal PrecoVenda { get; set; }

    public IReadOnlyList<SelectListItem> Linhas { get; } = LinhasDeProduto.ParaLista();

    public static ProdutoFormViewModel De(ProdutoResposta produto) => new()
    {
        Id = produto.Id,
        Codigo = produto.Codigo,
        Nome = produto.Nome,
        LinhaProdutoId = produto.LinhaProdutoId,
        PrecoVenda = Formatacao.SemZerosAMais(produto.PrecoVenda),
    };

    public ProdutoRequisicao ParaRequisicao() => new(Codigo, Nome, LinhaProdutoId, PrecoVenda);
}
