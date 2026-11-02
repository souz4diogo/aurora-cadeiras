using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="EstacaoProducao"/> para a tabela do banco.</summary>
internal sealed class EstacaoProducaoConfiguration : IEntityTypeConfiguration<EstacaoProducao>
{
    public void Configure(EntityTypeBuilder<EstacaoProducao> e)
    {
        e.ToTable("EstacaoProducao", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("EstacaoId").ValueGeneratedNever();
        e.Property(x => x.Nome).HasColumnType("varchar(40)").IsRequired();
    }
}
