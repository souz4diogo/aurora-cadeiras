using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento de <see cref="OrdemProducao"/> para a tabela do banco.
/// <para>
/// Vale para o grupo de produção (OP, etapa, apontamento e perda): as procedures criam e avançam OPs, etapas e
/// apontamentos (DENY de escrita para o login da API); a aplicação só lê, exceto as perdas e a leitura dos alertas.
/// </para>
/// </summary>
internal sealed class OrdemProducaoConfiguration : IEntityTypeConfiguration<OrdemProducao>
{
    public void Configure(EntityTypeBuilder<OrdemProducao> e)
    {
        e.ToTable("OrdemProducao", "dbo", t => t.HasTrigger("trg_Auditoria_OrdemProducao"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("OrdemProducaoId");
        e.Property(x => x.Numero).HasColumnType("varchar(15)").ValueGeneratedOnAdd();
        e.Property(x => x.Status).HasColumnType("varchar(12)").HasConversion<string>();
        e.Property(x => x.PrevisaoConclusao).HasColumnType("date");
        e.Property(x => x.CriadaEm).HasColumnType("datetime2(0)");
        e.Property(x => x.ConcluidaEm).HasColumnType("datetime2(0)");
        e.HasIndex(x => x.Numero).IsUnique();
        e.HasIndex(x => x.CodigoQr).IsUnique();
        e.HasMany(x => x.Etapas).WithOne().HasForeignKey(x => x.OrdemProducaoId).OnDelete(DeleteBehavior.Restrict);
        e.Navigation(x => x.Etapas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
