using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Fornecedor"/> para a tabela do banco.</summary>
internal sealed class FornecedorConfiguration : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> e)
    {
        e.ToTable("Fornecedor", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("FornecedorId");
        e.Property(x => x.RazaoSocial).HasColumnType("nvarchar(150)").IsRequired();
        e.Property(x => x.Cnpj).HasColumnType("char(14)").IsRequired();
        e.Property(x => x.Email).HasColumnType("varchar(150)");
        e.Property(x => x.Telefone).HasColumnType("varchar(20)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.Cnpj).IsUnique();
    }
}
