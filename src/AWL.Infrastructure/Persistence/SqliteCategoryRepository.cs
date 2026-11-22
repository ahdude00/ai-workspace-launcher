using AWL.Core.Categories;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AWL.Infrastructure.Persistence;

/// <summary>Persists categories in the AWL SQLite database.</summary>
/// <param name="database">The shared AWL database.</param>
public sealed class SqliteCategoryRepository(SqliteDatabase database) : ICategoryRepository
{
    /// <inheritdoc />
    public async Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT id, name, sort_order AS SortOrder FROM categories WHERE id = @Id;";
        await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { Id = id.ToString("D") }, cancellationToken: cancellationToken);
        CategoryRow? row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(command).ConfigureAwait(false);
        return row?.ToCategory();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, sort_order AS SortOrder
            FROM categories
            ORDER BY sort_order, id;
            """;
        await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<CategoryRow> rows = await connection.QueryAsync<CategoryRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(static row => row.ToCategory()).ToArray();
    }

    /// <inheritdoc />
    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO categories (id, name, sort_order) VALUES (@Id, @Name, @SortOrder);";
        return database.ExecuteWriteAsync(sql, GetParameters(category), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE categories SET name = @Name, sort_order = @SortOrder WHERE id = @Id;";
        return await database.ExecuteWriteAsync(sql, GetParameters(category), cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM categories WHERE id = @Id;";
        return await database.ExecuteWriteAsync(sql, new { Id = id.ToString("D") }, cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    private static object GetParameters(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return new { Id = category.Id.ToString("D"), category.Name, category.SortOrder };
    }

    private sealed class CategoryRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int SortOrder { get; init; }

        public Category ToCategory() => new(Guid.Parse(Id), Name, SortOrder);
    }
}
