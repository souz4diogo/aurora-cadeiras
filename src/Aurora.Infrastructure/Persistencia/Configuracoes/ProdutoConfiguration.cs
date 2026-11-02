using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Produto"/> para a tabela do banco.</summary>
internal sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> e)
    {
        e.ToTable("Produto", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ProdutoId");
        e.Property(x => x.Codigo).HasColumnType("varchar(20)").IsRequired();
        e.Property(x => x.Nome).HasColumnType("nvarchar(120)").IsRequired();
        e.Property(x => x.Linha).HasColumnName("LinhaProdutoId").HasConversion<byte>();
        e.Property(x => x.PrecoVenda).HasColumnType("decimal(12,2)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.Codigo).IsUnique();
    }
}
