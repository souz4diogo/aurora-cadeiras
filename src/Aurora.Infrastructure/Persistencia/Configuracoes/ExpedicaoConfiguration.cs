using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="Expedicao"/> para a tabela do banco.</summary>
internal sealed class ExpedicaoConfiguration : IEntityTypeConfiguration<Expedicao>
{
    public void Configure(EntityTypeBuilder<Expedicao> e)
    {
        e.ToTable("Expedicao", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ExpedicaoId");
        e.Property(x => x.Transportadora).HasColumnType("nvarchar(80)");
        e.Property(x => x.CodigoRastreio).HasColumnType("varchar(40)");
        e.Property(x => x.DataHora).HasColumnType("datetime2(0)");
    }
}
