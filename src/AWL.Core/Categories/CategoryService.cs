using System.Globalization;
using AWL.Core.Common;

namespace AWL.Core.Categories;

/// <summary>Creates, renames, orders and deletes categories (KAT-01, KAT-02; D06).</summary>
public sealed class CategoryService(ICategoryRepository repository)
{
    /// <summary>The longest category name accepted.</summary>
    public const int MaxNameLength = 60;

    // Names are compared the way a Turkish user reads them: "İş" and "iş" are the same name.
    private static readonly CompareInfo Comparer = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    /// <summary>Raised after a category was created, renamed, moved or deleted.</summary>
    public event EventHandler? Changed;

    /// <summary>Lists the categories in display order.</summary>
    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    /// <summary>Adds a category at the end of the list.</summary>
    public async Task<Result<Category>> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var categories = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        if (Validate(name, categories, except: null) is { } error)
        {
            return Result.Fail<Category>(error);
        }
        int order = categories.Count == 0 ? 0 : categories.Max(category => category.SortOrder) + 1;
        var created = new Category(Guid.NewGuid(), name.Trim(), order);
        await repository.AddAsync(created, cancellationToken).ConfigureAwait(false);
        RaiseChanged();
        return Result.Ok(created);
    }

    /// <summary>Renames a category.</summary>
    public async Task<Result<Category>> RenameAsync(Guid categoryId, string name, CancellationToken cancellationToken = default)
    {
        var categories = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        if (categories.FirstOrDefault(category => category.Id == categoryId) is not { } existing)
        {
            return Result.Fail<Category>(Errors.NotFound);
        }
        if (Validate(name, categories, except: categoryId) is { } error)
        {
            return Result.Fail<Category>(error);
        }
        var renamed = existing with { Name = name.Trim() };
        if (!await repository.UpdateAsync(renamed, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail<Category>(Errors.NotFound);
        }
        RaiseChanged();
        return Result.Ok(renamed);
    }

    /// <summary>
    /// Moves a category up (negative offset) or down (positive offset). The order is renumbered from zero
    /// so that equal or sparse sort orders from older data cannot block a move.
    /// </summary>
    public async Task<Result> MoveAsync(Guid categoryId, int offset, CancellationToken cancellationToken = default)
    {
        var ordered = (await repository.ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
        int index = ordered.FindIndex(category => category.Id == categoryId);
        if (index < 0)
        {
            return Result.Fail(Errors.NotFound);
        }
        int target = Math.Clamp(index + offset, 0, ordered.Count - 1);
        if (target == index)
        {
            return Result.Ok();
        }

        var moving = ordered[index];
        ordered.RemoveAt(index);
        ordered.Insert(target, moving);
        for (int position = 0; position < ordered.Count; position++)
        {
            if (ordered[position].SortOrder != position)
            {
                await repository.UpdateAsync(ordered[position] with { SortOrder = position }, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        RaiseChanged();
        return Result.Ok();
    }

    /// <summary>Deletes a category; its sessions are kept and become uncategorized (KAT-02).</summary>
    public async Task<Result> DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(categoryId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail(Errors.NotFound);
        }
        RaiseChanged();
        return Result.Ok();
    }

    private static AppError? Validate(string name, IReadOnlyList<Category> categories, Guid? except)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return Errors.NameInvalid;
        }
        string trimmed = name.Trim();
        return categories.Any(category => category.Id != except
            && Comparer.Compare(category.Name, trimmed, CompareOptions.IgnoreCase) == 0)
            ? Errors.NameTaken
            : null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Error codes returned by <see cref="CategoryService"/>; each code is also its resource key.</summary>
    public static class Errors
    {
        public static readonly AppError NotFound = new("CategoryNotFound", "CategoryNotFound");
        public static readonly AppError NameInvalid = new("CategoryNameInvalid", "CategoryNameInvalid");
        public static readonly AppError NameTaken = new("CategoryNameTaken", "CategoryNameTaken");
    }
}
