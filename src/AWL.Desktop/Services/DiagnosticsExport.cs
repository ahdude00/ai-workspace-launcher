using System.Globalization;
using System.Runtime.InteropServices;
using AWL.Core.Claude;
using AWL.Core.Diagnostics;
using AWL.Core.Python;
using AWL.Desktop.Resources;
using AWL.Desktop.ViewModels;

namespace AWL.Desktop.Services;

/// <summary>"Tanılama paketini kaydet", shared by the About and Settings screens (TAN-03).</summary>
public static class DiagnosticsExport
{
    public static VersionReport Versions(AppVersion version, IClaudeInstallation claude, IPythonEnvironment python)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(claude);
        ArgumentNullException.ThrowIfNull(python);
        return new VersionReport(version.Value, claude.Version, python.InstalledVersion,
            Environment.OSVersion.VersionString, RuntimeInformation.OSArchitecture.ToString());
    }

    /// <summary>Asks where to save, writes the package and tells the user where it is.</summary>
    public static async Task RunAsync(IDialogService dialogs, IDiagnosticsPackage package, VersionReport versions,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(time);
        string name = string.Create(CultureInfo.InvariantCulture, $"AWL-tanilama-{time.GetLocalNow():yyyyMMdd-HHmm}.zip");
        if (dialogs.PickSaveFile(name, Strings.DiagnosticsFileFilter) is not { } destination)
        {
            return;
        }
        var result = await package.SaveAsync(destination, versions, CancellationToken.None).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            dialogs.ShowInfo(Strings.DiagnosticsSaved + Environment.NewLine + destination);
        }
        else
        {
            dialogs.ShowError(Strings.DiagnosticsSaveFailed);
        }
    }
}
