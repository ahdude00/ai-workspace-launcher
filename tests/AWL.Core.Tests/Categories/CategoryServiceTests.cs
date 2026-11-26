using AWL.Core.Categories;

namespace AWL.Core.Tests.Categories;

public sealed class CategoryServiceTests
{
    private readonly FakeCategoryRepository _repository = new();
    private readonly CategoryService _service;
    private int _changes;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_repository);
        _service.Changed += (_, _) => _changes++;
    }

    [Fact]
    public async Task Create_appends_trimmed_categories_in_order()
    {
        var first = (await _service.CreateAsync("  Raporlar ")).Value;
        var second = (await _service.CreateAsync("Dilekçeler")).Value;

        first.Name.ShouldBe("Raporlar");
        second.SortOrder.ShouldBeGreaterThan(first.SortOrder);
        (await _service.ListAsync()).Select(c => c.Name).ShouldBe(["Raporlar", "Dilekçeler"]);
        _changes.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_or_long_names_are_rejected(string name)
    {
        (await _service.CreateAsync(name)).Error!.Code.ShouldBe("CategoryNameInvalid");
        (await _service.CreateAsync(new string('k', CategoryService.MaxNameLength + 1))).Error!.Code
            .ShouldBe("CategoryNameInvalid");
        _repository.Items.ShouldBeEmpty();
        _changes.ShouldBe(0);
    }

    [Fact]
    public async Task Names_are_unique_ignoring_Turkish_case()
    {
        await _service.CreateAsync("İş");

        (await _service.CreateAsync("iş")).Error!.Code.ShouldBe("CategoryNameTaken");
        (await _service.CreateAsync(" İŞ ")).Error!.Code.ShouldBe("CategoryNameTaken");
        (await _service.CreateAsync("Is")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Rename_checks_other_names_but_allows_changing_the_case_of_its_own()
    {
        var reports = (await _service.CreateAsync("Raporlar")).Value;
        await _service.CreateAsync("Notlar");

        (await _service.RenameAsync(reports.Id, "notlar")).Error!.Code.ShouldBe("CategoryNameTaken");
        (await _service.RenameAsync(reports.Id, "RAPORLAR")).Value.Name.ShouldBe("RAPORLAR");
        (await _service.RenameAsync(reports.Id, " ")).Error!.Code.ShouldBe("CategoryNameInvalid");
        (await _service.RenameAsync(Guid.NewGuid(), "Yeni")).Error!.Code.ShouldBe("CategoryNotFound");
        _repository.Items.Single(c => c.Id == reports.Id).Name.ShouldBe("RAPORLAR");
    }

    [Fact]
    public async Task Move_reorders_and_renumbers_even_with_duplicate_orders()
    {
        var a = new Category(Guid.NewGuid(), "A", 5);
        var b = new Category(Guid.NewGuid(), "B", 5);
        var c = new Category(Guid.NewGuid(), "C", 9);
        await _repository.AddAsync(a);
        await _repository.AddAsync(b);
        await _repository.AddAsync(c);

        (await _service.MoveAsync(c.Id, -1)).IsSuccess.ShouldBeTrue();
        (await _service.ListAsync()).Select(x => x.Name).ShouldBe(["A", "C", "B"]);
        (await _service.ListAsync()).Select(x => x.SortOrder).ShouldBe([0, 1, 2]);

        (await _service.MoveAsync(a.Id, -1)).IsSuccess.ShouldBeTrue();
        (await _service.MoveAsync(a.Id, 10)).IsSuccess.ShouldBeTrue();
        (await _service.ListAsync()).Select(x => x.Name).ShouldBe(["C", "B", "A"]);
        (await _service.MoveAsync(Guid.NewGuid(), 1)).Error!.Code.ShouldBe("CategoryNotFound");
        _changes.ShouldBe(2);
    }

    [Fact]
    public async Task Delete_removes_only_the_category()
    {
        var category = (await _service.CreateAsync("Raporlar")).Value;

        (await _service.DeleteAsync(category.Id)).IsSuccess.ShouldBeTrue();
        (await _service.DeleteAsync(category.Id)).Error!.Code.ShouldBe("CategoryNotFound");
        _repository.Items.ShouldBeEmpty();
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        private readonly Dictionary<Guid, Category> _items = [];

        public IReadOnlyCollection<Category> Items => [.. _items.Values];

        public Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        // Same order as the SQLite repository: sort order, then identifier.
        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Category>>(
                [.. _items.Values.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Ordinal)]);

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            _items.Add(category.Id, category);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            bool found = _items.ContainsKey(category.Id);
            if (found)
            {
                _items[category.Id] = category;
            }
            return Task.FromResult(found);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Remove(id));
    }
}
