using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="EstoqueProduto"/> para a tabela do banco.</summary>
internal sealed class EstoqueProdutoConfiguration : IEntityTypeConfiguration<EstoqueProduto>
{
    public void Configure(EntityTypeBuilder<EstoqueProduto> e)
    {
        e.ToTable("EstoqueProduto", "dbo");
        e.HasKey(x => x.ProdutoId);
        e.Property(x => x.ProdutoId).ValueGeneratedNever();
        e.Ignore(x => x.Disponivel);
    }
}
