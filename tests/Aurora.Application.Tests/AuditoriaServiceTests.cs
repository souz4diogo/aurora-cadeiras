using Aurora.Application.Abstracoes;
using Aurora.Application.Auditoria;
using Aurora.Domain.Excecoes;
using Moq;

namespace Aurora.Application.Tests;

public class AuditoriaServiceTests
{
    private readonly Mock<IAuditoriaRepository> _repositorio = new();

    private AuditoriaService Servico() => new(_repositorio.Object);

    [Fact]
    public void Alteracao_lista_so_os_campos_que_mudaram()
    {
        var campos = DiferencaDeAuditoria.Calcular(
            """{"PedidoId":7,"Status":"Aberto","Observacao":null,"ValorTotal":10.5}""",
            """{"PedidoId":7,"Status":"Confirmado","Observacao":"urgente","ValorTotal":10.5}""");

        Assert.Equal(
            [new CampoAlteradoDto("Status", "Aberto", "Confirmado"), new CampoAlteradoDto("Observacao", null, "urgente")],
            campos);
    }

    [Fact]
    public void Inclusao_lista_todos_os_campos_e_exclusao_tambem()
    {
        var inclusao = DiferencaDeAuditoria.Calcular(null, """{"Id":1,"Nome":"A","Ativo":true}""");
        var exclusao = DiferencaDeAuditoria.Calcular("""{"Id":1,"Nome":"A"}""", null);

        Assert.Equal(
            [new CampoAlteradoDto("Id", null, "1"), new CampoAlteradoDto("Nome", null, "A"), new CampoAlteradoDto("Ativo", null, "true")],
            inclusao);
        Assert.Equal([new CampoAlteradoDto("Id", "1", null), new CampoAlteradoDto("Nome", "A", null)], exclusao);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "  ")]
    [InlineData("isso não é json", "{quebrado")]
    [InlineData("[1,2]", "\"texto\"")]
    public void Json_ausente_ou_invalido_vira_lista_vazia_sem_lancar(string? antes, string? depois) =>
        Assert.Empty(DiferencaDeAuditoria.Calcular(antes, depois));

    [Theory]
    [InlineData("I", "Inclusão")]
    [InlineData("U", "Alteração")]
    [InlineData("D", "Exclusão")]
    [InlineData("X", "X")]
    public void Nome_da_acao_em_portugues(string codigo, string esperado) =>
        Assert.Equal(esperado, DiferencaDeAuditoria.NomeDaAcao(codigo));

    [Fact]
    public async Task Tabela_que_nao_e_auditada_e_recusada_sem_consultar_o_banco()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().ListarAsync(new FiltroDeAuditoria("Usuario; DROP TABLE x", null, null, null, null), 1, 20));

        Assert.Equal("FILTRO_INVALIDO", erro.Codigo);
        _repositorio.Verify(
            r => r.ListarAsync(It.IsAny<FiltroDeAuditoria>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Periodo_invertido_e_recusado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().ListarAsync(
                new FiltroDeAuditoria(null, null, null, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)), 1, 20));

        Assert.Equal("FILTRO_INVALIDO", erro.Codigo);
    }

    [Fact]
    public async Task Filtros_em_branco_viram_nulos_e_a_linha_sai_com_a_diferenca_calculada()
    {
        FiltroDeAuditoria? recebido = null;
        IReadOnlyList<AuditoriaResumo> linhas =
        [
            new(5, "Pedido", "9", "U", 3, "Ana", new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc), "{\"S\":\"a\"}", "{\"S\":\"b\"}"),
        ];
        _repositorio.Setup(r => r.ListarAsync(It.IsAny<FiltroDeAuditoria>(), 1, 20, It.IsAny<CancellationToken>()))
            .Callback<FiltroDeAuditoria, int, int, CancellationToken>((f, _, _, _) => recebido = f)
            .ReturnsAsync((linhas, 1));

        var pagina = await Servico().ListarAsync(new FiltroDeAuditoria(" Pedido ", "  ", null, null, null), 1, 20);

        Assert.Equal(new FiltroDeAuditoria("Pedido", null, null, null, null), recebido);
        var linha = Assert.Single(pagina.Items);
        Assert.Equal("Alteração", linha.Acao);
        Assert.Equal("Ana", linha.UsuarioNome);
        Assert.Equal([new CampoAlteradoDto("S", "a", "b")], linha.Alteracoes);
    }
}
