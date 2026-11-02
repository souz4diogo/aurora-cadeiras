using Aurora.Infrastructure.Relatorios;

namespace Aurora.Api.IntegrationTests;

public class CulturaDosRelatoriosTests
{
    [Fact]
    public void Garantir_passa_quando_o_ambiente_formata_em_pt_BR() =>
        CulturaDosRelatorios.Garantir();
}
