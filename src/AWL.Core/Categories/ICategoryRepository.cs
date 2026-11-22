namespace AWL.Core.Categories;

/// <summary>Persists categories and their display order.</summary>
public interface ICategoryRepository
{
    /// <summary>Gets a category by its identifier, or null when it is missing.</summary>
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists categories in display order.</summary>
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a category with a new identifier.</summary>
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Updates a category and returns whether it existed.</summary>
    Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category and returns whether it existed. Its sessions become uncategorized.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
