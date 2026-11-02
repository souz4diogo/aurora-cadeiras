using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="ApontamentoProducao"/> para a tabela do banco.</summary>
internal sealed class ApontamentoProducaoConfiguration : IEntityTypeConfiguration<ApontamentoProducao>
{
    public void Configure(EntityTypeBuilder<ApontamentoProducao> e)
    {
        e.ToTable("ApontamentoProducao", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ApontamentoId");
        e.Property(x => x.Tipo).HasColumnType("varchar(6)").HasConversion<string>();
        e.Property(x => x.DataHoraCliente).HasColumnType("datetime2(0)");
        e.Property(x => x.DataHoraServidor).HasColumnType("datetime2(0)");
        e.HasIndex(x => x.ClientRequestId).IsUnique();
    }
}
