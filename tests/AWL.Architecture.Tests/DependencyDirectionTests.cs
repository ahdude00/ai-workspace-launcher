using System.Reflection;
using AWL.Core;
using AWL.Desktop.ViewModels;
using AWL.Infrastructure;
using NetArchTest.Rules;

namespace AWL.Architecture.Tests;

/// <summary>
/// Guards the dependency direction and keeps ViewModels independent of WPF (BAK-01).
/// </summary>
public sealed class DependencyDirectionTests
{
    private static readonly Assembly _desktopAssembly = typeof(MainWindowViewModel).Assembly;

    private static readonly string?[] _forbiddenInCore =
    [
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Management",
        "Microsoft.Win32.Registry",
        typeof(InfrastructureModule).Assembly.GetName().Name,
        _desktopAssembly.GetName().Name,
    ];

    [Fact]
    public void Core_does_not_reference_platform_or_outer_layers()
    {
        Assembly assembly = typeof(CoreModule).Assembly;
        TestResult rule = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.Win32", "System.Windows", "System.Management")
            .GetResult();

        (rule.FailingTypeNames ?? []).ShouldBeEmpty();
        ReferencedAssemblyNames(assembly).ShouldNotContain(name => _forbiddenInCore.Contains(name));
    }

    [Fact]
    public void ViewModels_do_not_depend_on_system_windows_types()
    {
        TestResult rule = Types.InAssembly(_desktopAssembly)
            .That().ResideInNamespace("AWL.Desktop.ViewModels.")
            .ShouldNot()
            .HaveDependencyOn("System.Windows")
            .GetResult();

        (rule.FailingTypeNames ?? []).ShouldBeEmpty();
    }

    [Fact]
    public void Infrastructure_does_not_reference_desktop()
    {
        Assembly assembly = typeof(InfrastructureModule).Assembly;
        TestResult rule = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("AWL.Desktop")
            .GetResult();

        (rule.FailingTypeNames ?? []).ShouldBeEmpty();
        ReferencedAssemblyNames(assembly).ShouldNotContain(_desktopAssembly.GetName().Name);
    }

    private static IEnumerable<string?> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name);
}
