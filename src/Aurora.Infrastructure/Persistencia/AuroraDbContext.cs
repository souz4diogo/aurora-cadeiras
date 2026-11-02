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

    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();

    public DbSet<Inspecao> Inspecoes => Set<Inspecao>();

    public DbSet<Expedicao> Expedicoes => Set<Expedicao>();

    public DbSet<AlertaEstoque> Alertas => Set<AlertaEstoque>();

    public DbSet<EstoqueProduto> EstoquesDeProduto => Set<EstoqueProduto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuroraDbContext).Assembly);
    }
}
