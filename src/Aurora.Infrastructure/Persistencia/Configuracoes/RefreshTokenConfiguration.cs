using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="RefreshToken"/> para a tabela do banco.</summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshToken", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("RefreshTokenId");
        e.Property(x => x.TokenHash).HasColumnType("varchar(255)").IsRequired();
        e.Property(x => x.ExpiraEm).HasColumnType("datetime2(0)");
        e.Property(x => x.RevogadoEm).HasColumnType("datetime2(0)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.TokenHash).IsUnique();
    }
}
