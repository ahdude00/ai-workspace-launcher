using AWL.Desktop.ViewModels;
using AWL.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Desktop;

/// <summary>
/// Registers windows, view models and UI services.
/// </summary>
internal static class DesktopModule
{
    public static IServiceCollection AddDesktop(this IServiceCollection services) =>
        services
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>();
}
