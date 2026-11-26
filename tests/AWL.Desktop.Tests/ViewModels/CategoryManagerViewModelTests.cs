using AWL.Core.Categories;
using AWL.Desktop.Resources;
using AWL.Desktop.Services;
using AWL.Desktop.ViewModels;
using NSubstitute;

namespace AWL.Desktop.Tests.ViewModels;

public sealed class CategoryManagerViewModelTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly CategoryManagerViewModel _manager;

    public CategoryManagerViewModelTests() =>
        _manager = new CategoryManagerViewModel(new CategoryService(_fixture.Categories), _dialogs);

    [Fact]
    public async Task Add_creates_categories_and_reports_invalid_or_duplicate_names()
    {
        await _manager.LoadAsync();
        _manager.Name = "Raporlar";
        await _manager.AddCommand.ExecuteAsync(null);
        _manager.Categories.ShouldHaveSingleItem().Name.ShouldBe("Raporlar");
        _manager.Name.ShouldBeEmpty();

        _manager.Name = "raporlar";
        await _manager.AddCommand.ExecuteAsync(null);
        _manager.ErrorMessage.ShouldBe(Strings.CategoryNameTaken);

        _manager.Name = " ";
        await _manager.AddCommand.ExecuteAsync(null);
        _manager.ErrorMessage.ShouldBe(Strings.CategoryNameInvalid);
        _manager.Categories.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Selecting_fills_the_name_and_rename_keeps_the_selection()
    {
        await _fixture.Categories.AddAsync(new Category(Guid.NewGuid(), "Eski", 0));
        await _manager.LoadAsync();
        _manager.RenameCommand.CanExecute(null).ShouldBeFalse();

        _manager.SelectedCategory = _manager.Categories[0];
        _manager.Name.ShouldBe("Eski");
        _manager.Name = "Yeni";
        await _manager.RenameCommand.ExecuteAsync(null);

        _manager.SelectedCategory!.Name.ShouldBe("Yeni");
        _manager.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task Move_changes_the_order()
    {
        await _fixture.Categories.AddAsync(new Category(Guid.NewGuid(), "A", 0));
        await _fixture.Categories.AddAsync(new Category(Guid.NewGuid(), "B", 1));
        await _manager.LoadAsync();
        _manager.SelectedCategory = _manager.Categories[1];

        await _manager.MoveUpCommand.ExecuteAsync(null);

        _manager.Categories.Select(c => c.Name).ShouldBe(["B", "A"]);
        _manager.SelectedCategory!.Name.ShouldBe("B");
        await _manager.MoveDownCommand.ExecuteAsync(null);
        _manager.Categories.Select(c => c.Name).ShouldBe(["A", "B"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_asks_first(bool confirm)
    {
        await _fixture.Categories.AddAsync(new Category(Guid.NewGuid(), "Raporlar", 0));
        await _manager.LoadAsync();
        _manager.SelectedCategory = _manager.Categories[0];
        _dialogs.Confirm(Arg.Any<string>()).Returns(confirm);

        await _manager.DeleteCommand.ExecuteAsync(null);

        _dialogs.Received(1).Confirm(Arg.Is<string>(text => text.Contains("Raporlar", StringComparison.Ordinal)));
        _manager.Categories.Count.ShouldBe(confirm ? 0 : 1);
    }

    public void Dispose() => _fixture.Dispose();
}
