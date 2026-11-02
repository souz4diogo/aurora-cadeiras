using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento de <see cref="MovimentoEstoque"/> para a tabela do banco.</summary>
internal sealed class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> e)
    {
        // Livro-razão: a aplicação só lê; as procedures gravam (DENY de escrita para o login da API)
        e.ToTable("MovimentoEstoque", "dbo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("MovimentoEstoqueId");
        e.Property(x => x.Tipo).HasColumnType("varchar(8)").HasConversion<string>();
        e.Property(x => x.Origem).HasColumnType("varchar(20)").HasConversion<string>();
        e.Property(x => x.Quantidade).HasColumnType("decimal(12,3)");
        e.Property(x => x.Motivo).HasColumnType("nvarchar(200)");
        e.Property(x => x.DataHora).HasColumnType("datetime2(0)");
    }
}
