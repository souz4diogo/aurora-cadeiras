using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="PedidoItem"/> para a tabela do banco.</summary>
internal sealed class PedidoItemConfiguration : IEntityTypeConfiguration<PedidoItem>
{
    public void Configure(EntityTypeBuilder<PedidoItem> e)
    {
        e.ToTable("PedidoItem", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("PedidoItemId");
        e.Property(x => x.PrecoUnitario).HasColumnType("decimal(12,2)");
        e.Ignore(x => x.Subtotal);
        e.HasIndex(x => new { x.PedidoId, x.ProdutoId }).IsUnique();
    }
}
