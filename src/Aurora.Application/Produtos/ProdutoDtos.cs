using Aurora.Domain.Entidades;

namespace Aurora.Application.Produtos;

public sealed record ProdutoDto(
    int Id,
    string Codigo,
    string Nome,
    byte LinhaProdutoId,
    string LinhaProduto,
    decimal PrecoVenda,
    bool Ativo,
    DateTime CriadoEm)
{
    public static ProdutoDto De(Produto p) =>
        new(p.Id, p.Codigo, p.Nome, (byte)p.Linha, p.Linha.ToString(), p.PrecoVenda, p.Ativo, p.CriadoEm);
}

/// <summary>Corpo de criação (POST) e de atualização (PUT) de produto.</summary>
public sealed record ProdutoRequest(string Codigo, string Nome, byte LinhaProdutoId, decimal PrecoVenda);
