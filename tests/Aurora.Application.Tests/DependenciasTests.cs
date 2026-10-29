using System.Reflection;

namespace Aurora.Application.Tests;

public class DependenciasTests
{
    [Fact]
    public void Application_referencia_apenas_Domain()
    {
        var assembly = Assembly.Load("Aurora.Application");

        var referenciasAurora = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Aurora.", StringComparison.Ordinal));

        Assert.All(referenciasAurora, n => Assert.Equal("Aurora.Domain", n));
    }
}
