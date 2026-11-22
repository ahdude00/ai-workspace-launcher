using System.Data;
using System.Data.Common;
using AWL.Core.Configuration;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AWL.Infrastructure.Persistence;

/// <summary>Opens AWL's database after applying its forward-only schema changes.</summary>
public sealed class SqliteDatabase : IDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const string SchemaVersionTable = """
        CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);
        """;
    private const string SchemaVersionOne = """
        CREATE TABLE categories (
            id         TEXT PRIMARY KEY,
            name       TEXT NOT NULL,
            sort_order INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE sessions (
            id                TEXT PRIMARY KEY,
            title             TEXT NOT NULL,
            category_id       TEXT NULL REFERENCES categories(id) ON DELETE SET NULL,
            workspace_path    TEXT NOT NULL,
            claude_session_id TEXT NOT NULL UNIQUE,
            claude_version    TEXT NULL,
            is_archived       INTEGER NOT NULL DEFAULT 0,
            last_error_code   TEXT NULL,
            created_at        TEXT NOT NULL,
            last_opened_at    TEXT NULL,
            last_activity_at  TEXT NULL
        );

        DELETE FROM schema_version;
        INSERT INTO schema_version (version) VALUES (1);
        """;

    private readonly string _databaseDirectory;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private volatile bool _initialized;

    /// <summary>Uses the database path in AWL's roaming data directory.</summary>
    public SqliteDatabase(AwlPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _databaseDirectory = paths.RoamingDataDirectory;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    /// <summary>Creates or upgrades the database. A failed migration can be retried.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                Directory.CreateDirectory(_databaseDirectory);
                await using SqliteConnection connection = await OpenNewConnectionAsync(cancellationToken).ConfigureAwait(false);
                await EnableWalAsync(connection, cancellationToken).ConfigureAwait(false);
                await MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    internal async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await OpenNewConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<int> ExecuteWriteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        int affectedRows = await connection.ExecuteAsync(command).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return affectedRows;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _initializationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<SqliteConnection> OpenNewConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA synchronous = FULL;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task EnableWalAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        string? journalMode = await connection.ExecuteScalarAsync<string>(new CommandDefinition("PRAGMA journal_mode;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            journalMode = await connection.ExecuteScalarAsync<string>(new CommandDefinition("PRAGMA journal_mode = WAL;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The database could not enable WAL mode.");
            }
        }
    }

    private static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // A non-deferred write transaction serializes the version check and migration across connections.
        await using DbTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(SchemaVersionTable, transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        int version = await connection.QuerySingleOrDefaultAsync<int>(new CommandDefinition(
            "SELECT version FROM schema_version;", transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (version is < 0 or > CurrentSchemaVersion)
        {
            throw new InvalidOperationException("The database schema is not supported by this version of AWL.");
        }

        if (version < 1)
        {
            await connection.ExecuteAsync(new CommandDefinition(SchemaVersionOne, transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
