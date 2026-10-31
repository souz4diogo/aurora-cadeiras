using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.MateriasPrimas;

public sealed class MateriaPrimaFormViewModel
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

    [Required(ErrorMessage = "Escolha a unidade de medida.")]
    [Display(Name = "Unidade de medida")]
    public string Unidade { get; set; } = "un";

    [Range(0, 999999999, ErrorMessage = "O estoque mínimo não pode ser negativo.")]
    [Display(Name = "Estoque mínimo")]
    public decimal EstoqueMinimo { get; set; }

    [Range(0, 999999999, ErrorMessage = "O custo unitário não pode ser negativo.")]
    [Display(Name = "Custo unitário (R$)")]
    public decimal CustoUnitario { get; set; }

    [Display(Name = "Fornecedor preferencial")]
    public int? FornecedorPreferencialId { get; set; }

    /// <summary>Opções da lista de fornecedores; preenchida pelo controller a cada exibição do formulário.</summary>
    public IReadOnlyList<SelectListItem> Fornecedores { get; set; } = [];

    public IReadOnlyList<SelectListItem> Unidades { get; } = UnidadesDeMedida.ParaLista();

    public static MateriaPrimaFormViewModel De(MateriaPrimaResposta materia) => new()
    {
        Id = materia.Id,
        Codigo = materia.Codigo,
        Nome = materia.Nome,
        Unidade = materia.Unidade,
        EstoqueMinimo = Formatacao.SemZerosAMais(materia.EstoqueMinimo),
        CustoUnitario = Formatacao.SemZerosAMais(materia.CustoUnitario),
        FornecedorPreferencialId = materia.FornecedorPreferencialId,
    };

    public MateriaPrimaRequisicao ParaRequisicao() =>
        new(Codigo, Nome, Unidade, EstoqueMinimo, CustoUnitario, FornecedorPreferencialId);
}
