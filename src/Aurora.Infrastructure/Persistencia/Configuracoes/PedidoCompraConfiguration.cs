using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="PedidoCompra"/> para a tabela do banco.</summary>
internal sealed class PedidoCompraConfiguration : IEntityTypeConfiguration<PedidoCompra>
{
    public void Configure(EntityTypeBuilder<PedidoCompra> e)
    {
        e.ToTable("PedidoCompra", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("PedidoCompraId");
        e.Property(x => x.Numero).HasColumnType("varchar(15)").ValueGeneratedOnAdd();
        e.Property(x => x.Status).HasColumnType("varchar(22)").HasConversion<string>();
        e.Property(x => x.DataPrevista).HasColumnType("date");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.Ignore(x => x.ValorTotal);
        e.Ignore(x => x.PodeEmitir);
        e.Ignore(x => x.PodeCancelar);
        e.Ignore(x => x.PodeReceber);
        e.HasIndex(x => x.Numero).IsUnique();
        e.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.PedidoCompraId).OnDelete(DeleteBehavior.Restrict);
        e.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
