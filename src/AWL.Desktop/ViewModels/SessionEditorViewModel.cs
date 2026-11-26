using System.Globalization;
using AWL.Core.Categories;
using AWL.Core.Sessions;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AWL.Desktop.ViewModels;

/// <summary>An entry of the category picker; <see cref="Id"/> is null for "no category".</summary>
public sealed record CategoryOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Creates a session or edits the title and category of an existing one (OTR-01, OTR-10).</summary>
public sealed partial class SessionEditorViewModel : ObservableObject
{
    private readonly SessionService _sessions;
    private readonly IDialogService _dialogs;
    private readonly Guid? _sessionId;

    public SessionEditorViewModel(SessionService sessions, IDialogService dialogs, IReadOnlyList<Category> categories,
        Session? existing = null, string? initialFolder = null)
    {
        ArgumentNullException.ThrowIfNull(categories);
        _sessions = sessions;
        _dialogs = dialogs;
        _sessionId = existing?.Id;
        Categories = [new CategoryOption(null, Strings.UncategorizedGroup),
            .. categories.Select(category => new CategoryOption(category.Id, category.Name))];
        Title = existing?.Title ?? "";
        WorkspacePath = existing?.WorkspacePath ?? initialFolder ?? "";
        SelectedCategory = Categories.FirstOrDefault(option => option.Id == existing?.CategoryId) ?? Categories[0];
    }

    /// <summary>Raised after a successful save; cancelling is handled by the window itself.</summary>
    public event EventHandler? Saved;

    public bool IsNew => _sessionId is null;

    /// <summary>The folder of an existing session is changed only when it disappears (OTR-20).</summary>
    public bool IsFolderReadOnly => !IsNew;

    public string WindowTitle => IsNew ? Strings.NewSessionTitle : Strings.EditSessionTitle;

    public string SaveText => IsNew ? Strings.CreateAndOpenButton : Strings.SaveButton;

    public IReadOnlyList<CategoryOption> Categories { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string WorkspacePath { get; set; }

    [ObservableProperty]
    public partial CategoryOption SelectedCategory { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    /// <summary>The created or edited session after a successful save.</summary>
    public Session? SavedSession { get; private set; }

    [RelayCommand]
    private void Browse()
    {
        if (_dialogs.PickFolder(string.IsNullOrWhiteSpace(WorkspacePath) ? null : WorkspacePath) is { } folder)
        {
            WorkspacePath = folder;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var result = _sessionId is Guid id
            ? await _sessions.UpdateDetailsAsync(id, Title, SelectedCategory.Id).ConfigureAwait(true)
            : await _sessions.CreateAsync(Title, WorkspacePath.Trim(), SelectedCategory.Id).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            ErrorMessage = Strings.ResourceManager.GetString(result.Error.UserMessageKey, CultureInfo.CurrentUICulture)
                ?? Strings.UnexpectedErrorMessage;
            return;
        }
        ErrorMessage = null;
        SavedSession = result.Value;
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
