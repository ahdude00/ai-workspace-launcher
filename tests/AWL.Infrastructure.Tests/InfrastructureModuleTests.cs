using AWL.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Infrastructure.Tests;

public sealed class InfrastructureModuleTests
{
    [Fact]
    public void AddInfrastructure_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure().ShouldBeSameAs(services);
    }
}
