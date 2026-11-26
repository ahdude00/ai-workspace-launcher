using AWL.Core.Claude;
using AWL.Core.Diagnostics;
using AWL.Core.Python;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AWL.Desktop.ViewModels;

/// <summary>The About screen: versions, third-party licenses and saving the diagnostics package (AYR-04, TAN-03).</summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly IDiagnosticsPackage _package;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;

    public AboutViewModel(AppVersion version, IClaudeInstallation claude, IPythonEnvironment python,
        IDiagnosticsPackage package, IDialogService dialogs, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(claude);
        ArgumentNullException.ThrowIfNull(python);
        _package = package;
        _dialogs = dialogs;
        _time = time ?? TimeProvider.System;
        Versions = DiagnosticsExport.Versions(version, claude, python);
        Notices = ThirdPartyNotices.Text;
    }

    public VersionReport Versions { get; }

    public string AwlVersion => Versions.Awl;

    public string ClaudeVersion => Versions.Claude ?? Strings.VersionUnknown;

    public string PythonVersion => Versions.Python ?? Strings.VersionNotInstalled;

    public string WindowsVersion => $"{Versions.Windows} ({Versions.Architecture})";

    public string Notices { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDiagnosticsCommand))]
    public partial bool IsSaving { get; private set; }

    private bool CanSave => !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveDiagnosticsAsync()
    {
        IsSaving = true;
        try
        {
            await DiagnosticsExport.RunAsync(_dialogs, _package, Versions, _time).ConfigureAwait(true);
        }
        finally
        {
            IsSaving = false;
        }
    }
}

/// <summary>AWL's informational version, resolved once at startup.</summary>
public sealed record AppVersion(string Value);
