using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

/// <summary>Regressões dos achados da auditoria de acessibilidade (axe e Lighthouse, fase 6).</summary>
[Collection("painel")]
public sealed class AcessibilidadeWebTests(CenarioDoPainel cenario)
{
    [Fact]
    public async Task Login_nao_tem_estilo_embutido_que_a_politica_de_conteudo_bloquearia()
    {
        using var anonimo = cenario.Painel.NovoNavegador();

        var pagina = await anonimo.PaginaAsync("/Conta/Login");

        Assert.DoesNotMatch(@"<[^>]+\sstyle=", pagina);
        Assert.Contains("<meta name=\"description\"", pagina, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"pt-BR\"", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Formulario_com_erro_continua_sem_estilo_embutido_e_com_o_resumo_de_erros()
    {
        using var almoxarife = await cenario.EntrarComoAsync("Almoxarife");

        var envio = await almoxarife.EnviarAsync("/Estoque/Ajustar", "/Estoque/Ajustar", []);
        var pagina = await envio.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Contains("role=\"alert\"", pagina, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<[^>]+\sstyle=", pagina);
    }

    [Fact]
    public async Task Robots_txt_e_publico()
    {
        using var anonimo = cenario.Painel.NovoNavegador();

        var resposta = await anonimo.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("User-agent", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_so_se_expande_em_telas_largas_e_o_quadro_rolavel_tem_acesso_por_teclado()
    {
        using var gerente = await cenario.EntrarComoAsync("GerenteProducao");

        var inicio = await gerente.PaginaAsync("/");
        var quadro = await gerente.PaginaAsync("/Producao");

        Assert.Contains("navbar-expand-xxl", inicio, StringComparison.Ordinal);
        Assert.Contains("class=\"quadro\" role=\"region\"", quadro, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", quadro, StringComparison.Ordinal);
    }
}
