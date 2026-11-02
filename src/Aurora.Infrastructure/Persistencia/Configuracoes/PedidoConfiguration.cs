using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Pedido"/> para a tabela do banco.</summary>
internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> e)
    {
        // O trigger de auditoria impede o uso de OUTPUT nos comandos do EF
        e.ToTable("Pedido", "dbo", t => t.HasTrigger("trg_Auditoria_Pedido"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("PedidoId");
        e.Property(x => x.Numero).HasColumnType("varchar(15)").ValueGeneratedOnAdd();
        e.Property(x => x.DataPedido).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.Property(x => x.PrazoEntrega).HasColumnType("date");
        e.Property(x => x.Status).HasColumnType("varchar(15)").HasConversion<string>();
        e.Property(x => x.Observacao).HasColumnType("nvarchar(300)");
        e.Property(x => x.ValorTotal).HasColumnType("decimal(14,2)");
        e.HasIndex(x => x.Numero).IsUnique();
        e.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.PedidoId).OnDelete(DeleteBehavior.Cascade);
        e.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
