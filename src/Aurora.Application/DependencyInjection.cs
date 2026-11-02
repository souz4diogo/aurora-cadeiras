using Aurora.Application.Auditoria;
using Aurora.Application.Autenticacao;
using Aurora.Application.Clientes;
using Aurora.Application.Compras;
using Aurora.Application.Estacoes;
using Aurora.Application.Estoque;
using Aurora.Application.Expedicoes;
using Aurora.Application.FichasTecnicas;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Painel;
using Aurora.Application.Pedidos;
using Aurora.Application.Producao;
using Aurora.Application.Produtos;
using Aurora.Application.Qualidade;
using Aurora.Application.Relatorios;
using Aurora.Application.Relatorios.Geradores;
using Aurora.Application.Roteiros;
using Aurora.Application.Usuarios;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuroraApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddScoped<AuthService>();
        services.AddScoped<UsuarioService>();
        services.AddScoped<ProdutoService>();
        services.AddScoped<ClienteService>();
        services.AddScoped<FornecedorService>();
        services.AddScoped<EstacaoService>();
        services.AddScoped<FichaTecnicaService>();
        services.AddScoped<RoteiroService>();
        services.AddScoped<PedidoService>();
        services.AddScoped<EstoqueService>();
        services.AddScoped<OrdemProducaoService>();
        services.AddScoped<EtapaService>();
        services.AddScoped<ApontamentoService>();
        services.AddScoped<PedidoCompraService>();
        services.AddScoped<PainelService>();
        services.AddScoped<RelatorioService>();
        // Um gerador por tipo de relatório: acrescentar um tipo novo é acrescentar uma linha aqui
        services.AddScoped<IGeradorDeRelatorio, RelatorioDePedidos>();
        services.AddScoped<IGeradorDeRelatorio, RelatorioDeProducao>();
        services.AddScoped<IGeradorDeRelatorio, RelatorioDeEstoque>();
        services.AddScoped<IGeradorDeRelatorio, RelatorioDeConsumo>();
        services.AddScoped<IGeradorDeRelatorio, RelatorioDeComprasComunitarias>();
        services.AddScoped<InspecaoService>();
        services.AddScoped<ExpedicaoService>();
        services.AddScoped<AuditoriaService>();
        services.AddScoped<MateriaPrimaService>();
        return services;
    }
}
