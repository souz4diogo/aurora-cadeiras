using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="RegistroPerda"/> para a tabela do banco.</summary>
internal sealed class RegistroPerdaConfiguration : IEntityTypeConfiguration<RegistroPerda>
{
    public void Configure(EntityTypeBuilder<RegistroPerda> e)
    {
        e.ToTable("RegistroPerda", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("RegistroPerdaId");
        e.Property(x => x.Tipo).HasColumnType("varchar(10)").HasConversion<string>();
        e.Property(x => x.Motivo).HasColumnType("nvarchar(200)").IsRequired();
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
    }
}
