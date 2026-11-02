using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="FichaTecnicaItem"/> para a tabela do banco.</summary>
internal sealed class FichaTecnicaItemConfiguration : IEntityTypeConfiguration<FichaTecnicaItem>
{
    public void Configure(EntityTypeBuilder<FichaTecnicaItem> e)
    {
        e.ToTable("FichaTecnicaItem", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("FichaTecnicaItemId");
        e.Property(x => x.Quantidade).HasColumnType("decimal(12,3)");
        e.HasIndex(x => new { x.ProdutoId, x.MateriaPrimaId, x.EstacaoId }).IsUnique();
    }
}
