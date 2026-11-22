using System.Globalization;
using AWL.Core.Sessions;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AWL.Infrastructure.Persistence;

/// <summary>Stores AWL session metadata in SQLite.</summary>
/// <param name="database">The shared AWL database.</param>
public sealed class SqliteSessionRepository(SqliteDatabase database) : ISessionRepository
{
    private const string SelectSql = """
        SELECT id AS Id, title AS Title, category_id AS CategoryId, workspace_path AS WorkspacePath,
               claude_session_id AS ClaudeSessionId, claude_version AS ClaudeVersion, is_archived AS IsArchived,
               created_at AS CreatedAt, last_opened_at AS LastOpenedAt, last_activity_at AS LastActivityAt,
               last_error_code AS LastErrorCode
        FROM sessions
        """;

    /// <inheritdoc />
    public async Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            SelectSql + " WHERE id = @Id;", new { Id = id.ToString("D") }, cancellationToken: cancellationToken);
        StoredSession? session = await connection.QuerySingleOrDefaultAsync<StoredSession>(command).ConfigureAwait(false);
        return session?.ToSession();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            SelectSql + " ORDER BY created_at DESC, id;", cancellationToken: cancellationToken);
        IEnumerable<StoredSession> sessions = await connection.QueryAsync<StoredSession>(command).ConfigureAwait(false);
        return sessions.Select(static session => session.ToSession()).ToArray();
    }

    /// <inheritdoc />
    public Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO sessions (
                id, title, category_id, workspace_path, claude_session_id, claude_version, is_archived,
                created_at, last_opened_at, last_activity_at, last_error_code
            )
            VALUES (
                @Id, @Title, @CategoryId, @WorkspacePath, @ClaudeSessionId, @ClaudeVersion, @IsArchived,
                @CreatedAt, @LastOpenedAt, @LastActivityAt, @LastErrorCode
            );
            """;

        return database.ExecuteWriteAsync(sql, StoredSession.FromSession(session), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sessions
            SET title = @Title, category_id = @CategoryId, workspace_path = @WorkspacePath,
                claude_session_id = @ClaudeSessionId, claude_version = @ClaudeVersion, is_archived = @IsArchived,
                created_at = @CreatedAt, last_opened_at = @LastOpenedAt, last_activity_at = @LastActivityAt,
                last_error_code = @LastErrorCode
            WHERE id = @Id;
            """;

        return await database.ExecuteWriteAsync(sql, StoredSession.FromSession(session), cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM sessions WHERE id = @Id;";
        return await database.ExecuteWriteAsync(sql, new { Id = id.ToString("D") }, cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    private sealed class StoredSession
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? CategoryId { get; init; }
        public string WorkspacePath { get; init; } = string.Empty;
        public string ClaudeSessionId { get; init; } = string.Empty;
        public string? ClaudeVersion { get; init; }
        public long IsArchived { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string? LastOpenedAt { get; init; }
        public string? LastActivityAt { get; init; }
        public string? LastErrorCode { get; init; }

        public static StoredSession FromSession(Session session)
        {
            ArgumentNullException.ThrowIfNull(session);
            return new StoredSession
            {
                Id = session.Id.ToString("D"),
                Title = session.Title,
                CategoryId = session.CategoryId?.ToString("D"),
                WorkspacePath = session.WorkspacePath,
                ClaudeSessionId = session.ClaudeSessionId.ToString("D"),
                ClaudeVersion = session.ClaudeVersion,
                IsArchived = session.IsArchived ? 1 : 0,
                CreatedAt = FormatTimestamp(session.CreatedAt),
                LastOpenedAt = session.LastOpenedAt is { } lastOpenedAt ? FormatTimestamp(lastOpenedAt) : null,
                LastActivityAt = session.LastActivityAt is { } lastActivityAt ? FormatTimestamp(lastActivityAt) : null,
                LastErrorCode = session.LastErrorCode,
            };
        }

        public Session ToSession() => new(
            Guid.Parse(Id),
            Title,
            CategoryId is null ? null : Guid.Parse(CategoryId),
            WorkspacePath,
            Guid.Parse(ClaudeSessionId),
            ClaudeVersion,
            IsArchived != 0,
            ParseTimestamp(CreatedAt),
            LastOpenedAt is null ? null : ParseTimestamp(LastOpenedAt),
            LastActivityAt is null ? null : ParseTimestamp(LastActivityAt),
            LastErrorCode);

        private static string FormatTimestamp(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static DateTimeOffset ParseTimestamp(string value) => DateTimeOffset.Parse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
