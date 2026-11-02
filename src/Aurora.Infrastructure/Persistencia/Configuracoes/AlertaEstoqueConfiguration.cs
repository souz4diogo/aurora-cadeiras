using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="AlertaEstoque"/> para a tabela do banco.</summary>
internal sealed class AlertaEstoqueConfiguration : IEntityTypeConfiguration<AlertaEstoque>
{
    public void Configure(EntityTypeBuilder<AlertaEstoque> e)
    {
        e.ToTable("AlertaEstoque", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("AlertaEstoqueId");
        e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)");
        e.Property(x => x.LidoEm).HasColumnType("datetime2(0)");
        e.Ignore(x => x.Lido);
    }
}
