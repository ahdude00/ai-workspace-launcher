using AWL.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Core.Tests;

public sealed class CoreModuleTests
{
    [Fact]
    public void AddCore_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddCore().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddCore_rejects_null()
    {
        Should.Throw<ArgumentNullException>(() => CoreModule.AddCore(null!));
    }
}
