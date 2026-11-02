using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="RoteiroProduto"/> para a tabela do banco.</summary>
internal sealed class RoteiroProdutoConfiguration : IEntityTypeConfiguration<RoteiroProduto>
{
    public void Configure(EntityTypeBuilder<RoteiroProduto> e)
    {
        e.ToTable("RoteiroProduto", "dbo");
        e.HasKey(x => new { x.ProdutoId, x.EstacaoId });
        e.HasIndex(x => new { x.ProdutoId, x.Sequencia }).IsUnique();
    }
}
