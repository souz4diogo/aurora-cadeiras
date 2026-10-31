using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public class AuroraDbContext(DbContextOptions<AuroraDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();

    public DbSet<EstacaoProducao> Estacoes => Set<EstacaoProducao>();

    public DbSet<FichaTecnicaItem> FichasTecnicas => Set<FichaTecnicaItem>();

    public DbSet<RoteiroProduto> Roteiros => Set<RoteiroProduto>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<MovimentoEstoque> Movimentos => Set<MovimentoEstoque>();

    public DbSet<MateriaPrima> MateriasPrimas => Set<MateriaPrima>();

    public DbSet<OrdemProducao> OrdensProducao => Set<OrdemProducao>();

    public DbSet<OrdemProducaoEtapa> Etapas => Set<OrdemProducaoEtapa>();

    public DbSet<ApontamentoProducao> Apontamentos => Set<ApontamentoProducao>();

    public DbSet<RegistroPerda> Perdas => Set<RegistroPerda>();

    public DbSet<AlertaEstoque> Alertas => Set<AlertaEstoque>();

    public DbSet<EstoqueProduto> EstoquesDeProduto => Set<EstoqueProduto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            // O trigger de auditoria impede o uso de OUTPUT nos comandos do EF
            e.ToTable("Usuario", "dbo", t => t.HasTrigger("trg_Auditoria_Usuario"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("UsuarioId");
            e.Property(x => x.Nome).HasColumnType("nvarchar(120)").IsRequired();
            e.Property(x => x.Email).HasColumnType("varchar(150)").IsRequired();
            e.Property(x => x.SenhaHash).HasColumnType("varchar(255)").IsRequired();
            e.Property(x => x.Perfil).HasColumnName("PerfilId").HasConversion<byte>();
            e.Property(x => x.BloqueadoAte).HasColumnType("datetime2(0)");
            e.Property(x => x.UltimoLoginEm).HasColumnType("datetime2(0)");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshToken", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("RefreshTokenId");
            e.Property(x => x.TokenHash).HasColumnType("varchar(255)").IsRequired();
            e.Property(x => x.ExpiraEm).HasColumnType("datetime2(0)");
            e.Property(x => x.RevogadoEm).HasColumnType("datetime2(0)");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("Cliente", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ClienteId");
            e.Property(x => x.Nome).HasColumnType("nvarchar(150)").IsRequired();
            e.Property(x => x.CpfCnpj).HasColumnType("varchar(14)").IsRequired();
            e.Property(x => x.Email).HasColumnType("varchar(150)");
            e.Property(x => x.Telefone).HasColumnType("varchar(20)");
            e.Property(x => x.Cidade).HasColumnType("nvarchar(80)");
            e.Property(x => x.Uf).HasColumnType("char(2)");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.HasIndex(x => x.CpfCnpj).IsUnique();
        });

        modelBuilder.Entity<Fornecedor>(e =>
        {
            e.ToTable("Fornecedor", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("FornecedorId");
            e.Property(x => x.RazaoSocial).HasColumnType("nvarchar(150)").IsRequired();
            e.Property(x => x.Cnpj).HasColumnType("char(14)").IsRequired();
            e.Property(x => x.Email).HasColumnType("varchar(150)");
            e.Property(x => x.Telefone).HasColumnType("varchar(20)");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.HasIndex(x => x.Cnpj).IsUnique();
        });

        modelBuilder.Entity<EstacaoProducao>(e =>
        {
            e.ToTable("EstacaoProducao", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("EstacaoId").ValueGeneratedNever();
            e.Property(x => x.Nome).HasColumnType("varchar(40)").IsRequired();
        });

        modelBuilder.Entity<FichaTecnicaItem>(e =>
        {
            e.ToTable("FichaTecnicaItem", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("FichaTecnicaItemId");
            e.Property(x => x.Quantidade).HasColumnType("decimal(12,3)");
            e.HasIndex(x => new { x.ProdutoId, x.MateriaPrimaId, x.EstacaoId }).IsUnique();
        });

        modelBuilder.Entity<RoteiroProduto>(e =>
        {
            e.ToTable("RoteiroProduto", "dbo");
            e.HasKey(x => new { x.ProdutoId, x.EstacaoId });
            e.HasIndex(x => new { x.ProdutoId, x.Sequencia }).IsUnique();
        });

        modelBuilder.Entity<MovimentoEstoque>(e =>
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
        });

        // Produção: as procedures criam e avançam OPs, etapas e apontamentos (DENY de escrita para o login da API);
        // a aplicação só lê, exceto as perdas e a leitura dos alertas
        modelBuilder.Entity<OrdemProducao>(e =>
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
        });

        modelBuilder.Entity<OrdemProducaoEtapa>(e =>
        {
            e.ToTable("OrdemProducaoEtapa", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("OrdemProducaoEtapaId");
            e.Property(x => x.Status).HasColumnType("varchar(12)").HasConversion<string>();
            e.Property(x => x.IniciadaEm).HasColumnType("datetime2(0)");
            e.Property(x => x.ConcluidaEm).HasColumnType("datetime2(0)");
            e.HasIndex(x => new { x.OrdemProducaoId, x.Sequencia }).IsUnique();
        });

        modelBuilder.Entity<ApontamentoProducao>(e =>
        {
            e.ToTable("ApontamentoProducao", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ApontamentoId");
            e.Property(x => x.Tipo).HasColumnType("varchar(6)").HasConversion<string>();
            e.Property(x => x.DataHoraCliente).HasColumnType("datetime2(0)");
            e.Property(x => x.DataHoraServidor).HasColumnType("datetime2(0)");
            e.HasIndex(x => x.ClientRequestId).IsUnique();
        });

        modelBuilder.Entity<RegistroPerda>(e =>
        {
            e.ToTable("RegistroPerda", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("RegistroPerdaId");
            e.Property(x => x.Tipo).HasColumnType("varchar(10)").HasConversion<string>();
            e.Property(x => x.Motivo).HasColumnType("nvarchar(200)").IsRequired();
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<AlertaEstoque>(e =>
        {
            e.ToTable("AlertaEstoque", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("AlertaEstoqueId");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)");
            e.Property(x => x.LidoEm).HasColumnType("datetime2(0)");
            e.Ignore(x => x.Lido);
        });

        modelBuilder.Entity<EstoqueProduto>(e =>
        {
            e.ToTable("EstoqueProduto", "dbo");
            e.HasKey(x => x.ProdutoId);
            e.Property(x => x.ProdutoId).ValueGeneratedNever();
            e.Ignore(x => x.Disponivel);
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            // O trigger de auditoria impede o uso de OUTPUT nos comandos do EF
            e.ToTable("Pedido", "dbo", t => t.HasTrigger("trg_Auditoria_Pedido"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("PedidoId");
            e.Property(x => x.Numero).HasColumnType("varchar(15)").ValueGeneratedOnAdd();
            e.Property(x => x.DataPedido).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.Property(x => x.PrazoEntrega).HasColumnType("date");
            e.Property(x => x.Status).HasColumnType("varchar(15)").HasConversion<string>();
            e.Property(x => x.Observacao).HasColumnType("nvarchar(300)");
            e.Property(x => x.ValorTotal).HasColumnType("decimal(14,2)");
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Itens).WithOne().HasForeignKey(i => i.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PedidoItem>(e =>
        {
            e.ToTable("PedidoItem", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("PedidoItemId");
            e.Property(x => x.PrecoUnitario).HasColumnType("decimal(12,2)");
            e.Ignore(x => x.Subtotal);
            e.HasIndex(x => new { x.PedidoId, x.ProdutoId }).IsUnique();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("Produto", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ProdutoId");
            e.Property(x => x.Codigo).HasColumnType("varchar(20)").IsRequired();
            e.Property(x => x.Nome).HasColumnType("nvarchar(120)").IsRequired();
            e.Property(x => x.Linha).HasColumnName("LinhaProdutoId").HasConversion<byte>();
            e.Property(x => x.PrecoVenda).HasColumnType("decimal(12,2)");
            e.Property(x => x.CriadoEm).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<MateriaPrima>(e =>
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
        });
    }
}
