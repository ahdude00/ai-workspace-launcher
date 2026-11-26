namespace AWL.Desktop.Services;

/// <summary>Shows modal messages and pickers owned by the main window.</summary>
public interface IDialogService
{
    void ShowError(string message);

    void ShowInfo(string message);

    /// <summary>Asks a yes/no question and returns whether the user chose yes.</summary>
    bool Confirm(string message);

    /// <summary>Lets the user choose a folder; returns null when cancelled.</summary>
    string? PickFolder(string? initialFolder);

    /// <summary>Shows the session editor and returns whether the session was saved.</summary>
    bool ShowSessionEditor(ViewModels.SessionEditorViewModel editor);

    /// <summary>Shows the category manager until the user closes it.</summary>
    void ShowCategoryManager(ViewModels.CategoryManagerViewModel manager);

    /// <summary>Asks where to save a file; returns null when cancelled.</summary>
    string? PickSaveFile(string suggestedName, string filter);

    /// <summary>Shows the About screen.</summary>
    void ShowAbout();

    /// <summary>Shows the Settings screen.</summary>
    void ShowSettings();
}
