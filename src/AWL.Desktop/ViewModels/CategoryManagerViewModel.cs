using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using AWL.Core.Categories;
using AWL.Core.Common;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AWL.Desktop.ViewModels;

/// <summary>Adds, renames, orders and deletes categories (KAT-01, KAT-02).</summary>
public sealed partial class CategoryManagerViewModel(CategoryService categories, IDialogService dialogs) : ObservableObject
{
    private static readonly CompositeFormat DeleteConfirm = CompositeFormat.Parse(Strings.CategoryDeleteConfirm);

    public ObservableCollection<Category> Categories { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand), nameof(DeleteCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial Category? SelectedCategory { get; set; }

    /// <summary>The text box used both for a new name and for renaming the selected category.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    public bool HasError => ErrorMessage is not null;

    private bool HasSelection => SelectedCategory is not null;

    public async Task LoadAsync(Guid? select = null)
    {
        var list = await categories.ListAsync().ConfigureAwait(true);
        Categories.Clear();
        foreach (var category in list)
        {
            Categories.Add(category);
        }
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == select);
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is not null)
        {
            Name = value.Name;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var result = await categories.CreateAsync(Name).ConfigureAwait(true);
        if (Report(result.Error))
        {
            Name = "";
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameAsync()
    {
        if (SelectedCategory is not { } selected)
        {
            return;
        }
        var result = await categories.RenameAsync(selected.Id, Name).ConfigureAwait(true);
        if (Report(result.Error))
        {
            await LoadAsync(selected.Id).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task MoveUpAsync() => MoveAsync(-1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task MoveDownAsync() => MoveAsync(1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedCategory is not { } selected
            || !dialogs.Confirm(string.Format(CultureInfo.CurrentCulture, DeleteConfirm, selected.Name)))
        {
            return;
        }
        var result = await categories.DeleteAsync(selected.Id).ConfigureAwait(true);
        if (Report(result.Error))
        {
            Name = "";
            await LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task MoveAsync(int offset)
    {
        if (SelectedCategory is not { } selected)
        {
            return;
        }
        var result = await categories.MoveAsync(selected.Id, offset).ConfigureAwait(true);
        if (Report(result.Error))
        {
            await LoadAsync(selected.Id).ConfigureAwait(true);
        }
    }

    // Returns whether the operation succeeded; otherwise shows its message next to the name box.
    private bool Report(AppError? error)
    {
        ErrorMessage = error is null ? null
            : Strings.ResourceManager.GetString(error.UserMessageKey, CultureInfo.CurrentUICulture) ?? Strings.UnexpectedErrorMessage;
        return error is null;
    }
}
