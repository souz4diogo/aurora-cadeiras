using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Cliente"/> para a tabela do banco.</summary>
internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> e)
    {
        e.ToTable("Cliente", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ClienteId");
        e.Property(x => x.Nome).HasColumnType("nvarchar(150)").IsRequired();
        e.Property(x => x.CpfCnpj).HasColumnType("varchar(14)").IsRequired();
        e.Property(x => x.Email).HasColumnType("varchar(150)");
        e.Property(x => x.Telefone).HasColumnType("varchar(20)");
        e.Property(x => x.Cidade).HasColumnType("nvarchar(80)");
        e.Property(x => x.Uf).HasColumnType("char(2)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.CpfCnpj).IsUnique();
    }
}
