using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AWL.Core.Categories;
using AWL.Core.Common;
using AWL.Core.Sessions;
using AWL.Desktop.Messages;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AWL.Desktop.ViewModels;

/// <summary>The pages of the main window (D06).</summary>
public enum MainPage
{
    Sessions,
    Tools,
}

/// <summary>
/// View model of the main window: the grouped session list and the actions of the selected session.
/// Holds no WPF types (MVVM rule, Teknik Tasarım §08).
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>The folder inside each workspace where Claude writes new files (BLG-03).</summary>
    public const string OutputsFolderName = "AWL_Ciktilar";

    private const int UncategorizedOrder = int.MaxValue - 1;
    private const int ArchiveOrder = int.MaxValue;

    private readonly SessionService _sessions;
    private readonly ICategoryRepository _categories;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<Guid, SessionItemViewModel> _items = [];
    private int _refreshQueued;

    public MainWindowViewModel(SessionService sessions, ICategoryRepository categories, IDialogService dialogs,
        IShellService shell, IUiDispatcher dispatcher, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _sessions = sessions;
        _categories = categories;
        _dialogs = dialogs;
        _shell = shell;
        _dispatcher = dispatcher;
        messenger.Register<MainWindowViewModel, SessionsChangedMessage>(this, static (recipient, _) => recipient.QueueRefresh());
    }

    public string Title { get; } = Strings.MainWindowTitle;

    /// <summary>Every session; the view groups and sorts them.</summary>
    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    public bool IsEmpty => Sessions.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand), nameof(OpenFolderCommand), nameof(OpenOutputsCommand),
        nameof(EditCommand), nameof(ToggleArchiveCommand))]
    public partial SessionItemViewModel? SelectedSession { get; set; }

    public bool HasSelection => SelectedSession is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSessionsPage), nameof(IsToolsPage))]
    public partial MainPage CurrentPage { get; set; }

    public bool IsSessionsPage => CurrentPage == MainPage.Sessions;

    public bool IsToolsPage => CurrentPage == MainPage.Tools;

    /// <summary>Reconciles stored sessions with Claude's files (OTR-15) and loads the list.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => _sessions.ReconcileAsync(cancellationToken), cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reloads sessions and categories and updates the list in place. Must run on the UI thread.</summary>
    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await Task.Run(() => _sessions.ListAsync(cancellationToken), cancellationToken).ConfigureAwait(true);
        var categories = await _categories.ListAsync(cancellationToken).ConfigureAwait(true);
        Apply(snapshots, categories);
    }

    [RelayCommand]
    private void ShowSessions() => CurrentPage = MainPage.Sessions;

    [RelayCommand]
    private void ShowTools() => CurrentPage = MainPage.Tools;

    /// <summary>Shows the new-session window, optionally with a folder already chosen, then opens the session.</summary>
    public async Task NewSessionAsync(string? folder = null)
    {
        var categories = await _categories.ListAsync().ConfigureAwait(true);
        var editor = new SessionEditorViewModel(_sessions, _dialogs, categories, initialFolder: folder);
        if (!_dialogs.ShowSessionEditor(editor) || editor.SavedSession is not { } created)
        {
            return;
        }
        await RefreshAsync().ConfigureAwait(true);
        if (_items.TryGetValue(created.Id, out var item))
        {
            CurrentPage = MainPage.Sessions;
            SelectedSession = item;
            await OpenSessionAsync(item).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task NewSession() => NewSessionAsync();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedSession is not { } item)
        {
            return;
        }
        var categories = await _categories.ListAsync().ConfigureAwait(true);
        var editor = new SessionEditorViewModel(_sessions, _dialogs, categories, item.Session);
        if (_dialogs.ShowSessionEditor(editor))
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleArchiveAsync()
    {
        if (SelectedSession is not { } item)
        {
            return;
        }
        var result = await _sessions.SetArchivedAsync(item.Id, !item.IsArchived).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            _dialogs.ShowError(Message(result.Error));
        }
        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenAsync()
    {
        if (SelectedSession is { } item)
        {
            await OpenSessionAsync(item).ConfigureAwait(true);
        }
    }

    private async Task OpenSessionAsync(SessionItemViewModel item)
    {
        var result = await _sessions.OpenAsync(item.Id).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            if (result.Value == OpenOutcome.AlreadyOpen)
            {
                _dialogs.ShowInfo(Strings.SessionAlreadyOpen);
            }
            return;
        }

        switch (result.Error.Code)
        {
            case "HistoryMissing" when _dialogs.Confirm(Strings.HistoryMissingStartOver):
                ReportFailure(await _sessions.StartOverAsync(item.Id).ConfigureAwait(true));
                break;
            case "WorkspaceMissing" when _dialogs.Confirm(Strings.WorkspaceMissingPickFolder):
                await RelocateAndOpenAsync(item).ConfigureAwait(true);
                break;
            case "HistoryMissing" or "WorkspaceMissing":
                break;
            default:
                _dialogs.ShowError(Message(result.Error));
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenFolder()
    {
        if (SelectedSession is { } item)
        {
            OpenInExplorer(item.WorkspacePath, create: false);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenOutputs()
    {
        if (SelectedSession is { } item)
        {
            OpenInExplorer(Path.Combine(item.WorkspacePath, OutputsFolderName), create: true);
        }
    }

    private async Task RelocateAndOpenAsync(SessionItemViewModel item)
    {
        string? folder = _dialogs.PickFolder(Path.GetDirectoryName(item.WorkspacePath));
        if (folder is null)
        {
            return;
        }
        var moved = await _sessions.RelocateAsync(item.Id, folder).ConfigureAwait(true);
        if (!moved.IsSuccess)
        {
            _dialogs.ShowError(Message(moved.Error));
            return;
        }
        await OpenSessionAsync(item).ConfigureAwait(true);
    }

    private void OpenInExplorer(string folder, bool create)
    {
        try
        {
            if (create)
            {
                // The outputs folder lives inside an existing workspace; never recreate a vanished workspace.
                if (!Directory.Exists(Path.GetDirectoryName(folder)))
                {
                    _dialogs.ShowError(Strings.WorkspaceMissing);
                    return;
                }
                Directory.CreateDirectory(folder);
            }
            else if (!Directory.Exists(folder))
            {
                _dialogs.ShowError(Strings.WorkspaceMissing);
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Strings.FolderNotAccessible);
            return;
        }
        if (!_shell.OpenFolder(folder))
        {
            _dialogs.ShowError(Strings.FolderNotAccessible);
        }
    }

    private void ReportFailure(Result<OpenOutcome> result)
    {
        if (!result.IsSuccess)
        {
            _dialogs.ShowError(Message(result.Error));
        }
    }

    private static string Message(AppError error) =>
        Strings.ResourceManager.GetString(error.UserMessageKey, CultureInfo.CurrentUICulture) ?? Strings.UnexpectedErrorMessage;

    // Change notifications can arrive in bursts from any thread; coalesce them into one refresh on the UI thread.
    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1)
        {
            return;
        }
        _dispatcher.Post(async () =>
        {
            Volatile.Write(ref _refreshQueued, 0);
            await RefreshAsync().ConfigureAwait(true);
        });
    }

    internal void Apply(IReadOnlyList<SessionSnapshot> snapshots, IReadOnlyList<Category> categories)
    {
        var groups = categories.ToDictionary(category => category.Id);
        var seen = new HashSet<Guid>();
        foreach (var snapshot in snapshots)
        {
            seen.Add(snapshot.Session.Id);
            var (name, order) = Group(snapshot.Session, groups);
            if (_items.TryGetValue(snapshot.Session.Id, out var existing))
            {
                existing.Update(snapshot, name, order);
            }
            else
            {
                var item = new SessionItemViewModel(snapshot, name, order);
                _items.Add(item.Id, item);
                Sessions.Add(item);
            }
        }

        foreach (var removed in _items.Values.Where(item => !seen.Contains(item.Id)).ToList())
        {
            _items.Remove(removed.Id);
            Sessions.Remove(removed);
            if (ReferenceEquals(SelectedSession, removed))
            {
                SelectedSession = null;
            }
        }
        SelectedSession ??= Sessions.MaxBy(item => item.SortKey);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static (string Name, int Order) Group(Session session, Dictionary<Guid, Category> categories) =>
        session.IsArchived ? (Strings.ArchiveGroup, ArchiveOrder)
        : session.CategoryId is { } id && categories.TryGetValue(id, out var category) ? (category.Name, category.SortOrder)
        : (Strings.UncategorizedGroup, UncategorizedOrder);
}
