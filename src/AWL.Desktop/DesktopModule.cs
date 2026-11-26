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
            .AddSingleton<SessionChangeRelay>()
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<ISetupStep, ClaudeLoginStep>();
}
