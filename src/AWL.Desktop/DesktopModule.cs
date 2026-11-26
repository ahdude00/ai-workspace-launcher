using System.Reflection;
using System.Windows;
using AWL.Core.Setup;
using AWL.Desktop.Messages;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using AWL.Desktop.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Desktop;

/// <summary>
/// Registers windows, view models and UI services.
/// </summary>
internal static class DesktopModule
{
    public static IServiceCollection AddDesktop(this IServiceCollection services) =>
        services
            .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
            .AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Application.Current.Dispatcher))
            .AddSingleton<IDialogService, WpfDialogService>()
            .AddSingleton<IShellService, ExplorerShellService>()
            .AddSingleton<IWindowActivator, WpfWindowActivator>()
            .AddSingleton<IClipboardService, WpfClipboardService>()
            .AddSingleton<LaunchRequestCoordinator>()
            .AddSingleton<SessionChangeRelay>()
            .AddSingleton<MainWindowViewModel>()
            .AddTransient<AboutViewModel>()
            .AddSingleton(_ => new AppVersion(ReadVersion()))
            .AddSingleton<DropZoneViewModel>()
            .AddSingleton<MainWindow>()
            // Order follows KUR-03: … configuration, Windows integration, sign-in.
            .AddSingleton<ISetupStep>(provider => new ShellIntegrationStep(
                provider.GetRequiredService<AWL.Core.Shell.IShellIntegration>()))
            .AddSingleton<ISetupStep, ClaudeLoginStep>();

    internal static string ReadVersion()
    {
        Assembly assembly = typeof(App).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
