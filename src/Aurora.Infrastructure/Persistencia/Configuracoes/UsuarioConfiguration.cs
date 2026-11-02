using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Usuario"/> para a tabela do banco.</summary>
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> e)
    {
        // O trigger de auditoria impede o uso de OUTPUT nos comandos do EF
        e.ToTable("Usuario", "dbo", t => t.HasTrigger("trg_Auditoria_Usuario"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("UsuarioId");
        e.Property(x => x.Nome).HasColumnType("nvarchar(120)").IsRequired();
        e.Property(x => x.Email).HasColumnType("varchar(150)").IsRequired();
        e.Property(x => x.SenhaHash).HasColumnType("varchar(255)").IsRequired();
        e.Property(x => x.Perfil).HasColumnName("PerfilId").HasConversion<byte>();
        e.Property(x => x.BloqueadoAte).HasColumnType("datetime2(0)");
        e.Property(x => x.UltimoLoginEm).HasColumnType("datetime2(0)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.Email).IsUnique();
    }
}
