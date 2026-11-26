using System.Globalization;
using System.Runtime.InteropServices;
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
        Versions = new VersionReport(version.Value, claude.Version, python.InstalledVersion,
            Environment.OSVersion.VersionString, RuntimeInformation.OSArchitecture.ToString());
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
        string name = string.Create(CultureInfo.InvariantCulture, $"AWL-tanilama-{_time.GetLocalNow():yyyyMMdd-HHmm}.zip");
        if (_dialogs.PickSaveFile(name, Strings.DiagnosticsFileFilter) is not { } destination)
        {
            return;
        }
        IsSaving = true;
        try
        {
            var result = await _package.SaveAsync(destination, Versions, CancellationToken.None).ConfigureAwait(true);
            if (result.IsSuccess)
            {
                _dialogs.ShowInfo(Strings.DiagnosticsSaved + Environment.NewLine + destination);
            }
            else
            {
                _dialogs.ShowError(Strings.DiagnosticsSaveFailed);
            }
        }
        finally
        {
            IsSaving = false;
        }
    }
}

/// <summary>AWL's informational version, resolved once at startup.</summary>
public sealed record AppVersion(string Value);
