using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="MateriaPrima"/> para a tabela do banco.</summary>
internal sealed class MateriaPrimaConfiguration : IEntityTypeConfiguration<MateriaPrima>
{
    public void Configure(EntityTypeBuilder<MateriaPrima> e)
    {
        // Os triggers de alerta e de auditoria impedem o uso de OUTPUT nos comandos do EF
        e.ToTable("MateriaPrima", "dbo", t =>
        {
            t.HasTrigger("trg_MateriaPrima_AU_Alerta");
            t.HasTrigger("trg_Auditoria_MateriaPrima");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("MateriaPrimaId");
        e.Property(x => x.Codigo).HasColumnType("varchar(20)").IsRequired();
        e.Property(x => x.Nome).HasColumnType("nvarchar(120)").IsRequired();
        e.Property(x => x.Unidade).HasColumnType("varchar(5)")
            .HasConversion(u => u.ParaCodigo(), c => UnidadesDeMedida.DeCodigo(c));
        e.Property(x => x.EstoqueAtual).HasColumnType("decimal(12,3)");
        e.Property(x => x.EstoqueMinimo).HasColumnType("decimal(12,3)");
        e.Property(x => x.CustoUnitario).HasColumnType("decimal(12,4)");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        e.HasIndex(x => x.Codigo).IsUnique();
    }
}
