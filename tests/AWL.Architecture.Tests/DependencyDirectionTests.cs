using System.Reflection;
using AWL.Core;
using AWL.Infrastructure;

namespace AWL.Architecture.Tests;

/// <summary>
/// Core must stay platform-free; Infrastructure must not know the UI (Teknik Tasarım §02).
/// </summary>
public sealed class DependencyDirectionTests
{
    private static readonly string[] ForbiddenInCore =
    [
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Management",
        "Microsoft.Win32.Registry",
        "AWL.Infrastructure",
        "AWL.Desktop",
    ];

    [Fact]
    public void Core_does_not_reference_platform_or_outer_layers()
    {
        IEnumerable<string?> references = ReferencedAssemblyNames(typeof(CoreModule).Assembly);

        references.ShouldNotContain(name => ForbiddenInCore.Contains(name));
    }

    [Fact]
    public void Infrastructure_does_not_reference_desktop()
    {
        IEnumerable<string?> references = ReferencedAssemblyNames(typeof(InfrastructureModule).Assembly);

        references.ShouldNotContain("AWL.Desktop");
    }

    private static IEnumerable<string?> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name);
}
