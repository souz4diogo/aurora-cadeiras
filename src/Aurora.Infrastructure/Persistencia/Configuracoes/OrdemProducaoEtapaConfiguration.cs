using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="OrdemProducaoEtapa"/> para a tabela do banco.</summary>
internal sealed class OrdemProducaoEtapaConfiguration : IEntityTypeConfiguration<OrdemProducaoEtapa>
{
    public void Configure(EntityTypeBuilder<OrdemProducaoEtapa> e)
    {
        e.ToTable("OrdemProducaoEtapa", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("OrdemProducaoEtapaId");
        e.Property(x => x.Status).HasColumnType("varchar(12)").HasConversion<string>();
        e.Property(x => x.IniciadaEm).HasColumnType("datetime2(0)");
        e.Property(x => x.ConcluidaEm).HasColumnType("datetime2(0)");
        e.HasIndex(x => new { x.OrdemProducaoId, x.Sequencia }).IsUnique();
    }
}
