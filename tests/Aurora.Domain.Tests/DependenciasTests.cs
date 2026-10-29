using System.Reflection;

namespace Aurora.Domain.Tests;

public class DependenciasTests
{
    [Fact]
    public void Domain_nao_referencia_outros_projetos_aurora()
    {
        var assembly = Assembly.Load("Aurora.Domain");

        var referenciasAurora = assembly.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("Aurora.", StringComparison.Ordinal));

        Assert.Empty(referenciasAurora);
    }
}
