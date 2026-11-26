using System.Globalization;
using System.IO;
using System.Text;
using AWL.Core.Claude;
using AWL.Core.Configuration;
using AWL.Core.Diagnostics;
using AWL.Core.Python;
using AWL.Core.Retention;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AWL.Desktop.ViewModels;

/// <summary>
/// The few settings a user can change, plus repair and support commands (AYR-01, AYR-02, KUR-17, TAN-03).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly CompositeFormat ShortenConfirm = CompositeFormat.Parse(Strings.SettingsRetentionConfirm);

    private readonly IAwlSettingsStore _store;
    private readonly SessionDeletionService _deletion;
    private readonly IDialogService _dialogs;
    private readonly IRepairService _repair;
    private readonly IDiagnosticsPackage _package;
    private readonly VersionReport _versions;
    private readonly TimeProvider _time;
    private readonly int _savedRetention;

    public SettingsViewModel(IAwlSettingsStore store, SessionDeletionService deletion, IDialogService dialogs,
        IRepairService repair, IDiagnosticsPackage package, AppVersion version, IClaudeInstallation claude,
        IPythonEnvironment python, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _deletion = deletion;
        _dialogs = dialogs;
        _repair = repair;
        _package = package;
        _versions = DiagnosticsExport.Versions(version, claude, python);
        _time = time ?? TimeProvider.System;
        var settings = store.Load();
        _savedRetention = settings.RetentionDays;
        RetentionText = settings.RetentionDays.ToString(CultureInfo.CurrentCulture);
        UseWindowsTerminal = settings.TerminalPreference == TerminalPreference.WindowsTerminal;
    }

    /// <summary>Raised when the window should close (after saving or before a repair).</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    public partial string RetentionText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseConsole))]
    public partial bool UseWindowsTerminal { get; set; }

    public bool UseConsole
    {
        get => !UseWindowsTerminal;
        set => UseWindowsTerminal = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!int.TryParse(RetentionText.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out int days)
            || !RetentionPolicy.IsValid(days))
        {
            ErrorMessage = Strings.SettingsRetentionInvalid;
            return;
        }

        // A shorter period removes sessions at once (AYR-01: the same period applies to AWL's records); ask first.
        int expiring = days < _savedRetention ? await _deletion.CountExpiredAsync(days).ConfigureAwait(true) : 0;
        if (expiring > 0 && !_dialogs.Confirm(string.Format(CultureInfo.CurrentCulture, ShortenConfirm, expiring, days)))
        {
            return;
        }

        try
        {
            // Save also rewrites claude-settings.json, so Claude keeps its transcripts for the same period.
            _store.Save(_store.Load() with
            {
                RetentionDays = days,
                TerminalPreference = UseWindowsTerminal ? TerminalPreference.WindowsTerminal : TerminalPreference.WindowsConsole,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Strings.SettingsSaveFailed;
            return;
        }
        if (expiring > 0)
        {
            await _deletion.PurgeExpiredAsync(days).ConfigureAwait(true);
        }
        ErrorMessage = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Repair()
    {
        if (!_dialogs.Confirm(Strings.RepairConfirm))
        {
            return;
        }
        CloseRequested?.Invoke(this, EventArgs.Empty);
        _repair.RequestRepair();
    }

    [RelayCommand]
    private Task SaveDiagnosticsAsync() => DiagnosticsExport.RunAsync(_dialogs, _package, _versions, _time);

    [RelayCommand]
    private void ShowAbout() => _dialogs.ShowAbout();
}
