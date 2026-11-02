using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="PedidoCompraItem"/> para a tabela do banco.</summary>
internal sealed class PedidoCompraItemConfiguration : IEntityTypeConfiguration<PedidoCompraItem>
{
    public void Configure(EntityTypeBuilder<PedidoCompraItem> e)
    {
        e.ToTable("PedidoCompraItem", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("PedidoCompraItemId");
        e.Property(x => x.Quantidade).HasColumnType("decimal(12,3)");
        e.Property(x => x.CustoUnitario).HasColumnType("decimal(12,4)");
        e.Property(x => x.QuantidadeRecebida).HasColumnType("decimal(12,3)");
        e.Ignore(x => x.Saldo);
        e.Ignore(x => x.Subtotal);
        e.HasIndex(x => new { x.PedidoCompraId, x.MateriaPrimaId }).IsUnique();
    }
}
