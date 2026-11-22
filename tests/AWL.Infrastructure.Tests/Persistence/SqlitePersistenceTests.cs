using AWL.Core;
using AWL.Core.Categories;
using AWL.Core.Configuration;
using AWL.Core.Sessions;
using AWL.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AWL.Infrastructure.Tests.Persistence;

public sealed class SqlitePersistenceTests : IDisposable
{
    private readonly DirectoryInfo _temporaryDirectory = Directory.CreateTempSubdirectory("awl-sqlite-tests-");
    private readonly AwlPaths _paths;

    public SqlitePersistenceTests()
    {
        string root = _temporaryDirectory.FullName;
        _paths = new AwlPaths(root, root, root);
    }

    [Fact]
    public async Task Migration_can_run_again_after_reopening_the_database()
    {
        var category = new Category(Guid.NewGuid(), "Ölçümler");
        using (ServiceProvider first = CreateProvider())
        {
            await first.GetRequiredService<SqliteDatabase>().InitializeAsync();
            await first.GetRequiredService<ICategoryRepository>().AddAsync(category);
        }

        using ServiceProvider reopened = CreateProvider();
        await reopened.GetRequiredService<SqliteDatabase>().InitializeAsync();
        (await reopened.GetRequiredService<ICategoryRepository>().GetAsync(category.Id)).ShouldBe(category);

        await using SqliteConnection connection = await OpenReadOnlyConnectionAsync();
        (await connection.QuerySingleAsync<string>("PRAGMA journal_mode;")).ShouldBe("wal");
        (await connection.QuerySingleAsync<long>("SELECT version FROM schema_version;")).ShouldBe(1L);
    }

    [Fact]
    public async Task Concurrent_initialization_applies_schema_v1_once()
    {
        using ServiceProvider first = CreateProvider();
        using ServiceProvider second = CreateProvider();
        SqliteDatabase firstDatabase = first.GetRequiredService<SqliteDatabase>();
        SqliteDatabase secondDatabase = second.GetRequiredService<SqliteDatabase>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task InitializeAsync(SqliteDatabase database) => Task.Run(async () =>
        {
            await ready.Task;
            await database.InitializeAsync();
        });

        Task firstAttempt = InitializeAsync(firstDatabase);
        Task secondAttempt = InitializeAsync(secondDatabase);
        ready.SetResult();
        await Task.WhenAll(firstAttempt, secondAttempt);

        await using SqliteConnection connection = await OpenReadOnlyConnectionAsync();
        (await connection.QuerySingleAsync<long>("SELECT version FROM schema_version;")).ShouldBe(1L);
    }

    [Fact]
    public async Task Category_crud_uses_sort_order()
    {
        using ServiceProvider provider = CreateProvider();
        ICategoryRepository categories = provider.GetRequiredService<ICategoryRepository>();
        var later = new Category(Guid.NewGuid(), "Ölçümün 'taslağı'", 20);
        var earlier = new Category(Guid.NewGuid(), "Raporlar");

        await categories.AddAsync(later);
        await categories.AddAsync(earlier);
        (await categories.ListAsync()).ShouldBe(new[] { earlier, later });

        Category updated = later with { Name = "Sonuçlar", SortOrder = -1 };
        (await categories.UpdateAsync(updated)).ShouldBeTrue();
        (await categories.GetAsync(later.Id)).ShouldBe(updated);
        (await categories.ListAsync()).ShouldBe(new[] { updated, earlier });

        (await categories.DeleteAsync(later.Id)).ShouldBeTrue();
        (await categories.GetAsync(later.Id)).ShouldBeNull();
        (await categories.UpdateAsync(updated)).ShouldBeFalse();
        (await categories.DeleteAsync(later.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Session_crud_round_trips_metadata_and_optional_values()
    {
        using ServiceProvider provider = CreateProvider();
        ISessionRepository sessions = provider.GetRequiredService<ISessionRepository>();
        var createdAt = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.FromHours(3));
        Session earlier = CreateSession(createdAt);
        Session later = CreateSession(createdAt.AddMinutes(1)) with
        {
            ClaudeVersion = "2.1.0",
            IsArchived = true,
            LastOpenedAt = createdAt.AddMinutes(2),
            LastActivityAt = createdAt.AddMinutes(3).AddTicks(1234),
            LastErrorCode = "history.unavailable",
        };

        await sessions.AddAsync(later);
        await sessions.AddAsync(earlier);
        (await sessions.GetAsync(earlier.Id)).ShouldBe(earlier);
        (await sessions.GetAsync(later.Id)).ShouldBe(later);
        (await sessions.ListAsync()).Select(session => session.Id).ShouldBe(new[] { later.Id, earlier.Id });

        Session updated = later with { Title = "Güncel başlık", LastErrorCode = null, IsArchived = false };
        (await sessions.UpdateAsync(updated)).ShouldBeTrue();
        (await sessions.GetAsync(later.Id)).ShouldBe(updated);
        (await sessions.DeleteAsync(later.Id)).ShouldBeTrue();
        (await sessions.GetAsync(later.Id)).ShouldBeNull();
        (await sessions.UpdateAsync(updated)).ShouldBeFalse();
        (await sessions.DeleteAsync(later.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_a_category_keeps_its_sessions_uncategorized()
    {
        using ServiceProvider provider = CreateProvider();
        ICategoryRepository categories = provider.GetRequiredService<ICategoryRepository>();
        ISessionRepository sessions = provider.GetRequiredService<ISessionRepository>();
        var category = new Category(Guid.NewGuid(), "Raporlar");
        Session session = CreateSession(DateTimeOffset.UnixEpoch) with { CategoryId = category.Id };
        await categories.AddAsync(category);
        await sessions.AddAsync(session);
        (await sessions.GetAsync(session.Id)).ShouldBe(session);

        (await categories.DeleteAsync(category.Id)).ShouldBeTrue();
        (await sessions.GetAsync(session.Id)).ShouldBe(session with { CategoryId = null });
    }

    [Fact]
    public async Task Duplicate_claude_session_id_rejects_the_update_without_changing_metadata()
    {
        using ServiceProvider provider = CreateProvider();
        ISessionRepository sessions = provider.GetRequiredService<ISessionRepository>();
        Session first = CreateSession(DateTimeOffset.UnixEpoch);
        Session second = CreateSession(DateTimeOffset.UnixEpoch.AddMinutes(1));
        await sessions.AddAsync(first);
        await sessions.AddAsync(second);

        Session duplicate = second with { ClaudeSessionId = first.ClaudeSessionId, Title = "Geçersiz güncelleme" };
        SqliteException exception = await Should.ThrowAsync<SqliteException>(() => sessions.UpdateAsync(duplicate));

        exception.SqliteErrorCode.ShouldBe(19);
        (await sessions.GetAsync(second.Id)).ShouldBe(second);
    }

    public void Dispose() => _temporaryDirectory.Delete(recursive: true);

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_paths);
        return services.AddCore().AddInfrastructure().BuildServiceProvider();
    }

    private Session CreateSession(DateTimeOffset createdAt) => new(
        Guid.NewGuid(), "Ölçümün 'taslağı'", null, Path.Combine(_temporaryDirectory.FullName, "Çalışma Alanı"),
        Guid.NewGuid(), null, false, createdAt, null, null);

    private async Task<SqliteConnection> OpenReadOnlyConnectionAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabaseFile,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
