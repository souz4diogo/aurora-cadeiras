using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="RegistroDeAuditoria"/> para a tabela Auditoria (somente leitura).</summary>
internal sealed class RegistroDeAuditoriaConfiguration : IEntityTypeConfiguration<RegistroDeAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroDeAuditoria> e)
    {
        e.ToTable("Auditoria", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("AuditoriaId");
        e.Property(x => x.Tabela).HasColumnType("sysname");
        e.Property(x => x.Registro).HasColumnType("varchar(50)");
        e.Property(x => x.Acao).HasColumnType("char(1)");
        e.Property(x => x.DataHora).HasColumnType("datetime2(0)");
        e.Property(x => x.Antes).HasColumnType("nvarchar(max)");
        e.Property(x => x.Depois).HasColumnType("nvarchar(max)");
    }
}
