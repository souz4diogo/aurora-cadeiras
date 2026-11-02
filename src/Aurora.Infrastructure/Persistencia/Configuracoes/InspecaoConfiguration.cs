using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento de <see cref="Inspecao"/> para a tabela do banco.
/// <para>Inspeção e expedição são gravadas por sp_RegistrarInspecao e sp_Expedir; a aplicação só consulta</para>
/// </summary>
internal sealed class InspecaoConfiguration : IEntityTypeConfiguration<Inspecao>
{
    public void Configure(EntityTypeBuilder<Inspecao> e)
    {
        e.ToTable("Inspecao", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("InspecaoId");
        e.Property(x => x.Resultado).HasColumnType("varchar(10)").HasConversion<string>();
        e.Property(x => x.Observacao).HasColumnType("nvarchar(300)");
        e.Property(x => x.DataHora).HasColumnType("datetime2(0)");
    }
}
