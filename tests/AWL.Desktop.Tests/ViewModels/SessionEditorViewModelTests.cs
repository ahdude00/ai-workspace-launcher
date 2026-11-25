using AWL.Core.Categories;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using NSubstitute;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class SessionEditorViewModelTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly Category _work = new(Guid.NewGuid(), "İş", 0);

    [Fact]
    public async Task New_session_requires_a_title_and_an_existing_folder()
    {
        var editor = Create();
        int saved = 0;
        editor.Saved += (_, _) => saved++;

        await editor.SaveCommand.ExecuteAsync(null);
        editor.ErrorMessage.ShouldBe(Strings.SessionTitleInvalid);

        editor.Title = "Dava";
        editor.WorkspacePath = "göreli";
        await editor.SaveCommand.ExecuteAsync(null);
        editor.ErrorMessage.ShouldBe(Strings.WorkspaceInvalid);

        editor.WorkspacePath = Path.Combine(_fixture.Root, "yok");
        await editor.SaveCommand.ExecuteAsync(null);
        editor.ErrorMessage.ShouldBe(Strings.WorkspaceMissing);

        saved.ShouldBe(0);
        editor.HasError.ShouldBeTrue();
        _fixture.Repository.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task New_session_is_created_with_the_chosen_category()
    {
        var editor = Create(initialFolder: _fixture.Root);
        int saved = 0;
        editor.Saved += (_, _) => saved++;
        editor.IsNew.ShouldBeTrue();
        editor.WorkspacePath.ShouldBe(_fixture.Root);
        editor.Categories.Select(option => option.Name).ShouldBe([Strings.UncategorizedGroup, "İş"]);
        editor.SelectedCategory.Id.ShouldBeNull();

        editor.Title = " Sözleşme ";
        editor.SelectedCategory = editor.Categories[1];
        await editor.SaveCommand.ExecuteAsync(null);

        saved.ShouldBe(1);
        editor.HasError.ShouldBeFalse();
        var stored = _fixture.Repository.Items.ShouldHaveSingleItem();
        stored.ShouldBe(editor.SavedSession);
        stored.Title.ShouldBe("Sözleşme");
        stored.CategoryId.ShouldBe(_work.Id);
        _fixture.Launches.ShouldBeEmpty();
    }

    [Fact]
    public void Browse_fills_the_folder_and_keeps_it_when_cancelled()
    {
        var editor = Create();
        _dialogs.PickFolder(null).Returns(_fixture.Root);
        editor.BrowseCommand.Execute(null);
        editor.WorkspacePath.ShouldBe(_fixture.Root);

        _dialogs.PickFolder(_fixture.Root).Returns((string?)null);
        editor.BrowseCommand.Execute(null);
        editor.WorkspacePath.ShouldBe(_fixture.Root);
    }

    [Fact]
    public async Task Editing_renames_and_recategorizes_but_keeps_the_folder()
    {
        var session = await _fixture.AddAsync("Eski Ad", _work.Id);
        var editor = Create(existing: session);
        editor.IsNew.ShouldBeFalse();
        editor.IsFolderReadOnly.ShouldBeTrue();
        editor.Title.ShouldBe("Eski Ad");
        editor.SelectedCategory.Id.ShouldBe(_work.Id);
        editor.WindowTitle.ShouldBe(Strings.EditSessionTitle);

        editor.Title = "Yeni Ad";
        editor.WorkspacePath = Path.Combine(_fixture.Root, "başka");
        editor.SelectedCategory = editor.Categories[0];
        await editor.SaveCommand.ExecuteAsync(null);

        var stored = _fixture.Repository.Items.Single();
        stored.Title.ShouldBe("Yeni Ad");
        stored.CategoryId.ShouldBeNull();
        stored.WorkspacePath.ShouldBe(session.WorkspacePath);
    }

    [Fact]
    public async Task A_session_in_a_deleted_category_starts_uncategorized()
    {
        var session = await _fixture.AddAsync("Dava", Guid.NewGuid());
        Create(existing: session).SelectedCategory.Id.ShouldBeNull();
    }

    private SessionEditorViewModel Create(AWL.Core.Sessions.Session? existing = null, string? initialFolder = null) =>
        new(_fixture.Service, _dialogs, [_work], existing, initialFolder);

    public void Dispose() => _fixture.Dispose();
}
